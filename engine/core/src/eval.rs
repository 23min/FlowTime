//! Matrix evaluator: executes a plan against a flat f64 matrix.
//!
//! The matrix is row-major: state[col * bins + t] is column `col` at bin `t`.
//! All bins for one series are contiguous in memory.

use crate::plan::{Op, Plan};

/// Execute a plan and return the filled matrix.
pub fn evaluate(plan: &Plan) -> Vec<f64> {
    let bins = plan.bins;
    let cols = plan.column_map.len();
    let mut state = vec![0.0f64; cols * bins];

    for op in &plan.ops {
        execute_op(op, &mut state, bins);
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

fn execute_op(op: &Op, state: &mut [f64], bins: usize) {
    match op {
        Op::Const { out, values } => {
            let len = values.len().min(bins);
            for t in 0..len {
                set(state, *out, t, bins, values[t]);
            }
        }

        Op::VecAdd { out, a, b } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *a, t, bins) + get(state, *b, t, bins));
            }
        }
        Op::VecSub { out, a, b } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *a, t, bins) - get(state, *b, t, bins));
            }
        }
        Op::VecMul { out, a, b } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *a, t, bins) * get(state, *b, t, bins));
            }
        }
        Op::VecDiv { out, a, b } => {
            for t in 0..bins {
                let denom = get(state, *b, t, bins);
                let val = if denom != 0.0 { get(state, *a, t, bins) / denom } else { 0.0 };
                set(state, *out, t, bins, val);
            }
        }
        Op::VecMin { out, a, b } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *a, t, bins).min(get(state, *b, t, bins)));
            }
        }
        Op::VecMax { out, a, b } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *a, t, bins).max(get(state, *b, t, bins)));
            }
        }

        Op::Clamp { out, val, lo, hi } => {
            for t in 0..bins {
                let v = get(state, *val, t, bins);
                let l = get(state, *lo, t, bins);
                let h = get(state, *hi, t, bins);
                set(state, *out, t, bins, v.max(l).min(h));
            }
        }

        Op::Mod { out, a, b } => {
            for t in 0..bins {
                let denom = get(state, *b, t, bins);
                let val = if denom.abs() <= f64::EPSILON { 0.0 } else { get(state, *a, t, bins).rem_euclid(denom) };
                set(state, *out, t, bins, val);
            }
        }

        Op::ScalarAdd { out, input, k } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *input, t, bins) + k);
            }
        }
        Op::ScalarMul { out, input, k } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *input, t, bins) * k);
            }
        }

        Op::Floor { out, input } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *input, t, bins).floor());
            }
        }
        Op::Ceil { out, input } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *input, t, bins).ceil());
            }
        }
        Op::Round { out, input } => {
            for t in 0..bins {
                let v = get(state, *input, t, bins);
                // Round half away from zero (matches C# Math.Round MidpointRounding.AwayFromZero)
                set(state, *out, t, bins, if v >= 0.0 { (v + 0.5).floor() } else { (v - 0.5).ceil() });
            }
        }

        Op::Step { out, input, threshold } => {
            for t in 0..bins {
                let val = if get(state, *input, t, bins) >= get(state, *threshold, t, bins) { 1.0 } else { 0.0 };
                set(state, *out, t, bins, val);
            }
        }

        Op::Pulse { out, period, phase, amplitude } => {
            for t in 0..bins {
                let delta = t as isize - *phase as isize;
                let val = if delta >= 0 && (delta as usize) % *period == 0 {
                    amplitude.map_or(1.0, |a| get(state, a, t, bins))
                } else {
                    0.0
                };
                set(state, *out, t, bins, val);
            }
        }

        Op::Copy { out, input } => {
            for t in 0..bins {
                set(state, *out, t, bins, get(state, *input, t, bins));
            }
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
}
