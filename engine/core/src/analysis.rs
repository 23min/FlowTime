//! Invariant analysis: post-evaluation checks producing warnings.
//!
//! Reads from the evaluated matrix (read-only) and produces a list of warnings.

use crate::compiler::EvalResult;
use crate::model::{ModelDefinition, TopologyNodeDefinition};

const TOLERANCE: f64 = 1e-6;
const STATIONARITY_THRESHOLD: f64 = 0.25;

/// A warning produced by invariant analysis.
#[derive(Debug, Clone)]
pub struct Warning {
    pub node_id: String,
    pub code: String,
    pub message: String,
    pub bins: Vec<usize>,
    pub severity: String,
}

/// Run invariant analysis on an evaluation result.
pub fn analyze(model: &ModelDefinition, result: &EvalResult) -> Vec<Warning> {
    let mut warnings = Vec::new();
    let topo = match &model.topology {
        Some(t) => t,
        None => return warnings,
    };

    for tnode in &topo.nodes {
        let kind = tnode.kind.as_deref().unwrap_or("serviceWithBuffer").to_lowercase();
        if !matches!(kind.as_str(), "servicewithbuffer" | "queue" | "dlq" | "service") {
            continue;
        }

        check_non_negativity(tnode, result, &mut warnings);
        check_conservation(tnode, result, &mut warnings);
        check_queue_balance(tnode, result, &mut warnings);
        check_stationarity(tnode, result, &mut warnings);
    }

    warnings
}

/// Check non-negativity of key series.
fn check_non_negativity(tnode: &TopologyNodeDefinition, result: &EvalResult, warnings: &mut Vec<Warning>) {
    let checks: Vec<(&str, &str)> = vec![
        (&tnode.semantics.arrivals, "arrivals_negative"),
        (&tnode.semantics.served, "served_negative"),
    ];

    for (series_ref, code) in &checks {
        if series_ref.is_empty() { continue; }
        if let Some(values) = result.series(series_ref) {
            let bad_bins: Vec<usize> = values.iter().enumerate()
                .filter(|(_, v)| **v < -TOLERANCE)
                .map(|(i, _)| i)
                .collect();
            if !bad_bins.is_empty() {
                warnings.push(Warning {
                    node_id: tnode.id.clone(),
                    code: code.to_string(),
                    message: format!("{} has negative values in {} bins", series_ref, bad_bins.len()),
                    bins: bad_bins,
                    severity: "warning".into(),
                });
            }
        }
    }

    // Queue depth non-negativity
    if let Some(qd_ref) = &tnode.semantics.queue_depth {
        if !qd_ref.is_empty() {
            if let Some(values) = result.series(qd_ref) {
                let bad_bins: Vec<usize> = values.iter().enumerate()
                    .filter(|(_, v)| **v < -TOLERANCE)
                    .map(|(i, _)| i)
                    .collect();
                if !bad_bins.is_empty() {
                    warnings.push(Warning {
                        node_id: tnode.id.clone(),
                        code: "queue_negative".into(),
                        message: format!("{} has negative values in {} bins", qd_ref, bad_bins.len()),
                        bins: bad_bins,
                        severity: "warning".into(),
                    });
                }
            }
        }
    }

    // Errors non-negativity
    if let Some(err_ref) = &tnode.semantics.errors {
        if !err_ref.is_empty() {
            if let Some(values) = result.series(err_ref) {
                let bad_bins: Vec<usize> = values.iter().enumerate()
                    .filter(|(_, v)| **v < -TOLERANCE)
                    .map(|(i, _)| i)
                    .collect();
                if !bad_bins.is_empty() {
                    warnings.push(Warning {
                        node_id: tnode.id.clone(),
                        code: "errors_negative".into(),
                        message: format!("{} has negative values in {} bins", err_ref, bad_bins.len()),
                        bins: bad_bins,
                        severity: "warning".into(),
                    });
                }
            }
        }
    }
}

/// Check conservation: served ≤ arrivals, served ≤ capacity.
fn check_conservation(tnode: &TopologyNodeDefinition, result: &EvalResult, warnings: &mut Vec<Warning>) {
    let arrivals = result.series(&tnode.semantics.arrivals);
    let served = result.series(&tnode.semantics.served);

    if let (Some(arr), Some(srv)) = (&arrivals, &served) {
        let kind = tnode.kind.as_deref().unwrap_or("serviceWithBuffer").to_lowercase();
        // served ≤ arrivals check (not for queue/dlq nodes where queue drains exceed current arrivals)
        if !matches!(kind.as_str(), "servicewithbuffer" | "queue" | "dlq") {
            let bad_bins: Vec<usize> = arr.iter().zip(srv.iter()).enumerate()
                .filter(|(_, (a, s))| **s > **a + TOLERANCE)
                .map(|(i, _)| i)
                .collect();
            if !bad_bins.is_empty() {
                warnings.push(Warning {
                    node_id: tnode.id.clone(),
                    code: "served_exceeds_arrivals".into(),
                    message: format!("served > arrivals in {} bins", bad_bins.len()),
                    bins: bad_bins,
                    severity: "warning".into(),
                });
            }
        }
    }

    // served ≤ capacity
    if let Some(cap_ref) = &tnode.semantics.capacity {
        if !cap_ref.is_empty() {
            if let (Some(srv), Some(cap)) = (&served, result.series(cap_ref)) {
                let bad_bins: Vec<usize> = srv.iter().zip(cap.iter()).enumerate()
                    .filter(|(_, (s, c))| **s > **c + TOLERANCE)
                    .map(|(i, _)| i)
                    .collect();
                if !bad_bins.is_empty() {
                    warnings.push(Warning {
                        node_id: tnode.id.clone(),
                        code: "served_exceeds_capacity".into(),
                        message: format!("served > capacity in {} bins", bad_bins.len()),
                        bins: bad_bins,
                        severity: "warning".into(),
                    });
                }
            }
        }
    }
}

/// Check queue balance: computed Q vs actual Q.
fn check_queue_balance(tnode: &TopologyNodeDefinition, result: &EvalResult, warnings: &mut Vec<Warning>) {
    let kind = tnode.kind.as_deref().unwrap_or("serviceWithBuffer").to_lowercase();
    if !matches!(kind.as_str(), "servicewithbuffer" | "queue" | "dlq") { return; }

    let default_q_name = crate::compiler::queue_column_id_pub(&tnode.id);
    let q_col_name = tnode.semantics.queue_depth.as_deref()
        .filter(|s| !s.is_empty())
        .unwrap_or(&default_q_name);

    let queue = match result.series(q_col_name) {
        Some(q) => q,
        None => return,
    };
    let arrivals = match result.series(&tnode.semantics.arrivals) {
        Some(a) => a,
        None => return,
    };
    let served = match result.series(&tnode.semantics.served) {
        Some(s) => s,
        None => return,
    };
    let errors = tnode.semantics.errors.as_deref()
        .and_then(|r| if r.is_empty() { None } else { result.series(r) });

    let init = tnode.initial_condition.as_ref().map_or(0.0, |ic| ic.queue_depth);
    let mut computed_q = init;
    let mut bad_bins = Vec::new();

    for t in 0..queue.len() {
        let inf = arrivals.get(t).copied().unwrap_or(0.0);
        let outf = served.get(t).copied().unwrap_or(0.0);
        let loss = errors.as_ref().and_then(|e| e.get(t)).copied().unwrap_or(0.0);
        computed_q = (computed_q + inf - outf - loss).max(0.0);

        // Apply WIP limit if present
        if let Some(wl) = tnode.wip_limit {
            computed_q = computed_q.min(wl);
        }

        if (computed_q - queue[t]).abs() > TOLERANCE {
            bad_bins.push(t);
        }
    }

    if !bad_bins.is_empty() {
        warnings.push(Warning {
            node_id: tnode.id.clone(),
            code: "queue_depth_mismatch".into(),
            message: format!("computed queue depth diverges from actual in {} bins", bad_bins.len()),
            bins: bad_bins,
            severity: "warning".into(),
        });
    }
}

/// Check stationarity: first-half vs second-half mean divergence.
fn check_stationarity(tnode: &TopologyNodeDefinition, result: &EvalResult, warnings: &mut Vec<Warning>) {
    let kind = tnode.kind.as_deref().unwrap_or("serviceWithBuffer").to_lowercase();
    if !matches!(kind.as_str(), "servicewithbuffer" | "queue" | "dlq") { return; }

    let arrivals = match result.series(&tnode.semantics.arrivals) {
        Some(a) => a,
        None => return,
    };

    if arrivals.len() < 4 { return; } // Need enough bins to split

    let mid = arrivals.len() / 2;
    let first_half: f64 = arrivals[..mid].iter().sum::<f64>() / mid as f64;
    let second_half: f64 = arrivals[mid..].iter().sum::<f64>() / (arrivals.len() - mid) as f64;

    let max_half = first_half.max(second_half);
    if max_half <= 0.0 { return; }

    let divergence = (first_half - second_half).abs() / max_half;
    if divergence > STATIONARITY_THRESHOLD {
        warnings.push(Warning {
            node_id: tnode.id.clone(),
            code: "non_stationary".into(),
            message: format!("arrivals non-stationary: divergence {:.1}% (threshold {}%)",
                divergence * 100.0, STATIONARITY_THRESHOLD * 100.0),
            bins: vec![],
            severity: "warning".into(),
        });
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::compiler::eval_model;
    use crate::model::parse_model_yaml;

    #[test]
    fn analysis_no_warnings_for_balanced_model() {
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let warnings = analyze(&model, &result);
        assert!(warnings.is_empty(), "Expected no warnings, got: {:?}", warnings);
    }

    #[test]
    fn analysis_stationarity_warning() {
        let yaml = r#"
grid:
  bins: 8
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10, 50, 50, 50, 50]
  - id: served
    kind: const
    values: [5, 5, 5, 5, 5, 5, 5, 5]
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let warnings = analyze(&model, &result);
        let stationarity: Vec<_> = warnings.iter().filter(|w| w.code == "non_stationary").collect();
        assert_eq!(stationarity.len(), 1, "Expected 1 stationarity warning");
    }

    #[test]
    fn analysis_conservation_served_exceeds_capacity() {
        // served=15 > capacity=10 → warning
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [20, 20, 20]
  - id: served
    kind: const
    values: [15, 15, 15]
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let warnings = analyze(&model, &result);
        let cap_warnings: Vec<_> = warnings.iter().filter(|w| w.code == "served_exceeds_capacity").collect();
        assert_eq!(cap_warnings.len(), 1);
        assert_eq!(cap_warnings[0].bins.len(), 3);
    }

    #[test]
    fn analysis_stationarity_flat_series_no_warning() {
        let yaml = r#"
grid:
  bins: 8
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10, 10, 10, 10, 10]
  - id: served
    kind: const
    values: [5, 5, 5, 5, 5, 5, 5, 5]
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let warnings = analyze(&model, &result);
        let stationarity: Vec<_> = warnings.iter().filter(|w| w.code == "non_stationary").collect();
        assert!(stationarity.is_empty(), "Flat series should not trigger stationarity warning");
    }

    #[test]
    fn analysis_stationarity_too_few_bins() {
        // 3 bins: < 4 minimum → skip stationarity check
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 50, 50]
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let warnings = analyze(&model, &result);
        let stationarity: Vec<_> = warnings.iter().filter(|w| w.code == "non_stationary").collect();
        assert!(stationarity.is_empty(), "Too few bins should skip stationarity check");
    }

    #[test]
    fn analysis_stationarity_zero_arrivals() {
        let yaml = r#"
grid:
  bins: 8
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [0, 0, 0, 0, 0, 0, 0, 0]
  - id: served
    kind: const
    values: [0, 0, 0, 0, 0, 0, 0, 0]
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let warnings = analyze(&model, &result);
        let stationarity: Vec<_> = warnings.iter().filter(|w| w.code == "non_stationary").collect();
        assert!(stationarity.is_empty(), "Zero arrivals should not trigger stationarity");
    }

    #[test]
    fn analysis_no_topology_no_warnings() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: a
    kind: const
    values: [1, 2, 3, 4]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let warnings = analyze(&model, &result);
        assert!(warnings.is_empty());
    }

    #[test]
    fn analysis_conservation_within_tolerance() {
        // served = arrivals + 1e-7 → within tolerance, no warning
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
    values: [10, 10, 10]
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let warnings = analyze(&model, &result);
        let cap_warnings: Vec<_> = warnings.iter().filter(|w| w.code == "served_exceeds_capacity").collect();
        assert!(cap_warnings.is_empty(), "served = capacity should not trigger warning");
    }
}
