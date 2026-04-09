//! FlowTime model types for YAML deserialization.
//!
//! These mirror the C# ModelDefinition types in `src/FlowTime.Core/Models/ModelParser.cs`.
//! All fields use camelCase to match the existing YAML schema.

use serde::Deserialize;
use std::collections::HashMap;

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct ModelDefinition {
    pub schema_version: i32,
    pub grid: Option<GridDefinition>,
    pub classes: Vec<ClassDefinition>,
    pub traffic: Option<TrafficDefinition>,
    pub nodes: Vec<NodeDefinition>,
    pub outputs: Vec<OutputDefinition>,
    pub topology: Option<TopologyDefinition>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct GridDefinition {
    pub bins: i32,
    pub bin_size: i32,
    pub bin_unit: String,
    pub start_time_utc: Option<String>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct ClassDefinition {
    pub id: String,
    pub display_name: Option<String>,
    pub description: Option<String>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct TrafficDefinition {
    pub arrivals: Vec<ArrivalDefinition>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct ArrivalDefinition {
    pub node_id: String,
    pub class_id: String,
    pub pattern: ArrivalPatternDefinition,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct ArrivalPatternDefinition {
    pub kind: String,
    pub rate_per_bin: Option<f64>,
    pub rate: Option<f64>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct NodeDefinition {
    pub id: String,
    pub kind: String,
    pub values: Option<Vec<f64>>,
    pub expr: Option<String>,
    pub pmf: Option<PmfDefinition>,
    pub metadata: Option<HashMap<String, String>>,
    // serviceWithBuffer fields
    pub inflow: Option<String>,
    pub outflow: Option<String>,
    pub loss: Option<String>,
    pub dispatch_schedule: Option<DispatchScheduleDefinition>,
    pub wip_limit: Option<f64>,
    pub wip_limit_series: Option<String>,
    pub wip_overflow: Option<String>,
    // router fields
    pub router: Option<RouterDefinition>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct RouterDefinition {
    pub inputs: RouterInputsDefinition,
    pub routes: Vec<RouterRouteDefinition>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct RouterInputsDefinition {
    pub queue: Option<String>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct RouterRouteDefinition {
    pub target: String,
    pub classes: Option<Vec<String>>,
    pub weight: Option<f64>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct DispatchScheduleDefinition {
    pub kind: String,
    pub period_bins: i32,
    pub phase_offset: Option<i32>,
    pub capacity_series: Option<String>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct PmfDefinition {
    pub values: Vec<f64>,
    pub probabilities: Vec<f64>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct OutputDefinition {
    pub series: String,
    #[serde(rename = "as")]
    pub as_name: String,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct TopologyDefinition {
    pub nodes: Vec<TopologyNodeDefinition>,
    pub edges: Vec<TopologyEdgeDefinition>,
    pub constraints: Vec<ConstraintDefinition>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct TopologyNodeDefinition {
    pub id: String,
    pub kind: Option<String>,
    pub node_role: Option<String>,
    pub group: Option<String>,
    pub ui: Option<UiHintsDefinition>,
    pub constraints: Option<Vec<String>>,
    pub semantics: TopologyNodeSemanticsDefinition,
    pub initial_condition: Option<InitialConditionDefinition>,
    pub dispatch_schedule: Option<DispatchScheduleDefinition>,
    pub wip_limit: Option<f64>,
    pub wip_limit_series: Option<String>,
    pub wip_overflow: Option<String>,
}

/// Parallelism can be a scalar number or a series reference string in YAML.
/// We deserialize it as a raw YAML value and resolve during compilation.
#[derive(Debug, Deserialize)]
#[serde(untagged)]
pub enum ParallelismValue {
    Scalar(f64),
    Reference(String),
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct TopologyNodeSemanticsDefinition {
    pub arrivals: String,
    pub served: String,
    pub errors: Option<String>,
    pub attempts: Option<String>,
    pub failures: Option<String>,
    pub exhausted_failures: Option<String>,
    pub retry_echo: Option<String>,
    pub retry_budget_remaining: Option<String>,
    pub retry_kernel: Option<Vec<f64>>,
    pub external_demand: Option<String>,
    pub queue_depth: Option<String>,
    pub capacity: Option<String>,
    pub parallelism: Option<ParallelismValue>,
    pub processing_time_ms_sum: Option<String>,
    pub served_count: Option<String>,
    pub sla_min: Option<f64>,
    pub max_attempts: Option<f64>,
    pub backoff_strategy: Option<String>,
    pub exhausted_policy: Option<String>,
    pub aliases: Option<HashMap<String, String>>,
    pub metadata: Option<HashMap<String, String>>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct ConstraintDefinition {
    pub id: String,
    pub semantics: ConstraintSemanticsDefinition,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct ConstraintSemanticsDefinition {
    pub arrivals: String,
    pub served: String,
    pub errors: Option<String>,
    pub latency_minutes: Option<String>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct TopologyEdgeDefinition {
    pub source: String,
    pub target: String,
    #[serde(default = "default_weight")]
    pub weight: f64,
    pub id: Option<String>,
    #[serde(rename = "type")]
    pub edge_type: Option<String>,
    pub measure: Option<String>,
    pub multiplier: Option<f64>,
    pub lag: Option<i32>,
}

fn default_weight() -> f64 {
    1.0
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct InitialConditionDefinition {
    pub queue_depth: f64,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct UiHintsDefinition {
    pub x: Option<f64>,
    pub y: Option<f64>,
}

/// Parse a YAML string into a ModelDefinition.
pub fn parse_model_yaml(yaml: &str) -> Result<ModelDefinition, String> {
    serde_yaml::from_str(yaml).map_err(|e| format!("YAML parse error: {e}"))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_minimal_model() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 20, 30, 40]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        assert_eq!(model.grid.as_ref().unwrap().bins, 4);
        assert_eq!(model.nodes.len(), 1);
        assert_eq!(model.nodes[0].id, "arrivals");
        assert_eq!(model.nodes[0].values.as_ref().unwrap(), &[10.0, 20.0, 30.0, 40.0]);
    }

    #[test]
    fn parse_model_with_topology() {
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
    expr: capacity
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      wipLimit: 20
      semantics:
        arrivals: arrivals
        served: served
        queueDepth: queue_depth
  edges:
    - source: A
      target: B
      weight: 0.5
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let topo = model.topology.as_ref().unwrap();
        assert_eq!(topo.nodes.len(), 1);
        assert_eq!(topo.nodes[0].id, "Queue");
        assert_eq!(topo.nodes[0].wip_limit, Some(20.0));
        assert_eq!(topo.edges.len(), 1);
        assert_eq!(topo.edges[0].weight, 0.5);
    }
}
