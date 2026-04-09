//! Matrix evaluator: executes a plan against a flat f64 matrix.
//!
//! The matrix is row-major: state[col * bins + t] is column `col` at bin `t`.
//! All bins for one series are contiguous in memory.
//!
//! Evaluation is **bin-major**: the outer loop iterates bins, the inner loop
//! iterates ops. This naturally supports SHIFT-based feedback cycles —
//! each bin reads previous-bin state already written by earlier iterations.

use crate::plan::{Op, Plan};

/// Execute a plan and return the filled matrix.
///
/// Uses bin-major evaluation: for each bin t, execute all ops at bin t.
/// This allows SHIFT-based feedback cycles to work without special handling —
/// when processing bin t, all ops read from bin t-1 (already computed).
pub fn evaluate(plan: &Plan) -> Vec<f64> {
    let bins = plan.bins;
    let cols = plan.column_map.len();
    let mut state = vec![0.0f64; cols * bins];

    // Pre-write Const ops (they don't depend on other ops and fill all bins)
    for op in &plan.ops {
        if let Op::Const { out, values } = op {
            let len = values.len().min(bins);
            for t in 0..len {
                set(&mut state, *out, t, bins, values[t]);
            }
        }
    }

    // Bin-major evaluation: process all non-Const ops for each bin
    for t in 0..bins {
        for op in &plan.ops {
            execute_op_at_bin(op, &mut state, t, bins);
        }
    }

    state
}

/// Read a single value from the matrix.
#[inline]
pub fn get(state: &[f64], col: usize, t: usize, bins: usize) -> f64 {
    state[col * bins + t]
}

/// Write a single value to the matrix.
#[inline]
fn set(state: &mut [f64], col: usize, t: usize, bins: usize, val: f64) {
    state[col * bins + t] = val;
}

/// Treat non-finite values (NaN, Inf) as 0, matching C# safe() pattern.
#[inline]
fn safe_val(v: f64) -> f64 {
    if v.is_finite() { v } else { 0.0 }
}

/// Execute a single op at a single bin.
/// For bin-major evaluation: called once per (op, bin) pair.
fn execute_op_at_bin(op: &Op, state: &mut [f64], t: usize, bins: usize) {
    match op {
        // Const is pre-written before the bin loop; skip here.
        Op::Const { .. } => {}

        Op::VecAdd { out, a, b } => {
            set(state, *out, t, bins, get(state, *a, t, bins) + get(state, *b, t, bins));
        }
        Op::VecSub { out, a, b } => {
            set(state, *out, t, bins, get(state, *a, t, bins) - get(state, *b, t, bins));
        }
        Op::VecMul { out, a, b } => {
            set(state, *out, t, bins, get(state, *a, t, bins) * get(state, *b, t, bins));
        }
        Op::VecDiv { out, a, b } => {
            let denom = get(state, *b, t, bins);
            let val = if denom != 0.0 { get(state, *a, t, bins) / denom } else { 0.0 };
            set(state, *out, t, bins, val);
        }
        Op::VecMin { out, a, b } => {
            set(state, *out, t, bins, get(state, *a, t, bins).min(get(state, *b, t, bins)));
        }
        Op::VecMax { out, a, b } => {
            set(state, *out, t, bins, get(state, *a, t, bins).max(get(state, *b, t, bins)));
        }

        Op::Clamp { out, val, lo, hi } => {
            let v = get(state, *val, t, bins);
            let l = get(state, *lo, t, bins);
            let h = get(state, *hi, t, bins);
            set(state, *out, t, bins, v.max(l).min(h));
        }

        Op::Mod { out, a, b } => {
            let denom = get(state, *b, t, bins);
            let val = if denom.abs() <= f64::EPSILON { 0.0 } else { get(state, *a, t, bins).rem_euclid(denom) };
            set(state, *out, t, bins, val);
        }

        Op::ScalarAdd { out, input, k } => {
            set(state, *out, t, bins, get(state, *input, t, bins) + k);
        }
        Op::ScalarMul { out, input, k } => {
            set(state, *out, t, bins, get(state, *input, t, bins) * k);
        }

        Op::Floor { out, input } => {
            set(state, *out, t, bins, get(state, *input, t, bins).floor());
        }
        Op::Ceil { out, input } => {
            set(state, *out, t, bins, get(state, *input, t, bins).ceil());
        }
        Op::Round { out, input } => {
            let v = get(state, *input, t, bins);
            set(state, *out, t, bins, if v >= 0.0 { (v + 0.5).floor() } else { (v - 0.5).ceil() });
        }

        Op::Step { out, input, threshold } => {
            let val = if get(state, *input, t, bins) >= get(state, *threshold, t, bins) { 1.0 } else { 0.0 };
            set(state, *out, t, bins, val);
        }

        Op::Pulse { out, period, phase, amplitude } => {
            let delta = t as isize - *phase as isize;
            let val = if delta >= 0 && (delta as usize) % *period == 0 {
                amplitude.map_or(1.0, |a| get(state, a, t, bins))
            } else {
                0.0
            };
            set(state, *out, t, bins, val);
        }

        Op::Shift { out, input, lag } => {
            let val = if t >= *lag { get(state, *input, t - lag, bins) } else { 0.0 };
            set(state, *out, t, bins, val);
        }

        Op::Convolve { out, input, kernel } => {
            let mut sum = 0.0;
            for (k, &w) in kernel.iter().enumerate() {
                if t >= k {
                    let v = get(state, *input, t - k, bins);
                    let v = if v.is_finite() { v } else { 0.0 };
                    sum += v * w;
                }
            }
            set(state, *out, t, bins, sum);
        }

        Op::QueueRecurrence { out, inflow, outflow, loss, init, wip_limit, overflow_out } => {
            let prev_q = if t == 0 { *init } else { get(state, *out, t - 1, bins) };
            let inf = safe_val(get(state, *inflow, t, bins));
            let outf = safe_val(get(state, *outflow, t, bins));
            let l = loss.map_or(0.0, |c| safe_val(get(state, c, t, bins)));
            let mut q = (prev_q + inf - outf - l).max(0.0);
            if let Some(wip_col) = wip_limit {
                let limit = get(state, *wip_col, t, bins);
                if limit.is_finite() && q > limit {
                    if let Some(ov_col) = overflow_out {
                        set(state, *ov_col, t, bins, q - limit);
                    }
                    q = limit;
                }
            }
            set(state, *out, t, bins, q);
        }

        Op::DispatchGate { out, input, period, phase, capacity } => {
            let norm_phase = *phase % *period;
            let is_dispatch = t >= norm_phase && (t - norm_phase) % *period == 0;
            let val = if is_dispatch {
                let v = get(state, *input, t, bins);
                if let Some(cap_col) = capacity {
                    let cap = get(state, *cap_col, t, bins);
                    if cap.is_finite() { v.min(cap) } else { v }
                } else {
                    v
                }
            } else {
                0.0
            };
            set(state, *out, t, bins, val);
        }

        Op::Copy { out, input } => {
            set(state, *out, t, bins, get(state, *input, t, bins));
        }
    }
}

/// Extract a single column from the matrix as a Vec.
pub fn extract_column(state: &[f64], col: usize, bins: usize) -> Vec<f64> {
    (0..bins).map(|t| get(state, col, t, bins)).collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::plan::{ColumnMap, Plan};

    #[test]
    fn eval_const_and_scalar_mul() {
        let mut cm = ColumnMap::new();
        let demand = cm.insert("demand");
        let served = cm.insert("served");

        let plan = Plan {
            ops: vec![
                Op::Const { out: demand, values: vec![10.0, 10.0, 10.0, 10.0] },
                Op::ScalarMul { out: served, input: demand, k: 0.8 },
            ],
            column_map: cm,
            bins: 4,
        };

        let state = evaluate(&plan);
        assert_eq!(extract_column(&state, demand, 4), vec![10.0, 10.0, 10.0, 10.0]);
        assert_eq!(extract_column(&state, served, 4), vec![8.0, 8.0, 8.0, 8.0]);
    }

    #[test]
    fn eval_vec_add_sub_mul_div() {
        let mut cm = ColumnMap::new();
        let a = cm.insert("a");
        let b = cm.insert("b");
        let add = cm.insert("add");
        let sub = cm.insert("sub");
        let mul = cm.insert("mul");
        let div = cm.insert("div");

        let plan = Plan {
            ops: vec![
                Op::Const { out: a, values: vec![10.0, 20.0, 30.0] },
                Op::Const { out: b, values: vec![3.0, 5.0, 0.0] },
                Op::VecAdd { out: add, a, b },
                Op::VecSub { out: sub, a, b },
                Op::VecMul { out: mul, a, b },
                Op::VecDiv { out: div, a, b },
            ],
            column_map: cm,
            bins: 3,
        };

        let state = evaluate(&plan);
        assert_eq!(extract_column(&state, add, 3), vec![13.0, 25.0, 30.0]);
        assert_eq!(extract_column(&state, sub, 3), vec![7.0, 15.0, 30.0]);
        assert_eq!(extract_column(&state, mul, 3), vec![30.0, 100.0, 0.0]);
        assert_eq!(extract_column(&state, div, 3), vec![10.0 / 3.0, 4.0, 0.0]); // div by zero → 0
    }

    #[test]
    fn eval_min_max_clamp() {
        let mut cm = ColumnMap::new();
        let a = cm.insert("a");
        let b = cm.insert("b");
        let lo = cm.insert("lo");
        let hi = cm.insert("hi");
        let mn = cm.insert("min");
        let mx = cm.insert("max");
        let cl = cm.insert("clamp");

        let plan = Plan {
            ops: vec![
                Op::Const { out: a, values: vec![1.0, 5.0, 9.0] },
                Op::Const { out: b, values: vec![3.0, 3.0, 3.0] },
                Op::Const { out: lo, values: vec![2.0, 2.0, 2.0] },
                Op::Const { out: hi, values: vec![7.0, 7.0, 7.0] },
                Op::VecMin { out: mn, a, b },
                Op::VecMax { out: mx, a, b },
                Op::Clamp { out: cl, val: a, lo, hi },
            ],
            column_map: cm,
            bins: 3,
        };

        let state = evaluate(&plan);
        assert_eq!(extract_column(&state, mn, 3), vec![1.0, 3.0, 3.0]);
        assert_eq!(extract_column(&state, mx, 3), vec![3.0, 5.0, 9.0]);
        assert_eq!(extract_column(&state, cl, 3), vec![2.0, 5.0, 7.0]);
    }

    #[test]
    fn eval_shift_lag2() {
        let mut cm = ColumnMap::new();
        let input = cm.insert("input");
        let shifted = cm.insert("shifted");

        let plan = Plan {
            ops: vec![
                Op::Const { out: input, values: vec![10.0, 20.0, 30.0, 40.0, 50.0] },
                Op::Shift { out: shifted, input, lag: 2 },
            ],
            column_map: cm,
            bins: 5,
        };

        let state = evaluate(&plan);
        // t<lag → 0, otherwise input[t-lag]
        assert_eq!(extract_column(&state, shifted, 5), vec![0.0, 0.0, 10.0, 20.0, 30.0]);
    }

    #[test]
    fn eval_shift_lag0_is_copy() {
        let mut cm = ColumnMap::new();
        let input = cm.insert("input");
        let shifted = cm.insert("shifted");

        let plan = Plan {
            ops: vec![
                Op::Const { out: input, values: vec![1.0, 2.0, 3.0] },
                Op::Shift { out: shifted, input, lag: 0 },
            ],
            column_map: cm,
            bins: 3,
        };

        let state = evaluate(&plan);
        assert_eq!(extract_column(&state, shifted, 3), vec![1.0, 2.0, 3.0]);
    }

    #[test]
    fn eval_convolve_simple_kernel() {
        let mut cm = ColumnMap::new();
        let input = cm.insert("input");
        let output = cm.insert("output");

        // kernel [0.0, 0.6, 0.3, 0.1] — default retry kernel
        let plan = Plan {
            ops: vec![
                Op::Const { out: input, values: vec![100.0, 0.0, 0.0, 0.0, 0.0] },
                Op::Convolve { out: output, input, kernel: vec![0.0, 0.6, 0.3, 0.1] },
            ],
            column_map: cm,
            bins: 5,
        };

        let state = evaluate(&plan);
        // t=0: 100*0.0 = 0
        // t=1: 0*0.0 + 100*0.6 = 60
        // t=2: 0*0.0 + 0*0.6 + 100*0.3 = 30
        // t=3: 0*0.0 + 0*0.6 + 0*0.3 + 100*0.1 = 10
        // t=4: 0
        assert_eq!(extract_column(&state, output, 5), vec![0.0, 60.0, 30.0, 10.0, 0.0]);
    }

    #[test]
    fn eval_queue_recurrence_basic() {
        let mut cm = ColumnMap::new();
        let inflow = cm.insert("inflow");
        let outflow = cm.insert("outflow");
        let queue = cm.insert("queue");

        let plan = Plan {
            ops: vec![
                Op::Const { out: inflow, values: vec![10.0, 10.0, 10.0, 10.0] },
                Op::Const { out: outflow, values: vec![3.0, 3.0, 3.0, 3.0] },
                Op::QueueRecurrence {
                    out: queue, inflow, outflow,
                    loss: None, init: 0.0,
                    wip_limit: None, overflow_out: None,
                },
            ],
            column_map: cm,
            bins: 4,
        };

        let state = evaluate(&plan);
        // Q[0]=max(0, 0+10-3)=7, Q[1]=14, Q[2]=21, Q[3]=28
        assert_eq!(extract_column(&state, queue, 4), vec![7.0, 14.0, 21.0, 28.0]);
    }

    #[test]
    fn eval_queue_recurrence_with_wip_limit() {
        let mut cm = ColumnMap::new();
        let inflow = cm.insert("inflow");
        let outflow = cm.insert("outflow");
        let wip = cm.insert("wip_limit");
        let overflow = cm.insert("overflow");
        let queue = cm.insert("queue");

        let plan = Plan {
            ops: vec![
                Op::Const { out: inflow, values: vec![10.0, 10.0, 10.0, 10.0] },
                Op::Const { out: outflow, values: vec![2.0, 2.0, 2.0, 2.0] },
                Op::Const { out: wip, values: vec![20.0; 4] },
                Op::QueueRecurrence {
                    out: queue, inflow, outflow,
                    loss: None, init: 0.0,
                    wip_limit: Some(wip), overflow_out: Some(overflow),
                },
            ],
            column_map: cm,
            bins: 4,
        };

        let state = evaluate(&plan);
        // Q[0]=8, Q[1]=16, Q[2]=24→clamped to 20 (overflow=4), Q[3]=20+8=28→20 (overflow=8)
        assert_eq!(extract_column(&state, queue, 4), vec![8.0, 16.0, 20.0, 20.0]);
        assert_eq!(extract_column(&state, overflow, 4), vec![0.0, 0.0, 4.0, 8.0]);
    }

    #[test]
    fn eval_dispatch_gate_period3() {
        let mut cm = ColumnMap::new();
        let input = cm.insert("input");
        let gated = cm.insert("gated");

        let plan = Plan {
            ops: vec![
                Op::Const { out: input, values: vec![10.0; 6] },
                Op::DispatchGate { out: gated, input, period: 3, phase: 0, capacity: None },
            ],
            column_map: cm,
            bins: 6,
        };

        let state = evaluate(&plan);
        // Dispatch at t=0,3 (every 3 bins, phase 0)
        assert_eq!(extract_column(&state, gated, 6), vec![10.0, 0.0, 0.0, 10.0, 0.0, 0.0]);
    }

    #[test]
    fn eval_dispatch_gate_with_phase_and_capacity() {
        let mut cm = ColumnMap::new();
        let input = cm.insert("input");
        let cap = cm.insert("capacity");
        let gated = cm.insert("gated");

        let plan = Plan {
            ops: vec![
                Op::Const { out: input, values: vec![100.0; 6] },
                Op::Const { out: cap, values: vec![50.0; 6] },
                Op::DispatchGate { out: gated, input, period: 2, phase: 1, capacity: Some(cap) },
            ],
            column_map: cm,
            bins: 6,
        };

        let state = evaluate(&plan);
        // period=2, phase=1 → dispatch at t=1,3,5; capped at 50
        assert_eq!(extract_column(&state, gated, 6), vec![0.0, 50.0, 0.0, 50.0, 0.0, 50.0]);
    }
}
