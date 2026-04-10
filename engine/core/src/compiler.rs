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
    pub warnings: Vec<crate::analysis::Warning>,
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
    let plan = compile(model)?;
    let state = crate::eval::evaluate(&plan);
    let mut result = EvalResult {
        state,
        column_map: plan.column_map,
        bins: plan.bins,
        warnings: Vec::new(),
    };
    result.warnings = crate::analysis::analyze(model, &result);
    Ok(result)
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
}
