//! Engine session: persistent process holding compiled Plan and current state.

use crate::protocol::{
    self, CompileResult, EvalResultMsg, GraphEdgeMsg, GraphInfoMsg, GraphNodeMsg, GridInfo,
    ParamInfo, Request, Response,
};
use flowtime_core::compiler::{self, EvalResult};
use flowtime_core::eval;
use flowtime_core::model::{self, ModelDefinition};
use flowtime_core::plan::{Plan, ParamValue};
use std::collections::HashMap;
use std::io;
use std::time::Instant;

/// Session state: holds compiled model, plan, and current evaluation.
struct Session {
    model: Option<ModelDefinition>,
    plan: Option<Plan>,
    current: Option<EvalResult>,
}

impl Session {
    fn new() -> Self {
        Self { model: None, plan: None, current: None }
    }

    fn handle(&mut self, req: Request) -> Response {
        match req.method.as_str() {
            "compile" => self.handle_compile(req.params),
            "eval" => self.handle_eval(req.params),
            "get_params" => self.handle_get_params(),
            "get_series" => self.handle_get_series(req.params),
            other => Response::err("unknown_method", &format!("Unknown method: {other}")),
        }
    }

    fn handle_compile(&mut self, params: serde_json::Value) -> Response {
        let yaml = match params.get("yaml").and_then(|v| v.as_str()) {
            Some(y) => y.to_string(),
            None => return Response::err("invalid_params", "compile requires params.yaml (string)"),
        };

        let md = match model::parse_model_yaml(&yaml) {
            Ok(m) => m,
            Err(e) => return Response::err("compile_error", &e),
        };

        // Compile once — store the Plan for reuse
        let plan = match compiler::compile(&md) {
            Ok(p) => p,
            Err(e) => return Response::err("compile_error", &e.0),
        };

        // Evaluate with defaults using the compiled Plan
        let result = match run_full_pipeline(&md, &plan, &[]) {
            Ok(r) => r,
            Err(e) => return Response::err("compile_error", &e),
        };

        // Build parameter info from the stored Plan
        let param_infos = build_param_infos(&plan);

        // Build initial series
        let series = extract_all_series(&result);

        // Derive graph for UI topology visualization
        let graph = compiler::derive_graph(&md);
        let graph_msg = GraphInfoMsg {
            nodes: graph.nodes.iter().map(|n| GraphNodeMsg {
                id: n.id.clone(),
                kind: n.kind.clone(),
            }).collect(),
            edges: graph.edges.iter().map(|e| GraphEdgeMsg {
                from: e.from.clone(),
                to: e.to.clone(),
            }).collect(),
        };

        let grid = md.grid.as_ref().unwrap();
        let compile_result = CompileResult {
            params: param_infos,
            series,
            bins: result.bins,
            grid: GridInfo {
                bins: grid.bins,
                bin_size: grid.bin_size,
                bin_unit: grid.bin_unit.clone(),
            },
            graph: graph_msg,
        };

        self.model = Some(md);
        self.plan = Some(plan);
        self.current = Some(result);

        Response::ok(serde_json::to_value(&compile_result).unwrap())
    }

    fn handle_eval(&mut self, params: serde_json::Value) -> Response {
        let md = match &self.model {
            Some(m) => m,
            None => return Response::err("not_compiled", "No model compiled. Call compile first."),
        };
        let plan = match &self.plan {
            Some(p) => p,
            None => return Response::err("not_compiled", "No plan compiled. Call compile first."),
        };

        // Parse overrides
        let overrides = parse_overrides(&params);

        // Re-evaluate using the stored Plan — NO recompilation
        let start = Instant::now();
        let result = match run_full_pipeline(md, plan, &overrides) {
            Ok(r) => r,
            Err(e) => return Response::err("eval_error", &e),
        };
        let elapsed = start.elapsed();

        let series = extract_all_series(&result);
        let eval_msg = EvalResultMsg {
            series,
            elapsed_us: elapsed.as_micros() as u64,
        };

        self.current = Some(result);

        Response::ok(serde_json::to_value(&eval_msg).unwrap())
    }

    fn handle_get_params(&self) -> Response {
        let plan = match &self.plan {
            Some(p) => p,
            None => return Response::err("not_compiled", "No model compiled. Call compile first."),
        };

        let param_infos = build_param_infos(plan);
        Response::ok(serde_json::json!({ "params": param_infos }))
    }

    fn handle_get_series(&self, params: serde_json::Value) -> Response {
        let result = match &self.current {
            Some(r) => r,
            None => return Response::err("not_compiled", "No evaluation results. Call compile or eval first."),
        };

        let names: Option<Vec<String>> = params.get("names")
            .and_then(|v| v.as_array())
            .map(|arr| arr.iter().filter_map(|v| v.as_str().map(String::from)).collect());

        let series = if let Some(names) = names {
            let mut map = HashMap::new();
            for name in &names {
                if let Some(vals) = result.series(name) {
                    map.insert(name.clone(), vals);
                }
            }
            map
        } else {
            extract_all_series(result)
        };

        Response::ok(serde_json::json!({ "series": series }))
    }
}

/// Run the full post-eval pipeline using a pre-compiled Plan.
/// This does NOT recompile — it reuses the Plan and only re-evaluates.
fn run_full_pipeline(
    model: &ModelDefinition,
    plan: &Plan,
    overrides: &[(String, ParamValue)],
) -> Result<EvalResult, String> {
    let bins = plan.bins;
    let mut state = eval::evaluate_with_params(plan, overrides);
    let mut column_map = plan.column_map.clone();

    // Post-eval: class decomposition, edge series, analysis
    let mut class_map = compiler::build_class_map_pub(&column_map);
    if !class_map.is_empty() {
        compiler::propagate_class_decomposition_pub(model, &mut state, &mut column_map, &mut class_map, bins);
    }
    let edge_map = compiler::compute_edge_series_pub(model, &mut state, &mut column_map, &class_map, bins);

    let mut result = EvalResult {
        state,
        column_map,
        bins,
        warnings: Vec::new(),
        class_map,
        classes: model.classes.clone(),
        edge_map,
    };
    result.warnings = flowtime_core::analysis::analyze(model, &result);
    Ok(result)
}

fn parse_overrides(params: &serde_json::Value) -> Vec<(String, ParamValue)> {
    match params.get("overrides") {
        Some(obj) if obj.is_object() => {
            let map = obj.as_object().unwrap();
            let mut ov = Vec::new();
            for (key, val) in map {
                let pv = if let Some(n) = val.as_f64() {
                    ParamValue::Scalar(n)
                } else if let Some(arr) = val.as_array() {
                    let nums: Vec<f64> = arr.iter().filter_map(|v| v.as_f64()).collect();
                    ParamValue::Vector(nums)
                } else {
                    continue;
                };
                ov.push((key.clone(), pv));
            }
            ov
        }
        _ => Vec::new(),
    }
}

fn build_param_infos(plan: &Plan) -> Vec<ParamInfo> {
    plan.params.entries.iter().map(|p| {
        ParamInfo {
            id: p.id.clone(),
            kind: format!("{:?}", p.kind),
            default: match &p.default {
                ParamValue::Scalar(v) => serde_json::json!(v),
                ParamValue::Vector(v) => serde_json::json!(v),
            },
        }
    }).collect()
}

/// Extract all non-internal series from an EvalResult.
fn extract_all_series(result: &EvalResult) -> HashMap<String, Vec<f64>> {
    let mut map = HashMap::new();
    for (_, name) in result.column_map.iter() {
        if name.starts_with("__temp_") || name.starts_with("__edge_") {
            continue;
        }
        if let Some(vals) = result.series(name) {
            map.insert(name.to_string(), vals);
        }
    }
    map
}

/// Run the session loop: read requests from stdin, write responses to stdout.
pub fn run_session() {
    let mut stdin = io::stdin().lock();
    let mut stdout = io::stdout().lock();
    let mut session = Session::new();

    eprintln!("flowtime-engine session ready");

    loop {
        let req = match protocol::read_message(&mut stdin) {
            Ok(Some(r)) => r,
            Ok(None) => {
                eprintln!("stdin EOF — session ending");
                break;
            }
            Err(e) => {
                let resp = Response::err("protocol_error", &format!("Failed to read message: {e}"));
                let _ = protocol::write_message(&mut stdout, &resp);
                continue;
            }
        };

        let resp = session.handle(req);

        if let Err(e) = protocol::write_message(&mut stdout, &resp) {
            eprintln!("Failed to write response: {e}");
            break;
        }
    }
}
