//! Integration tests: verify all reference YAML fixtures deserialize without error.

use flowtime_core::model::parse_model_yaml;
use std::fs;
use std::path::PathBuf;

fn fixtures_dir() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .parent()
        .unwrap()
        .join("fixtures")
}

fn load_fixture(name: &str) -> String {
    let path = fixtures_dir().join(name);
    fs::read_to_string(&path).unwrap_or_else(|e| panic!("Failed to read fixture {name}: {e}"))
}

#[test]
fn parse_simple_const() {
    let model = parse_model_yaml(&load_fixture("simple-const.yaml")).unwrap();
    assert!(model.grid.is_some());
    assert!(!model.nodes.is_empty());
}

#[test]
fn parse_hello() {
    let model = parse_model_yaml(&load_fixture("hello.yaml")).unwrap();
    assert!(model.grid.is_some());
}

#[test]
fn parse_pmf() {
    let model = parse_model_yaml(&load_fixture("pmf.yaml")).unwrap();
    let pmf_nodes: Vec<_> = model.nodes.iter().filter(|n| n.pmf.is_some()).collect();
    assert!(!pmf_nodes.is_empty(), "Should have at least one PMF node");
}

#[test]
fn parse_complex_pmf() {
    parse_model_yaml(&load_fixture("complex-pmf.yaml")).unwrap();
}

#[test]
fn parse_class_enabled() {
    let model = parse_model_yaml(&load_fixture("class-enabled.yaml")).unwrap();
    assert!(!model.classes.is_empty(), "Should have class definitions");
}

#[test]
fn parse_http_service() {
    let model = parse_model_yaml(&load_fixture("http-service.yaml")).unwrap();
    assert!(model.topology.is_some(), "Should have topology");
}

#[test]
fn parse_microservices() {
    let model = parse_model_yaml(&load_fixture("microservices.yaml")).unwrap();
    let topo = model.topology.as_ref().unwrap();
    assert!(topo.nodes.len() > 1, "Should have multiple topology nodes");
}

#[test]
fn parse_order_system() {
    let model = parse_model_yaml(&load_fixture("order-system.yaml")).unwrap();
    assert!(model.topology.is_some());
}

#[test]
fn parse_retry_service_time() {
    let model = parse_model_yaml(&load_fixture("retry-service-time.yaml")).unwrap();
    // This fixture uses retry kernel and service time semantics
    let topo = model.topology.as_ref().unwrap();
    let has_retry = topo.nodes.iter().any(|n| n.semantics.retry_kernel.is_some());
    assert!(has_retry, "Should have at least one node with retry kernel");
}

#[test]
fn all_fixtures_parse() {
    let dir = fixtures_dir();
    let mut count = 0;
    for entry in fs::read_dir(&dir).unwrap() {
        let entry = entry.unwrap();
        let path = entry.path();
        if path.extension().map_or(false, |e| e == "yaml") {
            let yaml = fs::read_to_string(&path).unwrap();
            let result = parse_model_yaml(&yaml);
            assert!(
                result.is_ok(),
                "Failed to parse {}: {}",
                path.display(),
                result.unwrap_err()
            );
            count += 1;
        }
    }
    assert!(count >= 15, "Expected at least 15 fixtures, found {count}");
}

// --- Topology fixture evaluation tests (m-E20-03 parity) ---

use flowtime_core::compiler::eval_model;

#[test]
fn eval_topology_simple_queue() {
    let model = parse_model_yaml(&load_fixture("topology-simple-queue.yaml")).unwrap();
    let result = eval_model(&model).unwrap();
    assert_eq!(result.series("queue_queue").unwrap(), vec![7.0, 14.0, 21.0, 28.0]);
}

#[test]
fn eval_topology_wip_limit() {
    let model = parse_model_yaml(&load_fixture("topology-wip-limit.yaml")).unwrap();
    let result = eval_model(&model).unwrap();
    assert_eq!(result.series("queue_queue").unwrap(), vec![8.0, 16.0, 20.0, 20.0]);
    assert_eq!(result.series("queue_overflow").unwrap(), vec![0.0, 0.0, 4.0, 8.0]);
}

#[test]
fn eval_topology_dispatch() {
    let model = parse_model_yaml(&load_fixture("topology-dispatch.yaml")).unwrap();
    let result = eval_model(&model).unwrap();
    assert_eq!(result.series("queue_queue").unwrap(), vec![5.0, 15.0, 25.0, 30.0, 40.0, 50.0]);
}

#[test]
fn eval_topology_retry_echo() {
    let model = parse_model_yaml(&load_fixture("topology-retry-echo.yaml")).unwrap();
    let result = eval_model(&model).unwrap();
    assert_eq!(result.series("retry_echo").unwrap(), vec![0.0, 6.0, 9.0, 10.0, 10.0]);
}

#[test]
fn eval_topology_backpressure() {
    let model = parse_model_yaml(&load_fixture("topology-backpressure.yaml")).unwrap();
    let result = eval_model(&model).unwrap();
    let q = result.series("queue_depth").unwrap();
    // Approximate comparison due to floating-point chained arithmetic
    let expected = [80.0, 60.0, 40.0, 40.0, 40.0, 40.0];
    for (i, (a, e)) in q.iter().zip(expected.iter()).enumerate() {
        assert!((a - e).abs() < 1e-10, "bin {i}: actual={a}, expected={e}");
    }
}

#[test]
fn eval_topology_cascading_overflow() {
    let model = parse_model_yaml(&load_fixture("topology-cascading-overflow.yaml")).unwrap();
    let result = eval_model(&model).unwrap();
    assert_eq!(result.series("a_queue").unwrap(), vec![10.0, 10.0, 10.0]);
    assert_eq!(result.series("a_overflow").unwrap(), vec![5.0, 15.0, 15.0]);
    assert_eq!(result.series("b_queue").unwrap(), vec![5.0, 5.0, 5.0]);
    assert_eq!(result.series("b_overflow").unwrap(), vec![0.0, 15.0, 15.0]);
    assert_eq!(result.series("c_queue").unwrap(), vec![0.0, 15.0, 30.0]);
}
