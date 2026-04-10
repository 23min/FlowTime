//! Evaluation plan: column map, Op enum, and plan structure.
//!
//! The plan is the intermediate representation between model compilation
//! and matrix evaluation. It is inspectable data, not opaque code.

use std::collections::HashMap;
use std::fmt;

/// Bidirectional mapping between series names and column indices.
#[derive(Debug, Clone)]
pub struct ColumnMap {
    name_to_idx: HashMap<String, usize>,
    idx_to_name: Vec<String>,
}

impl ColumnMap {
    pub fn new() -> Self {
        Self {
            name_to_idx: HashMap::new(),
            idx_to_name: Vec::new(),
        }
    }

    /// Assign a new column index for the given name. Returns the index.
    /// Panics if the name is already assigned.
    pub fn insert(&mut self, name: &str) -> usize {
        if self.name_to_idx.contains_key(name) {
            panic!("Column '{}' already exists in ColumnMap", name);
        }
        let idx = self.idx_to_name.len();
        self.name_to_idx.insert(name.to_string(), idx);
        self.idx_to_name.push(name.to_string());
        idx
    }

    /// Get or create a column index for the given name.
    pub fn get_or_insert(&mut self, name: &str) -> usize {
        if let Some(&idx) = self.name_to_idx.get(name) {
            return idx;
        }
        self.insert(name)
    }

    /// Allocate a temporary column with a generated name.
    pub fn alloc_temp(&mut self) -> usize {
        let idx = self.idx_to_name.len();
        let name = format!("__temp_{}", idx);
        self.name_to_idx.insert(name.clone(), idx);
        self.idx_to_name.push(name);
        idx
    }

    /// Look up a column index by name. Returns None if not found.
    pub fn get(&self, name: &str) -> Option<usize> {
        self.name_to_idx.get(name).copied()
    }

    /// Look up a column name by index. Returns None if out of bounds.
    pub fn name(&self, idx: usize) -> Option<&str> {
        self.idx_to_name.get(idx).map(|s| s.as_str())
    }

    /// Total number of columns (series count).
    pub fn len(&self) -> usize {
        self.idx_to_name.len()
    }

    pub fn is_empty(&self) -> bool {
        self.idx_to_name.is_empty()
    }

    /// Iterate over (index, name) pairs.
    pub fn iter(&self) -> impl Iterator<Item = (usize, &str)> {
        self.idx_to_name.iter().enumerate().map(|(i, s)| (i, s.as_str()))
    }
}

/// A single operation in the evaluation plan.
/// Each op reads input columns and writes one output column.
#[derive(Debug, Clone)]
pub enum Op {
    /// Write constant values to a column.
    Const { out: usize, values: Vec<f64> },

    // Element-wise binary ops
    VecAdd { out: usize, a: usize, b: usize },
    VecSub { out: usize, a: usize, b: usize },
    VecMul { out: usize, a: usize, b: usize },
    VecDiv { out: usize, a: usize, b: usize },
    VecMin { out: usize, a: usize, b: usize },
    VecMax { out: usize, a: usize, b: usize },

    /// Clamp each element: max(lo, min(hi, val))
    Clamp { out: usize, val: usize, lo: usize, hi: usize },

    /// Modulo: a % b (Euclidean)
    Mod { out: usize, a: usize, b: usize },

    // Scalar ops
    ScalarAdd { out: usize, input: usize, k: f64 },
    ScalarMul { out: usize, input: usize, k: f64 },

    // Unary math
    Floor { out: usize, input: usize },
    Ceil { out: usize, input: usize },
    Round { out: usize, input: usize },

    /// Step function: 1.0 if input[t] >= threshold[t], else 0.0
    Step { out: usize, input: usize, threshold: usize },

    /// Periodic pulse: amplitude at every period-th bin (with phase offset)
    Pulse { out: usize, period: usize, phase: usize, amplitude: Option<usize> },

    // --- Sequential ops (process bins in order, reading previous bins) ---

    /// Temporal shift: out[t] = input[t - lag] (0 for t < lag).
    Shift { out: usize, input: usize, lag: usize },

    /// Causal convolution: out[t] = Σ(k) input[t-k] * kernel[k].
    Convolve { out: usize, input: usize, kernel: Vec<f64> },

    /// Queue recurrence: Q[t] = max(0, Q[t-1] + inflow - outflow - loss).
    /// Optional WIP limit clamps Q and writes excess to overflow_out.
    QueueRecurrence {
        out: usize,
        inflow: usize,
        outflow: usize,
        loss: Option<usize>,
        init: f64,
        wip_limit: Option<usize>,
        overflow_out: Option<usize>,
    },

    /// Dispatch gate: zeros outflow on non-dispatch bins, caps at capacity on dispatch bins.
    DispatchGate { out: usize, input: usize, period: usize, phase: usize, capacity: Option<usize> },

    /// Proportional allocation: when total demand > capacity, cap each demand proportionally.
    /// demands[i] and outs[i] are paired: outs[i][t] = demand[i][t] if total <= capacity,
    /// else outs[i][t] = capacity[t] * demand[i][t] / totalDemand[t].
    ProportionalAlloc { outs: Vec<usize>, demands: Vec<usize>, capacity: usize },

    /// Copy one column to another (used for intermediate forwarding).
    Copy { out: usize, input: usize },
}

/// A parameter value: either a single scalar (fills all bins) or a per-bin vector.
#[derive(Debug, Clone, PartialEq)]
pub enum ParamValue {
    /// Fill all bins with this value.
    Scalar(f64),
    /// Per-bin values (length must equal plan.bins).
    Vector(Vec<f64>),
}

/// What kind of model input this parameter represents.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ParamKind {
    /// A `kind: const` node.
    ConstNode,
    /// A `traffic.arrivals` entry with `ratePerBin`.
    ArrivalRate,
    /// A topology node's scalar `wipLimit`.
    WipLimit,
    /// A topology node's `initialCondition.queueDepth`.
    InitialCondition,
}

/// A single tweakable parameter extracted from the model.
#[derive(Debug, Clone)]
pub struct ParamEntry {
    /// Stable identifier matching the model YAML source.
    /// Examples: `"arrivals"`, `"arrivals.Order"`, `"Queue.wipLimit"`, `"Queue.init"`.
    pub id: String,
    /// Column index in the state matrix that this parameter fills.
    pub column: usize,
    /// Original value from the model.
    pub default: ParamValue,
    /// What kind of model input this is.
    pub kind: ParamKind,
}

/// Parameter table: all tweakable parameters in a compiled Plan.
#[derive(Debug, Clone, Default)]
pub struct ParamTable {
    pub entries: Vec<ParamEntry>,
}

impl ParamTable {
    pub fn new() -> Self {
        Self { entries: Vec::new() }
    }

    /// Register a parameter. Duplicates (same id) are silently ignored.
    pub fn register(&mut self, entry: ParamEntry) {
        if !self.entries.iter().any(|e| e.id == entry.id) {
            self.entries.push(entry);
        }
    }

    /// Look up a parameter by id.
    pub fn get(&self, id: &str) -> Option<&ParamEntry> {
        self.entries.iter().find(|e| e.id == id)
    }

    pub fn is_empty(&self) -> bool {
        self.entries.is_empty()
    }

    pub fn len(&self) -> usize {
        self.entries.len()
    }
}

/// The compiled evaluation plan: an ordered list of ops + the column map.
#[derive(Debug, Clone)]
pub struct Plan {
    pub ops: Vec<Op>,
    pub column_map: ColumnMap,
    pub bins: usize,
    /// Tweakable parameters extracted from the model.
    pub params: ParamTable,
}

impl Plan {
    /// Format the plan as a human-readable string.
    pub fn format(&self) -> String {
        let mut out = String::new();
        out.push_str(&format!("Plan: {} ops, {} columns, {} bins\n", self.ops.len(), self.column_map.len(), self.bins));
        out.push_str("Columns:\n");
        for (idx, name) in self.column_map.iter() {
            out.push_str(&format!("  [{idx}] {name}\n"));
        }
        out.push_str("Ops:\n");
        for (i, op) in self.ops.iter().enumerate() {
            out.push_str(&format!("  {i}: {}\n", self.format_op(op)));
        }
        out
    }

    fn col_name(&self, idx: usize) -> &str {
        self.column_map.name(idx).unwrap_or("?")
    }

    fn format_op(&self, op: &Op) -> String {
        match op {
            Op::Const { out, values } => {
                let preview: Vec<String> = values.iter().take(4).map(|v| format!("{v}")).collect();
                let suffix = if values.len() > 4 { ", ..." } else { "" };
                format!("Const({} = [{}{}])", self.col_name(*out), preview.join(", "), suffix)
            }
            Op::VecAdd { out, a, b } => format!("{} = {} + {}", self.col_name(*out), self.col_name(*a), self.col_name(*b)),
            Op::VecSub { out, a, b } => format!("{} = {} - {}", self.col_name(*out), self.col_name(*a), self.col_name(*b)),
            Op::VecMul { out, a, b } => format!("{} = {} * {}", self.col_name(*out), self.col_name(*a), self.col_name(*b)),
            Op::VecDiv { out, a, b } => format!("{} = {} / {}", self.col_name(*out), self.col_name(*a), self.col_name(*b)),
            Op::VecMin { out, a, b } => format!("{} = MIN({}, {})", self.col_name(*out), self.col_name(*a), self.col_name(*b)),
            Op::VecMax { out, a, b } => format!("{} = MAX({}, {})", self.col_name(*out), self.col_name(*a), self.col_name(*b)),
            Op::Clamp { out, val, lo, hi } => format!("{} = CLAMP({}, {}, {})", self.col_name(*out), self.col_name(*val), self.col_name(*lo), self.col_name(*hi)),
            Op::Mod { out, a, b } => format!("{} = MOD({}, {})", self.col_name(*out), self.col_name(*a), self.col_name(*b)),
            Op::ScalarAdd { out, input, k } => format!("{} = {} + {k}", self.col_name(*out), self.col_name(*input)),
            Op::ScalarMul { out, input, k } => format!("{} = {} * {k}", self.col_name(*out), self.col_name(*input)),
            Op::Floor { out, input } => format!("{} = FLOOR({})", self.col_name(*out), self.col_name(*input)),
            Op::Ceil { out, input } => format!("{} = CEIL({})", self.col_name(*out), self.col_name(*input)),
            Op::Round { out, input } => format!("{} = ROUND({})", self.col_name(*out), self.col_name(*input)),
            Op::Step { out, input, threshold } => format!("{} = STEP({}, {})", self.col_name(*out), self.col_name(*input), self.col_name(*threshold)),
            Op::Pulse { out, period, phase, amplitude } => {
                let amp = amplitude.map(|a| self.col_name(a).to_string()).unwrap_or("1.0".to_string());
                format!("{} = PULSE(period={period}, phase={phase}, amp={amp})", self.col_name(*out))
            }
            Op::Shift { out, input, lag } => format!("{} = SHIFT({}, {lag})", self.col_name(*out), self.col_name(*input)),
            Op::Convolve { out, input, kernel } => {
                let preview: Vec<String> = kernel.iter().take(4).map(|v| format!("{v}")).collect();
                let suffix = if kernel.len() > 4 { ", ..." } else { "" };
                format!("{} = CONV({}, [{}{}])", self.col_name(*out), self.col_name(*input), preview.join(", "), suffix)
            }
            Op::QueueRecurrence { out, inflow, outflow, loss, init, wip_limit, overflow_out } => {
                let mut s = format!("{} = QUEUE({}, {}", self.col_name(*out), self.col_name(*inflow), self.col_name(*outflow));
                if let Some(l) = loss { s.push_str(&format!(", loss={}", self.col_name(*l))); }
                if *init != 0.0 { s.push_str(&format!(", init={init}")); }
                if let Some(w) = wip_limit { s.push_str(&format!(", wip={}", self.col_name(*w))); }
                if let Some(o) = overflow_out { s.push_str(&format!(", overflow={}", self.col_name(*o))); }
                s.push(')');
                s
            }
            Op::DispatchGate { out, input, period, phase, capacity } => {
                let cap = capacity.map(|c| format!(", cap={}", self.col_name(c))).unwrap_or_default();
                format!("{} = DISPATCH({}, period={period}, phase={phase}{cap})", self.col_name(*out), self.col_name(*input))
            }
            Op::ProportionalAlloc { outs, demands, capacity } => {
                let demand_names: Vec<&str> = demands.iter().map(|d| self.col_name(*d)).collect();
                format!("PROPORTIONAL_ALLOC([{}] / {}, cap={})", demand_names.join(", "),
                    outs.iter().map(|o| self.col_name(*o)).collect::<Vec<_>>().join(", "),
                    self.col_name(*capacity))
            }
            Op::Copy { out, input } => format!("{} = COPY({})", self.col_name(*out), self.col_name(*input)),
        }
    }
}

impl fmt::Display for Plan {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.format())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn column_map_insert_and_lookup() {
        let mut cm = ColumnMap::new();
        let a = cm.insert("arrivals");
        let b = cm.insert("served");
        assert_eq!(a, 0);
        assert_eq!(b, 1);
        assert_eq!(cm.get("arrivals"), Some(0));
        assert_eq!(cm.get("served"), Some(1));
        assert_eq!(cm.get("missing"), None);
        assert_eq!(cm.name(0), Some("arrivals"));
        assert_eq!(cm.name(1), Some("served"));
        assert_eq!(cm.len(), 2);
    }

    #[test]
    fn column_map_alloc_temp() {
        let mut cm = ColumnMap::new();
        cm.insert("a");
        let t = cm.alloc_temp();
        assert_eq!(t, 1);
        assert!(cm.name(t).unwrap().starts_with("__temp_"));
    }

    #[test]
    fn column_map_get_or_insert() {
        let mut cm = ColumnMap::new();
        let a = cm.get_or_insert("x");
        let b = cm.get_or_insert("x");
        assert_eq!(a, b);
        assert_eq!(cm.len(), 1);
    }
}
