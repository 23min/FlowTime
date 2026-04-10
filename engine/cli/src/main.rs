use std::env;
use std::fs;
use std::path::Path;
use flowtime_core::{model, compiler, eval, writer};

fn main() {
    let args: Vec<String> = env::args().collect();

    if args.len() < 2 {
        eprintln!("Usage: flowtime-engine <command> [args]");
        eprintln!("Commands:");
        eprintln!("  parse <model.yaml>                — parse and summarize a model");
        eprintln!("  plan <model.yaml>                 — compile and print the evaluation plan");
        eprintln!("  eval <model.yaml> [--output <dir>] — evaluate (optionally write artifacts)");
        eprintln!("  validate <model.yaml>             — parse, compile, analyze (no artifacts)");
        std::process::exit(1);
    }

    let command = &args[1];
    match command.as_str() {
        "parse" => cmd_parse(&args[2..]),
        "plan" => cmd_plan(&args[2..]),
        "eval" => cmd_eval(&args[2..]),
        "validate" => cmd_validate(&args[2..]),
        // Legacy: bare path treated as parse
        path if path.ends_with(".yaml") || path.ends_with(".yml") => cmd_parse(&args[1..]),
        _ => {
            eprintln!("Unknown command: {command}");
            std::process::exit(1);
        }
    }
}

fn read_model_yaml(args: &[String]) -> String {
    if args.is_empty() {
        eprintln!("Error: model path required");
        std::process::exit(1);
    }
    let path = &args[0];
    fs::read_to_string(path).unwrap_or_else(|e| {
        eprintln!("Error reading {path}: {e}");
        std::process::exit(1);
    })
}

fn read_model(args: &[String]) -> model::ModelDefinition {
    let yaml = read_model_yaml(args);
    model::parse_model_yaml(&yaml).unwrap_or_else(|e| {
        eprintln!("YAML parse error: {e}");
        std::process::exit(1);
    })
}

/// Parse --output <dir> from args after the model path. Returns (model_path_args, output_dir).
fn parse_output_flag(args: &[String]) -> (&[String], Option<String>) {
    for (i, arg) in args.iter().enumerate() {
        if arg == "--output" {
            if i + 1 < args.len() {
                return (&args[..i], Some(args[i + 1].clone()));
            } else {
                eprintln!("Error: --output requires a directory argument");
                std::process::exit(1);
            }
        }
    }
    (args, None)
}

fn cmd_parse(args: &[String]) {
    let model = read_model(args);
    let grid = model.grid.as_ref();
    println!("Model parsed successfully.");
    if let Some(g) = grid {
        println!("  Grid: {} bins x {} {}", g.bins, g.bin_size, g.bin_unit);
    }
    println!("  Nodes: {}", model.nodes.len());
    if let Some(topo) = &model.topology {
        println!("  Topology nodes: {}", topo.nodes.len());
        println!("  Topology edges: {}", topo.edges.len());
    }
}

fn cmd_plan(args: &[String]) {
    let model = read_model(args);
    match compiler::compile(&model) {
        Ok(plan) => print!("{}", plan.format()),
        Err(e) => {
            eprintln!("{e}");
            std::process::exit(1);
        }
    }
}

fn cmd_eval(args: &[String]) {
    let (model_args, output_dir) = parse_output_flag(args);
    let yaml = read_model_yaml(model_args);
    let model = model::parse_model_yaml(&yaml).unwrap_or_else(|e| {
        eprintln!("YAML parse error: {e}");
        std::process::exit(1);
    });
    match compiler::eval_model(&model) {
        Ok(result) => {
            if let Some(dir) = output_dir {
                // Write artifacts to output directory (with YAML text for SHA256 hashing)
                match writer::write_artifacts_with_yaml(Path::new(&dir), &model, &result, Some(&yaml)) {
                    Ok(()) => {
                        let series_count = result.column_map.iter()
                            .filter(|(_, name)| !name.starts_with("__temp_"))
                            .count();
                        println!("Artifacts written to {dir}/");
                        println!("  Series: {series_count} files");
                        if !result.warnings.is_empty() {
                            println!("  Warnings: {}", result.warnings.len());
                        }
                    }
                    Err(e) => {
                        eprintln!("Error writing artifacts: {e}");
                        std::process::exit(1);
                    }
                }
            } else {
                // Print summary to stdout (existing behavior)
                let grid = model.grid.as_ref();
                println!("Evaluation complete.");
                if let Some(g) = grid {
                    println!("  Grid: {} bins x {} {}", g.bins, g.bin_size, g.bin_unit);
                }
                for (idx, name) in result.column_map.iter() {
                    if name.starts_with("__temp_") {
                        continue;
                    }
                    let series = eval::extract_column(&result.state, idx, result.bins);
                    let preview: Vec<String> = series.iter().take(8).map(|v| format!("{v:.2}")).collect();
                    let suffix = if series.len() > 8 { " ..." } else { "" };
                    println!("  {name}: [{}{}]", preview.join(", "), suffix);
                }
                if !result.warnings.is_empty() {
                    println!("Warnings ({}):", result.warnings.len());
                    for w in &result.warnings {
                        println!("  [{}/{}] {}", w.severity, w.code, w.message);
                    }
                }
            }
        }
        Err(e) => {
            eprintln!("{e}");
            std::process::exit(1);
        }
    }
}

fn cmd_validate(args: &[String]) {
    let model = read_model(args);
    match compiler::eval_model(&model) {
        Ok(result) => {
            if result.warnings.is_empty() {
                println!("{{\"valid\": true, \"warnings\": []}}");
            } else {
                let warning_json: Vec<String> = result.warnings.iter()
                    .map(|w| format!(
                        r#"    {{"nodeId": "{}", "code": "{}", "message": "{}", "severity": "{}"}}"#,
                        w.node_id, w.code, w.message.replace('"', "\\\""), w.severity
                    ))
                    .collect();
                println!("{{\n  \"valid\": true,\n  \"warnings\": [\n{}\n  ]\n}}", warning_json.join(",\n"));
            }
        }
        Err(e) => {
            eprintln!("{{\n  \"valid\": false,\n  \"error\": \"{}\"\n}}", e.0.replace('"', "\\\""));
            std::process::exit(1);
        }
    }
}
