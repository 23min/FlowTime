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

    Ok(Plan { ops, column_map, bins })
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
    cm: &ColumnMap,
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

/// Synthesize queue column ID from topology node ID (e.g., "Queue" → "queue_queue").
fn queue_column_id(topo_node_id: &str) -> String {
    format!("{}_queue", to_snake_case(topo_node_id))
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

    /// Assert approximate equality with f64 tolerance.
    fn assert_approx(actual: &[f64], expected: &[f64]) {
        assert_eq!(actual.len(), expected.len(), "length mismatch: {} vs {}", actual.len(), expected.len());
        for (i, (a, e)) in actual.iter().zip(expected).enumerate() {
            assert!((a - e).abs() < 1e-10, "bin {i}: actual={a}, expected={e}");
        }
    }
}
