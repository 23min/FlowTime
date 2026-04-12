//! Integration tests for the engine session protocol.
//!
//! Spawns `flowtime-engine session` as a subprocess and exercises
//! the full MessagePack protocol over stdin/stdout.

use std::io::{Read, Write};
use std::process::{Command, Stdio};

fn engine_path() -> String {
    // Use release binary if available, fallback to debug
    let release = concat!(env!("CARGO_MANIFEST_DIR"), "/../target/release/flowtime-engine");
    let debug = concat!(env!("CARGO_MANIFEST_DIR"), "/../target/debug/flowtime-engine");
    if std::path::Path::new(release).exists() {
        release.to_string()
    } else {
        debug.to_string()
    }
}

fn send_request(stdin: &mut impl Write, method: &str, params: serde_json::Value) {
    let req = serde_json::json!({ "method": method, "params": params });
    let payload = rmp_serde::to_vec_named(&req).unwrap();
    let len = payload.len() as u32;
    stdin.write_all(&len.to_be_bytes()).unwrap();
    stdin.write_all(&payload).unwrap();
    stdin.flush().unwrap();
}

fn read_response(stdout: &mut impl Read) -> serde_json::Value {
    let mut len_buf = [0u8; 4];
    stdout.read_exact(&mut len_buf).unwrap();
    let len = u32::from_be_bytes(len_buf) as usize;

    let mut buf = vec![0u8; len];
    stdout.read_exact(&mut buf).unwrap();

    rmp_serde::from_slice(&buf).unwrap()
}

const SIMPLE_MODEL: &str = r#"
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

const CLASS_MODEL: &str = r#"
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

#[test]
fn session_compile_and_eval() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .expect("Failed to spawn engine session");

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    // 1. Compile
    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let resp = read_response(&mut stdout);

    assert!(resp.get("result").is_some(), "compile should succeed: {resp:?}");
    let result = &resp["result"];
    assert_eq!(result["bins"], 4);
    assert!(result["params"].as_array().unwrap().len() >= 1, "should have at least 1 param");

    // Check initial series values
    let arrivals: Vec<f64> = serde_json::from_value(result["series"]["arrivals"].clone()).unwrap();
    assert_eq!(arrivals, vec![10.0, 10.0, 10.0, 10.0]);
    let served: Vec<f64> = serde_json::from_value(result["series"]["served"].clone()).unwrap();
    assert_eq!(served, vec![5.0, 5.0, 5.0, 5.0]);

    // 2. Eval with override
    send_request(&mut stdin, "eval", serde_json::json!({
        "overrides": { "arrivals": 20.0 }
    }));
    let resp2 = read_response(&mut stdout);

    assert!(resp2.get("result").is_some(), "eval should succeed: {resp2:?}");
    let served2: Vec<f64> = serde_json::from_value(resp2["result"]["series"]["served"].clone()).unwrap();
    assert_eq!(served2, vec![10.0, 10.0, 10.0, 10.0]); // 20 * 0.5 = 10

    assert!(resp2["result"]["elapsed_us"].as_u64().is_some(), "should report elapsed_us");

    // 3. Eval with different override (independence)
    send_request(&mut stdin, "eval", serde_json::json!({
        "overrides": { "arrivals": 6.0 }
    }));
    let resp3 = read_response(&mut stdout);
    let served3: Vec<f64> = serde_json::from_value(resp3["result"]["series"]["served"].clone()).unwrap();
    assert_eq!(served3, vec![3.0, 3.0, 3.0, 3.0]); // 6 * 0.5 = 3

    // Close stdin → session exits
    drop(stdin);
    let status = child.wait().unwrap();
    assert!(status.success(), "session should exit cleanly on stdin EOF");
}

#[test]
fn session_get_params() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let _ = read_response(&mut stdout);

    send_request(&mut stdin, "get_params", serde_json::json!({}));
    let resp = read_response(&mut stdout);

    let params = resp["result"]["params"].as_array().unwrap();
    assert!(params.iter().any(|p| p["id"] == "arrivals"), "should list arrivals param");

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_get_series_specific_names() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let _ = read_response(&mut stdout);

    send_request(&mut stdin, "get_series", serde_json::json!({ "names": ["served"] }));
    let resp = read_response(&mut stdout);

    let series = resp["result"]["series"].as_object().unwrap();
    assert!(series.contains_key("served"), "should return requested series");
    assert!(!series.contains_key("arrivals"), "should not return unrequested series");

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_error_before_compile() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    // Eval before compile → error
    send_request(&mut stdin, "eval", serde_json::json!({}));
    let resp = read_response(&mut stdout);

    assert!(resp.get("error").is_some(), "should return error: {resp:?}");
    assert_eq!(resp["error"]["code"], "not_compiled");

    // Unknown method → error
    send_request(&mut stdin, "bogus_method", serde_json::json!({}));
    let resp2 = read_response(&mut stdout);
    assert_eq!(resp2["error"]["code"], "unknown_method");

    // Session should still be alive — compile should work
    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let resp3 = read_response(&mut stdout);
    assert!(resp3.get("result").is_some(), "session should recover after errors");

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_eval_performance() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let _ = read_response(&mut stdout);

    // Run 100 evals and check total time
    let start = std::time::Instant::now();
    for i in 0..100 {
        let rate = 5.0 + (i as f64);
        send_request(&mut stdin, "eval", serde_json::json!({
            "overrides": { "arrivals": rate }
        }));
        let resp = read_response(&mut stdout);
        assert!(resp.get("result").is_some());
    }
    let elapsed = start.elapsed();

    // 100 evals should complete in under 5 seconds (generous for CI)
    assert!(elapsed.as_secs() < 5,
        "100 evals took {:?} — too slow", elapsed);

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_class_model_with_overrides() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": CLASS_MODEL }));
    let resp = read_response(&mut stdout);
    assert!(resp.get("result").is_some(), "compile should succeed: {resp:?}");

    // Check params include class rates
    let params = resp["result"]["params"].as_array().unwrap();
    assert!(params.iter().any(|p| p["id"] == "arrivals.Fast"),
        "should have Fast arrival rate param. Params: {params:?}");

    // Override class rate
    send_request(&mut stdin, "eval", serde_json::json!({
        "overrides": { "arrivals.Fast": 90.0 }
    }));
    let resp2 = read_response(&mut stdout);
    assert!(resp2.get("result").is_some());

    // Verify served changed (should still be capped at 80)
    let served: Vec<f64> = serde_json::from_value(resp2["result"]["series"]["served"].clone()).unwrap();
    assert_eq!(served, vec![80.0, 80.0, 80.0]);

    drop(stdin);
    child.wait().unwrap();
}

// ── Edge case tests ──

#[test]
fn session_compile_invalid_yaml() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": "this: [[[invalid yaml" }));
    let resp = read_response(&mut stdout);
    assert!(resp.get("error").is_some(), "invalid YAML should error: {resp:?}");
    assert_eq!(resp["error"]["code"], "compile_error");

    // Session should still be alive
    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let resp2 = read_response(&mut stdout);
    assert!(resp2.get("result").is_some(), "session should recover after compile error");

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_compile_missing_grid() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": "nodes: []" }));
    let resp = read_response(&mut stdout);
    assert!(resp.get("error").is_some(), "missing grid should error: {resp:?}");

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_recompile_replaces_state() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let resp1 = read_response(&mut stdout);
    let served1: Vec<f64> = serde_json::from_value(resp1["result"]["series"]["served"].clone()).unwrap();
    assert_eq!(served1, vec![5.0, 5.0, 5.0, 5.0]);

    let model_b = r#"
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
    expr: "arrivals * 0.5"
"#;
    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": model_b }));
    let resp2 = read_response(&mut stdout);
    let served2: Vec<f64> = serde_json::from_value(resp2["result"]["series"]["served"].clone()).unwrap();
    assert_eq!(served2, vec![50.0, 50.0, 50.0]);

    send_request(&mut stdin, "eval", serde_json::json!({ "overrides": { "arrivals": 200.0 } }));
    let resp3 = read_response(&mut stdout);
    let served3: Vec<f64> = serde_json::from_value(resp3["result"]["series"]["served"].clone()).unwrap();
    assert_eq!(served3, vec![100.0, 100.0, 100.0]);

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_eval_vector_override() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let _ = read_response(&mut stdout);

    send_request(&mut stdin, "eval", serde_json::json!({
        "overrides": { "arrivals": [10.0, 20.0, 30.0, 40.0] }
    }));
    let resp = read_response(&mut stdout);
    let served: Vec<f64> = serde_json::from_value(resp["result"]["series"]["served"].clone()).unwrap();
    assert_eq!(served, vec![5.0, 10.0, 15.0, 20.0]);

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_eval_unknown_override_ignored() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let _ = read_response(&mut stdout);

    send_request(&mut stdin, "eval", serde_json::json!({
        "overrides": { "nonexistent_param": 999.0 }
    }));
    let resp = read_response(&mut stdout);
    assert!(resp.get("result").is_some(), "unknown override should not cause error");
    let served: Vec<f64> = serde_json::from_value(resp["result"]["series"]["served"].clone()).unwrap();
    assert_eq!(served, vec![5.0, 5.0, 5.0, 5.0]);

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_eval_empty_overrides_uses_defaults() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let compile_resp = read_response(&mut stdout);
    let initial: Vec<f64> = serde_json::from_value(compile_resp["result"]["series"]["served"].clone()).unwrap();

    send_request(&mut stdin, "eval", serde_json::json!({}));
    let resp = read_response(&mut stdout);
    let served: Vec<f64> = serde_json::from_value(resp["result"]["series"]["served"].clone()).unwrap();
    assert_eq!(served, initial);

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_get_series_all_when_no_names() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let _ = read_response(&mut stdout);

    send_request(&mut stdin, "get_series", serde_json::json!({}));
    let resp = read_response(&mut stdout);
    let series = resp["result"]["series"].as_object().unwrap();
    assert!(series.contains_key("arrivals"));
    assert!(series.contains_key("served"));

    drop(stdin);
    child.wait().unwrap();
}

const TOPOLOGY_MODEL: &str = r#"
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

#[test]
fn session_topology_wip_override() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": TOPOLOGY_MODEL }));
    let resp = read_response(&mut stdout);
    assert!(resp.get("result").is_some(), "topology model should compile: {resp:?}");

    let q_key = "q_queue";
    let queue: Vec<f64> = serde_json::from_value(resp["result"]["series"][q_key].clone())
        .unwrap_or_else(|_| panic!("q_queue should exist. Keys: {:?}",
            resp["result"]["series"].as_object().unwrap().keys().collect::<Vec<_>>()));
    assert_eq!(queue, vec![15.0, 30.0, 45.0, 50.0]);

    send_request(&mut stdin, "eval", serde_json::json!({
        "overrides": { "Q.wipLimit": 25.0 }
    }));
    let resp2 = read_response(&mut stdout);
    let queue2: Vec<f64> = serde_json::from_value(resp2["result"]["series"][q_key].clone()).unwrap();
    assert_eq!(queue2, vec![15.0, 25.0, 25.0, 25.0]);

    drop(stdin);
    child.wait().unwrap();
}

// ── m-E17-04: warnings surface ──

const CAPACITY_CONSTRAINED_MODEL: &str = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [15, 15, 15, 15]
  - id: capacity
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "arrivals"
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

#[test]
fn session_compile_returns_warnings() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": CAPACITY_CONSTRAINED_MODEL }));
    let resp = read_response(&mut stdout);

    assert!(resp.get("result").is_some(), "compile should succeed: {resp:?}");
    let warnings = resp["result"]["warnings"].as_array().expect("warnings must be an array");
    assert!(!warnings.is_empty(), "expected served_exceeds_capacity warning. Response: {resp:?}");

    let first = &warnings[0];
    assert_eq!(first["node_id"], "Service");
    assert_eq!(first["code"], "served_exceeds_capacity");
    assert_eq!(first["severity"], "warning");
    let bins = first["bins"].as_array().unwrap();
    assert_eq!(bins.len(), 4, "all 4 bins should be affected");

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_eval_warnings_clear_and_return_on_parameter_tweak() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": CAPACITY_CONSTRAINED_MODEL }));
    let compile_resp = read_response(&mut stdout);
    let compile_warnings = compile_resp["result"]["warnings"].as_array().unwrap();
    assert_eq!(compile_warnings.len(), 1, "compile should have 1 warning");

    // Raise capacity 10 → 20 — should clear the warning
    send_request(&mut stdin, "eval", serde_json::json!({
        "overrides": { "capacity": 20.0 }
    }));
    let eval_resp = read_response(&mut stdout);
    assert!(eval_resp.get("result").is_some(), "eval should succeed");
    let eval_warnings = eval_resp["result"]["warnings"]
        .as_array()
        .expect("eval result must carry a warnings array");
    assert!(eval_warnings.is_empty(),
        "warnings should clear after raising capacity. Got: {eval_warnings:?}");

    // Drop capacity to 8 — warning returns
    send_request(&mut stdin, "eval", serde_json::json!({
        "overrides": { "capacity": 8.0 }
    }));
    let eval_resp2 = read_response(&mut stdout);
    let eval_warnings2 = eval_resp2["result"]["warnings"].as_array().unwrap();
    assert_eq!(eval_warnings2.len(), 1, "warning should return. Got: {eval_warnings2:?}");
    assert_eq!(eval_warnings2[0]["code"], "served_exceeds_capacity");

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn session_simple_model_has_empty_warnings_array() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "compile", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let resp = read_response(&mut stdout);

    // Simple model (no topology) → no warnings possible, but array should still be present
    let warnings = resp["result"]["warnings"].as_array()
        .expect("warnings array must always be present");
    assert!(warnings.is_empty(), "simple model should have no warnings. Got: {warnings:?}");

    // Same for eval responses
    send_request(&mut stdin, "eval", serde_json::json!({ "overrides": {} }));
    let eval_resp = read_response(&mut stdout);
    let eval_warnings = eval_resp["result"]["warnings"].as_array().unwrap();
    assert!(eval_warnings.is_empty());

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn validate_schema_valid_model_returns_is_valid_true() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    send_request(&mut stdin, "validate_schema", serde_json::json!({ "yaml": SIMPLE_MODEL }));
    let resp = read_response(&mut stdout);

    assert!(resp.get("error").is_none(), "validate_schema should not return an error envelope: {resp:?}");
    let result = resp.get("result").expect("validate_schema must return a result");
    assert_eq!(result["is_valid"], true, "valid model should return is_valid=true");
    let errors = result["errors"].as_array().expect("errors must be an array");
    assert!(errors.is_empty(), "valid model should have no errors");

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn validate_schema_invalid_yaml_returns_is_valid_false() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    let bad_yaml = "nodes: [\n  invalid: [\n    yaml: {broken";
    send_request(&mut stdin, "validate_schema", serde_json::json!({ "yaml": bad_yaml }));
    let resp = read_response(&mut stdout);

    assert!(resp.get("error").is_none(), "validate_schema uses result envelope even on failure: {resp:?}");
    let result = resp.get("result").expect("validate_schema must return a result");
    assert_eq!(result["is_valid"], false, "invalid YAML should return is_valid=false");
    let errors = result["errors"].as_array().expect("errors must be an array");
    assert!(!errors.is_empty(), "invalid YAML should have at least one error");

    drop(stdin);
    child.wait().unwrap();
}

#[test]
fn validate_schema_missing_yaml_param_returns_error() {
    let mut child = Command::new(engine_path())
        .arg("session")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();

    let mut stdin = child.stdin.take().unwrap();
    let mut stdout = child.stdout.take().unwrap();

    // Send with wrong param name — should get an error envelope
    send_request(&mut stdin, "validate_schema", serde_json::json!({ "content": "yaml: wrong key" }));
    let resp = read_response(&mut stdout);

    assert!(resp.get("error").is_some(), "missing yaml param should return error envelope: {resp:?}");

    drop(stdin);
    child.wait().unwrap();
}
