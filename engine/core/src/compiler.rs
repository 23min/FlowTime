//! Model compiler: ModelDefinition → Plan (column map + ops).
//!
//! For m-E20-02: handles const and expr nodes only (no topology).
//! Topology synthesis (queues, routers, constraints) comes in m-E20-03+.

use crate::expr::{self, BinaryOp, Expr};
use crate::model::ModelDefinition;
use crate::plan::{ColumnMap, Op, Plan};
use std::collections::{HashMap, HashSet, VecDeque};

#[derive(Debug)]
pub struct CompileError(pub String);

impl std::fmt::Display for CompileError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "Compile error: {}", self.0)
    }
}

impl std::error::Error for CompileError {}

/// Compiled evaluation result with named series access.
#[derive(Debug)]
pub struct EvalResult {
    pub state: Vec<f64>,
    pub column_map: ColumnMap,
    pub bins: usize,
}

impl EvalResult {
    /// Get a series by name.
    pub fn series(&self, name: &str) -> Option<Vec<f64>> {
        let col = self.column_map.get(name)?;
        Some(crate::eval::extract_column(&self.state, col, self.bins))
    }
}

/// Compile and evaluate a model, returning named series.
pub fn eval_model(model: &ModelDefinition) -> Result<EvalResult, CompileError> {
    let plan = compile(model)?;
    let state = crate::eval::evaluate(&plan);
    Ok(EvalResult {
        state,
        column_map: plan.column_map,
        bins: plan.bins,
    })
}

/// Compile a model into an evaluation plan.
pub fn compile(model: &ModelDefinition) -> Result<Plan, CompileError> {
    let grid = model.grid.as_ref()
        .ok_or_else(|| CompileError("Model must have a grid definition".into()))?;
    let bins = grid.bins as usize;
    if bins == 0 {
        return Err(CompileError("Grid bins must be positive".into()));
    }

    let mut column_map = ColumnMap::new();
    let mut ops = Vec::new();

    // Phase 1: assign column indices to all nodes
    for node in &model.nodes {
        if node.id.is_empty() {
            return Err(CompileError("Node must have an id".into()));
        }
        column_map.insert(&node.id);
    }

    // Phase 2: topological sort based on expression dependencies
    let order = topo_sort(&model.nodes, &column_map)?;

    // Phase 3: emit ops in topo order
    for &idx in &order {
        let node = &model.nodes[idx];
        let out = column_map.get(&node.id).unwrap();

        match node.kind.to_lowercase().as_str() {
            "const" => {
                let values = node.values.as_ref()
                    .ok_or_else(|| CompileError(format!("Const node '{}' requires values", node.id)))?;
                ops.push(Op::Const { out, values: values.clone() });
            }
            "expr" => {
                let expr_str = node.expr.as_ref()
                    .ok_or_else(|| CompileError(format!("Expr node '{}' requires expr field", node.id)))?;
                let ast = expr::parse(expr_str)
                    .map_err(|e| CompileError(format!("Node '{}': {}", node.id, e)))?;
                compile_expr(&ast, out, &mut column_map, &mut ops, bins)?;
            }
            "pmf" => {
                // PMF node: compute expected value, emit as constant
                let pmf = node.pmf.as_ref()
                    .ok_or_else(|| CompileError(format!("PMF node '{}' requires pmf field", node.id)))?;
                if pmf.values.len() != pmf.probabilities.len() {
                    return Err(CompileError(format!("PMF node '{}': values and probabilities must have same length", node.id)));
                }
                let prob_sum: f64 = pmf.probabilities.iter().sum();
                if prob_sum <= 0.0 {
                    return Err(CompileError(format!("PMF node '{}': probabilities must sum to positive value", node.id)));
                }
                let expected: f64 = pmf.values.iter()
                    .zip(pmf.probabilities.iter())
                    .map(|(v, p)| v * p / prob_sum)
                    .sum();
                ops.push(Op::Const { out, values: vec![expected; bins] });
            }
            other => {
                // Topology-dependent kinds (serviceWithBuffer, router) are handled in m-E20-03+.
                // For now, skip unknown kinds with a warning rather than failing,
                // so models with topology can at least compile their const/expr nodes.
                return Err(CompileError(format!("Unsupported node kind '{}' on node '{}' (topology nodes require m-E20-03)", other, node.id)));
            }
        }
    }

    Ok(Plan { ops, column_map, bins })
}

/// Compile an expression AST into ops. Returns the column index of the result.
/// If the expression is a simple node reference, returns that node's column
/// and emits a Copy op to the output column.
fn compile_expr(
    ast: &Expr,
    out: usize,
    cm: &mut ColumnMap,
    ops: &mut Vec<Op>,
    bins: usize,
) -> Result<(), CompileError> {
    let result_col = emit_expr(ast, cm, ops, bins)?;
    if result_col != out {
        ops.push(Op::Copy { out, input: result_col });
    }
    Ok(())
}

/// Recursively emit ops for an expression, returning the column index
/// where the result lives. May allocate temp columns for intermediates.
fn emit_expr(
    ast: &Expr,
    cm: &mut ColumnMap,
    ops: &mut Vec<Op>,
    bins: usize,
) -> Result<usize, CompileError> {
    match ast {
        Expr::Literal(val) => {
            let col = cm.alloc_temp();
            ops.push(Op::Const { out: col, values: vec![*val; bins] });
            Ok(col)
        }
        Expr::NodeRef(name) => {
            cm.get(name).ok_or_else(|| CompileError(format!("Unknown node reference: '{name}'")))
        }
        Expr::BinaryOp { op, left, right } => {
            let l = emit_expr(left, cm, ops, bins)?;
            let r = emit_expr(right, cm, ops, bins)?;
            let out = cm.alloc_temp();
            let op = match op {
                BinaryOp::Add => Op::VecAdd { out, a: l, b: r },
                BinaryOp::Subtract => Op::VecSub { out, a: l, b: r },
                BinaryOp::Multiply => Op::VecMul { out, a: l, b: r },
                BinaryOp::Divide => Op::VecDiv { out, a: l, b: r },
            };
            ops.push(op);
            Ok(out)
        }
        Expr::FunctionCall { name, args } => {
            emit_function(name, args, cm, ops, bins)
        }
        Expr::ArrayLiteral(values) => {
            let col = cm.alloc_temp();
            ops.push(Op::Const { out: col, values: values.clone() });
            Ok(col)
        }
    }
}

fn emit_function(
    name: &str,
    args: &[Expr],
    cm: &mut ColumnMap,
    ops: &mut Vec<Op>,
    bins: usize,
) -> Result<usize, CompileError> {
    let upper = name.to_uppercase();
    match upper.as_str() {
        "MIN" => {
            check_args(name, args, 2)?;
            let a = emit_expr(&args[0], cm, ops, bins)?;
            let b = emit_expr(&args[1], cm, ops, bins)?;
            let out = cm.alloc_temp();
            ops.push(Op::VecMin { out, a, b });
            Ok(out)
        }
        "MAX" => {
            check_args(name, args, 2)?;
            let a = emit_expr(&args[0], cm, ops, bins)?;
            let b = emit_expr(&args[1], cm, ops, bins)?;
            let out = cm.alloc_temp();
            ops.push(Op::VecMax { out, a, b });
            Ok(out)
        }
        "CLAMP" => {
            check_args(name, args, 3)?;
            let val = emit_expr(&args[0], cm, ops, bins)?;
            let lo = emit_expr(&args[1], cm, ops, bins)?;
            let hi = emit_expr(&args[2], cm, ops, bins)?;
            let out = cm.alloc_temp();
            ops.push(Op::Clamp { out, val, lo, hi });
            Ok(out)
        }
        "MOD" => {
            check_args(name, args, 2)?;
            let a = emit_expr(&args[0], cm, ops, bins)?;
            let b = emit_expr(&args[1], cm, ops, bins)?;
            let out = cm.alloc_temp();
            ops.push(Op::Mod { out, a, b });
            Ok(out)
        }
        "FLOOR" => {
            check_args(name, args, 1)?;
            let input = emit_expr(&args[0], cm, ops, bins)?;
            let out = cm.alloc_temp();
            ops.push(Op::Floor { out, input });
            Ok(out)
        }
        "CEIL" => {
            check_args(name, args, 1)?;
            let input = emit_expr(&args[0], cm, ops, bins)?;
            let out = cm.alloc_temp();
            ops.push(Op::Ceil { out, input });
            Ok(out)
        }
        "ROUND" => {
            check_args(name, args, 1)?;
            let input = emit_expr(&args[0], cm, ops, bins)?;
            let out = cm.alloc_temp();
            ops.push(Op::Round { out, input });
            Ok(out)
        }
        "STEP" => {
            check_args(name, args, 2)?;
            let input = emit_expr(&args[0], cm, ops, bins)?;
            let threshold = emit_expr(&args[1], cm, ops, bins)?;
            let out = cm.alloc_temp();
            ops.push(Op::Step { out, input, threshold });
            Ok(out)
        }
        "PULSE" => {
            if args.is_empty() || args.len() > 3 {
                return Err(CompileError(format!("PULSE requires 1-3 arguments, got {}", args.len())));
            }
            let period = match &args[0] {
                Expr::Literal(v) => *v as usize,
                _ => return Err(CompileError("PULSE period must be a literal".into())),
            };
            let phase = if args.len() >= 2 {
                match &args[1] {
                    Expr::Literal(v) => *v as usize,
                    _ => return Err(CompileError("PULSE phase must be a literal".into())),
                }
            } else { 0 };
            let amplitude = if args.len() == 3 {
                Some(emit_expr(&args[2], cm, ops, bins)?)
            } else { None };
            let out = cm.alloc_temp();
            ops.push(Op::Pulse { out, period, phase, amplitude });
            Ok(out)
        }
        "SHIFT" | "CONV" => {
            // These are sequential ops added in m-E20-03
            Err(CompileError(format!("{name} function not yet supported (requires m-E20-03)")))
        }
        _ => Err(CompileError(format!("Unknown function: {name}"))),
    }
}

fn check_args(name: &str, args: &[Expr], expected: usize) -> Result<(), CompileError> {
    if args.len() != expected {
        Err(CompileError(format!("{name} requires {expected} arguments, got {}", args.len())))
    } else {
        Ok(())
    }
}

/// Topological sort of nodes based on expression dependencies.
/// Returns indices into model.nodes in evaluation order.
fn topo_sort(
    nodes: &[crate::model::NodeDefinition],
    _cm: &ColumnMap,
) -> Result<Vec<usize>, CompileError> {
    let n = nodes.len();
    let name_to_idx: HashMap<&str, usize> = nodes.iter().enumerate()
        .map(|(i, nd)| (nd.id.as_str(), i))
        .collect();

    // Build adjacency: node index → set of dependency node indices
    let mut in_degree = vec![0usize; n];
    let mut dependents: Vec<Vec<usize>> = vec![Vec::new(); n]; // dep → [nodes that depend on dep]

    for (i, node) in nodes.iter().enumerate() {
        if let Some(expr_str) = &node.expr {
            if let Ok(ast) = expr::parse(expr_str) {
                let refs = collect_refs(&ast);
                for r in &refs {
                    if let Some(&dep_idx) = name_to_idx.get(r.as_str()) {
                        in_degree[i] += 1;
                        dependents[dep_idx].push(i);
                    }
                }
            }
        }
    }

    // Kahn's algorithm
    let mut queue: VecDeque<usize> = (0..n).filter(|&i| in_degree[i] == 0).collect();
    let mut order = Vec::with_capacity(n);

    while let Some(idx) = queue.pop_front() {
        order.push(idx);
        for &dep in &dependents[idx] {
            in_degree[dep] -= 1;
            if in_degree[dep] == 0 {
                queue.push_back(dep);
            }
        }
    }

    if order.len() != n {
        return Err(CompileError("Model has a dependency cycle".into()));
    }

    Ok(order)
}

/// Collect all node references from an expression AST.
fn collect_refs(ast: &Expr) -> HashSet<String> {
    let mut refs = HashSet::new();
    collect_refs_inner(ast, &mut refs);
    refs
}

fn collect_refs_inner(ast: &Expr, refs: &mut HashSet<String>) {
    match ast {
        Expr::NodeRef(name) => { refs.insert(name.clone()); }
        Expr::BinaryOp { left, right, .. } => {
            collect_refs_inner(left, refs);
            collect_refs_inner(right, refs);
        }
        Expr::FunctionCall { args, .. } => {
            for arg in args {
                collect_refs_inner(arg, refs);
            }
        }
        Expr::Literal(_) | Expr::ArrayLiteral(_) => {}
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::{ModelDefinition, GridDefinition, NodeDefinition};

    fn make_grid(bins: i32) -> GridDefinition {
        GridDefinition { bins, bin_size: 1, bin_unit: "hours".into(), ..Default::default() }
    }

    #[test]
    fn compile_const_only() {
        let model = ModelDefinition {
            grid: Some(make_grid(4)),
            nodes: vec![
                NodeDefinition { id: "a".into(), kind: "const".into(), values: Some(vec![1.0, 2.0, 3.0, 4.0]), ..Default::default() },
            ],
            ..Default::default()
        };

        let result = eval_model(&model).unwrap();
        assert_eq!(result.series("a").unwrap(), vec![1.0, 2.0, 3.0, 4.0]);
    }

    #[test]
    fn compile_expr_scalar_mul() {
        let model = ModelDefinition {
            grid: Some(make_grid(4)),
            nodes: vec![
                NodeDefinition { id: "demand".into(), kind: "const".into(), values: Some(vec![10.0; 4]), ..Default::default() },
                NodeDefinition { id: "served".into(), kind: "expr".into(), expr: Some("demand * 0.8".into()), ..Default::default() },
            ],
            ..Default::default()
        };

        let result = eval_model(&model).unwrap();
        assert_eq!(result.series("demand").unwrap(), vec![10.0; 4]);
        assert_eq!(result.series("served").unwrap(), vec![8.0; 4]);
    }

    #[test]
    fn compile_chained_expressions() {
        let model = ModelDefinition {
            grid: Some(make_grid(3)),
            nodes: vec![
                NodeDefinition { id: "a".into(), kind: "const".into(), values: Some(vec![10.0, 20.0, 30.0]), ..Default::default() },
                NodeDefinition { id: "b".into(), kind: "expr".into(), expr: Some("a * 2".into()), ..Default::default() },
                NodeDefinition { id: "c".into(), kind: "expr".into(), expr: Some("b + a".into()), ..Default::default() },
            ],
            ..Default::default()
        };

        let result = eval_model(&model).unwrap();
        assert_eq!(result.series("a").unwrap(), vec![10.0, 20.0, 30.0]);
        assert_eq!(result.series("b").unwrap(), vec![20.0, 40.0, 60.0]);
        assert_eq!(result.series("c").unwrap(), vec![30.0, 60.0, 90.0]);
    }

    #[test]
    fn compile_min_max_clamp() {
        let model = ModelDefinition {
            grid: Some(make_grid(3)),
            nodes: vec![
                NodeDefinition { id: "a".into(), kind: "const".into(), values: Some(vec![1.0, 5.0, 9.0]), ..Default::default() },
                NodeDefinition { id: "b".into(), kind: "const".into(), values: Some(vec![3.0, 3.0, 3.0]), ..Default::default() },
                NodeDefinition { id: "mn".into(), kind: "expr".into(), expr: Some("MIN(a, b)".into()), ..Default::default() },
                NodeDefinition { id: "mx".into(), kind: "expr".into(), expr: Some("MAX(a, b)".into()), ..Default::default() },
                NodeDefinition { id: "cl".into(), kind: "expr".into(), expr: Some("CLAMP(a, 2, 7)".into()), ..Default::default() },
            ],
            ..Default::default()
        };

        let result = eval_model(&model).unwrap();
        assert_eq!(result.series("mn").unwrap(), vec![1.0, 3.0, 3.0]);
        assert_eq!(result.series("mx").unwrap(), vec![3.0, 5.0, 9.0]);
        assert_eq!(result.series("cl").unwrap(), vec![2.0, 5.0, 7.0]);
    }

    #[test]
    fn compile_pmf_expected_value() {
        let model = ModelDefinition {
            grid: Some(make_grid(3)),
            nodes: vec![
                NodeDefinition {
                    id: "latency".into(), kind: "pmf".into(),
                    pmf: Some(crate::model::PmfDefinition {
                        values: vec![10.0, 20.0, 30.0],
                        probabilities: vec![0.5, 0.3, 0.2],
                    }),
                    ..Default::default()
                },
            ],
            ..Default::default()
        };

        let result = eval_model(&model).unwrap();
        let expected = 10.0 * 0.5 + 20.0 * 0.3 + 30.0 * 0.2; // 17.0
        assert_eq!(result.series("latency").unwrap(), vec![expected; 3]);
    }

    #[test]
    fn compile_cycle_detection() {
        let model = ModelDefinition {
            grid: Some(make_grid(3)),
            nodes: vec![
                NodeDefinition { id: "a".into(), kind: "expr".into(), expr: Some("b".into()), ..Default::default() },
                NodeDefinition { id: "b".into(), kind: "expr".into(), expr: Some("a".into()), ..Default::default() },
            ],
            ..Default::default()
        };

        let err = eval_model(&model).unwrap_err();
        assert!(err.0.contains("cycle"), "Expected cycle error, got: {}", err.0);
    }

    #[test]
    fn compile_hello_fixture() {
        let yaml = std::fs::read_to_string(
            std::path::PathBuf::from(env!("CARGO_MANIFEST_DIR")).parent().unwrap().join("fixtures/hello.yaml")
        ).unwrap();
        let model = crate::model::parse_model_yaml(&yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // demand = [10, 10, 10, 10, 10, 10, 10, 10]
        // served = demand * 0.8 = [8, 8, 8, 8, 8, 8, 8, 8]
        assert_eq!(result.series("demand").unwrap(), vec![10.0; 8]);
        assert_eq!(result.series("served").unwrap(), vec![8.0; 8]);
    }

    #[test]
    fn plan_format_readable() {
        let model = ModelDefinition {
            grid: Some(make_grid(4)),
            nodes: vec![
                NodeDefinition { id: "demand".into(), kind: "const".into(), values: Some(vec![10.0; 4]), ..Default::default() },
                NodeDefinition { id: "served".into(), kind: "expr".into(), expr: Some("demand * 0.8".into()), ..Default::default() },
            ],
            ..Default::default()
        };

        let plan = compile(&model).unwrap();
        let text = plan.format();
        assert!(text.contains("demand"));
        assert!(text.contains("served"));
        assert!(text.contains("Const"));
    }
}
