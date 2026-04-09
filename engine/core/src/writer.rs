//! Artifact writer: writes evaluation results to an output directory.
//!
//! Produces:
//! - `series/{seriesId}.csv` — per-series CSV files (bin_index,value)
//! - `series/index.json` — series metadata index
//! - `run.json` — run metadata with warnings

use crate::analysis::Warning;
use crate::compiler::EvalResult;
use crate::eval::extract_column;
use crate::model::ModelDefinition;
use std::fs;
use std::io::Write;
use std::path::Path;

/// Write all artifacts to the given output directory.
pub fn write_artifacts(
    output_dir: &Path,
    model: &ModelDefinition,
    result: &EvalResult,
) -> Result<(), String> {
    let series_dir = output_dir.join("series");
    fs::create_dir_all(&series_dir)
        .map_err(|e| format!("Failed to create series directory: {e}"))?;

    let grid = model.grid.as_ref()
        .ok_or("Model must have a grid definition")?;

    // Collect non-temp series
    let series_list: Vec<(usize, &str)> = result.column_map.iter()
        .filter(|(_, name)| !name.starts_with("__temp_"))
        .collect();

    // Write CSVs
    for &(idx, name) in &series_list {
        let values = extract_column(&result.state, idx, result.bins);
        write_series_csv(&series_dir, name, &values)?;
    }

    // Write index.json
    write_index_json(&series_dir, grid, &series_list, result.bins)?;

    // Write run.json
    write_run_json(output_dir, grid, &series_list, &result.warnings)?;

    Ok(())
}

/// Write a single series as a CSV file.
fn write_series_csv(series_dir: &Path, name: &str, values: &[f64]) -> Result<(), String> {
    let path = series_dir.join(format!("{name}.csv"));
    let mut file = fs::File::create(&path)
        .map_err(|e| format!("Failed to create {}: {e}", path.display()))?;

    writeln!(file, "bin_index,value")
        .map_err(|e| format!("Write error: {e}"))?;
    for (t, v) in values.iter().enumerate() {
        writeln!(file, "{t},{v}")
            .map_err(|e| format!("Write error: {e}"))?;
    }

    Ok(())
}

/// Write series/index.json.
fn write_index_json(
    series_dir: &Path,
    grid: &crate::model::GridDefinition,
    series_list: &[(usize, &str)],
    bins: usize,
) -> Result<(), String> {
    let path = series_dir.join("index.json");

    let series_entries: Vec<String> = series_list.iter()
        .map(|(_, name)| {
            format!(
                r#"    {{"id": "{name}", "path": "series/{name}.csv", "points": {bins}}}"#
            )
        })
        .collect();

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
  ]
}}"#,
        bins = grid.bins,
        bin_size = grid.bin_size,
        bin_unit = grid.bin_unit,
        series = series_entries.join(",\n"),
    );

    fs::write(&path, json)
        .map_err(|e| format!("Failed to write index.json: {e}"))?;

    Ok(())
}

/// Write run.json.
fn write_run_json(
    output_dir: &Path,
    grid: &crate::model::GridDefinition,
    series_list: &[(usize, &str)],
    warnings: &[Warning],
) -> Result<(), String> {
    let path = output_dir.join("run.json");

    let series_entries: Vec<String> = series_list.iter()
        .map(|(_, name)| {
            format!(r#"    {{"id": "{name}", "path": "series/{name}.csv"}}"#)
        })
        .collect();

    let warning_entries: Vec<String> = warnings.iter()
        .map(|w| {
            format!(
                r#"    {{"nodeId": "{}", "code": "{}", "message": "{}", "severity": "{}"}}"#,
                w.node_id,
                w.code,
                w.message.replace('"', "\\\""),
                w.severity,
            )
        })
        .collect();

    let json = format!(
        r#"{{
  "schemaVersion": 1,
  "engineVersion": "0.1.0",
  "grid": {{
    "bins": {bins},
    "binSize": {bin_size},
    "binUnit": "{bin_unit}"
  }},
  "warnings": [
{warnings}
  ],
  "series": [
{series}
  ]
}}"#,
        bins = grid.bins,
        bin_size = grid.bin_size,
        bin_unit = grid.bin_unit,
        warnings = warning_entries.join(",\n"),
        series = series_entries.join(",\n"),
    );

    fs::write(&path, json)
        .map_err(|e| format!("Failed to write run.json: {e}"))?;

    Ok(())
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
        let dir = std::env::temp_dir().join(format!("flowtime_test_{}_{n}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn write_simple_model_produces_csvs() {
        let yaml = r#"
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: demand
    kind: const
    values: [10, 20, 30, 40]
  - id: served
    kind: expr
    expr: "demand * 0.8"
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();
        write_artifacts(&dir, &model, &result).unwrap();

        // Check CSV exists and has correct content
        let csv = fs::read_to_string(dir.join("series/demand.csv")).unwrap();
        assert!(csv.starts_with("bin_index,value\n"));
        assert!(csv.contains("0,10"));
        assert!(csv.contains("3,40"));

        let served_csv = fs::read_to_string(dir.join("series/served.csv")).unwrap();
        assert!(served_csv.contains("0,8"));
        assert!(served_csv.contains("3,32"));

        // Cleanup
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn write_produces_valid_index_json() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 5
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();
        write_artifacts(&dir, &model, &result).unwrap();

        let index = fs::read_to_string(dir.join("series/index.json")).unwrap();
        assert!(index.contains("\"schemaVersion\": 1"));
        assert!(index.contains("\"bins\": 3"));
        assert!(index.contains("\"binSize\": 5"));
        assert!(index.contains("\"binUnit\": \"minutes\""));
        assert!(index.contains("\"id\": \"arrivals\""));
        assert!(index.contains("\"points\": 3"));

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn write_produces_run_json_with_warnings() {
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
        assert!(!result.warnings.is_empty(), "Should have stationarity warning");

        let dir = tmp_dir();
        write_artifacts(&dir, &model, &result).unwrap();

        let run = fs::read_to_string(dir.join("run.json")).unwrap();
        assert!(run.contains("\"schemaVersion\": 1"));
        assert!(run.contains("\"engineVersion\": \"0.1.0\""));
        assert!(run.contains("\"non_stationary\""));

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn write_excludes_temp_columns() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: a
    kind: const
    values: [10, 20, 30]
  - id: b
    kind: expr
    expr: "a + 5"
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();
        write_artifacts(&dir, &model, &result).unwrap();

        // Should have a.csv and b.csv but no __temp_*.csv
        let series_dir = dir.join("series");
        let entries: Vec<_> = fs::read_dir(&series_dir).unwrap()
            .filter_map(|e| e.ok())
            .filter(|e| e.path().extension().map_or(false, |ext| ext == "csv"))
            .collect();

        for entry in &entries {
            let name = entry.file_name().to_string_lossy().to_string();
            assert!(!name.starts_with("__temp_"), "Temp column should not be written: {name}");
        }

        assert!(series_dir.join("a.csv").exists());
        assert!(series_dir.join("b.csv").exists());

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn write_empty_model_no_series() {
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
        write_artifacts(&dir, &model, &result).unwrap();

        let index = fs::read_to_string(dir.join("series/index.json")).unwrap();
        assert!(index.contains("\"series\": [\n\n  ]") || index.contains("\"series\": []"));

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn csv_format_precision() {
        let yaml = r#"
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: vals
    kind: const
    values: [0.1, 0.2, 0.30000000000000004]
"#;
        let model = parse_model_yaml(yaml).unwrap();
        let result = eval_model(&model).unwrap();
        let dir = tmp_dir();
        write_artifacts(&dir, &model, &result).unwrap();

        let csv = fs::read_to_string(dir.join("series/vals.csv")).unwrap();
        // Should use '.' decimal separator (invariant culture)
        assert!(csv.contains("0.1"));
        assert!(csv.contains("0.2"));
        // Values use '.' not ',' for decimal
        let lines: Vec<&str> = csv.lines().skip(1).collect(); // skip header
        for line in &lines {
            let val = line.split(',').nth(1).unwrap();
            assert!(val.contains('.') || val.parse::<f64>().is_ok(), "Invalid value format: {val}");
        }

        let _ = fs::remove_dir_all(&dir);
    }
}
