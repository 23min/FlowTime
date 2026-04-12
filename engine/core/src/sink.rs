//! Full artifact sink: writes evaluation results in the StateQueryService-compatible layout.
//!
//! Produces:
//! - `series/{seriesId}@{COMPONENT}@{CLASS}.csv` — per-series CSV files
//! - `series/index.json` — full series metadata index
//! - `run.json` — run metadata with warnings, classes, coverage
//! - `manifest.json` — hashes, provenance, RNG, classes
//! - `model/model.yaml` — copy of input model
//! - `model/metadata.json` — template/mode metadata
//! - `model/provenance.json` — provenance metadata (optional)
//! - `spec.yaml` — normalized model with file:// URIs
//! - `aggregates/` — placeholder directory

use crate::analysis::Warning;
use crate::compiler::EvalResult;
use crate::eval::extract_column;
use crate::model::ModelDefinition;
use sha2::{Sha256, Digest};
use std::collections::HashMap;
use std::fs;
use std::io::Write;
use std::path::Path;

/// Configuration for the artifact sink.
#[derive(Debug, Default, Clone)]
pub struct SinkConfig {
    /// Template ID for run naming and metadata.
    pub template_id: Option<String>,
    /// Template display title.
    pub template_title: Option<String>,
    /// Template version.
    pub template_version: Option<String>,
    /// Run mode: "simulation" or "telemetry".
    pub mode: Option<String>,
    /// External source identifier.
    pub source: Option<String>,
    /// If true, generate a deterministic run ID from template+hash.
    pub deterministic: bool,
    /// Provenance metadata (passed through as-is to provenance.json).
    pub provenance: Option<String>,
}

/// A single series descriptor for the sink output.
#[derive(Debug)]
struct SeriesDescriptor {
    /// Full series ID: {measure}@{COMPONENT}@{class}
    id: String,
    /// The column index in the state matrix
    col: usize,
    /// Component ID (uppercase)
    component_id: String,
    /// Class ID ("DEFAULT" or specific class name)
    class_id: String,
    /// Class kind: "fallback" for DEFAULT, "specific" for named class
    class_kind: &'static str,
    /// Series kind: "flow", "stock", "edge"
    kind: &'static str,
}

/// Generate a deterministic run ID from template ID and model hash.
///
/// Algorithm (matches C# DeterministicRunNaming):
/// 1. Sanitize template_id: lowercase, keep [a-z0-9_-], replace others with '-', collapse runs
/// 2. Strip "sha256:" prefix from hash if present
/// 3. Return "run_{sanitized}_{hash}"
pub fn deterministic_run_id(template_id: &str, input_hash: &str) -> String {
    let sanitized = sanitize_template_id(template_id);
    let hash = input_hash.strip_prefix("sha256:").unwrap_or(input_hash);
    format!("run_{}_{}", sanitized, hash)
}

fn sanitize_template_id(id: &str) -> String {
    let mut result = String::new();
    for ch in id.to_lowercase().chars() {
        if ch.is_ascii_alphanumeric() || ch == '_' {
            result.push(ch);
        } else {
            // Replace whitespace and invalid chars with '-'
            if !result.ends_with('-') {
                result.push('-');
            }
        }
    }
    // Trim leading/trailing hyphens
    let trimmed = result.trim_matches('-').to_string();
    if trimmed.is_empty() {
        "template".to_string()
    } else {
        trimmed
    }
}

/// Build the series ID in the convention: {measure}@{COMPONENT}@{class}
fn series_id(measure: &str, class: &str) -> String {
    let component = measure.to_uppercase();
    format!("{measure}@{component}@{class}")
}

/// Write full artifact sink to the given output directory.
pub fn write_sink(
    output_dir: &Path,
    model: &ModelDefinition,
    result: &EvalResult,
    model_yaml: &str,
    config: &SinkConfig,
) -> Result<(), String> {
    let series_dir = output_dir.join("series");
    let model_dir = output_dir.join("model");
    let aggregates_dir = output_dir.join("aggregates");

    fs::create_dir_all(&series_dir)
        .map_err(|e| format!("Failed to create series directory: {e}"))?;
    fs::create_dir_all(&model_dir)
        .map_err(|e| format!("Failed to create model directory: {e}"))?;
    fs::create_dir_all(&aggregates_dir)
        .map_err(|e| format!("Failed to create aggregates directory: {e}"))?;

    let grid = model.grid.as_ref()
        .ok_or("Model must have a grid definition")?;
    let model_hash = sha256_hex(model_yaml);

    // Build series descriptors
    let all_series: Vec<(usize, &str)> = result.column_map.iter()
        .filter(|(_, name)| !name.starts_with("__temp_") && !name.starts_with("__edge_"))
        .collect();
    let filtered = filter_by_outputs(&all_series, &model.outputs);

    let mut descriptors = build_series_descriptors(&filtered, result);

    // Add edge series descriptors
    add_edge_descriptors(&mut descriptors, result);

    // Write series CSVs
    let mut series_hashes: Vec<(&str, String)> = Vec::new();
    for desc in &descriptors {
        let values = extract_column(&result.state, desc.col, result.bins);
        write_series_csv(&series_dir, &desc.id, &values)?;
        let csv_bytes = fs::read(series_dir.join(format!("{}.csv", desc.id)))
            .map_err(|e| format!("Read error: {e}"))?;
        series_hashes.push((&desc.id, sha256_bytes(&csv_bytes)));
    }

    // Write model/model.yaml
    fs::write(model_dir.join("model.yaml"), model_yaml)
        .map_err(|e| format!("Failed to write model.yaml: {e}"))?;

    // Write model/metadata.json
    write_metadata_json(&model_dir, &model_hash, config)?;

    // Write model/provenance.json (if provided)
    if let Some(ref prov) = config.provenance {
        fs::write(model_dir.join("provenance.json"), prov)
            .map_err(|e| format!("Failed to write provenance.json: {e}"))?;
    }

    // Write spec.yaml
    write_spec_yaml(output_dir, model_yaml, model, &descriptors)?;

    // Write series/index.json
    write_full_index_json(&series_dir, grid, &descriptors, result.bins, &model.classes, &series_hashes)?;

    // Write run.json
    let run_id = if config.deterministic {
        let tid = config.template_id.as_deref().unwrap_or("adhoc-model");
        deterministic_run_id(tid, &format!("sha256:{model_hash}"))
    } else {
        format!("run_{}", &model_hash[..16.min(model_hash.len())])
    };
    write_full_run_json(output_dir, grid, &descriptors, &result.warnings, &model.classes,
                        &run_id, &model_hash, config)?;

    // Write manifest.json
    write_full_manifest_json(output_dir, grid, &model_hash, &series_hashes, &model.classes, config)?;

    Ok(())
}

/// Build series descriptors from filtered column list.
fn build_series_descriptors<'a>(
    filtered: &[(usize, &'a str)],
    result: &EvalResult,
) -> Vec<SeriesDescriptor> {
    let mut descriptors = Vec::new();

    // Collect class IDs for per-class naming
    let class_ids: Vec<String> = {
        let mut ids = std::collections::HashSet::new();
        for ((_, cid), _) in &result.class_map {
            ids.insert(cid.clone());
        }
        let mut v: Vec<_> = ids.into_iter().collect();
        v.sort();
        v
    };

    for &(col, name) in filtered {
        // Skip per-class columns — they'll be added separately below
        if name.contains("__class_") { continue; }

        let kind = infer_series_kind(name);

        // Default series (total / no class)
        descriptors.push(SeriesDescriptor {
            id: series_id(name, "DEFAULT"),
            col,
            component_id: name.to_uppercase(),
            class_id: "DEFAULT".to_string(),
            class_kind: "fallback",
            kind,
        });

        // Per-class series
        for cid in &class_ids {
            if let Some(&class_col) = result.class_map.get(&(name.to_string(), cid.clone())) {
                descriptors.push(SeriesDescriptor {
                    id: series_id(name, cid),
                    col: class_col,
                    component_id: name.to_uppercase(),
                    class_id: cid.clone(),
                    class_kind: "specific",
                    kind,
                });
            }
        }
    }

    descriptors
}

/// Add edge series descriptors.
fn add_edge_descriptors(descriptors: &mut Vec<SeriesDescriptor>, result: &EvalResult) {
    let mut seen = std::collections::HashSet::new();
    for ((edge_id, metric), &col) in &result.edge_map {
        // Skip per-class edges (contain '@')
        if edge_id.contains('@') { continue; }

        let measure = format!("edge_{}_{}", edge_id.replace('→', "_"), metric);
        let sid = series_id(&measure, "DEFAULT");
        if seen.insert(sid.clone()) {
            descriptors.push(SeriesDescriptor {
                id: sid,
                col,
                component_id: measure.to_uppercase(),
                class_id: "DEFAULT".to_string(),
                class_kind: "fallback",
                kind: "edge",
            });
        }

        // Per-class edge series
        for ((class_edge_id, m), &class_col) in &result.edge_map {
            if m != metric { continue; }
            if let Some(at_pos) = class_edge_id.find('@') {
                let base = &class_edge_id[..at_pos];
                let class_id = &class_edge_id[at_pos + 1..];
                if base == edge_id.as_str() {
                    let class_sid = series_id(&measure, class_id);
                    if seen.insert(class_sid.clone()) {
                        descriptors.push(SeriesDescriptor {
                            id: class_sid,
                            col: class_col,
                            component_id: measure.to_uppercase(),
                            class_id: class_id.to_string(),
                            class_kind: "specific",
                            kind: "edge",
                        });
                    }
                }
            }
        }
    }
}

/// Infer series kind from name.
fn infer_series_kind(name: &str) -> &'static str {
    let lower = name.to_lowercase();
    if lower.contains("queue") || lower.contains("depth") || lower.contains("wip") {
        "stock"
    } else if lower.contains("utilization") || lower.contains("efficiency") || lower.contains("ratio") {
        "ratio"
    } else if lower.contains("time") || lower.contains("latency") || lower.contains("kingman") {
        "time"
    } else {
        "flow"
    }
}

// ──────────────────────────────── File Writers ────────────────────────────────

fn write_series_csv(series_dir: &Path, name: &str, values: &[f64]) -> Result<(), String> {
    let path = series_dir.join(format!("{name}.csv"));
    let mut file = fs::File::create(&path)
        .map_err(|e| format!("Failed to create {}: {e}", path.display()))?;
    writeln!(file, "bin_index,value").map_err(|e| format!("Write error: {e}"))?;
    for (t, v) in values.iter().enumerate() {
        writeln!(file, "{t},{v}").map_err(|e| format!("Write error: {e}"))?;
    }
    Ok(())
}

fn write_metadata_json(model_dir: &Path, model_hash: &str, config: &SinkConfig) -> Result<(), String> {
    let path = model_dir.join("metadata.json");
    let template_id = config.template_id.as_deref().unwrap_or("adhoc-model");
    let template_title = config.template_title.as_deref().unwrap_or("Ad Hoc Model");
    let template_version = config.template_version.as_deref().unwrap_or("0.0.0");
    let mode = config.mode.as_deref().unwrap_or("simulation");
    let source_json = config.source.as_ref()
        .map(|s| format!("\"{}\"", s.replace('"', "\\\"")))
        .unwrap_or_else(|| "null".to_string());

    let json = format!(
r#"{{
  "schemaVersion": 1,
  "templateId": "{template_id}",
  "templateTitle": "{template_title}",
  "templateVersion": "{template_version}",
  "mode": "{mode}",
  "modelHash": "sha256:{model_hash}",
  "source": {source_json},
  "hasTelemetrySources": false,
  "telemetrySources": [],
  "nodeSources": {{}},
  "parameters": {{}}
}}"#
    );

    fs::write(&path, json).map_err(|e| format!("Failed to write metadata.json: {e}"))?;
    Ok(())
}

/// Known YAML semantics keys that should be rewritten to file: URIs.
const SEMANTICS_KEYS: &[&str] = &[
    "arrivals", "served", "errors", "queueDepth", "capacity",
    "attempts", "failures", "exhaustedFailures", "retryEcho",
    "retryBudgetRemaining", "externalDemand", "processingTimeMsSum",
    "servedCount", "latencyMinutes",
];

fn write_spec_yaml(
    output_dir: &Path,
    model_yaml: &str,
    _model: &ModelDefinition,
    descriptors: &[SeriesDescriptor],
) -> Result<(), String> {
    // Build a map: original series name → sink file path for DEFAULT series
    let mut series_path_map: HashMap<&str, String> = HashMap::new();
    for desc in descriptors {
        if desc.class_id == "DEFAULT" && desc.kind != "edge" {
            let measure = desc.id.split('@').next().unwrap_or(&desc.id);
            series_path_map.insert(measure, format!("series/{}.csv", desc.id));
        }
    }

    // Line-by-line rewriting: only replace values on lines where the key is
    // a known semantics field AND the value matches a series we've written.
    // This avoids false positives from global string replacement.
    let mut output_lines: Vec<String> = Vec::new();
    for line in model_yaml.lines() {
        let trimmed = line.trim();

        // Skip empty lines, comments, list items
        if trimmed.is_empty() || trimmed.starts_with('#') || trimmed.starts_with('-') {
            output_lines.push(line.to_string());
            continue;
        }

        // Try to parse as "key: value" (simple YAML scalar)
        if let Some(colon_pos) = trimmed.find(": ") {
            let key = trimmed[..colon_pos].trim();
            let value = trimmed[colon_pos + 2..].trim().trim_matches('"');

            if SEMANTICS_KEYS.contains(&key) && series_path_map.contains_key(value) {
                let path = &series_path_map[value];
                let indent = &line[..line.len() - line.trim_start().len()];
                output_lines.push(format!("{indent}{key}: \"file:{path}\""));
                continue;
            }
        }

        output_lines.push(line.to_string());
    }

    let mut spec = output_lines.join("\n");
    // Preserve trailing newline if original had one
    if model_yaml.ends_with('\n') && !spec.ends_with('\n') {
        spec.push('\n');
    }
    fs::write(output_dir.join("spec.yaml"), spec)
        .map_err(|e| format!("Failed to write spec.yaml: {e}"))?;
    Ok(())
}

fn write_full_index_json(
    series_dir: &Path,
    grid: &crate::model::GridDefinition,
    descriptors: &[SeriesDescriptor],
    bins: usize,
    classes: &[crate::model::ClassDefinition],
    series_hashes: &[(&str, String)],
) -> Result<(), String> {
    let hash_map: HashMap<&str, &str> = series_hashes.iter()
        .map(|(id, h)| (*id, h.as_str()))
        .collect();

    let series_entries: Vec<String> = descriptors.iter()
        .map(|d| {
            let hash = hash_map.get(d.id.as_str()).unwrap_or(&"");
            format!(
r#"    {{
      "id": "{}",
      "kind": "{}",
      "path": "series/{}.csv",
      "unit": "entities/bin",
      "componentId": "{}",
      "class": "{}",
      "classKind": "{}",
      "points": {},
      "hash": "sha256:{}"
    }}"#,
                d.id, d.kind, d.id, d.component_id, d.class_id, d.class_kind, bins, hash
            )
        })
        .collect();

    let classes_entries: Vec<String> = classes.iter()
        .map(|c| {
            let display = c.display_name.as_deref().unwrap_or(&c.id);
            let desc = c.description.as_deref().unwrap_or("");
            format!(r#"    {{"id": "{}", "displayName": "{}", "description": "{}"}}"#, c.id, display, desc)
        })
        .collect();

    let coverage = if classes.is_empty() { "none" } else { "full" };

    let json = format!(
r#"{{
  "schemaVersion": 1,
  "grid": {{
    "bins": {bins},
    "binSize": {bin_size},
    "binUnit": "{bin_unit}"
  }},
  "series": [
{series}
  ],
  "classes": [
{classes}
  ],
  "classesCoverage": "{coverage}",
  "formats": {{
    "aggregatesTable": {{
      "path": "aggregates/node_time_bin.parquet"
    }}
  }}
}}"#,
        bins = grid.bins,
        bin_size = grid.bin_size,
        bin_unit = grid.bin_unit,
        series = series_entries.join(",\n"),
        classes = classes_entries.join(",\n"),
        coverage = coverage,
    );

    fs::write(series_dir.join("index.json"), json)
        .map_err(|e| format!("Failed to write index.json: {e}"))?;
    Ok(())
}

fn write_full_run_json(
    output_dir: &Path,
    grid: &crate::model::GridDefinition,
    descriptors: &[SeriesDescriptor],
    warnings: &[Warning],
    classes: &[crate::model::ClassDefinition],
    run_id: &str,
    model_hash: &str,
    config: &SinkConfig,
) -> Result<(), String> {
    let series_entries: Vec<String> = descriptors.iter()
        .map(|d| format!(r#"    {{"id": "{}", "path": "series/{}.csv", "unit": "entities/bin"}}"#, d.id, d.id))
        .collect();

    let warning_entries: Vec<String> = warnings.iter()
        .map(|w| format!(
            r#"    {{"nodeId": "{}", "code": "{}", "message": "{}", "severity": "{}"}}"#,
            w.node_id, w.code, w.message.replace('"', "\\\""), w.severity
        ))
        .collect();

    let classes_entries: Vec<String> = classes.iter()
        .map(|c| {
            let display = c.display_name.as_deref().unwrap_or(&c.id);
            let desc = c.description.as_deref().unwrap_or("");
            format!(r#"    {{"id": "{}", "displayName": "{}", "description": "{}"}}"#, c.id, display, desc)
        })
        .collect();

    let coverage = if classes.is_empty() { "none" } else { "full" };
    let source = config.source.as_deref().unwrap_or("engine");

    let timestamp = utc_now_iso();

    let json = format!(
r#"{{
  "schemaVersion": 1,
  "runId": "{run_id}",
  "engineVersion": "0.1.0",
  "source": "{source}",
  "inputHash": "sha256:{model_hash}",
  "scenarioHash": "sha256:{model_hash}",
  "createdUtc": "{timestamp}",
  "grid": {{
    "bins": {bins},
    "binSize": {bin_size},
    "binUnit": "{bin_unit}",
    "timezone": "UTC",
    "align": "left"
  }},
  "modelHash": "sha256:{model_hash}",
  "classesCoverage": "{coverage}",
  "warnings": [
{warnings}
  ],
  "series": [
{series}
  ],
  "classes": [
{classes}
  ]
}}"#,
        bins = grid.bins,
        bin_size = grid.bin_size,
        bin_unit = grid.bin_unit,
        warnings = warning_entries.join(",\n"),
        series = series_entries.join(",\n"),
        classes = classes_entries.join(",\n"),
    );

    fs::write(output_dir.join("run.json"), json)
        .map_err(|e| format!("Failed to write run.json: {e}"))?;
    Ok(())
}

fn write_full_manifest_json(
    output_dir: &Path,
    _grid: &crate::model::GridDefinition,
    model_hash: &str,
    series_hashes: &[(&str, String)],
    classes: &[crate::model::ClassDefinition],
    config: &SinkConfig,
) -> Result<(), String> {
    let hash_entries: Vec<String> = series_hashes.iter()
        .map(|(id, hash)| format!(r#"    "{}": "sha256:{}""#, id, hash))
        .collect();

    let classes_entries: Vec<String> = classes.iter()
        .map(|c| {
            let display = c.display_name.as_deref().unwrap_or(&c.id);
            format!(r#"    {{"id": "{}", "displayName": "{}"}}"#, c.id, display)
        })
        .collect();

    let template_id = config.template_id.as_deref().unwrap_or("adhoc-model");
    let has_prov = config.provenance.is_some();

    let json = format!(
r#"{{
  "schemaVersion": 1,
  "scenarioHash": "sha256:{model_hash}",
  "modelHash": "sha256:{model_hash}",
  "rng": {{
    "kind": "none",
    "seed": 0
  }},
  "seriesHashes": {{
{hashes}
  }},
  "eventCount": 0,
  "createdUtc": "{timestamp}",
  "provenance": {{
    "hasProvenance": {has_prov},
    "templateId": "{template_id}",
    "inputHash": "sha256:{model_hash}"
  }},
  "classes": [
{classes}
  ]
}}"#,
        hashes = hash_entries.join(",\n"),
        timestamp = utc_now_iso(),
        has_prov = has_prov,
        classes = classes_entries.join(",\n"),
    );

    fs::write(output_dir.join("manifest.json"), json)
        .map_err(|e| format!("Failed to write manifest.json: {e}"))?;
    Ok(())
}

fn sha256_hex(input: &str) -> String {
    let mut hasher = Sha256::new();
    hasher.update(input.as_bytes());
    format!("{:x}", hasher.finalize())
}

fn sha256_bytes(input: &[u8]) -> String {
    let mut hasher = Sha256::new();
    hasher.update(input);
    format!("{:x}", hasher.finalize())
}

fn utc_now_iso() -> String {
    // Simple ISO-8601 UTC timestamp
    use std::time::SystemTime;
    let d = SystemTime::now().duration_since(SystemTime::UNIX_EPOCH).unwrap_or_default();
    let secs = d.as_secs();
    // Format as ISO-8601 (simplified — seconds precision)
    let days = secs / 86400;
    let rem = secs % 86400;
    let hours = rem / 3600;
    let mins = (rem % 3600) / 60;
    let s = rem % 60;
    // Approximate date from days since epoch (good enough for timestamps)
    let (year, month, day) = days_to_ymd(days);
    format!("{year:04}-{month:02}-{day:02}T{hours:02}:{mins:02}:{s:02}Z")
}

fn days_to_ymd(days: u64) -> (u64, u64, u64) {
    // Simplified Gregorian calendar conversion
    let mut y = 1970;
    let mut remaining = days;
    loop {
        let year_days = if is_leap(y) { 366 } else { 365 };
        if remaining < year_days { break; }
        remaining -= year_days;
        y += 1;
    }
    let month_days: [u64; 12] = if is_leap(y) {
        [31,29,31,30,31,30,31,31,30,31,30,31]
    } else {
        [31,28,31,30,31,30,31,31,30,31,30,31]
    };
    let mut m = 0;
    for (i, &md) in month_days.iter().enumerate() {
        if remaining < md { m = i; break; }
        remaining -= md;
    }
    (y, (m + 1) as u64, remaining + 1)
}

fn is_leap(y: u64) -> bool {
    (y % 4 == 0 && y % 100 != 0) || (y % 400 == 0)
}

/// Filter series list by the model's `outputs` section (reused from writer.rs).
fn filter_by_outputs<'a>(
    all_series: &[(usize, &'a str)],
    outputs: &[crate::model::OutputDefinition],
) -> Vec<(usize, &'a str)> {
    if outputs.is_empty() {
        return all_series.to_vec();
    }

    let mut result = Vec::new();
    let mut seen = std::collections::HashSet::new();

    for output in outputs {
        let pattern = output.series.trim();
        if pattern.is_empty() { continue; }

        if pattern == "*" {
            for &(idx, name) in all_series {
                if seen.insert(name) { result.push((idx, name)); }
            }
        } else if pattern.ends_with("/*") {
            let prefix = &pattern[..pattern.len() - 2];
            for &(idx, name) in all_series {
                if name.starts_with(prefix) && seen.insert(name) { result.push((idx, name)); }
            }
        } else {
            for &(idx, name) in all_series {
                if name.eq_ignore_ascii_case(pattern) && seen.insert(name) { result.push((idx, name)); }
            }
        }
    }

    if result.is_empty() { return all_series.to_vec(); }
    result
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::compiler::eval_model;
    use crate::model::parse_model_yaml;
    use std::path::PathBuf;

    fn tmp_dir() -> PathBuf {
        use std::sync::atomic::{AtomicU32, Ordering};
        static COUNTER: AtomicU32 = AtomicU32::new(0);
        let n = COUNTER.fetch_add(1, Ordering::Relaxed);
        let dir = std::env::temp_dir().join(format!("flowtime_sink_{}_{n}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    // ── AC-8: Deterministic run ID ──

    #[test]
    fn deterministic_run_id_basic() {
        let id = deterministic_run_id("My Template", "sha256:abc123def");
        assert_eq!(id, "run_my-template_abc123def");
    }

    #[test]
    fn deterministic_run_id_strips_sha256_prefix() {
        let id = deterministic_run_id("test", "sha256:deadbeef");
        assert_eq!(id, "run_test_deadbeef");
    }

    #[test]
    fn deterministic_run_id_no_prefix() {
        let id = deterministic_run_id("test", "deadbeef");
        assert_eq!(id, "run_test_deadbeef");
    }

    #[test]
    fn deterministic_run_id_empty_template() {
        let id = deterministic_run_id("", "abc");
        assert_eq!(id, "run_template_abc");
    }

    #[test]
    fn deterministic_run_id_special_chars() {
        let id = deterministic_run_id("My Model! @#$ v2.0", "hash123");
        assert_eq!(id, "run_my-model-v2-0_hash123");
    }

    #[test]
    fn sanitize_collapses_hyphens() {
        // Hyphens are not alphanumeric or underscore, so they trigger replacement
        // The replacement logic collapses consecutive non-alphanumeric chars to single '-'
        let s = sanitize_template_id("a---b");
        assert_eq!(s, "a-b");
    }

    #[test]
    fn sanitize_underscores_preserved() {
        let s = sanitize_template_id("my_template_v2");
        assert_eq!(s, "my_template_v2");
    }

    // ── AC-7: Directory structure ──

    #[test]
    fn sink_creates_directory_structure() {
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        assert!(dir.join("series").is_dir(), "series/ directory should exist");
        assert!(dir.join("model").is_dir(), "model/ directory should exist");
        assert!(dir.join("aggregates").is_dir(), "aggregates/ directory should exist");
        assert!(dir.join("model/model.yaml").exists(), "model/model.yaml should exist");
        assert!(dir.join("model/metadata.json").exists(), "model/metadata.json should exist");
        assert!(dir.join("spec.yaml").exists(), "spec.yaml should exist");
        assert!(dir.join("run.json").exists(), "run.json should exist");
        assert!(dir.join("manifest.json").exists(), "manifest.json should exist");
        assert!(dir.join("series/index.json").exists(), "series/index.json should exist");

        let _ = fs::remove_dir_all(&dir);
    }

    // ── AC-3: Series naming convention ──

    #[test]
    fn series_id_convention() {
        assert_eq!(series_id("arrivals", "DEFAULT"), "arrivals@ARRIVALS@DEFAULT");
        assert_eq!(series_id("served", "Order"), "served@SERVED@Order");
        assert_eq!(series_id("queue_depth", "DEFAULT"), "queue_depth@QUEUE_DEPTH@DEFAULT");
    }

    #[test]
    fn sink_uses_series_id_naming() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 20]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        assert!(dir.join("series/arrivals@ARRIVALS@DEFAULT.csv").exists(),
            "Series CSV should use @COMPONENT@CLASS naming. Files: {:?}",
            fs::read_dir(dir.join("series")).unwrap()
                .filter_map(|e| e.ok()).map(|e| e.file_name()).collect::<Vec<_>>());

        let _ = fs::remove_dir_all(&dir);
    }

    // ── AC-1: model/ directory ──

    #[test]
    fn sink_writes_model_directory() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [5, 10]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        let config = SinkConfig {
            template_id: Some("test-template".to_string()),
            template_title: Some("Test Title".to_string()),
            ..Default::default()
        };
        write_sink(&dir, &model, &result, yaml, &config).unwrap();

        // model.yaml should be a copy of input
        let written = fs::read_to_string(dir.join("model/model.yaml")).unwrap();
        assert_eq!(written, yaml);

        // metadata.json should contain template info
        let meta = fs::read_to_string(dir.join("model/metadata.json")).unwrap();
        assert!(meta.contains("\"templateId\": \"test-template\""));
        assert!(meta.contains("\"templateTitle\": \"Test Title\""));
        assert!(meta.contains("\"mode\": \"simulation\""));
        assert!(meta.contains("\"schemaVersion\": 1"));

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn sink_writes_provenance_when_provided() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [1, 2]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        let config = SinkConfig {
            provenance: Some(r#"{"source": "test"}"#.to_string()),
            ..Default::default()
        };
        write_sink(&dir, &model, &result, yaml, &config).unwrap();

        assert!(dir.join("model/provenance.json").exists());
        let prov = fs::read_to_string(dir.join("model/provenance.json")).unwrap();
        assert!(prov.contains("\"source\": \"test\""));

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn sink_omits_provenance_when_absent() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [1, 2]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        assert!(!dir.join("model/provenance.json").exists());

        let _ = fs::remove_dir_all(&dir);
    }

    // ── AC-3: Per-class series naming ──

    #[test]
    fn sink_per_class_series_naming() {
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
  - id: served
    kind: expr
    expr: "MIN(ingest, 8)"
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        // Default series
        assert!(dir.join("series/ingest@INGEST@DEFAULT.csv").exists());
        assert!(dir.join("series/served@SERVED@DEFAULT.csv").exists());

        // Per-class series
        assert!(dir.join("series/ingest@INGEST@Order.csv").exists(),
            "Per-class series should exist. Files: {:?}",
            fs::read_dir(dir.join("series")).unwrap()
                .filter_map(|e| e.ok()).map(|e| e.file_name()).collect::<Vec<_>>());
        assert!(dir.join("series/ingest@INGEST@Refund.csv").exists());
        assert!(dir.join("series/served@SERVED@Order.csv").exists());
        assert!(dir.join("series/served@SERVED@Refund.csv").exists());

        let _ = fs::remove_dir_all(&dir);
    }

    // ── AC-4: Full index.json schema ──

    #[test]
    fn sink_index_json_has_full_schema() {
        let yaml = r#"
schemaVersion: 1
classes:
  - id: Alpha
    displayName: Alpha Class
grid:
  bins: 2
  binSize: 5
  binUnit: minutes
nodes:
  - id: arr
    kind: const
    values: [10, 20]
traffic:
  arrivals:
    - nodeId: arr
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 10
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        let index = fs::read_to_string(dir.join("series/index.json")).unwrap();
        assert!(index.contains("\"kind\": \"flow\""), "index.json should have kind field");
        assert!(index.contains("\"componentId\": \"ARR\""), "index.json should have componentId");
        assert!(index.contains("\"class\": \"DEFAULT\""), "index.json should have class field");
        assert!(index.contains("\"classKind\": \"fallback\""), "index.json should have classKind");
        assert!(index.contains("\"unit\": \"entities/bin\""), "index.json should have unit");
        assert!(index.contains("\"classesCoverage\": \"full\""), "should have coverage");
        assert!(index.contains("\"displayName\": \"Alpha Class\""), "should have class metadata");
        assert!(index.contains("\"aggregatesTable\""), "should have formats section");

        let _ = fs::remove_dir_all(&dir);
    }

    // ── AC-5: Full run.json schema ──

    #[test]
    fn sink_run_json_has_full_schema() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [1, 2]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        let run = fs::read_to_string(dir.join("run.json")).unwrap();
        assert!(run.contains("\"runId\":"), "run.json should have runId");
        assert!(run.contains("\"source\": \"engine\""), "run.json should have source");
        assert!(run.contains("\"inputHash\": \"sha256:"), "run.json should have inputHash");
        assert!(run.contains("\"modelHash\": \"sha256:"), "run.json should have modelHash");
        assert!(run.contains("\"classesCoverage\":"), "run.json should have classesCoverage");

        let _ = fs::remove_dir_all(&dir);
    }

    // ── AC-6: Full manifest.json schema ──

    #[test]
    fn sink_manifest_json_has_full_schema() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [1, 2]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        let manifest = fs::read_to_string(dir.join("manifest.json")).unwrap();
        assert!(manifest.contains("\"rng\":"), "manifest should have rng section");
        assert!(manifest.contains("\"provenance\":"), "manifest should have provenance");
        assert!(manifest.contains("\"createdUtc\":"), "manifest should have timestamp");
        assert!(manifest.contains("\"seriesHashes\":"), "manifest should have seriesHashes");
        assert!(manifest.contains("\"scenarioHash\":"), "manifest should have scenarioHash");

        let _ = fs::remove_dir_all(&dir);
    }

    // ── AC-2: spec.yaml with file:// URIs ──

    #[test]
    fn sink_spec_yaml_rewrites_topology_refs() {
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
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        let spec = fs::read_to_string(dir.join("spec.yaml")).unwrap();
        assert!(spec.contains("file:series/arrivals@ARRIVALS@DEFAULT.csv"),
            "spec.yaml should rewrite arrivals to file:// URI. Content:\n{}", spec);
        assert!(spec.contains("file:series/served@SERVED@DEFAULT.csv"),
            "spec.yaml should rewrite served to file:// URI");

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn sink_spec_yaml_no_topology_passes_through() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [1, 2]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        let spec = fs::read_to_string(dir.join("spec.yaml")).unwrap();
        // No topology → spec is just a copy of the original
        assert_eq!(spec, yaml);

        let _ = fs::remove_dir_all(&dir);
    }

    // ── spec.yaml robustness tests ──

    #[test]
    fn spec_yaml_only_rewrites_semantics_keys() {
        // "arrivals" appears as both a node ID and a semantics value.
        // Only the semantics value should be rewritten, not the node id line.
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
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        let spec = fs::read_to_string(dir.join("spec.yaml")).unwrap();

        // The node id line "  - id: arrivals" must NOT be rewritten
        assert!(spec.contains("id: arrivals"), "node id line should be preserved");
        // The semantics value SHOULD be rewritten
        assert!(spec.contains("arrivals: \"file:series/arrivals@ARRIVALS@DEFAULT.csv\""),
            "semantics arrivals should be rewritten to file: URI.\nSpec:\n{spec}");
        assert!(spec.contains("served: \"file:series/served@SERVED@DEFAULT.csv\""),
            "semantics served should be rewritten to file: URI");
        // The node kind line should be untouched
        assert!(spec.contains("kind: const"), "non-semantics fields should be preserved");

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn spec_yaml_handles_optional_semantics_fields() {
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
  - id: err
    kind: const
    values: [1, 1, 1]
  - id: cap
    kind: const
    values: [20, 20, 20]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      semantics:
        arrivals: arr
        served: srv
        errors: err
        capacity: cap
  edges: []
  constraints: []
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        let spec = fs::read_to_string(dir.join("spec.yaml")).unwrap();
        assert!(spec.contains("arrivals: \"file:series/arr@ARR@DEFAULT.csv\""),
            "arrivals should be rewritten. Spec:\n{spec}");
        assert!(spec.contains("served: \"file:series/srv@SRV@DEFAULT.csv\""));
        assert!(spec.contains("errors: \"file:series/err@ERR@DEFAULT.csv\""));
        assert!(spec.contains("capacity: \"file:series/cap@CAP@DEFAULT.csv\""));

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn spec_yaml_preserves_comments_and_formatting() {
        let yaml = "# This is a comment\ngrid:\n  bins: 2\n  binSize: 1\n  binUnit: hours\nnodes:\n  - id: x\n    kind: const\n    values: [1, 2]\n";
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        let spec = fs::read_to_string(dir.join("spec.yaml")).unwrap();
        assert!(spec.contains("# This is a comment"), "comments should be preserved");

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn spec_yaml_parses_back_as_valid_yaml() {
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
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        let spec = fs::read_to_string(dir.join("spec.yaml")).unwrap();
        // The rewritten spec should still parse as valid YAML
        let reparsed = parse_model_yaml(&spec);
        assert!(reparsed.is_ok(), "spec.yaml should parse as valid YAML: {:?}", reparsed.err());

        let _ = fs::remove_dir_all(&dir);
    }

    // ── Edge case tests ──

    #[test]
    fn sink_empty_model_no_nodes() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes: []
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        assert!(dir.join("series").is_dir());
        assert!(dir.join("model/model.yaml").exists());
        assert!(dir.join("run.json").exists());

        // index.json should have empty series
        let index = fs::read_to_string(dir.join("series/index.json")).unwrap();
        assert!(index.contains("\"series\": ["));

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn sink_model_with_outputs_filter() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: demand
    kind: const
    values: [10, 20, 30]
  - id: served
    kind: expr
    expr: "demand * 0.8"
outputs:
  - series: served
    as: served.csv
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        // Only 'served' should be written, not 'demand'
        assert!(dir.join("series/served@SERVED@DEFAULT.csv").exists());
        assert!(!dir.join("series/demand@DEMAND@DEFAULT.csv").exists());

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn sink_combined_classes_and_topology() {
        // Model with both per-class arrivals AND a serviceWithBuffer topology node
        let yaml = r#"
schemaVersion: 1
classes:
  - id: Fast
  - id: Slow
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [20, 20, 20, 20]
  - id: served
    kind: expr
    expr: "MIN(arrivals, 15)"
traffic:
  arrivals:
    - nodeId: arrivals
      classId: Fast
      pattern:
        kind: constant
        ratePerBin: 12
    - nodeId: arrivals
      classId: Slow
      pattern:
        kind: constant
        ratePerBin: 8
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
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        // Total series
        assert!(dir.join("series/arrivals@ARRIVALS@DEFAULT.csv").exists());
        assert!(dir.join("series/served@SERVED@DEFAULT.csv").exists());

        // Per-class series
        assert!(dir.join("series/arrivals@ARRIVALS@Fast.csv").exists());
        assert!(dir.join("series/arrivals@ARRIVALS@Slow.csv").exists());
        assert!(dir.join("series/served@SERVED@Fast.csv").exists());
        assert!(dir.join("series/served@SERVED@Slow.csv").exists());

        // Queue depth (topology-derived)
        assert!(dir.join("series/processor_queue@PROCESSOR_QUEUE@DEFAULT.csv").exists(),
            "Queue depth should exist. Files: {:?}",
            fs::read_dir(dir.join("series")).unwrap()
                .filter_map(|e| e.ok()).map(|e| e.file_name()).collect::<Vec<_>>());

        // Queue depth per-class
        assert!(dir.join("series/processor_queue@PROCESSOR_QUEUE@Fast.csv").exists());
        assert!(dir.join("series/processor_queue@PROCESSOR_QUEUE@Slow.csv").exists());

        // index.json should list classes
        let index = fs::read_to_string(dir.join("series/index.json")).unwrap();
        assert!(index.contains("\"classesCoverage\": \"full\""));
        assert!(index.contains("\"id\": \"Fast\""));
        assert!(index.contains("\"id\": \"Slow\""));

        // spec.yaml should have file: URIs
        let spec = fs::read_to_string(dir.join("spec.yaml")).unwrap();
        assert!(spec.contains("file:series/arrivals@ARRIVALS@DEFAULT.csv"));
        assert!(spec.contains("file:series/served@SERVED@DEFAULT.csv"));

        // Verify CSV values are correct (normalization holds)
        let served_csv = fs::read_to_string(dir.join("series/served@SERVED@DEFAULT.csv")).unwrap();
        assert!(served_csv.contains("0,15"), "served should be MIN(20, 15) = 15");

        let fast_csv = fs::read_to_string(dir.join("series/served@SERVED@Fast.csv")).unwrap();
        // Fast fraction = 12/(12+8) = 0.6, served_Fast = 15 * 0.6 = 9
        assert!(fast_csv.contains("0,9"), "served@Fast should be 9. Content:\n{fast_csv}");

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn sink_with_edge_series() {
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
  constraints: []
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        // Edge series should exist with the naming convention
        let series_files: Vec<String> = fs::read_dir(dir.join("series")).unwrap()
            .filter_map(|e| e.ok())
            .map(|e| e.file_name().to_string_lossy().to_string())
            .filter(|f| f.contains("edge_"))
            .collect();

        assert!(!series_files.is_empty(),
            "Edge series CSVs should be written. All files: {:?}",
            fs::read_dir(dir.join("series")).unwrap()
                .filter_map(|e| e.ok()).map(|e| e.file_name()).collect::<Vec<_>>());

        // Edge series values
        let edge_csv_name = series_files.iter()
            .find(|f| f.contains("DEFAULT"))
            .expect("Should have DEFAULT edge series");
        let edge_csv = fs::read_to_string(dir.join("series").join(edge_csv_name)).unwrap();
        assert!(edge_csv.contains("0,80"), "Edge flow should be 80 (srv). Content:\n{edge_csv}");

        // Edge should appear in index.json
        let index = fs::read_to_string(dir.join("series/index.json")).unwrap();
        assert!(index.contains("\"kind\": \"edge\""), "Edge series should have kind=edge in index.json");

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn sink_deterministic_run_id_in_run_json() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [1, 2]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        let config = SinkConfig {
            template_id: Some("My Model".to_string()),
            deterministic: true,
            ..Default::default()
        };
        write_sink(&dir, &model, &result, yaml, &config).unwrap();

        let run = fs::read_to_string(dir.join("run.json")).unwrap();
        assert!(run.contains("\"runId\": \"run_my-model_"),
            "run.json should have deterministic run ID. Content:\n{run}");

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn sink_json_files_are_valid_json() {
        let yaml = r#"
schemaVersion: 1
classes:
  - id: Alpha
grid:
  bins: 3
  binSize: 5
  binUnit: minutes
nodes:
  - id: demand
    kind: const
    values: [10, 20, 30]
traffic:
  arrivals:
    - nodeId: demand
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 10
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();

        write_sink(&dir, &model, &result, yaml, &SinkConfig::default()).unwrap();

        // All JSON files should be valid JSON (parseable)
        for file in &["run.json", "manifest.json", "series/index.json", "model/metadata.json"] {
            let content = fs::read_to_string(dir.join(file)).unwrap();
            let parsed: Result<serde_json::Value, _> = serde_json::from_str(&content);
            assert!(parsed.is_ok(), "{file} should be valid JSON: {:?}\nContent:\n{content}", parsed.err());
        }

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn sink_manifest_hashes_are_deterministic() {
        let yaml = r#"
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: x
    kind: const
    values: [1, 2]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();

        let dir1 = tmp_dir();
        write_sink(&dir1, &model, &result, yaml, &SinkConfig::default()).unwrap();
        let m1 = fs::read_to_string(dir1.join("manifest.json")).unwrap();

        let dir2 = tmp_dir();
        write_sink(&dir2, &model, &result, yaml, &SinkConfig::default()).unwrap();
        let m2 = fs::read_to_string(dir2.join("manifest.json")).unwrap();

        // seriesHashes and modelHash should be identical
        // (createdUtc may differ so compare only the hash portions)
        let h1: Vec<&str> = m1.lines().filter(|l| l.contains("sha256:")).collect();
        let h2: Vec<&str> = m2.lines().filter(|l| l.contains("sha256:")).collect();
        assert_eq!(h1, h2, "Manifest hashes should be deterministic");

        let _ = fs::remove_dir_all(&dir1);
        let _ = fs::remove_dir_all(&dir2);
    }
}
