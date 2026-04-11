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

/// Per-class series mapping: (node_id, class_id) → column index in the state matrix.
pub type ClassMap = HashMap<(String, String), usize>;

/// Per-edge series mapping: (edge_id, metric) → column index in the state matrix.
/// Metrics: "flowVolume", "attemptsVolume", "failuresVolume", "retryVolume".
pub type EdgeMap = HashMap<(String, String), usize>;

/// Compiled evaluation result with named series access.
#[derive(Debug)]
pub struct EvalResult {
    pub state: Vec<f64>,
    pub column_map: ColumnMap,
    pub bins: usize,
    pub warnings: Vec<crate::analysis::Warning>,
    /// Per-class column indices: (node_id, class_id) → column in state matrix.
    pub class_map: ClassMap,
    /// Declared class definitions from the model.
    pub classes: Vec<crate::model::ClassDefinition>,
    /// Per-edge series: (edge_id, metric) → column in state matrix.
    pub edge_map: EdgeMap,
}

impl EvalResult {
    /// Get a series by name.
    pub fn series(&self, name: &str) -> Option<Vec<f64>> {
        let col = self.column_map.get(name)?;
        Some(crate::eval::extract_column(&self.state, col, self.bins))
    }
}

/// Compile, evaluate, and analyze a model, returning named series + warnings.
pub fn eval_model(model: &ModelDefinition) -> Result<EvalResult, CompileError> {
    eval_model_with_params(model, &[])
}

/// Compile, evaluate with parameter overrides, and analyze a model.
///
/// This is the full pipeline: compile → evaluate (with overrides) → class decomposition →
/// edge series → analysis warnings. The Plan is compiled fresh each call.
/// For compile-once eval-many, use `compile()` + `evaluate_with_params()` directly.
pub fn eval_model_with_params(
    model: &ModelDefinition,
    overrides: &[(String, crate::plan::ParamValue)],
) -> Result<EvalResult, CompileError> {
    let plan = compile(model)?;
    let bins = plan.bins;
    let mut state = crate::eval::evaluate_with_params(&plan, overrides);
    let mut column_map = plan.column_map;

    // Build class map from columns named {nodeId}__class_{classId}
    let mut class_map = build_class_map(&column_map);

    // Post-evaluation: propagate per-class decomposition to downstream nodes.
    // For each non-class node whose inputs have per-class columns, compute
    // per-class decomposition via proportional allocation.
    if !class_map.is_empty() {
        propagate_class_decomposition(model, &mut state, &mut column_map, &mut class_map, bins);
    }

    // Compute edge series: flowVolume for each topology edge
    let edge_map = compute_edge_series(model, &mut state, &mut column_map, &class_map, bins);

    let mut result = EvalResult {
        state,
        column_map,
        bins,
        warnings: Vec::new(),
        class_map,
        classes: model.classes.clone(),
        edge_map,
    };
    result.warnings = crate::analysis::analyze(model, &result);
    Ok(result)
}

/// Propagate per-class series to downstream nodes using proportional allocation.
///
/// For each expr node that depends on a class-aware source, we compute:
///   node__class_{c}[t] = node[t] * (source__class_{c}[t] / source[t])
///
/// This is the same proportional decomposition the C# ClassContributionBuilder uses,
/// but simpler: we only handle the primary source (first dependency).
///
/// After model nodes, topology nodes (serviceWithBuffer) also get per-class
/// decomposition for queue depth based on arrival class fractions.
fn propagate_class_decomposition(
    model: &ModelDefinition,
    state: &mut Vec<f64>,
    cm: &mut ColumnMap,
    class_map: &mut ClassMap,
    bins: usize,
) {
    // Collect class IDs from class_map
    let class_ids: Vec<String> = {
        let mut ids: HashSet<String> = HashSet::new();
        for ((_, class_id), _) in class_map.iter() {
            ids.insert(class_id.clone());
        }
        ids.into_iter().collect()
    };

    if class_ids.is_empty() { return; }

    // Pass 0: Normalize source arrival columns so per-class sums equal node totals.
    // Phase 1c creates per-class arrival columns with absolute rates from traffic.arrivals.
    // For normalization (AC-9), we rescale: col[t] = node[t] * (col[t] / sum_classes[t])
    // This preserves the relative fractions while ensuring sum == total.
    normalize_source_class_columns(model, state, cm, class_map, &class_ids, bins);

    // Pass 1: Model nodes (const, expr, pmf)
    // Process in model order (respects dependency order for simple chains).
    for node in &model.nodes {
        // Skip if this node already has per-class columns
        if class_map.contains_key(&(node.id.clone(), class_ids[0].clone())) {
            continue;
        }

        // Find the primary class-aware source for this node
        let source_node_id = find_class_source(node, model, class_map, &class_ids);
        let source_node_id = match source_node_id {
            Some(id) => id,
            None => continue,
        };

        allocate_proportional(state, cm, class_map, &class_ids, &node.id, &source_node_id, bins);
    }

    // Pass 2: Topology nodes — queue depth per-class decomposition
    // For each serviceWithBuffer node, if its arrivals source has per-class columns,
    // compute per-class queue depth proportional to arrival class fractions.
    if let Some(topo) = &model.topology {
        for tnode in &topo.nodes {
            let kind = tnode.kind.as_deref().unwrap_or("serviceWithBuffer").to_lowercase();
            if !matches!(kind.as_str(), "servicewithbuffer" | "queue" | "dlq") {
                continue;
            }

            // Resolve queue depth column name
            let q_col_name = if let Some(qd_ref) = &tnode.semantics.queue_depth {
                if !qd_ref.is_empty() { qd_ref.clone() }
                else { queue_column_id(&tnode.id) }
            } else {
                queue_column_id(&tnode.id)
            };

            // Skip if already has per-class columns
            if class_map.contains_key(&(q_col_name.clone(), class_ids[0].clone())) {
                continue;
            }

            // Find the arrivals source — it must have per-class columns
            let arrivals_ref = &tnode.semantics.arrivals;
            if arrivals_ref.is_empty() { continue; }
            if !class_map.contains_key(&(arrivals_ref.clone(), class_ids[0].clone())) {
                continue;
            }

            // Proportional decomposition of queue depth based on arrivals fractions
            allocate_proportional(state, cm, class_map, &class_ids, &q_col_name, arrivals_ref, bins);
        }
    }
}

/// Normalize source arrival per-class columns so their sum equals the node total at each bin.
///
/// Traffic arrivals create per-class columns with absolute rates. For the normalization
/// invariant (AC-9: sum of per-class == total), we rescale each column:
///   col[t] = node_total[t] * (col[t] / sum_of_classes[t])
///
/// This preserves the relative class fractions while ensuring sum == total.
/// Must run AFTER evaluation (source values are final) and BEFORE downstream propagation
/// (so downstream nodes inherit the correct proportional values).
fn normalize_source_class_columns(
    model: &ModelDefinition,
    state: &mut Vec<f64>,
    cm: &ColumnMap,
    class_map: &ClassMap,
    class_ids: &[String],
    bins: usize,
) {
    // Find source nodes that have per-class columns from traffic arrivals
    let traffic = match &model.traffic {
        Some(t) => t,
        None => return,
    };

    let mut source_nodes: HashSet<String> = HashSet::new();
    for arrival in &traffic.arrivals {
        if !arrival.class_id.is_empty() {
            source_nodes.insert(arrival.node_id.clone());
        }
    }

    for source_id in &source_nodes {
        let node_col = match cm.get(source_id) {
            Some(col) => col,
            None => continue,
        };

        // Compute sum of all per-class columns
        let mut class_sum = vec![0.0_f64; bins];
        for class_id in class_ids {
            if let Some(&col) = class_map.get(&(source_id.clone(), class_id.clone())) {
                for t in 0..bins {
                    class_sum[t] += state[col * bins + t];
                }
            }
        }

        // Rescale each per-class column: col[t] = total[t] * (col[t] / class_sum[t])
        for class_id in class_ids {
            if let Some(&col) = class_map.get(&(source_id.clone(), class_id.clone())) {
                for t in 0..bins {
                    let total = state[node_col * bins + t];
                    let class_val = state[col * bins + t];
                    let normalized = if class_sum[t].abs() < 1e-15 {
                        0.0
                    } else {
                        total * (class_val / class_sum[t])
                    };
                    state[col * bins + t] = normalized;
                }
            }
        }
    }
}

/// Allocate per-class columns for `target_id` proportional to class fractions of `source_id`.
///
/// Computes: target__class_{c}[t] = target[t] * (source__class_{c}[t] / sum_classes[t])
fn allocate_proportional(
    state: &mut Vec<f64>,
    cm: &mut ColumnMap,
    class_map: &mut ClassMap,
    class_ids: &[String],
    target_id: &str,
    source_id: &str,
    bins: usize,
) {
    let target_col = match cm.get(target_id) {
        Some(col) => col,
        None => return,
    };
    // source_col not directly used but needed for validation
    if cm.get(source_id).is_none() { return; }

    // Compute sum of all per-class columns for the source at each bin
    let mut class_sum = vec![0.0_f64; bins];
    for class_id in class_ids {
        if let Some(&col) = class_map.get(&(source_id.to_string(), class_id.clone())) {
            for t in 0..bins {
                class_sum[t] += state[col * bins + t];
            }
        }
    }

    // Proportional decomposition for each class
    for class_id in class_ids {
        let source_class_col = match class_map.get(&(source_id.to_string(), class_id.clone())) {
            Some(&col) => col,
            None => continue,
        };

        let class_col_name = format!("{}__class_{}", target_id, class_id);
        let new_col = cm.get_or_insert(&class_col_name);

        // Extend state to accommodate new column
        let required_len = (new_col + 1) * bins;
        if state.len() < required_len {
            state.resize(required_len, 0.0);
        }

        for t in 0..bins {
            let target_val = state[target_col * bins + t];
            let source_class_val = state[source_class_col * bins + t];

            let class_val = if class_sum[t].abs() < 1e-15 {
                0.0
            } else {
                target_val * (source_class_val / class_sum[t])
            };

            state[new_col * bins + t] = class_val;
        }

        class_map.insert((target_id.to_string(), class_id.clone()), new_col);
    }
}

/// Compute edge series for each topology edge.
///
/// For throughput edges: flowVolume = source_served * (edge_weight / sum_sibling_weights)
/// Per-class edge flow is proportional to class fractions at source.
fn compute_edge_series(
    model: &ModelDefinition,
    state: &mut Vec<f64>,
    cm: &mut ColumnMap,
    class_map: &ClassMap,
    bins: usize,
) -> EdgeMap {
    let mut edge_map = EdgeMap::new();

    let topo = match &model.topology {
        Some(t) => t,
        None => return edge_map,
    };

    if topo.edges.is_empty() { return edge_map; }

    // Group edges by source to compute weight normalization
    let mut source_total_weight: HashMap<String, f64> = HashMap::new();
    for edge in &topo.edges {
        *source_total_weight.entry(edge.source.clone()).or_insert(0.0) += edge.weight;
    }

    // Collect class IDs
    let class_ids: Vec<String> = {
        let mut ids: HashSet<String> = HashSet::new();
        for ((_, class_id), _) in class_map.iter() {
            ids.insert(class_id.clone());
        }
        ids.into_iter().collect()
    };

    for edge in &topo.edges {
        let edge_id = edge.id.clone().unwrap_or_else(|| format!("{}→{}", edge.source, edge.target));

        // Resolve source series: find the topology node's served semantic, or fall back to source node
        let source_series_name = resolve_edge_source_series(topo, &edge.source, edge.measure.as_deref());
        let source_col = match cm.get(&source_series_name) {
            Some(col) => col,
            None => continue,
        };

        let total_weight = *source_total_weight.get(&edge.source).unwrap_or(&1.0);
        let fraction = if total_weight > 0.0 { edge.weight / total_weight } else { 0.0 };
        let multiplier = edge.multiplier.unwrap_or(1.0);

        // Determine metric name based on measure
        let metric = match edge.measure.as_deref() {
            Some("attempts") | Some("load") => "attemptsVolume",
            Some("errors") | Some("failures") | Some("exhaustedfailures") => "failuresVolume",
            _ => "flowVolume",
        };

        // Compute edge flow: source * fraction * multiplier
        let edge_col_name = format!("__edge_{}_{}", edge_id, metric);
        let edge_col = cm.get_or_insert(&edge_col_name);

        let required_len = (edge_col + 1) * bins;
        if state.len() < required_len {
            state.resize(required_len, 0.0);
        }

        for t in 0..bins {
            let source_val = state[source_col * bins + t];
            state[edge_col * bins + t] = source_val * fraction * multiplier;
        }

        edge_map.insert((edge_id.clone(), metric.to_string()), edge_col);

        // Per-class edge flow
        if !class_ids.is_empty() && class_map.contains_key(&(source_series_name.clone(), class_ids[0].clone())) {
            for class_id in &class_ids {
                if let Some(&src_class_col) = class_map.get(&(source_series_name.clone(), class_id.clone())) {
                    let class_edge_name = format!("__edge_{}_{}__class_{}", edge_id, metric, class_id);
                    let class_edge_col = cm.get_or_insert(&class_edge_name);

                    let required_len = (class_edge_col + 1) * bins;
                    if state.len() < required_len {
                        state.resize(required_len, 0.0);
                    }

                    for t in 0..bins {
                        let src_class_val = state[src_class_col * bins + t];
                        state[class_edge_col * bins + t] = src_class_val * fraction * multiplier;
                    }

                    edge_map.insert(
                        (format!("{}@{}", edge_id, class_id), metric.to_string()),
                        class_edge_col,
                    );
                }
            }
        }
    }

    edge_map
}

/// Resolve the source series name for an edge.
/// Looks at the topology node's served semantic for the source, or uses the source ID directly.
fn resolve_edge_source_series(
    topo: &crate::model::TopologyDefinition,
    source: &str,
    measure: Option<&str>,
) -> String {
    // Check if source is a topology node with semantics
    if let Some(tnode) = topo.nodes.iter().find(|n| n.id == source) {
        match measure {
            Some("attempts") | Some("load") => {
                if let Some(ref a) = tnode.semantics.attempts {
                    if !a.is_empty() { return a.clone(); }
                }
            }
            Some("errors") => {
                if let Some(ref e) = tnode.semantics.errors {
                    if !e.is_empty() { return e.clone(); }
                }
            }
            Some("failures") => {
                if let Some(ref f) = tnode.semantics.failures {
                    if !f.is_empty() { return f.clone(); }
                }
            }
            _ => {}
        }
        // Default: use served
        if !tnode.semantics.served.is_empty() {
            return tnode.semantics.served.clone();
        }
    }
    // Fall back to source node ID directly (it's a model node, not a topology node)
    source.to_string()
}

/// Find the primary class-aware source node for a given node.
/// Walks expression dependencies to find the first source that has per-class columns.
fn find_class_source(
    node: &crate::model::NodeDefinition,
    model: &ModelDefinition,
    class_map: &ClassMap,
    class_ids: &[String],
) -> Option<String> {
    // For expr nodes: find dependencies that have per-class columns
    if let Some(expr_str) = &node.expr {
        let expr = crate::expr::parse(expr_str).ok()?;
        let deps = collect_refs(&expr); // returns HashSet<String>
        for dep in deps.iter() {
            if class_map.contains_key(&(dep.clone(), class_ids[0].clone())) {
                return Some(dep.clone());
            }
            // Check if any model node by this name has a class-aware source (transitive)
            let dep_node = model.nodes.iter().find(|n| n.id == *dep)?;
            if let Some(source) = find_class_source(dep_node, model, class_map, class_ids) {
                return Some(source);
            }
        }
    }
    None
}

// collect_refs is defined later in this file (line ~1349) — reused here.

/// Extract class map from column names matching the pattern `{nodeId}__class_{classId}`.
/// Build the parameter table by identifying user-visible constants in the model.
fn build_param_table(model: &ModelDefinition, cm: &crate::plan::ColumnMap) -> crate::plan::ParamTable {
    use crate::plan::{ParamTable, ParamEntry, ParamValue, ParamKind};

    let mut params = ParamTable::new();
    let bins = model.grid.as_ref().map_or(0, |g| g.bins as usize);

    // 1. Const nodes
    for node in &model.nodes {
        if node.kind.to_lowercase() != "const" { continue; }
        if let Some(col) = cm.get(&node.id) {
            if let Some(values) = &node.values {
                let is_uniform = values.iter().all(|v| (*v - values[0]).abs() < 1e-15);
                let default = if is_uniform && !values.is_empty() {
                    ParamValue::Scalar(values[0])
                } else {
                    ParamValue::Vector(values.clone())
                };
                params.register(ParamEntry {
                    id: node.id.clone(),
                    column: col,
                    default,
                    kind: ParamKind::ConstNode,
                });
            }
        }
    }

    // 2. Traffic arrival rates
    if let Some(traffic) = &model.traffic {
        for arrival in &traffic.arrivals {
            if arrival.class_id.is_empty() { continue; }
            if let Some(rate) = arrival.pattern.rate_per_bin {
                let class_col_name = format!("{}__class_{}", arrival.node_id, arrival.class_id);
                if let Some(col) = cm.get(&class_col_name) {
                    params.register(ParamEntry {
                        id: format!("{}.{}", arrival.node_id, arrival.class_id),
                        column: col,
                        default: ParamValue::Scalar(rate),
                        kind: ParamKind::ArrivalRate,
                    });
                }
            }
        }
    }

    // 3. Topology: WIP limits and initial conditions
    if let Some(topo) = &model.topology {
        for tnode in &topo.nodes {
            // WIP limit scalar
            if let Some(wl) = tnode.wip_limit {
                let wl_col_name = format!("{}_wip_limit", to_snake_case(&tnode.id));
                if let Some(col) = cm.get(&wl_col_name) {
                    params.register(ParamEntry {
                        id: format!("{}.wipLimit", tnode.id),
                        column: col,
                        default: ParamValue::Scalar(wl),
                        kind: ParamKind::WipLimit,
                    });
                }
            }

            // Initial condition
            if let Some(ic) = &tnode.initial_condition {
                if ic.queue_depth != 0.0 {
                    // Initial condition is baked into QueueRecurrence init field,
                    // not a Const op column. Register it for schema visibility
                    // but it can't be overridden via column patching.
                    // Future: add Op::QueueRecurrence init override support.
                }
            }
        }
    }

    params
}

fn build_class_map(cm: &ColumnMap) -> ClassMap {
    let mut map = ClassMap::new();
    for (idx, name) in cm.iter() {
        if let Some(pos) = name.find("__class_") {
            let node_id = &name[..pos];
            let class_id = &name[pos + 8..]; // len("__class_") == 8
            if !class_id.is_empty() {
                map.insert((node_id.to_string(), class_id.to_string()), idx);
            }
        }
    }
    map
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

    // Phase 1: assign column indices to all explicit nodes
    for node in &model.nodes {
        if node.id.is_empty() {
            return Err(CompileError("Node must have an id".into()));
        }
        column_map.insert(&node.id);
    }

    // Phase 1b: pre-allocate columns that topology synthesis will create,
    // so expressions can reference them (e.g., pressure = queue_depth / 50).
    if let Some(topo) = &model.topology {
        for tnode in &topo.nodes {
            let kind = tnode.kind.as_deref().unwrap_or("serviceWithBuffer").to_lowercase();
            if matches!(kind.as_str(), "servicewithbuffer" | "queue" | "dlq") {
                // Queue depth column
                if let Some(qd_ref) = &tnode.semantics.queue_depth {
                    if !qd_ref.is_empty() { column_map.get_or_insert(qd_ref); }
                    else { column_map.get_or_insert(&queue_column_id(&tnode.id)); }
                } else {
                    column_map.get_or_insert(&queue_column_id(&tnode.id));
                }
                // Retry echo column
                if let Some(echo_ref) = &tnode.semantics.retry_echo {
                    if !echo_ref.is_empty() { column_map.get_or_insert(echo_ref); }
                }
            }
        }
    }

    // Phase 1c: create per-class arrival columns from traffic.arrivals.
    // These are created as Const ops with the declared per-class rate.
    // The column name convention is {sourceNodeId}__class_{classId}.
    if let Some(traffic) = &model.traffic {
        for arrival in &traffic.arrivals {
            if arrival.class_id.is_empty() { continue; }
            let class_col_name = format!("{}__class_{}", arrival.node_id, arrival.class_id);
            if column_map.get(&class_col_name).is_some() { continue; } // already created by router
            if let Some(rate) = arrival.pattern.rate_per_bin {
                let col = column_map.get_or_insert(&class_col_name);
                ops.push(Op::Const { out: col, values: vec![rate; bins] });
            }
        }
    }

    // Phase 2: build unified dependency graph and topological sort.
    // Includes both expression nodes and topology-produced columns.
    // SHIFT dependencies are excluded from same-bin edges (they read t-1).
    let topo_columns = gather_topology_columns(model, &column_map);
    let order = unified_topo_sort(&model.nodes, &column_map, &topo_columns)?;

    // Phase 3: emit ops in unified topo order
    let mut emitted_topo_nodes: HashSet<String> = HashSet::new();
    for item in &order {
        match item {
            CompileItem::Node(idx) => {
                let node = &model.nodes[*idx];
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
                    "router" => {
                        compile_router(node, model, &mut column_map, &mut ops, bins)?;
                    }
                    other => {
                        return Err(CompileError(format!("Unsupported node kind '{}' on node '{}'", other, node.id)));
                    }
                }
            }
            CompileItem::TopologyNode(tnode_id) => {
                if !emitted_topo_nodes.insert(tnode_id.clone()) { continue; }
                if let Some(topo) = &model.topology {
                    compile_single_topology_node(topo, tnode_id, &mut column_map, &mut ops, bins)?;
                }
            }
        }
    }

    // Phase 4: Constraint allocation — emit ProportionalAlloc ops and patch
    // QueueRecurrence inflows to use capped arrivals.
    if let Some(topo) = &model.topology {
        compile_constraints(topo, &mut column_map, &mut ops, bins)?;
    }

    // Phase 5: Derived metrics — utilization, cycle time, Kingman, etc.
    if let Some(topo) = &model.topology {
        let grid = model.grid.as_ref().unwrap();
        let bin_ms = grid_bin_ms(grid);
        compile_derived_metrics(topo, model, &mut column_map, &mut ops, bins, bin_ms)?;
    }

    // Build parameter table: register user-visible constants
    let params = build_param_table(model, &column_map);

    Ok(Plan { ops, column_map, bins, params })
}

/// An item in the unified compilation order.
#[derive(Debug)]
enum CompileItem {
    /// A model node (const, expr, pmf) — index into model.nodes
    Node(usize),
    /// A topology-synthesized node (queue recurrence) — topology node ID
    TopologyNode(String),
}

/// Info about a topology-produced column.
struct TopoColumnInfo {
    /// The column name produced by this topology node
    column_name: String,
    /// The topology node ID
    tnode_id: String,
    /// Column names that this topology node depends on (inflow, outflow, etc.)
    dependencies: Vec<String>,
    /// Topology node ID this node overflows to (if any)
    overflow_target: Option<String>,
}

/// Gather topology-produced columns and their dependencies.
fn gather_topology_columns(model: &ModelDefinition, cm: &ColumnMap) -> Vec<TopoColumnInfo> {
    let mut result = Vec::new();
    if let Some(topo) = &model.topology {
        for tnode in &topo.nodes {
            let kind = tnode.kind.as_deref().unwrap_or("serviceWithBuffer").to_lowercase();
            if !matches!(kind.as_str(), "servicewithbuffer" | "queue" | "dlq") { continue; }

            let col_name = if let Some(qd_ref) = &tnode.semantics.queue_depth {
                if !qd_ref.is_empty() { qd_ref.clone() }
                else { queue_column_id(&tnode.id) }
            } else {
                queue_column_id(&tnode.id)
            };

            let mut deps = Vec::new();
            // Inflow dependency
            if !tnode.semantics.arrivals.is_empty() {
                deps.push(tnode.semantics.arrivals.clone());
            }
            // Outflow dependency
            if !tnode.semantics.served.is_empty() {
                deps.push(tnode.semantics.served.clone());
            } else if let Some(cap) = &tnode.semantics.capacity {
                deps.push(cap.clone());
            }
            // Loss dependency
            if let Some(err_ref) = &tnode.semantics.errors {
                if !err_ref.is_empty() { deps.push(err_ref.clone()); }
            }

            let overflow_target = tnode.wip_overflow.as_ref()
                .filter(|s| !s.is_empty())
                .cloned();

            result.push(TopoColumnInfo {
                column_name: col_name,
                tnode_id: tnode.id.clone(),
                dependencies: deps,
                overflow_target,
            });
        }
    }
    result
}

/// Unified topological sort including both model nodes and topology-produced columns.
/// SHIFT dependencies are excluded from same-bin edges (they read t-1).
fn unified_topo_sort(
    nodes: &[crate::model::NodeDefinition],
    _cm: &ColumnMap,
    topo_columns: &[TopoColumnInfo],
) -> Result<Vec<CompileItem>, CompileError> {
    // Assign unique IDs: model nodes get index 0..n, topo columns get n..n+m
    let n = nodes.len();
    let m = topo_columns.len();
    let total = n + m;

    let mut name_to_item: HashMap<&str, usize> = HashMap::new();
    for (i, node) in nodes.iter().enumerate() {
        name_to_item.insert(&node.id, i);
    }
    for (j, tc) in topo_columns.iter().enumerate() {
        name_to_item.insert(&tc.column_name, n + j);
    }

    let mut in_degree = vec![0usize; total];
    let mut dependents: Vec<Vec<usize>> = vec![Vec::new(); total];

    // Model node expression dependencies
    for (i, node) in nodes.iter().enumerate() {
        if let Some(expr_str) = &node.expr {
            if let Ok(ast) = expr::parse(expr_str) {
                let refs = collect_refs(&ast);
                let shift_refs = collect_shift_refs(&ast);
                for r in &refs {
                    if shift_refs.contains(r) { continue; } // SHIFT refs don't create same-bin deps
                    if let Some(&dep_idx) = name_to_item.get(r.as_str()) {
                        in_degree[i] += 1;
                        dependents[dep_idx].push(i);
                    }
                }
            }
        }
    }

    // Topology column dependencies on model nodes
    for (j, tc) in topo_columns.iter().enumerate() {
        let item_idx = n + j;
        for dep_name in &tc.dependencies {
            if let Some(&dep_idx) = name_to_item.get(dep_name.as_str()) {
                in_degree[item_idx] += 1;
                dependents[dep_idx].push(item_idx);
            }
        }
    }

    // Model nodes that depend on topology columns (non-SHIFT refs)
    for (i, node) in nodes.iter().enumerate() {
        if let Some(expr_str) = &node.expr {
            if let Ok(ast) = expr::parse(expr_str) {
                let refs = collect_refs(&ast);
                let shift_refs = collect_shift_refs(&ast);
                for r in &refs {
                    if shift_refs.contains(r) { continue; }
                    for (j, tc) in topo_columns.iter().enumerate() {
                        if tc.column_name == *r {
                            in_degree[i] += 1;
                            dependents[n + j].push(i);
                        }
                    }
                }
            }
        }
    }

    // Overflow edges between topology columns: source must come before target
    let tnode_id_to_tc: HashMap<&str, usize> = topo_columns.iter().enumerate()
        .map(|(j, tc)| (tc.tnode_id.as_str(), j))
        .collect();
    for (j, tc) in topo_columns.iter().enumerate() {
        if let Some(target_id) = &tc.overflow_target {
            if let Some(&target_j) = tnode_id_to_tc.get(target_id.as_str()) {
                let src_idx = n + j;
                let tgt_idx = n + target_j;
                in_degree[tgt_idx] += 1;
                dependents[src_idx].push(tgt_idx);
            }
        }
    }

    // Kahn's algorithm
    let mut queue: VecDeque<usize> = (0..total).filter(|&i| in_degree[i] == 0).collect();
    let mut order = Vec::with_capacity(total);

    while let Some(idx) = queue.pop_front() {
        order.push(idx);
        for &dep in &dependents[idx] {
            in_degree[dep] -= 1;
            if in_degree[dep] == 0 {
                queue.push_back(dep);
            }
        }
    }

    if order.len() != total {
        return Err(CompileError("Model has a dependency cycle (check for circular SHIFT references)".into()));
    }

    Ok(order.into_iter().map(|idx| {
        if idx < n {
            CompileItem::Node(idx)
        } else {
            CompileItem::TopologyNode(topo_columns[idx - n].tnode_id.clone())
        }
    }).collect())
}

/// Collect node references that are inside SHIFT function calls.
/// These are temporal dependencies (read t-1) and don't create same-bin edges.
fn collect_shift_refs(ast: &Expr) -> HashSet<String> {
    let mut refs = HashSet::new();
    collect_shift_refs_inner(ast, &mut refs, false);
    refs
}

fn collect_shift_refs_inner(ast: &Expr, refs: &mut HashSet<String>, inside_shift: bool) {
    match ast {
        Expr::NodeRef(name) => {
            if inside_shift { refs.insert(name.clone()); }
        }
        Expr::BinaryOp { left, right, .. } => {
            collect_shift_refs_inner(left, refs, inside_shift);
            collect_shift_refs_inner(right, refs, inside_shift);
        }
        Expr::FunctionCall { name, args } => {
            let is_shift = name.to_uppercase() == "SHIFT";
            for arg in args {
                collect_shift_refs_inner(arg, refs, inside_shift || is_shift);
            }
        }
        Expr::Literal(_) | Expr::ArrayLiteral(_) => {}
    }
}

/// Convert a PascalCase or mixed-case ID to snake_case (matches C# ToSnakeCase).
/// Handles consecutive uppercase: "DLQ" → "dlq", "QueueA" → "queue_a",
/// "HTTPService" → "http_service".
fn to_snake_case(s: &str) -> String {
    let chars: Vec<char> = s.chars().collect();
    let mut result = String::with_capacity(s.len() + 4);
    for (i, &ch) in chars.iter().enumerate() {
        if ch.is_uppercase() && i > 0 {
            let prev_upper = chars[i - 1].is_uppercase();
            let next_lower = chars.get(i + 1).map_or(false, |c| c.is_lowercase());
            // Insert underscore before uppercase if previous was lowercase,
            // or if previous was uppercase but next is lowercase (end of acronym)
            if !prev_upper || next_lower {
                result.push('_');
            }
        }
        result.push(ch.to_lowercase().next().unwrap());
    }
    result
}

/// Compile a router node: split source series across targets by weight and/or class.
fn compile_router(
    node: &crate::model::NodeDefinition,
    model: &ModelDefinition,
    cm: &mut ColumnMap,
    ops: &mut Vec<Op>,
    bins: usize,
) -> Result<(), CompileError> {
    let router = node.router.as_ref()
        .ok_or_else(|| CompileError(format!("Router node '{}' requires router field", node.id)))?;

    // Resolve source column
    let source_ref = router.inputs.queue.as_deref()
        .ok_or_else(|| CompileError(format!("Router '{}': inputs.queue is required", node.id)))?;
    let source_col = cm.get(source_ref)
        .ok_or_else(|| CompileError(format!("Router '{}': source '{}' not found", node.id, source_ref)))?;

    if router.routes.is_empty() {
        return Err(CompileError(format!("Router '{}': must have at least one route", node.id)));
    }

    // Separate class routes and weight routes
    let weight_routes: Vec<&crate::model::RouterRouteDefinition> = router.routes.iter()
        .filter(|r| r.classes.as_ref().map_or(true, |c| c.is_empty()))
        .collect();

    let class_routes: Vec<&crate::model::RouterRouteDefinition> = router.routes.iter()
        .filter(|r| r.classes.as_ref().map_or(false, |c| !c.is_empty()))
        .collect();

    // Track accumulated target columns for multi-route-to-same-target
    let mut target_cols: HashMap<String, usize> = HashMap::new();

    // Build per-class column map from traffic.arrivals
    // Maps (sourceNodeId, classId) → column with per-class arrival values
    let mut class_columns: HashMap<String, usize> = HashMap::new();
    if let Some(traffic) = &model.traffic {
        for arrival in &traffic.arrivals {
            if arrival.node_id == source_ref && !arrival.class_id.is_empty() {
                // Look for an explicit per-class const node named {classId}_arrivals or similar
                let class_col_name = format!("{}__class_{}", source_ref, arrival.class_id);
                // Try to find a pre-existing column, or create from rate
                if let Some(col) = cm.get(&class_col_name) {
                    class_columns.insert(arrival.class_id.clone(), col);
                } else if let Some(rate) = arrival.pattern.rate_per_bin {
                    let col = cm.get_or_insert(&class_col_name);
                    ops.push(Op::Const { out: col, values: vec![rate; bins] });
                    class_columns.insert(arrival.class_id.clone(), col);
                }
            }
        }
    }

    // Phase 1: Class-based routes (extract per-class columns)
    let mut class_routed_total: Option<usize> = None;
    for route in &class_routes {
        let classes = route.classes.as_ref().unwrap();
        // Sum all class columns for this route
        let mut route_sum: Option<usize> = None;
        for class_id in classes {
            let class_col = class_columns.get(class_id)
                .ok_or_else(|| CompileError(format!("Router '{}': class '{}' not found in traffic arrivals", node.id, class_id)))?;
            route_sum = Some(match route_sum {
                None => *class_col,
                Some(prev) => {
                    let combined = cm.alloc_temp();
                    ops.push(Op::VecAdd { out: combined, a: prev, b: *class_col });
                    combined
                }
            });
        }

        if let Some(route_col) = route_sum {
            // Accumulate class-routed total (for subtracting from source later)
            class_routed_total = Some(match class_routed_total {
                None => route_col,
                Some(prev) => {
                    let combined = cm.alloc_temp();
                    ops.push(Op::VecAdd { out: combined, a: prev, b: route_col });
                    combined
                }
            });

            // Write to target
            let target_col = cm.get_or_insert(&route.target);
            if let Some(&existing) = target_cols.get(&route.target) {
                let combined = cm.alloc_temp();
                ops.push(Op::VecAdd { out: combined, a: existing, b: route_col });
                ops.push(Op::Copy { out: target_col, input: combined });
                target_cols.insert(route.target.clone(), combined);
            } else {
                ops.push(Op::Copy { out: target_col, input: route_col });
                target_cols.insert(route.target.clone(), route_col);
            }
        }
    }

    // Phase 2: Weight-based routes (split remaining flow by weight)
    let total_weight: f64 = weight_routes.iter()
        .map(|r| r.weight.unwrap_or(1.0))
        .sum();

    if total_weight <= 0.0 && !weight_routes.is_empty() {
        return Err(CompileError(format!("Router '{}': total weight must be positive", node.id)));
    }

    // Source for weight routes: source minus class-routed total
    let weight_source = match class_routed_total {
        Some(class_total) => {
            let remaining = cm.alloc_temp();
            ops.push(Op::VecSub { out: remaining, a: source_col, b: class_total });
            remaining
        }
        None => source_col,
    };

    for route in &weight_routes {
        let weight = route.weight.unwrap_or(1.0);
        let fraction = weight / total_weight;

        let route_col = cm.alloc_temp();
        ops.push(Op::ScalarMul { out: route_col, input: weight_source, k: fraction });

        let target_col = cm.get_or_insert(&route.target);
        if let Some(&existing) = target_cols.get(&route.target) {
            let combined = cm.alloc_temp();
            ops.push(Op::VecAdd { out: combined, a: existing, b: route_col });
            ops.push(Op::Copy { out: target_col, input: combined });
            target_cols.insert(route.target.clone(), combined);
        } else {
            ops.push(Op::Copy { out: target_col, input: route_col });
            target_cols.insert(route.target.clone(), route_col);
        }
    }

    Ok(())
}

/// Synthesize queue column ID from topology node ID (e.g., "Queue" → "queue_queue").
fn queue_column_id(topo_node_id: &str) -> String {
    format!("{}_queue", to_snake_case(topo_node_id))
}

/// Public accessor for queue column ID (used by analysis module).
pub fn queue_column_id_pub(topo_node_id: &str) -> String {
    queue_column_id(topo_node_id)
}

/// Public accessor for build_class_map (used by session for post-eval pipeline).
pub fn build_class_map_pub(cm: &crate::plan::ColumnMap) -> ClassMap {
    build_class_map(cm)
}

/// Public accessor for propagate_class_decomposition (used by session).
pub fn propagate_class_decomposition_pub(
    model: &ModelDefinition,
    state: &mut Vec<f64>,
    cm: &mut crate::plan::ColumnMap,
    class_map: &mut ClassMap,
    bins: usize,
) {
    propagate_class_decomposition(model, state, cm, class_map, bins)
}

/// Public accessor for compute_edge_series (used by session).
pub fn compute_edge_series_pub(
    model: &ModelDefinition,
    state: &mut Vec<f64>,
    cm: &mut crate::plan::ColumnMap,
    class_map: &ClassMap,
    bins: usize,
) -> EdgeMap {
    compute_edge_series(model, state, cm, class_map, bins)
}

/// A single graph node (for the UI topology visualization).
#[derive(Debug, Clone)]
pub struct GraphNodeInfo {
    pub id: String,
    pub kind: String,
}

/// A single graph edge (from → to).
#[derive(Debug, Clone)]
pub struct GraphEdgeInfo {
    pub from: String,
    pub to: String,
}

/// Graph derived from a model: logical computation DAG.
#[derive(Debug, Clone, Default)]
pub struct GraphInfo {
    pub nodes: Vec<GraphNodeInfo>,
    pub edges: Vec<GraphEdgeInfo>,
}

/// Derive a computation-graph view of the model for UI visualization.
///
/// Nodes:
/// - Every model.nodes entry (const, expr, pmf, router) becomes a node
/// - Every topology.nodes entry (queue/service) becomes a node with kind="queue"
///   (or the declared topology kind)
///
/// Edges:
/// - For expr nodes: edges from each referenced node → this expr node
/// - For topology nodes: edges from semantics.arrivals → topology node,
///   and topology node → semantics.served (if served references a different node)
/// - For router nodes: edges from inputs.queue → router, router → each route target
///
/// This is a one-shot derivation from the model definition — no evaluation needed.
pub fn derive_graph(model: &ModelDefinition) -> GraphInfo {
    let mut nodes = Vec::new();
    let mut edges = Vec::new();
    let mut node_ids: HashSet<String> = HashSet::new();

    // Pass 1: model.nodes
    for node in &model.nodes {
        let kind = node.kind.to_lowercase();
        nodes.push(GraphNodeInfo { id: node.id.clone(), kind: kind.clone() });
        node_ids.insert(node.id.clone());

        // Expr dependencies
        if kind == "expr" {
            if let Some(expr_str) = &node.expr {
                if let Ok(ast) = crate::expr::parse(expr_str) {
                    let refs = collect_refs(&ast);
                    for r in refs {
                        edges.push(GraphEdgeInfo { from: r, to: node.id.clone() });
                    }
                }
            }
        }

        // Router dependencies
        if kind == "router" {
            if let Some(router) = &node.router {
                if let Some(ref q) = router.inputs.queue {
                    if !q.is_empty() {
                        edges.push(GraphEdgeInfo { from: q.clone(), to: node.id.clone() });
                    }
                }
                for route in &router.routes {
                    if !route.target.is_empty() {
                        edges.push(GraphEdgeInfo { from: node.id.clone(), to: route.target.clone() });
                    }
                }
            }
        }
    }

    // Pass 2: topology.nodes
    if let Some(topo) = &model.topology {
        for tnode in &topo.nodes {
            // Skip if already in model.nodes under the same id (shouldn't happen for topology)
            if node_ids.contains(&tnode.id) {
                continue;
            }
            let kind = tnode.kind.as_deref().unwrap_or("queue").to_lowercase();
            nodes.push(GraphNodeInfo { id: tnode.id.clone(), kind });
            node_ids.insert(tnode.id.clone());

            // Edge: arrivals source → topology node
            if !tnode.semantics.arrivals.is_empty() {
                edges.push(GraphEdgeInfo {
                    from: tnode.semantics.arrivals.clone(),
                    to: tnode.id.clone(),
                });
            }
            // Edge: topology node → served source (if different from arrivals)
            if !tnode.semantics.served.is_empty()
                && tnode.semantics.served != tnode.semantics.arrivals
            {
                edges.push(GraphEdgeInfo {
                    from: tnode.id.clone(),
                    to: tnode.semantics.served.clone(),
                });
            }
        }
    }

    GraphInfo { nodes, edges }
}

/// Compile constraint allocation: emit ProportionalAlloc ops and patch QueueRecurrence inflows.
fn compile_constraints(
    topo: &crate::model::TopologyDefinition,
    cm: &mut ColumnMap,
    ops: &mut Vec<Op>,
    bins: usize,
) -> Result<(), CompileError> {
    for constraint in &topo.constraints {
        // Find topology nodes that reference this constraint
        let constrained_nodes: Vec<&crate::model::TopologyNodeDefinition> = topo.nodes.iter()
            .filter(|n| n.constraints.as_ref().map_or(false, |cs| cs.iter().any(|c| c == &constraint.id)))
            .collect();

        if constrained_nodes.is_empty() { continue; }

        // Resolve capacity column
        let cap_ref = &constraint.semantics.served;
        if cap_ref.is_empty() {
            return Err(CompileError(format!("Constraint '{}': missing semantics.served (capacity)", constraint.id)));
        }
        let cap_col = cm.get(cap_ref)
            .ok_or_else(|| CompileError(format!("Constraint '{}': capacity ref '{}' not found", constraint.id, cap_ref)))?;

        // Resolve demand columns (arrivals of each constrained node)
        let mut demand_cols = Vec::new();
        let mut capped_cols = Vec::new();
        for tnode in &constrained_nodes {
            let arrivals_ref = &tnode.semantics.arrivals;
            if arrivals_ref.is_empty() {
                return Err(CompileError(format!("Constraint '{}': node '{}' has no arrivals", constraint.id, tnode.id)));
            }
            let demand_col = cm.get(arrivals_ref)
                .ok_or_else(|| CompileError(format!("Constraint '{}': arrivals '{}' not found for node '{}'", constraint.id, arrivals_ref, tnode.id)))?;
            demand_cols.push(demand_col);

            // Create capped output column
            let capped_name = format!("{}_capped_{}", to_snake_case(&tnode.id), constraint.id);
            let capped_col = cm.get_or_insert(&capped_name);
            capped_cols.push(capped_col);
        }

        // Emit ProportionalAlloc op
        let alloc_op = Op::ProportionalAlloc {
            outs: capped_cols.clone(),
            demands: demand_cols,
            capacity: cap_col,
        };

        // Insert before the first QueueRecurrence that uses any of these arrivals.
        // Find the earliest QueueRecurrence position in ops.
        let mut insert_pos: Option<usize> = None;
        for (i, op) in ops.iter().enumerate() {
            if let Op::QueueRecurrence { inflow, .. } = op {
                for tnode in &constrained_nodes {
                    let arrivals_ref = &tnode.semantics.arrivals;
                    if let Some(arr_col) = cm.get(arrivals_ref) {
                        if *inflow == arr_col {
                            insert_pos = Some(match insert_pos {
                                None => i,
                                Some(prev) => prev.min(i),
                            });
                        }
                    }
                }
            }
        }

        if let Some(pos) = insert_pos {
            ops.insert(pos, alloc_op);
            // Patch QueueRecurrence inflows to use capped columns
            // (indices shifted by 1 after insert)
            for (j, tnode) in constrained_nodes.iter().enumerate() {
                let arrivals_ref = &tnode.semantics.arrivals;
                if let Some(arr_col) = cm.get(arrivals_ref) {
                    for op in ops.iter_mut() {
                        if let Op::QueueRecurrence { inflow, .. } = op {
                            if *inflow == arr_col {
                                *inflow = capped_cols[j];
                                break;
                            }
                        }
                    }
                }
            }
        } else {
            // No QueueRecurrence found — just append (edge case)
            ops.push(alloc_op);
        }
    }

    Ok(())
}

/// Convert grid bin size + unit to milliseconds.
fn grid_bin_ms(grid: &crate::model::GridDefinition) -> f64 {
    let size = grid.bin_size as f64;
    match grid.bin_unit.to_lowercase().as_str() {
        "ms" | "milliseconds" => size,
        "s" | "seconds" => size * 1000.0,
        "m" | "min" | "minutes" => size * 60_000.0,
        "h" | "hr" | "hours" => size * 3_600_000.0,
        "d" | "days" => size * 86_400_000.0,
        _ => size * 60_000.0, // default to minutes
    }
}

/// Emit derived metric ops for topology nodes (utilization, cycle time, Kingman).
fn compile_derived_metrics(
    topo: &crate::model::TopologyDefinition,
    model: &ModelDefinition,
    cm: &mut ColumnMap,
    ops: &mut Vec<Op>,
    bins: usize,
    bin_ms: f64,
) -> Result<(), CompileError> {
    for tnode in &topo.nodes {
        let kind = tnode.kind.as_deref().unwrap_or("serviceWithBuffer").to_lowercase();
        let snake = to_snake_case(&tnode.id);

        // Determine node category
        let has_queue = matches!(kind.as_str(), "servicewithbuffer" | "queue" | "dlq");
        let has_service = matches!(kind.as_str(), "servicewithbuffer" | "service");

        // --- Utilization ---
        // Requires served + capacity (or served semantics)
        if !tnode.semantics.served.is_empty() {
            if let Some(cap_ref) = &tnode.semantics.capacity {
                if !cap_ref.is_empty() {
                    let served_col = cm.get(&tnode.semantics.served).unwrap();
                    let cap_col = cm.get(cap_ref).unwrap();

                    // Handle parallelism: effectiveCapacity = capacity × parallelism
                    let effective_cap = if let Some(par) = &tnode.semantics.parallelism {
                        match par {
                            crate::model::ParallelismValue::Scalar(p) => {
                                let eff_col = cm.get_or_insert(&format!("{}_effective_capacity", snake));
                                ops.push(Op::ScalarMul { out: eff_col, input: cap_col, k: *p });
                                eff_col
                            }
                            crate::model::ParallelismValue::Reference(ref_name) => {
                                let par_col = cm.get(ref_name)
                                    .ok_or_else(|| CompileError(format!("Node '{}': parallelism ref '{}' not found", tnode.id, ref_name)))?;
                                let eff_col = cm.get_or_insert(&format!("{}_effective_capacity", snake));
                                ops.push(Op::VecMul { out: eff_col, a: cap_col, b: par_col });
                                eff_col
                            }
                        }
                    } else {
                        cap_col
                    };

                    let util_col = cm.get_or_insert(&format!("{}_utilization", snake));
                    ops.push(Op::VecDiv { out: util_col, a: served_col, b: effective_cap });
                }
            }
        }

        // --- Queue Time ---
        if has_queue {
            // Resolve queue depth column
            let q_col_name = if let Some(qd_ref) = &tnode.semantics.queue_depth {
                if !qd_ref.is_empty() { qd_ref.clone() } else { queue_column_id(&tnode.id) }
            } else {
                queue_column_id(&tnode.id)
            };

            if let Some(q_col) = cm.get(&q_col_name) {
                let served_ref = &tnode.semantics.served;
                if !served_ref.is_empty() {
                    if let Some(served_col) = cm.get(served_ref) {
                        // queueTimeMs = (queueDepth / served) * binMs
                        let ratio_col = cm.get_or_insert(&format!("{}_q_ratio", snake));
                        ops.push(Op::VecDiv { out: ratio_col, a: q_col, b: served_col });
                        let qt_col = cm.get_or_insert(&format!("{}_queue_time_ms", snake));
                        ops.push(Op::ScalarMul { out: qt_col, input: ratio_col, k: bin_ms });

                        // latencyMinutes = queueTimeMs / 60000
                        let lat_col = cm.get_or_insert(&format!("{}_latency_min", snake));
                        ops.push(Op::ScalarMul { out: lat_col, input: qt_col, k: 1.0 / 60_000.0 });
                    }
                }
            }
        }

        // --- Service Time ---
        if has_service {
            if let (Some(pt_ref), Some(sc_ref)) = (&tnode.semantics.processing_time_ms_sum, &tnode.semantics.served_count) {
                if !pt_ref.is_empty() && !sc_ref.is_empty() {
                    if let (Some(pt_col), Some(sc_col)) = (cm.get(pt_ref), cm.get(sc_ref)) {
                        let st_col = cm.get_or_insert(&format!("{}_service_time_ms", snake));
                        ops.push(Op::VecDiv { out: st_col, a: pt_col, b: sc_col });
                    }
                }
            }
        }

        // --- Cycle Time & Flow Efficiency ---
        let qt_col = cm.get(&format!("{}_queue_time_ms", snake));
        let st_col = cm.get(&format!("{}_service_time_ms", snake));
        if qt_col.is_some() || st_col.is_some() {
            let ct_col = cm.get_or_insert(&format!("{}_cycle_time_ms", snake));
            match (qt_col, st_col) {
                (Some(q), Some(s)) => {
                    ops.push(Op::VecAdd { out: ct_col, a: q, b: s });
                }
                (Some(q), None) => {
                    ops.push(Op::Copy { out: ct_col, input: q });
                }
                (None, Some(s)) => {
                    ops.push(Op::Copy { out: ct_col, input: s });
                }
                (None, None) => unreachable!(),
            }

            // Flow efficiency = serviceTime / cycleTime (only if both exist)
            if let (Some(_), Some(s)) = (qt_col, st_col) {
                let fe_col = cm.get_or_insert(&format!("{}_flow_efficiency", snake));
                ops.push(Op::VecDiv { out: fe_col, a: s, b: ct_col });
            }
        }

        // --- Kingman G/G/1 Approximation ---
        // E[Wq] ≈ (ρ/(1-ρ)) × ((Ca² + Cs²)/2) × E[S]
        // Requires: utilization column, arrivals Cv, service Cv, service time
        // Cv from PMF nodes, 0 for const nodes.
        if let Some(util_col) = cm.get(&format!("{}_utilization", snake)) {
            if let Some(st_col) = cm.get(&format!("{}_service_time_ms", snake)) {
                // Compute Cv for arrivals
                let ca = compute_node_cv(&tnode.semantics.arrivals, &model.nodes);
                // Compute Cv for service (from processingTimeMsSum or served)
                let cs = tnode.semantics.processing_time_ms_sum.as_deref()
                    .and_then(|r| if r.is_empty() { None } else { Some(compute_node_cv(r, &model.nodes)) })
                    .unwrap_or(0.0);

                if ca.is_finite() && cs.is_finite() {
                    let cv_factor = (ca * ca + cs * cs) / 2.0;
                    // Kingman column: per-bin using utilization and service time
                    // E[Wq][t] = (ρ[t]/(1-ρ[t])) × cv_factor × serviceTime[t]
                    // We need a per-bin computation. Use existing ops:
                    // step1: 1 - ρ
                    let one_minus_rho = cm.get_or_insert(&format!("{}_one_minus_rho", snake));
                    ops.push(Op::ScalarAdd { out: one_minus_rho, input: util_col, k: -1.0 });
                    // Negate: (1-ρ) = -(ρ-1)
                    let denom = cm.alloc_temp();
                    ops.push(Op::ScalarMul { out: denom, input: one_minus_rho, k: -1.0 });
                    // ρ / (1-ρ)
                    let rho_ratio = cm.alloc_temp();
                    ops.push(Op::VecDiv { out: rho_ratio, a: util_col, b: denom });
                    // × cv_factor × serviceTime
                    let scaled = cm.alloc_temp();
                    ops.push(Op::ScalarMul { out: scaled, input: rho_ratio, k: cv_factor });
                    let kingman_col = cm.get_or_insert(&format!("{}_kingman_wq", snake));
                    ops.push(Op::VecMul { out: kingman_col, a: scaled, b: st_col });
                }
            }
        }
    }

    Ok(())
}

/// Compute the coefficient of variation for a node referenced by name.
/// PMF → σ/μ, Const → 0, otherwise 0 (conservative default).
fn compute_node_cv(node_ref: &str, nodes: &[crate::model::NodeDefinition]) -> f64 {
    let node = nodes.iter().find(|n| n.id == node_ref);
    match node {
        Some(n) if n.kind.to_lowercase() == "pmf" => {
            if let Some(pmf) = &n.pmf {
                let prob_sum: f64 = pmf.probabilities.iter().sum();
                if prob_sum <= 0.0 { return 0.0; }
                let mean: f64 = pmf.values.iter().zip(&pmf.probabilities)
                    .map(|(v, p)| v * p / prob_sum).sum();
                if mean.abs() < f64::EPSILON { return 0.0; }
                let variance: f64 = pmf.values.iter().zip(&pmf.probabilities)
                    .map(|(v, p)| (v - mean).powi(2) * p / prob_sum).sum();
                variance.sqrt() / mean
            } else { 0.0 }
        }
        Some(n) if n.kind.to_lowercase() == "const" => 0.0,
        _ => 0.0, // Conservative: treat unknown as deterministic
    }
}

/// Compile a single topology node into ops (QueueRecurrence + optional DispatchGate, Convolve).
fn compile_single_topology_node(
    topo: &crate::model::TopologyDefinition,
    tnode_id: &str,
    cm: &mut ColumnMap,
    ops: &mut Vec<Op>,
    bins: usize,
) -> Result<(), CompileError> {
    let tnode = topo.nodes.iter().find(|n| n.id == tnode_id)
        .ok_or_else(|| CompileError(format!("Topology node '{}' not found", tnode_id)))?;
    let kind = tnode.kind.as_deref().unwrap_or("serviceWithBuffer").to_lowercase();

    if !matches!(kind.as_str(), "servicewithbuffer" | "queue" | "dlq") {
        return Ok(()); // Non-queue kinds handled in m-E20-04
    }

    // Resolve inflow
    let arrivals_ref = &tnode.semantics.arrivals;
    if arrivals_ref.is_empty() {
        return Err(CompileError(format!("Topology node '{}': missing semantics.arrivals", tnode.id)));
    }
    let mut inflow_col = cm.get(arrivals_ref)
        .ok_or_else(|| CompileError(format!("Topology node '{}': arrivals ref '{}' not found", tnode.id, arrivals_ref)))?;

    // Check if any source nodes overflow into this one — inject VecAdd before QueueRecurrence
    for src_node in &topo.nodes {
        if let Some(target) = &src_node.wip_overflow {
            if target == tnode_id {
                let ov_col_name = format!("{}_overflow", to_snake_case(&src_node.id));
                if let Some(ov_col) = cm.get(&ov_col_name) {
                    let combined = cm.get_or_insert(&format!("{}_inflow_from_{}", to_snake_case(tnode_id), to_snake_case(&src_node.id)));
                    ops.push(Op::VecAdd { out: combined, a: inflow_col, b: ov_col });
                    inflow_col = combined;
                }
            }
        }
    }

    // Resolve outflow
    let outflow_ref = if !tnode.semantics.served.is_empty() {
        &tnode.semantics.served
    } else if let Some(cap) = &tnode.semantics.capacity {
        cap
    } else {
        return Err(CompileError(format!("Topology node '{}': missing semantics.served or capacity", tnode.id)));
    };
    let outflow_col = cm.get(outflow_ref)
        .ok_or_else(|| CompileError(format!("Topology node '{}': outflow ref '{}' not found", tnode.id, outflow_ref)))?;

    // Loss (optional)
    let loss_col = if let Some(err_ref) = &tnode.semantics.errors {
        if !err_ref.is_empty() {
            Some(cm.get(err_ref).ok_or_else(|| CompileError(format!("Topology node '{}': loss ref '{}' not found", tnode.id, err_ref)))?)
        } else { None }
    } else { None };

    let init = tnode.initial_condition.as_ref().map_or(0.0, |ic| ic.queue_depth);

    // Dispatch gate
    let effective_outflow = if let Some(ds) = &tnode.dispatch_schedule {
        let period = ds.period_bins as usize;
        if period == 0 {
            return Err(CompileError(format!("Topology node '{}': dispatch period must be > 0", tnode.id)));
        }
        let phase = ds.phase_offset.unwrap_or(0) as usize;
        let capacity = if let Some(cap_ref) = &ds.capacity_series {
            Some(cm.get(cap_ref).ok_or_else(|| CompileError(format!("Topology node '{}': capacity series '{}' not found", tnode.id, cap_ref)))?)
        } else { None };
        let gated = cm.get_or_insert(&format!("{}_gated_outflow", to_snake_case(&tnode.id)));
        ops.push(Op::DispatchGate { out: gated, input: outflow_col, period, phase, capacity });
        gated
    } else {
        outflow_col
    };

    // WIP limit
    let wip_limit_col = if let Some(wl_ref) = &tnode.wip_limit_series {
        Some(cm.get(wl_ref).ok_or_else(|| CompileError(format!("Topology node '{}': wip_limit_series '{}' not found", tnode.id, wl_ref)))?)
    } else if let Some(wl_scalar) = tnode.wip_limit {
        let wl_col = cm.get_or_insert(&format!("{}_wip_limit", to_snake_case(&tnode.id)));
        ops.push(Op::Const { out: wl_col, values: vec![wl_scalar; bins] });
        Some(wl_col)
    } else { None };

    // Overflow output
    let overflow_col = if wip_limit_col.is_some() {
        Some(cm.get_or_insert(&format!("{}_overflow", to_snake_case(&tnode.id))))
    } else { None };

    // Queue depth output
    let queue_col = if let Some(qd_ref) = &tnode.semantics.queue_depth {
        if !qd_ref.is_empty() { cm.get_or_insert(qd_ref) }
        else { cm.get_or_insert(&queue_column_id(&tnode.id)) }
    } else {
        cm.get_or_insert(&queue_column_id(&tnode.id))
    };

    ops.push(Op::QueueRecurrence {
        out: queue_col,
        inflow: inflow_col,
        outflow: effective_outflow,
        loss: loss_col,
        init,
        wip_limit: wip_limit_col,
        overflow_out: overflow_col,
    });

    // Retry echo → Convolve
    if let (Some(echo_ref), Some(kernel)) = (&tnode.semantics.retry_echo, &tnode.semantics.retry_kernel) {
        if !echo_ref.is_empty() && !kernel.is_empty() {
            let failures_ref = tnode.semantics.failures.as_deref()
                .or(tnode.semantics.errors.as_deref())
                .unwrap_or("");
            if failures_ref.is_empty() {
                return Err(CompileError(format!("Topology node '{}': retryEcho requires failures or errors series", tnode.id)));
            }
            let failures_col = cm.get(failures_ref)
                .ok_or_else(|| CompileError(format!("Topology node '{}': failures ref '{}' not found", tnode.id, failures_ref)))?;
            let echo_col = cm.get_or_insert(echo_ref);
            ops.push(Op::Convolve { out: echo_col, input: failures_col, kernel: kernel.clone() });
        }
    }

    Ok(())
}


/// Validate no cycles in WIP overflow routing graph.
fn validate_no_overflow_cycles(topo: &crate::model::TopologyDefinition) -> Result<(), CompileError> {
    let mut overflow_edges: HashMap<&str, &str> = HashMap::new();
    for tnode in &topo.nodes {
        if let Some(target) = &tnode.wip_overflow {
            if !target.is_empty() {
                overflow_edges.insert(&tnode.id, target);
            }
        }
    }

    for start in overflow_edges.keys() {
        let mut visited = HashSet::new();
        let mut current = *start;
        while let Some(&next) = overflow_edges.get(current) {
            if !visited.insert(current) {
                return Err(CompileError(format!("WIP overflow cycle detected involving '{}'", current)));
            }
            current = next;
        }
    }

    Ok(())
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
        "SHIFT" => {
            if args.len() != 2 {
                return Err(CompileError(format!("SHIFT requires 2 arguments (input, lag), got {}", args.len())));
            }
            let input = emit_expr(&args[0], cm, ops, bins)?;
            let lag = match &args[1] {
                Expr::Literal(v) => *v as usize,
                _ => return Err(CompileError("SHIFT lag must be a literal".into())),
            };
            let out = cm.alloc_temp();
            ops.push(Op::Shift { out, input, lag });
            Ok(out)
        }
        "CONV" => {
            if args.len() != 2 {
                return Err(CompileError(format!("CONV requires 2 arguments (input, kernel), got {}", args.len())));
            }
            let input = emit_expr(&args[0], cm, ops, bins)?;
            let kernel = match &args[1] {
                Expr::ArrayLiteral(vals) => vals.clone(),
                _ => return Err(CompileError("CONV kernel must be an array literal".into())),
            };
            let out = cm.alloc_temp();
            ops.push(Op::Convolve { out, input, kernel });
            Ok(out)
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

    #[test]
    fn compile_simple_queue_topology() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: const
    values: [3, 3, 3, 3]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Q[0]=7, Q[1]=14, Q[2]=21, Q[3]=28
        let q = result.series("queue_queue").unwrap();
        assert_eq!(q, vec![7.0, 14.0, 21.0, 28.0]);
    }

    #[test]
    fn compile_queue_with_initial_depth() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: const
    values: [3, 3, 3, 3]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      initialCondition:
        queueDepth: 5.0
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Q[0]=5+10-3=12, Q[1]=19, Q[2]=26, Q[3]=33
        let q = result.series("queue_queue").unwrap();
        assert_eq!(q, vec![12.0, 19.0, 26.0, 33.0]);
    }

    #[test]
    fn compile_queue_with_loss() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [20, 20, 20, 20]
  - id: served
    kind: const
    values: [5, 5, 5, 5]
  - id: errors
    kind: const
    values: [1, 1, 1, 1]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Q[0]=20-5-1=14, Q[1]=14+14=28, Q[2]=42, Q[3]=56
        let q = result.series("queue_queue").unwrap();
        assert_eq!(q, vec![14.0, 28.0, 42.0, 56.0]);
    }

    #[test]
    fn compile_shift_function_in_expr() {
        let yaml = r#"
grid:
  bins: 5
  binSize: 1
  binUnit: hours
nodes:
  - id: signal
    kind: const
    values: [1, 2, 3, 4, 5]
  - id: delayed
    kind: expr
    expr: "SHIFT(signal, 2)"
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_eq!(result.series("delayed").unwrap(), vec![0.0, 0.0, 1.0, 2.0, 3.0]);
    }

    #[test]
    fn compile_conv_function_in_expr() {
        let yaml = r#"
grid:
  bins: 5
  binSize: 1
  binUnit: hours
nodes:
  - id: failures
    kind: const
    values: [100, 0, 0, 0, 0]
  - id: retries
    kind: expr
    expr: "CONV(failures, [0.0, 0.6, 0.3, 0.1])"
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // t=0: 100*0.0=0, t=1: 100*0.6=60, t=2: 100*0.3=30, t=3: 100*0.1=10, t=4: 0
        assert_eq!(result.series("retries").unwrap(), vec![0.0, 60.0, 30.0, 10.0, 0.0]);
    }

    #[test]
    fn compile_dispatch_gated_queue() {
        let yaml = r#"
grid:
  bins: 6
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10, 10, 10]
  - id: served
    kind: const
    values: [5, 5, 5, 5, 5, 5]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      dispatchSchedule:
        kind: periodic
        periodBins: 3
        phaseOffset: 0
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Outflow gated: dispatch at t=0,3 → outflow=[5,0,0,5,0,0]
        // Q[0]=10-5=5, Q[1]=5+10=15, Q[2]=25, Q[3]=25+10-5=30, Q[4]=40, Q[5]=50
        let q = result.series("queue_queue").unwrap();
        assert_eq!(q, vec![5.0, 15.0, 25.0, 30.0, 40.0, 50.0]);
    }

    #[test]
    fn compile_retry_echo_from_topology() {
        let yaml = r#"
grid:
  bins: 5
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [100, 100, 100, 100, 100]
  - id: served
    kind: const
    values: [90, 90, 90, 90, 90]
  - id: errors
    kind: const
    values: [10, 10, 10, 10, 10]
topology:
  nodes:
    - id: Service
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
        retryEcho: retry_echo
        retryKernel: [0.0, 0.6, 0.3, 0.1]
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // retry_echo = CONV(errors, [0.0, 0.6, 0.3, 0.1])
        // t=0: 10*0.0=0, t=1: 10*0.0+10*0.6=6, t=2: 10*0.0+10*0.6+10*0.3=9
        // t=3: 10*0.0+10*0.6+10*0.3+10*0.1=10, t=4: same=10
        let echo = result.series("retry_echo").unwrap();
        assert_eq!(echo, vec![0.0, 6.0, 9.0, 10.0, 10.0]);
    }

    #[test]
    fn compile_queue_with_wip_limit() {
        // C# parity: inflow=10, outflow=2, wipLimit=20 → Q=[8,16,20,20], overflow=[0,0,4,8]
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: const
    values: [2, 2, 2, 2]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      wipLimit: 20
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_eq!(result.series("queue_queue").unwrap(), vec![8.0, 16.0, 20.0, 20.0]);
        assert_eq!(result.series("queue_overflow").unwrap(), vec![0.0, 0.0, 4.0, 8.0]);
    }

    #[test]
    fn compile_queue_wip_limit_with_loss() {
        // C# parity: inflow=20, outflow=5, loss=1, wipLimit=15
        // Q[0]=14, Q[1]=14+14=28→15(ov=13), Q[2]=15+14=29→15(ov=14), Q[3]=15+14=29→15(ov=14)
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [20, 20, 20, 20]
  - id: served
    kind: const
    values: [5, 5, 5, 5]
  - id: errors
    kind: const
    values: [1, 1, 1, 1]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      wipLimit: 15
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_eq!(result.series("queue_queue").unwrap(), vec![14.0, 15.0, 15.0, 15.0]);
        assert_eq!(result.series("queue_overflow").unwrap(), vec![0.0, 13.0, 14.0, 14.0]);
    }

    #[test]
    fn compile_queue_zero_wip_limit() {
        // C# parity: wipLimit=0 → all inflow-outflow overflows
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10]
  - id: served
    kind: const
    values: [5, 5, 5]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      wipLimit: 0
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_eq!(result.series("queue_queue").unwrap(), vec![0.0, 0.0, 0.0]);
        assert_eq!(result.series("queue_overflow").unwrap(), vec![5.0, 5.0, 5.0]);
    }

    #[test]
    fn compile_overflow_routing_to_dlq() {
        // Main queue: inflow=15, outflow=5, wipLimit=10 → overflow routes to DLQ
        // Main: Q[0]=10(ov=0), Q[1]=10(ov=10), Q[2]=10(ov=10)
        // DLQ: inflow=dlq_arrivals+overflow, outflow=0
        //   DLQ Q[0]=0+0=0, Q[1]=0+10=10, Q[2]=0+10=20
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [15, 15, 15]
  - id: served
    kind: const
    values: [5, 5, 5]
  - id: dlq_arrivals
    kind: const
    values: [0, 0, 0]
  - id: dlq_outflow
    kind: const
    values: [0, 0, 0]
topology:
  nodes:
    - id: Main
      kind: serviceWithBuffer
      wipLimit: 10
      wipOverflow: DLQ
      semantics:
        arrivals: arrivals
        served: served
    - id: DLQ
      kind: dlq
      semantics:
        arrivals: dlq_arrivals
        served: dlq_outflow
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_eq!(result.series("main_queue").unwrap(), vec![10.0, 10.0, 10.0]);
        assert_eq!(result.series("main_overflow").unwrap(), vec![0.0, 10.0, 10.0]);
        assert_eq!(result.series("dlq_queue").unwrap(), vec![0.0, 10.0, 20.0]);
    }

    #[test]
    fn compile_overflow_cycle_detected() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arr
    kind: const
    values: [10, 10, 10]
  - id: srv
    kind: const
    values: [5, 5, 5]
topology:
  nodes:
    - id: A
      kind: serviceWithBuffer
      wipLimit: 5
      wipOverflow: B
      semantics:
        arrivals: arr
        served: srv
    - id: B
      kind: serviceWithBuffer
      wipLimit: 5
      wipOverflow: A
      semantics:
        arrivals: arr
        served: srv
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let err = eval_model(&model).unwrap_err();
        assert!(err.0.contains("cycle"), "Expected overflow cycle error, got: {}", err.0);
    }

    #[test]
    fn compile_shift_feedback_backpressure() {
        // Backpressure model: queue → pressure → SHIFT → effective_arrivals → queue
        // This tests that bin-major evaluation makes SHIFT feedback work naturally.
        let yaml = r#"
grid:
  bins: 6
  binSize: 1
  binUnit: hours
nodes:
  - id: raw_arrivals
    kind: const
    values: [100, 100, 100, 100, 100, 100]
  - id: served
    kind: const
    values: [20, 20, 20, 20, 20, 20]
  - id: wip_capacity
    kind: const
    values: [50, 50, 50, 50, 50, 50]
  - id: pressure
    kind: expr
    expr: "CLAMP(queue_depth / wip_capacity, 0, 1)"
  - id: shifted_pressure
    kind: expr
    expr: "SHIFT(pressure, 1)"
  - id: throttle
    kind: expr
    expr: "1 - shifted_pressure"
  - id: effective_arrivals
    kind: expr
    expr: "raw_arrivals * throttle"
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      semantics:
        arrivals: effective_arrivals
        served: served
        queueDepth: queue_depth
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        let q = result.series("queue_depth").unwrap();
        // t=0: SHIFT=0→eff=100, Q=80, p=1.0
        // t=1: SHIFT=1.0→eff=0, Q=60, p=1.0
        // t=2: SHIFT=1.0→eff=0, Q=40, p=0.8
        // t=3: SHIFT=0.8→eff=20, Q=40, p=0.8
        // t=4: SHIFT=0.8→eff=20, Q=40, p=0.8  (stabilized)
        // t=5: same → 40
        assert_approx(&q, &[80.0, 60.0, 40.0, 40.0, 40.0, 40.0]);

        // Verify effective_arrivals shows the throttle pattern
        let eff = result.series("effective_arrivals").unwrap();
        assert_approx(&eff, &[100.0, 0.0, 0.0, 20.0, 20.0, 20.0]);
    }

    #[test]
    fn compile_cascading_overflow_a_b_c() {
        // A→B→C cascading overflow
        // A: inflow=20, outflow=5, wipLimit=10
        // B: inflow=0+A_overflow, outflow=0, wipLimit=5
        // C: inflow=0+B_overflow, outflow=0, no limit
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: a_in
    kind: const
    values: [20, 20, 20]
  - id: a_out
    kind: const
    values: [5, 5, 5]
  - id: b_in
    kind: const
    values: [0, 0, 0]
  - id: b_out
    kind: const
    values: [0, 0, 0]
  - id: c_in
    kind: const
    values: [0, 0, 0]
  - id: c_out
    kind: const
    values: [0, 0, 0]
topology:
  nodes:
    - id: A
      kind: serviceWithBuffer
      wipLimit: 10
      wipOverflow: B
      semantics:
        arrivals: a_in
        served: a_out
    - id: B
      kind: serviceWithBuffer
      wipLimit: 5
      wipOverflow: C
      semantics:
        arrivals: b_in
        served: b_out
    - id: C
      kind: serviceWithBuffer
      semantics:
        arrivals: c_in
        served: c_out
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // A: Q[0]=20-5=15→clamped to 10(ov=5), Q[1]=10+15=25→10(ov=15), Q[2]=10+15=25→10(ov=15)
        assert_eq!(result.series("a_queue").unwrap(), vec![10.0, 10.0, 10.0]);
        assert_eq!(result.series("a_overflow").unwrap(), vec![5.0, 15.0, 15.0]);

        // B: inflow=0+A_overflow=[5,15,15], outflow=0
        // Q[0]=5→clamped to 5(ov=0), Q[1]=5+15=20→5(ov=15), Q[2]=5+15=20→5(ov=15)
        assert_eq!(result.series("b_queue").unwrap(), vec![5.0, 5.0, 5.0]);
        assert_eq!(result.series("b_overflow").unwrap(), vec![0.0, 15.0, 15.0]);

        // C: inflow=0+B_overflow=[0,15,15], outflow=0
        // Q[0]=0, Q[1]=15, Q[2]=30
        assert_eq!(result.series("c_queue").unwrap(), vec![0.0, 15.0, 30.0]);
    }

    #[test]
    fn compile_router_weight_based() {
        // Router splits source [100, 100, 100] by weights [0.5, 0.3, 0.2]
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: source
    kind: const
    values: [100, 100, 100]
  - id: splitter
    kind: router
    router:
      inputs:
        queue: source
      routes:
        - target: target_a
          weight: 0.5
        - target: target_b
          weight: 0.3
        - target: target_c
          weight: 0.2
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_approx(&result.series("target_a").unwrap(), &[50.0, 50.0, 50.0]);
        assert_approx(&result.series("target_b").unwrap(), &[30.0, 30.0, 30.0]);
        assert_approx(&result.series("target_c").unwrap(), &[20.0, 20.0, 20.0]);
    }

    #[test]
    fn compile_router_default_weights() {
        // Router with no explicit weights → equal split
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: source
    kind: const
    values: [90, 90, 90]
  - id: splitter
    kind: router
    router:
      inputs:
        queue: source
      routes:
        - target: target_a
        - target: target_b
        - target: target_c
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_approx(&result.series("target_a").unwrap(), &[30.0, 30.0, 30.0]);
        assert_approx(&result.series("target_b").unwrap(), &[30.0, 30.0, 30.0]);
        assert_approx(&result.series("target_c").unwrap(), &[30.0, 30.0, 30.0]);
    }

    #[test]
    fn compile_router_accumulate_same_target() {
        // Two routes to the same target → accumulated
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: source
    kind: const
    values: [100, 100, 100]
  - id: splitter
    kind: router
    router:
      inputs:
        queue: source
      routes:
        - target: combined
          weight: 0.6
        - target: combined
          weight: 0.4
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_approx(&result.series("combined").unwrap(), &[100.0, 100.0, 100.0]);
    }

    #[test]
    fn compile_router_class_based() {
        // Two classes routed to different targets
        // Alpha=40/bin → airport, Beta=60/bin → general
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: total_arrivals
    kind: const
    values: [100, 100, 100]
  - id: alpha_arrivals
    kind: const
    values: [40, 40, 40]
  - id: beta_arrivals
    kind: const
    values: [60, 60, 60]
  - id: splitter
    kind: router
    router:
      inputs:
        queue: total_arrivals
      routes:
        - target: airport
          classes: [Alpha]
        - target: general
          classes: [Beta]
classes:
  - id: Alpha
  - id: Beta
traffic:
  arrivals:
    - nodeId: total_arrivals
      classId: Alpha
      pattern:
        kind: const
        ratePerBin: 40
    - nodeId: total_arrivals
      classId: Beta
      pattern:
        kind: const
        ratePerBin: 60
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_approx(&result.series("airport").unwrap(), &[40.0, 40.0, 40.0]);
        assert_approx(&result.series("general").unwrap(), &[60.0, 60.0, 60.0]);
    }

    #[test]
    fn compile_router_mixed_class_and_weight() {
        // Alpha=40 routed by class to airport
        // Remaining 60 split by weight: general=0.75(45), overflow=0.25(15)
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: total_arrivals
    kind: const
    values: [100, 100, 100]
  - id: alpha_arrivals
    kind: const
    values: [40, 40, 40]
  - id: beta_arrivals
    kind: const
    values: [60, 60, 60]
  - id: splitter
    kind: router
    router:
      inputs:
        queue: total_arrivals
      routes:
        - target: airport
          classes: [Alpha]
        - target: general
          weight: 0.75
        - target: overflow_target
          weight: 0.25
classes:
  - id: Alpha
  - id: Beta
traffic:
  arrivals:
    - nodeId: total_arrivals
      classId: Alpha
      pattern:
        kind: const
        ratePerBin: 40
    - nodeId: total_arrivals
      classId: Beta
      pattern:
        kind: const
        ratePerBin: 60
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert_approx(&result.series("airport").unwrap(), &[40.0, 40.0, 40.0]);
        assert_approx(&result.series("general").unwrap(), &[45.0, 45.0, 45.0]);
        assert_approx(&result.series("overflow_target").unwrap(), &[15.0, 15.0, 15.0]);
    }

    #[test]
    fn compile_constraint_proportional_split() {
        // Two demand sources sharing capacity=80, demands [60, 60] → capped [40, 40]
        // Verify the ProportionalAlloc op directly via eval_model
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: demand_a
    kind: const
    values: [60, 60, 60]
  - id: demand_b
    kind: const
    values: [60, 60, 60]
  - id: capacity
    kind: const
    values: [80, 80, 80]
  - id: served_a
    kind: const
    values: [10, 10, 10]
  - id: served_b
    kind: const
    values: [10, 10, 10]
topology:
  nodes:
    - id: NodeA
      kind: serviceWithBuffer
      constraints: [shared]
      semantics:
        arrivals: demand_a
        served: served_a
    - id: NodeB
      kind: serviceWithBuffer
      constraints: [shared]
      semantics:
        arrivals: demand_b
        served: served_b
  edges: []
  constraints:
    - id: shared
      semantics:
        arrivals: total_demand
        served: capacity
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // totalDemand=120 > capacity=80 → capped_a=40, capped_b=40
        // Queue A: inflow=40, outflow=10 → Q=[30, 60, 90]
        // Queue B: same
        assert_approx(&result.series("node_a_queue").unwrap(), &[30.0, 60.0, 90.0]);
        assert_approx(&result.series("node_b_queue").unwrap(), &[30.0, 60.0, 90.0]);
    }

    #[test]
    fn compile_constraint_below_capacity() {
        // Total demand=70 < capacity=80 → no capping
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: demand_a
    kind: const
    values: [30, 30, 30]
  - id: demand_b
    kind: const
    values: [40, 40, 40]
  - id: capacity
    kind: const
    values: [80, 80, 80]
  - id: served_a
    kind: const
    values: [10, 10, 10]
  - id: served_b
    kind: const
    values: [10, 10, 10]
topology:
  nodes:
    - id: NodeA
      kind: serviceWithBuffer
      constraints: [shared]
      semantics:
        arrivals: demand_a
        served: served_a
    - id: NodeB
      kind: serviceWithBuffer
      constraints: [shared]
      semantics:
        arrivals: demand_b
        served: served_b
  edges: []
  constraints:
    - id: shared
      semantics:
        arrivals: total_demand
        served: capacity
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // totalDemand=70 < capacity=80 → no capping, arrivals unchanged
        // Queue A: inflow=30, outflow=10 → Q=[20, 40, 60]
        // Queue B: inflow=40, outflow=10 → Q=[30, 60, 90]
        assert_approx(&result.series("node_a_queue").unwrap(), &[20.0, 40.0, 60.0]);
        assert_approx(&result.series("node_b_queue").unwrap(), &[30.0, 60.0, 90.0]);
    }

    #[test]
    fn compile_kingman_approximation() {
        // ρ=0.8, Ca=1.0, Cs=0.5, E[S]=10ms → E[Wq] = (0.8/0.2) * ((1+0.25)/2) * 10 = 4 * 0.625 * 10 = 25
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10]
  - id: served
    kind: const
    values: [8, 8, 8]
  - id: capacity
    kind: const
    values: [10, 10, 10]
  - id: proc_time_sum
    kind: const
    values: [100, 100, 100]
  - id: served_count
    kind: const
    values: [10, 10, 10]
topology:
  nodes:
    - id: Service
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        capacity: capacity
        processingTimeMsSum: proc_time_sum
        servedCount: served_count
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // utilization = 8/10 = 0.8 (all bins)
        assert_approx(&result.series("service_utilization").unwrap(), &[0.8, 0.8, 0.8]);
        // serviceTimeMs = 100/10 = 10 (all bins)
        assert_approx(&result.series("service_service_time_ms").unwrap(), &[10.0, 10.0, 10.0]);

        // Kingman: ρ=0.8, Ca=0 (const arrivals), Cs=0 (const service), E[S]=10
        // E[Wq] = (0.8/0.2) * ((0+0)/2) * 10 = 0 (both Cv=0 for const nodes)
        let kingman = result.series("service_kingman_wq").unwrap();
        assert_approx(&kingman, &[0.0, 0.0, 0.0]);
    }

    #[test]
    fn compile_derived_utilization() {
        // served=8, capacity=10 → utilization=0.8
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10]
  - id: served
    kind: const
    values: [8, 8, 8]
  - id: capacity
    kind: const
    values: [10, 10, 10]
topology:
  nodes:
    - id: Service
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        capacity: capacity
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        assert_approx(&result.series("service_utilization").unwrap(), &[0.8, 0.8, 0.8]);
    }

    #[test]
    fn compile_derived_queue_time() {
        // queueDepth=10, served=5, binMs=60000 (1 minute) → queueTimeMs=120000
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [15, 15, 15]
  - id: served
    kind: const
    values: [5, 5, 5]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Q[0]=10, Q[1]=20, Q[2]=30
        let q = result.series("queue_queue").unwrap();
        assert_eq!(q, vec![10.0, 20.0, 30.0]);

        // queueTimeMs = (Q / served) * 60000
        // t=0: (10/5)*60000 = 120000
        // t=1: (20/5)*60000 = 240000
        // t=2: (30/5)*60000 = 360000
        let qt = result.series("queue_queue_time_ms").unwrap();
        assert_approx(&qt, &[120_000.0, 240_000.0, 360_000.0]);

        // latencyMinutes = queueTimeMs / 60000
        let lat = result.series("queue_latency_min").unwrap();
        assert_approx(&lat, &[2.0, 4.0, 6.0]);
    }

    #[test]
    fn compile_derived_cycle_time_service_with_buffer() {
        // Queue + service → cycleTime = queueTime + serviceTime, flowEfficiency = service/cycle
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [15, 15, 15]
  - id: served
    kind: const
    values: [5, 5, 5]
  - id: proc_time_sum
    kind: const
    values: [250, 250, 250]
  - id: served_count
    kind: const
    values: [5, 5, 5]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        processingTimeMsSum: proc_time_sum
        servedCount: served_count
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // serviceTimeMs = 250/5 = 50
        let st = result.series("queue_service_time_ms").unwrap();
        assert_approx(&st, &[50.0, 50.0, 50.0]);

        // queueTimeMs = (Q/served)*60000 → Q=[10,20,30] → [120000, 240000, 360000]
        let qt = result.series("queue_queue_time_ms").unwrap();
        assert_approx(&qt, &[120_000.0, 240_000.0, 360_000.0]);

        // cycleTimeMs = queueTime + serviceTime
        let ct = result.series("queue_cycle_time_ms").unwrap();
        assert_approx(&ct, &[120_050.0, 240_050.0, 360_050.0]);

        // flowEfficiency = serviceTime / cycleTime
        let fe = result.series("queue_flow_efficiency").unwrap();
        assert_approx(&fe, &[50.0/120_050.0, 50.0/240_050.0, 50.0/360_050.0]);
    }

    // --- Edge case tests ---

    #[test]
    fn compile_derived_utilization_zero_capacity() {
        // capacity=0 → utilization=0 (VecDiv returns 0 for div by zero)
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10]
  - id: served
    kind: const
    values: [8, 8, 8]
  - id: capacity
    kind: const
    values: [0, 0, 0]
topology:
  nodes:
    - id: Service
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        capacity: capacity
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        assert_approx(&result.series("service_utilization").unwrap(), &[0.0, 0.0, 0.0]);
    }

    #[test]
    fn compile_derived_queue_time_zero_served() {
        // served=0 → queueTime=0 (VecDiv returns 0 for div by zero)
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10]
  - id: served
    kind: const
    values: [0, 0, 0]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        // Q=[10,20,30], served=0 → queueTime = Q/0 * 60000 = 0 (div by zero → 0)
        assert_approx(&result.series("queue_queue_time_ms").unwrap(), &[0.0, 0.0, 0.0]);
    }

    #[test]
    fn compile_constraint_zero_capacity() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: demand_a
    kind: const
    values: [50, 50]
  - id: demand_b
    kind: const
    values: [50, 50]
  - id: capacity
    kind: const
    values: [0, 0]
  - id: served_a
    kind: const
    values: [10, 10]
  - id: served_b
    kind: const
    values: [10, 10]
topology:
  nodes:
    - id: NodeA
      kind: serviceWithBuffer
      constraints: [shared]
      semantics:
        arrivals: demand_a
        served: served_a
    - id: NodeB
      kind: serviceWithBuffer
      constraints: [shared]
      semantics:
        arrivals: demand_b
        served: served_b
  edges: []
  constraints:
    - id: shared
      semantics:
        arrivals: total_demand
        served: capacity
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        // cap=0 → both capped to 0, queue = max(0, 0 - 10) = 0
        assert_approx(&result.series("node_a_queue").unwrap(), &[0.0, 0.0]);
        assert_approx(&result.series("node_b_queue").unwrap(), &[0.0, 0.0]);
    }

    #[test]
    fn compile_router_single_route_full_pass() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: source
    kind: const
    values: [100, 200, 300]
  - id: splitter
    kind: router
    router:
      inputs:
        queue: source
      routes:
        - target: only_target
          weight: 1.0
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        assert_approx(&result.series("only_target").unwrap(), &[100.0, 200.0, 300.0]);
    }

    #[test]
    fn compile_queue_with_time_varying_wip_limit() {
        // WIP limit varies per bin via a series
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [15, 15, 15]
  - id: served
    kind: const
    values: [2, 2, 2]
  - id: wip_series
    kind: const
    values: [20, 10, 5]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      wipLimitSeries: wip_series
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        // Q[0]=15-2=13 (limit=20, ok), Q[1]=13+13=26→clamped to 10(ov=16), Q[2]=10+13=23→clamped to 5(ov=18)
        assert_approx(&result.series("queue_queue").unwrap(), &[13.0, 10.0, 5.0]);
        assert_approx(&result.series("queue_overflow").unwrap(), &[0.0, 16.0, 18.0]);
    }

    #[test]
    fn compile_pmf_normalized_probabilities() {
        // PMF with probabilities that don't sum to 1 → normalized
        let model = ModelDefinition {
            grid: Some(make_grid(2)),
            nodes: vec![
                NodeDefinition {
                    id: "x".into(), kind: "pmf".into(),
                    pmf: Some(crate::model::PmfDefinition {
                        values: vec![10.0, 20.0],
                        probabilities: vec![2.0, 3.0], // sum=5, not 1
                    }),
                    ..Default::default()
                },
            ],
            ..Default::default()
        };
        let result = eval_model(&model).unwrap();
        // E[X] = (10*2/5) + (20*3/5) = 4 + 12 = 16
        assert_eq!(result.series("x").unwrap(), vec![16.0, 16.0]);
    }

    #[test]
    fn compile_empty_model_no_topology() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: a
    kind: const
    values: [1, 2, 3, 4]
  - id: b
    kind: expr
    expr: "a * 2"
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        assert_eq!(result.series("a").unwrap(), vec![1.0, 2.0, 3.0, 4.0]);
        assert_eq!(result.series("b").unwrap(), vec![2.0, 4.0, 6.0, 8.0]);
        assert!(result.warnings.is_empty());
    }

    /// Assert approximate equality with f64 tolerance.
    fn assert_approx(actual: &[f64], expected: &[f64]) {
        assert_eq!(actual.len(), expected.len(), "length mismatch: {} vs {}", actual.len(), expected.len());
        for (i, (a, e)) in actual.iter().zip(expected).enumerate() {
            assert!((a - e).abs() < 1e-10, "bin {i}: actual={a}, expected={e}");
        }
    }

    #[test]
    fn class_map_populated_for_router_class_model() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: total
    kind: const
    values: [100, 100, 100]
  - id: splitter
    kind: router
    router:
      inputs:
        queue: total
      routes:
        - target: fast
          classes: [Alpha]
        - target: slow
          classes: [Beta]
classes:
  - id: Alpha
  - id: Beta
traffic:
  arrivals:
    - nodeId: total
      classId: Alpha
      pattern:
        kind: const
        ratePerBin: 40
    - nodeId: total
      classId: Beta
      pattern:
        kind: const
        ratePerBin: 60
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Class map should contain entries for per-class arrivals at 'total'
        assert!(result.class_map.contains_key(&("total".to_string(), "Alpha".to_string())),
            "class_map missing (total, Alpha). Keys: {:?}", result.class_map.keys().collect::<Vec<_>>());
        assert!(result.class_map.contains_key(&("total".to_string(), "Beta".to_string())));

        // Verify per-class series values
        let alpha_col = result.class_map[&("total".to_string(), "Alpha".to_string())];
        let beta_col = result.class_map[&("total".to_string(), "Beta".to_string())];
        let alpha_vals = crate::eval::extract_column(&result.state, alpha_col, result.bins);
        let beta_vals = crate::eval::extract_column(&result.state, beta_col, result.bins);

        assert_approx(&alpha_vals, &[40.0, 40.0, 40.0]);
        assert_approx(&beta_vals, &[60.0, 60.0, 60.0]);

        // Router targets should have correct values
        assert_approx(&result.series("fast").unwrap(), &[40.0, 40.0, 40.0]);
        assert_approx(&result.series("slow").unwrap(), &[60.0, 60.0, 60.0]);

        // Classes metadata should be populated
        assert_eq!(result.classes.len(), 2);
        assert_eq!(result.classes[0].id, "Alpha");
        assert_eq!(result.classes[1].id, "Beta");
    }

    #[test]
    fn class_decomposition_propagates_to_expr_nodes() {
        // class-enabled fixture pattern: ingest has per-class arrivals,
        // served = MIN(ingest, 8) should get per-class decomposition
        let yaml = r#"
schemaVersion: 1
classes:
  - id: Order
  - id: Refund
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: ingest
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "MIN(ingest, 8)"
traffic:
  arrivals:
    - nodeId: ingest
      classId: Order
      pattern:
        kind: constant
        ratePerBin: 20
    - nodeId: ingest
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 5
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // ingest has per-class columns
        assert!(result.class_map.contains_key(&("ingest".to_string(), "Order".to_string())));
        assert!(result.class_map.contains_key(&("ingest".to_string(), "Refund".to_string())));

        // served should also have per-class columns (propagated from ingest)
        assert!(result.class_map.contains_key(&("served".to_string(), "Order".to_string())),
            "served should have per-class decomposition. class_map keys: {:?}",
            result.class_map.keys().collect::<Vec<_>>());
        assert!(result.class_map.contains_key(&("served".to_string(), "Refund".to_string())));

        // Verify proportional decomposition:
        // ingest = 10, ingest__class_Order = 20, ingest__class_Refund = 5
        // total class = 25, Order fraction = 20/25 = 0.8, Refund fraction = 5/25 = 0.2
        // served = MIN(10, 8) = 8
        // served__class_Order = 8 * 0.8 = 6.4
        // served__class_Refund = 8 * 0.2 = 1.6
        let served_total = result.series("served").unwrap();
        assert_approx(&served_total, &[8.0, 8.0, 8.0, 8.0]);

        let order_col = result.class_map[&("served".to_string(), "Order".to_string())];
        let refund_col = result.class_map[&("served".to_string(), "Refund".to_string())];
        let order_vals = crate::eval::extract_column(&result.state, order_col, result.bins);
        let refund_vals = crate::eval::extract_column(&result.state, refund_col, result.bins);

        assert_approx(&order_vals, &[6.4, 6.4, 6.4, 6.4]);
        assert_approx(&refund_vals, &[1.6, 1.6, 1.6, 1.6]);

        // Normalization: per-class should sum to total
        for t in 0..result.bins {
            let sum = order_vals[t] + refund_vals[t];
            assert!((sum - served_total[t]).abs() < 1e-10,
                "Normalization at bin {t}: sum={sum}, total={}", served_total[t]);
        }
    }

    #[test]
    fn class_map_empty_for_model_without_classes() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [10, 20]
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert!(result.class_map.is_empty());
        assert!(result.classes.is_empty());
    }

    #[test]
    fn class_series_sum_equals_total() {
        // Normalization invariant: per-class series should sum to total at each bin
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [100, 100, 100, 100]
  - id: router
    kind: router
    router:
      inputs:
        queue: arrivals
      routes:
        - target: lane_a
          classes: [A]
        - target: lane_b
          classes: [B]
classes:
  - id: A
  - id: B
traffic:
  arrivals:
    - nodeId: arrivals
      classId: A
      pattern:
        kind: const
        ratePerBin: 30
    - nodeId: arrivals
      classId: B
      pattern:
        kind: const
        ratePerBin: 70
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        let alpha_col = result.class_map[&("arrivals".to_string(), "A".to_string())];
        let beta_col = result.class_map[&("arrivals".to_string(), "B".to_string())];
        let alpha = crate::eval::extract_column(&result.state, alpha_col, result.bins);
        let beta = crate::eval::extract_column(&result.state, beta_col, result.bins);
        let total = result.series("arrivals").unwrap();

        // Sum of per-class series should equal total
        for t in 0..result.bins {
            let sum = alpha[t] + beta[t];
            assert!((sum - total[t]).abs() < 1e-10,
                "Normalization violated at bin {t}: class_sum={sum}, total={}", total[t]);
        }
    }

    #[test]
    fn queue_per_class_decomposition() {
        // ServiceWithBuffer with per-class arrivals:
        // arrivals=10 (const), served=MIN(arrivals,8)=8, queue grows by 2/bin
        // Classes: Order=6/bin, Refund=4/bin → fractions 0.6, 0.4
        // Queue depth per-class should be proportional to arrival fractions
        let yaml = r#"
schemaVersion: 1
classes:
  - id: Order
  - id: Refund
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "MIN(arrivals, 8)"
traffic:
  arrivals:
    - nodeId: arrivals
      classId: Order
      pattern:
        kind: constant
        ratePerBin: 6
    - nodeId: arrivals
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 4
topology:
  nodes:
    - id: Processor
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Queue depth: Q[0]=2, Q[1]=4, Q[2]=6, Q[3]=8
        let q_col_name = queue_column_id("Processor");
        let queue = result.series(&q_col_name).unwrap();
        assert_approx(&queue, &[2.0, 4.0, 6.0, 8.0]);

        // Queue depth should have per-class decomposition
        assert!(result.class_map.contains_key(&(q_col_name.clone(), "Order".to_string())),
            "queue depth should have per-class column for Order. class_map keys: {:?}",
            result.class_map.keys().collect::<Vec<_>>());
        assert!(result.class_map.contains_key(&(q_col_name.clone(), "Refund".to_string())));

        let order_col = result.class_map[&(q_col_name.clone(), "Order".to_string())];
        let refund_col = result.class_map[&(q_col_name.clone(), "Refund".to_string())];
        let order_vals = crate::eval::extract_column(&result.state, order_col, result.bins);
        let refund_vals = crate::eval::extract_column(&result.state, refund_col, result.bins);

        // Order fraction = 6/(6+4) = 0.6, Refund fraction = 0.4
        // Queue per-class: Q_Order = Q * 0.6, Q_Refund = Q * 0.4
        assert_approx(&order_vals, &[1.2, 2.4, 3.6, 4.8]);
        assert_approx(&refund_vals, &[0.8, 1.6, 2.4, 3.2]);

        // Normalization: per-class queue should sum to total queue
        for t in 0..result.bins {
            let sum = order_vals[t] + refund_vals[t];
            assert!((sum - queue[t]).abs() < 1e-10,
                "Queue normalization at bin {t}: sum={sum}, total={}", queue[t]);
        }
    }

    #[test]
    fn queue_per_class_with_custom_queue_depth_name() {
        // Queue with explicit queueDepth semantic name
        let yaml = r#"
schemaVersion: 1
classes:
  - id: A
  - id: B
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arr
    kind: const
    values: [20, 20, 20]
  - id: cap
    kind: const
    values: [10, 10, 10]
traffic:
  arrivals:
    - nodeId: arr
      classId: A
      pattern:
        kind: constant
        ratePerBin: 12
    - nodeId: arr
      classId: B
      pattern:
        kind: constant
        ratePerBin: 8
topology:
  nodes:
    - id: Q1
      kind: serviceWithBuffer
      semantics:
        arrivals: arr
        served: cap
        queueDepth: q1_depth
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Queue: Q[0]=10, Q[1]=20, Q[2]=30
        let queue = result.series("q1_depth").unwrap();
        assert_approx(&queue, &[10.0, 20.0, 30.0]);

        // Per-class queue depth: A=60%, B=40%
        assert!(result.class_map.contains_key(&("q1_depth".to_string(), "A".to_string())),
            "custom queue depth name should have per-class columns. keys: {:?}",
            result.class_map.keys().collect::<Vec<_>>());

        let a_col = result.class_map[&("q1_depth".to_string(), "A".to_string())];
        let b_col = result.class_map[&("q1_depth".to_string(), "B".to_string())];
        let a_vals = crate::eval::extract_column(&result.state, a_col, result.bins);
        let b_vals = crate::eval::extract_column(&result.state, b_col, result.bins);

        assert_approx(&a_vals, &[6.0, 12.0, 18.0]);
        assert_approx(&b_vals, &[4.0, 8.0, 12.0]);
    }

    #[test]
    fn edge_series_basic_flow_volume() {
        // Two topology nodes connected by an edge: A→B with weight 0.5
        // A.served = 100/bin → edge flowVolume = 100 * (0.5/0.5) = 100
        // (only one edge from source, so fraction = 1.0)
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arr
    kind: const
    values: [100, 100, 100]
  - id: cap
    kind: const
    values: [80, 80, 80]
  - id: arr_b
    kind: const
    values: [50, 50, 50]
  - id: cap_b
    kind: const
    values: [30, 30, 30]
topology:
  nodes:
    - id: ServiceA
      kind: serviceWithBuffer
      semantics:
        arrivals: arr
        served: cap
    - id: ServiceB
      kind: serviceWithBuffer
      semantics:
        arrivals: arr_b
        served: cap_b
  edges:
    - source: ServiceA
      target: ServiceB
      weight: 1.0
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Edge should exist with flowVolume
        let edge_id = "ServiceA→ServiceB".to_string();
        assert!(result.edge_map.contains_key(&(edge_id.clone(), "flowVolume".to_string())),
            "edge_map should contain ServiceA→ServiceB flowVolume. Keys: {:?}",
            result.edge_map.keys().collect::<Vec<_>>());

        let col = result.edge_map[&(edge_id, "flowVolume".to_string())];
        let flow = crate::eval::extract_column(&result.state, col, result.bins);
        // ServiceA served=cap=80, single edge from source → fraction=1.0
        assert_approx(&flow, &[80.0, 80.0, 80.0]);
    }

    #[test]
    fn edge_series_weighted_split() {
        // One source, two edges with weights 0.6 and 0.4
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [100, 100]
  - id: served
    kind: const
    values: [100, 100]
  - id: arr_b
    kind: const
    values: [0, 0]
  - id: srv_b
    kind: const
    values: [0, 0]
  - id: arr_c
    kind: const
    values: [0, 0]
  - id: srv_c
    kind: const
    values: [0, 0]
topology:
  nodes:
    - id: Source
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
    - id: TargetA
      kind: serviceWithBuffer
      semantics:
        arrivals: arr_b
        served: srv_b
    - id: TargetB
      kind: serviceWithBuffer
      semantics:
        arrivals: arr_c
        served: srv_c
  edges:
    - source: Source
      target: TargetA
      weight: 0.6
    - source: Source
      target: TargetB
      weight: 0.4
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        let a_col = result.edge_map[&("Source→TargetA".to_string(), "flowVolume".to_string())];
        let b_col = result.edge_map[&("Source→TargetB".to_string(), "flowVolume".to_string())];
        let a_flow = crate::eval::extract_column(&result.state, a_col, result.bins);
        let b_flow = crate::eval::extract_column(&result.state, b_col, result.bins);

        // served=100, weight 0.6/(0.6+0.4) = 0.6 → 60, weight 0.4 → 40
        assert_approx(&a_flow, &[60.0, 60.0]);
        assert_approx(&b_flow, &[40.0, 40.0]);
    }

    #[test]
    fn edge_series_no_edges_empty_map() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [10, 20]
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        assert!(result.edge_map.is_empty());
    }

    #[test]
    fn edge_series_with_explicit_id() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: arr
    kind: const
    values: [50, 50]
  - id: srv
    kind: const
    values: [40, 40]
  - id: arr2
    kind: const
    values: [0, 0]
  - id: srv2
    kind: const
    values: [0, 0]
topology:
  nodes:
    - id: A
      kind: serviceWithBuffer
      semantics:
        arrivals: arr
        served: srv
    - id: B
      kind: serviceWithBuffer
      semantics:
        arrivals: arr2
        served: srv2
  edges:
    - source: A
      target: B
      weight: 1.0
      id: primary-link
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        assert!(result.edge_map.contains_key(&("primary-link".to_string(), "flowVolume".to_string())),
            "edge_map should use explicit edge id. Keys: {:?}",
            result.edge_map.keys().collect::<Vec<_>>());

        let col = result.edge_map[&("primary-link".to_string(), "flowVolume".to_string())];
        let flow = crate::eval::extract_column(&result.state, col, result.bins);
        assert_approx(&flow, &[40.0, 40.0]);
    }

    #[test]
    fn edge_series_with_multiplier() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: arr
    kind: const
    values: [100, 100]
  - id: srv
    kind: const
    values: [80, 80]
  - id: arr2
    kind: const
    values: [0, 0]
  - id: srv2
    kind: const
    values: [0, 0]
topology:
  nodes:
    - id: S
      kind: serviceWithBuffer
      semantics:
        arrivals: arr
        served: srv
    - id: T
      kind: serviceWithBuffer
      semantics:
        arrivals: arr2
        served: srv2
  edges:
    - source: S
      target: T
      weight: 1.0
      multiplier: 2.0
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        let col = result.edge_map[&("S→T".to_string(), "flowVolume".to_string())];
        let flow = crate::eval::extract_column(&result.state, col, result.bins);
        // served=80, fraction=1.0, multiplier=2.0 → 160
        assert_approx(&flow, &[160.0, 160.0]);
    }

    #[test]
    fn edge_series_per_class_flow() {
        // Edge with per-class flow: source has class arrivals, edge flow splits by class
        let yaml = r#"
schemaVersion: 1
classes:
  - id: X
  - id: Y
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arr
    kind: const
    values: [100, 100, 100]
  - id: srv
    kind: expr
    expr: "arr * 0.8"
  - id: arr2
    kind: const
    values: [0, 0, 0]
  - id: srv2
    kind: const
    values: [0, 0, 0]
traffic:
  arrivals:
    - nodeId: arr
      classId: X
      pattern:
        kind: constant
        ratePerBin: 70
    - nodeId: arr
      classId: Y
      pattern:
        kind: constant
        ratePerBin: 30
topology:
  nodes:
    - id: Src
      kind: serviceWithBuffer
      semantics:
        arrivals: arr
        served: srv
    - id: Dst
      kind: serviceWithBuffer
      semantics:
        arrivals: arr2
        served: srv2
  edges:
    - source: Src
      target: Dst
      weight: 1.0
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        // Total edge flow: srv=80, fraction=1.0 → 80
        let total_col = result.edge_map[&("Src→Dst".to_string(), "flowVolume".to_string())];
        let total_flow = crate::eval::extract_column(&result.state, total_col, result.bins);
        assert_approx(&total_flow, &[80.0, 80.0, 80.0]);

        // Per-class edge flow: X fraction = 70/100 = 0.7, Y = 0.3
        // But per-class is based on served per-class (propagated): srv__class_X = 80 * 0.7 = 56
        // Edge class X = 56 * 1.0 = 56, Edge class Y = 24
        let x_key = ("Src→Dst@X".to_string(), "flowVolume".to_string());
        let y_key = ("Src→Dst@Y".to_string(), "flowVolume".to_string());
        assert!(result.edge_map.contains_key(&x_key),
            "edge_map should have per-class edge. Keys: {:?}",
            result.edge_map.keys().collect::<Vec<_>>());

        let x_col = result.edge_map[&x_key];
        let y_col = result.edge_map[&y_key];
        let x_flow = crate::eval::extract_column(&result.state, x_col, result.bins);
        let y_flow = crate::eval::extract_column(&result.state, y_col, result.bins);

        assert_approx(&x_flow, &[56.0, 56.0, 56.0]);
        assert_approx(&y_flow, &[24.0, 24.0, 24.0]);

        // Normalization: per-class edge flow should sum to total edge flow
        for t in 0..result.bins {
            let sum = x_flow[t] + y_flow[t];
            assert!((sum - total_flow[t]).abs() < 1e-10,
                "Edge per-class normalization at bin {t}: sum={sum}, total={}", total_flow[t]);
        }
    }

    #[test]
    fn normalization_invariant_class_enabled_fixture() {
        // AC-9: For every node with per-class series, sum of per-class == total within 1e-10
        let yaml = std::fs::read_to_string("../fixtures/class-enabled.yaml")
            .expect("class-enabled.yaml fixture not found");
        let model = crate::model::parse_model_yaml(&yaml).unwrap();
        let result = eval_model(&model).unwrap();

        verify_normalization_invariant(&result);
    }

    #[test]
    fn normalization_invariant_router_class_fixture() {
        let yaml = std::fs::read_to_string("../fixtures/router-class.yaml")
            .expect("router-class.yaml fixture not found");
        let model = crate::model::parse_model_yaml(&yaml).unwrap();
        let result = eval_model(&model).unwrap();

        verify_normalization_invariant(&result);
    }

    #[test]
    fn normalization_invariant_router_mixed_fixture() {
        let yaml = std::fs::read_to_string("../fixtures/router-mixed.yaml")
            .expect("router-mixed.yaml fixture not found");
        let model = crate::model::parse_model_yaml(&yaml).unwrap();
        let result = eval_model(&model).unwrap();

        verify_normalization_invariant(&result);
    }

    #[test]
    fn normalization_invariant_queue_with_classes() {
        // Normalization for queue per-class decomposition
        let yaml = r#"
schemaVersion: 1
classes:
  - id: Fast
  - id: Slow
grid:
  bins: 6
  binSize: 1
  binUnit: hours
nodes:
  - id: inflow
    kind: const
    values: [20, 20, 20, 20, 20, 20]
  - id: outflow
    kind: expr
    expr: "MIN(inflow, 12)"
traffic:
  arrivals:
    - nodeId: inflow
      classId: Fast
      pattern:
        kind: constant
        ratePerBin: 15
    - nodeId: inflow
      classId: Slow
      pattern:
        kind: constant
        ratePerBin: 5
topology:
  nodes:
    - id: WorkQueue
      kind: serviceWithBuffer
      semantics:
        arrivals: inflow
        served: outflow
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        verify_normalization_invariant(&result);
    }

    /// Helper: verify that for every node with per-class series, sum == total
    fn verify_normalization_invariant(result: &EvalResult) {
        // Collect unique (node_id, class_id) pairs
        let mut nodes_with_classes: std::collections::HashMap<String, Vec<String>> = std::collections::HashMap::new();
        for ((node_id, class_id), _) in &result.class_map {
            nodes_with_classes.entry(node_id.clone()).or_default().push(class_id.clone());
        }

        assert!(!nodes_with_classes.is_empty(), "Expected at least one node with per-class columns");

        for (node_id, class_ids) in &nodes_with_classes {
            let total = match result.series(node_id) {
                Some(t) => t,
                None => continue, // node may be internal (e.g., __temp_ or __edge_)
            };

            for t in 0..result.bins {
                let class_sum: f64 = class_ids.iter()
                    .map(|cid| {
                        let col = result.class_map[&(node_id.clone(), cid.clone())];
                        result.state[col * result.bins + t]
                    })
                    .sum();

                assert!((class_sum - total[t]).abs() < 1e-10,
                    "Normalization invariant violated for '{}' at bin {}: class_sum={}, total={}",
                    node_id, t, class_sum, total[t]);
            }
        }
    }

    // ── Parameter table tests ──

    #[test]
    fn param_table_registers_const_nodes() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: capacity
    kind: const
    values: [5, 5, 5, 5]
  - id: served
    kind: expr
    expr: "MIN(arrivals, capacity)"
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let plan = compile(&model).unwrap();

        assert!(plan.params.get("arrivals").is_some(), "const node 'arrivals' should be a param");
        assert!(plan.params.get("capacity").is_some(), "const node 'capacity' should be a param");
        assert!(plan.params.get("served").is_none(), "expr node should not be a param");

        let arr_param = plan.params.get("arrivals").unwrap();
        assert_eq!(arr_param.default, crate::plan::ParamValue::Scalar(10.0));
        assert_eq!(arr_param.kind, crate::plan::ParamKind::ConstNode);
    }

    #[test]
    fn param_table_registers_arrival_rates() {
        let yaml = r#"
schemaVersion: 1
classes:
  - id: Order
  - id: Refund
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: ingest
    kind: const
    values: [10, 10, 10]
traffic:
  arrivals:
    - nodeId: ingest
      classId: Order
      pattern:
        kind: constant
        ratePerBin: 6
    - nodeId: ingest
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 4
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let plan = compile(&model).unwrap();

        assert!(plan.params.get("ingest.Order").is_some(), "arrival rate should be a param");
        assert!(plan.params.get("ingest.Refund").is_some());

        let order_param = plan.params.get("ingest.Order").unwrap();
        assert_eq!(order_param.default, crate::plan::ParamValue::Scalar(6.0));
        assert_eq!(order_param.kind, crate::plan::ParamKind::ArrivalRate);
    }

    #[test]
    fn param_table_registers_wip_limit() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arr
    kind: const
    values: [20, 20, 20]
  - id: srv
    kind: const
    values: [5, 5, 5]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      wipLimit: 50
      semantics:
        arrivals: arr
        served: srv
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let plan = compile(&model).unwrap();

        assert!(plan.params.get("Queue.wipLimit").is_some(),
            "WIP limit should be a param. Params: {:?}",
            plan.params.entries.iter().map(|p| &p.id).collect::<Vec<_>>());

        let wip_param = plan.params.get("Queue.wipLimit").unwrap();
        assert_eq!(wip_param.default, crate::plan::ParamValue::Scalar(50.0));
        assert_eq!(wip_param.kind, crate::plan::ParamKind::WipLimit);
    }

    #[test]
    fn param_table_varying_const_is_vector() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: demand
    kind: const
    values: [10, 20, 30]
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let plan = compile(&model).unwrap();

        let param = plan.params.get("demand").unwrap();
        assert_eq!(param.default, crate::plan::ParamValue::Vector(vec![10.0, 20.0, 30.0]));
    }

    #[test]
    fn param_table_empty_for_expr_only_model() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let plan = compile(&model).unwrap();
        assert!(plan.params.is_empty());
    }

    #[test]
    fn extract_params_returns_param_table() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [5, 5]
  - id: y
    kind: const
    values: [10, 10]
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let plan = compile(&model).unwrap();

        let params = &plan.params;
        assert_eq!(params.len(), 2);
        assert!(params.get("x").is_some());
        assert!(params.get("y").is_some());
    }

    // ── AC-5/6: eval_model_with_params propagation ──

    #[test]
    fn eval_model_with_params_override_propagates_downstream() {
        // AC-6: override arrivals → served, queue_depth, per-class, edges all update
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "MIN(arrivals, 8)"
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();

        // Default: arrivals=10, served=MIN(10,8)=8, Q grows by 2/bin
        let result = eval_model(&model).unwrap();
        assert_approx(&result.series("served").unwrap(), &[8.0, 8.0, 8.0, 8.0]);

        // Override: arrivals=20 → served=MIN(20,8)=8 (still capped), Q grows by 12/bin
        let overrides = vec![("arrivals".to_string(), crate::plan::ParamValue::Scalar(20.0))];
        let result2 = eval_model_with_params(&model, &overrides).unwrap();
        assert_approx(&result2.series("served").unwrap(), &[8.0, 8.0, 8.0, 8.0]);
        let q_name = queue_column_id("Queue");
        let queue = result2.series(&q_name).unwrap();
        assert_approx(&queue, &[12.0, 24.0, 36.0, 48.0]); // 20-8=12 per bin

        // Override: arrivals=5 → served=MIN(5,8)=5, Q grows by 0 (no queue)
        let overrides3 = vec![("arrivals".to_string(), crate::plan::ParamValue::Scalar(5.0))];
        let result3 = eval_model_with_params(&model, &overrides3).unwrap();
        assert_approx(&result3.series("served").unwrap(), &[5.0, 5.0, 5.0, 5.0]);
        let queue3 = result3.series(&q_name).unwrap();
        assert_approx(&queue3, &[0.0, 0.0, 0.0, 0.0]);
    }

    #[test]
    fn eval_model_with_params_class_rate_override() {
        // AC-7: override class arrival rate, verify normalization holds
        let yaml = r#"
schemaVersion: 1
classes:
  - id: Fast
  - id: Slow
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [100, 100, 100]
  - id: served
    kind: expr
    expr: "MIN(arrivals, 80)"
traffic:
  arrivals:
    - nodeId: arrivals
      classId: Fast
      pattern:
        kind: constant
        ratePerBin: 60
    - nodeId: arrivals
      classId: Slow
      pattern:
        kind: constant
        ratePerBin: 40
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();

        // Default: Fast=60%, Slow=40%
        let result = eval_model(&model).unwrap();
        let fast_col = result.class_map[&("served".to_string(), "Fast".to_string())];
        let slow_col = result.class_map[&("served".to_string(), "Slow".to_string())];
        let fast_default = crate::eval::extract_column(&result.state, fast_col, result.bins);
        let slow_default = crate::eval::extract_column(&result.state, slow_col, result.bins);
        // served=80. Fast fraction = 60/100 = 0.6. served_Fast = 80 * 0.6 = 48
        assert_approx(&fast_default, &[48.0, 48.0, 48.0]);
        assert_approx(&slow_default, &[32.0, 32.0, 32.0]);

        // Override: Fast rate 90 (Slow stays 40) → new fractions 90/130, 40/130
        let overrides = vec![("arrivals.Fast".to_string(), crate::plan::ParamValue::Scalar(90.0))];
        let result2 = eval_model_with_params(&model, &overrides).unwrap();
        let fast2_col = result2.class_map[&("served".to_string(), "Fast".to_string())];
        let slow2_col = result2.class_map[&("served".to_string(), "Slow".to_string())];
        let fast2 = crate::eval::extract_column(&result2.state, fast2_col, result2.bins);
        let slow2 = crate::eval::extract_column(&result2.state, slow2_col, result2.bins);

        // served=80, Fast fraction = 90/(90+40) = 90/130 ≈ 0.6923
        let expected_fast = 80.0 * 90.0 / 130.0;
        let expected_slow = 80.0 * 40.0 / 130.0;
        assert!((fast2[0] - expected_fast).abs() < 1e-10,
            "Fast override: expected {expected_fast}, got {}", fast2[0]);
        assert!((slow2[0] - expected_slow).abs() < 1e-10);

        // Normalization: per-class should still sum to total
        let served2 = result2.series("served").unwrap();
        for t in 0..result2.bins {
            let sum = fast2[t] + slow2[t];
            assert!((sum - served2[t]).abs() < 1e-10,
                "Normalization violated after class override at bin {t}: sum={sum}, total={}", served2[t]);
        }
    }

    #[test]
    fn compile_once_eval_many_independent() {
        // AC-10: compile once, evaluate 10 times with different rates, verify independence
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "arrivals * 0.5"
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let plan = compile(&model).unwrap();

        // Verify plan has the parameter
        assert!(plan.params.get("arrivals").is_some());

        // Evaluate 10 times with different arrival rates
        let rates = [5.0, 10.0, 15.0, 20.0, 25.0, 30.0, 50.0, 100.0, 0.0, 1.0];
        let mut results = Vec::new();

        for &rate in &rates {
            let overrides = vec![("arrivals".to_string(), crate::plan::ParamValue::Scalar(rate))];
            let state = crate::eval::evaluate_with_params(&plan, &overrides);
            let served_col = plan.column_map.get("served").unwrap();
            let served = crate::eval::extract_column(&state, served_col, plan.bins);
            results.push((rate, served));
        }

        // Verify each result is independent and correct: served = arrivals * 0.5
        for (rate, served) in &results {
            let expected = rate * 0.5;
            for t in 0..plan.bins {
                assert!((served[t] - expected).abs() < 1e-10,
                    "Rate={rate}, bin {t}: expected {expected}, got {}", served[t]);
            }
        }

        // Verify original plan is unchanged (re-eval with defaults still works)
        let default_state = crate::eval::evaluate(&plan);
        let served_col = plan.column_map.get("served").unwrap();
        let default_served = crate::eval::extract_column(&default_state, served_col, plan.bins);
        assert_approx(&default_served, &[5.0, 5.0, 5.0, 5.0]);
    }

    #[test]
    fn eval_model_with_params_wip_limit_override() {
        // AC-8: override WIP limit → overflow changes
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arr
    kind: const
    values: [20, 20, 20, 20]
  - id: srv
    kind: const
    values: [5, 5, 5, 5]
topology:
  nodes:
    - id: Q
      kind: serviceWithBuffer
      wipLimit: 50
      semantics:
        arrivals: arr
        served: srv
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();

        // Default: WIP=50, inflow=20, outflow=5 → Q=[15,30,45,50(capped)]
        let result = eval_model(&model).unwrap();
        let q = result.series(&queue_column_id("Q")).unwrap();
        assert_approx(&q, &[15.0, 30.0, 45.0, 50.0]);

        // Override: WIP=25 → Q=[15,25(capped),25,25]
        let overrides = vec![("Q.wipLimit".to_string(), crate::plan::ParamValue::Scalar(25.0))];
        let result2 = eval_model_with_params(&model, &overrides).unwrap();
        let q2 = result2.series(&queue_column_id("Q")).unwrap();
        assert_approx(&q2, &[15.0, 25.0, 25.0, 25.0]);
    }

    // ── Graph derivation tests (m-E17-03) ──

    #[test]
    fn derive_graph_simple_expr() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "arrivals * 0.8"
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let graph = derive_graph(&model);

        assert_eq!(graph.nodes.len(), 2);
        let arrivals = graph.nodes.iter().find(|n| n.id == "arrivals").unwrap();
        assert_eq!(arrivals.kind, "const");
        let served = graph.nodes.iter().find(|n| n.id == "served").unwrap();
        assert_eq!(served.kind, "expr");

        // Edge: arrivals → served
        assert_eq!(graph.edges.len(), 1);
        assert_eq!(graph.edges[0].from, "arrivals");
        assert_eq!(graph.edges[0].to, "served");
    }

    #[test]
    fn derive_graph_multi_ref_expr() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: a
    kind: const
    values: [1, 2, 3]
  - id: b
    kind: const
    values: [4, 5, 6]
  - id: sum
    kind: expr
    expr: "a + b"
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let graph = derive_graph(&model);

        assert_eq!(graph.nodes.len(), 3);
        // Both a and b should be referenced by sum
        let edges_to_sum: Vec<&GraphEdgeInfo> = graph.edges.iter().filter(|e| e.to == "sum").collect();
        assert_eq!(edges_to_sum.len(), 2);
        let froms: HashSet<&str> = edges_to_sum.iter().map(|e| e.from.as_str()).collect();
        assert!(froms.contains("a"));
        assert!(froms.contains("b"));
    }

    #[test]
    fn derive_graph_with_topology() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: inflow
    kind: const
    values: [10, 10, 10, 10]
  - id: outflow
    kind: const
    values: [5, 5, 5, 5]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      semantics:
        arrivals: inflow
        served: outflow
  edges: []
  constraints: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let graph = derive_graph(&model);

        // 3 nodes: inflow, outflow, Queue
        assert_eq!(graph.nodes.len(), 3);
        let queue_node = graph.nodes.iter().find(|n| n.id == "Queue").unwrap();
        assert_eq!(queue_node.kind, "servicewithbuffer");

        // Edges: inflow → Queue, Queue → outflow
        let inflow_to_q = graph.edges.iter().any(|e| e.from == "inflow" && e.to == "Queue");
        let q_to_outflow = graph.edges.iter().any(|e| e.from == "Queue" && e.to == "outflow");
        assert!(inflow_to_q, "Should have edge inflow → Queue. Edges: {:?}", graph.edges);
        assert!(q_to_outflow, "Should have edge Queue → outflow");
    }

    #[test]
    fn derive_graph_router() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
classes:
  - id: Alpha
nodes:
  - id: total
    kind: const
    values: [100, 100, 100]
  - id: splitter
    kind: router
    router:
      inputs:
        queue: total
      routes:
        - target: fast
          classes: [Alpha]
        - target: slow
traffic:
  arrivals:
    - nodeId: total
      classId: Alpha
      pattern:
        kind: const
        ratePerBin: 60
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let graph = derive_graph(&model);

        // splitter should have edge from "total" and edges to "fast" and "slow"
        let total_to_splitter = graph.edges.iter().any(|e| e.from == "total" && e.to == "splitter");
        let splitter_to_fast = graph.edges.iter().any(|e| e.from == "splitter" && e.to == "fast");
        let splitter_to_slow = graph.edges.iter().any(|e| e.from == "splitter" && e.to == "slow");
        assert!(total_to_splitter);
        assert!(splitter_to_fast);
        assert!(splitter_to_slow);
    }

    #[test]
    fn derive_graph_empty_model() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes: []
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let graph = derive_graph(&model);
        assert!(graph.nodes.is_empty());
        assert!(graph.edges.is_empty());
    }

    #[test]
    fn derive_graph_const_only_no_edges() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: a
    kind: const
    values: [1, 2]
  - id: b
    kind: const
    values: [3, 4]
"#;
        let model = crate::model::parse_model_yaml(yaml).unwrap();
        let graph = derive_graph(&model);
        assert_eq!(graph.nodes.len(), 2);
        assert_eq!(graph.edges.len(), 0);
    }
}
