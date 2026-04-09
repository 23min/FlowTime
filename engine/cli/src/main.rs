use std::env;
use std::fs;
use flowtime_core::{model, compiler, eval};

fn main() {
    let args: Vec<String> = env::args().collect();

    if args.len() < 2 {
        eprintln!("Usage: flowtime-engine <command> [args]");
        eprintln!("Commands:");
        eprintln!("  parse <model.yaml>  — parse and summarize a model");
        eprintln!("  plan <model.yaml>   — compile and print the evaluation plan");
        eprintln!("  eval <model.yaml>   — compile, evaluate, and print series");
        std::process::exit(1);
    }

    let command = &args[1];
    match command.as_str() {
        "parse" => cmd_parse(&args[2..]),
        "plan" => cmd_plan(&args[2..]),
        "eval" => cmd_eval(&args[2..]),
        // Legacy: bare path treated as parse
        path if path.ends_with(".yaml") || path.ends_with(".yml") => cmd_parse(&args[1..]),
        _ => {
            eprintln!("Unknown command: {command}");
            std::process::exit(1);
        }
    }
}

fn read_model(args: &[String]) -> model::ModelDefinition {
    if args.is_empty() {
        eprintln!("Error: model path required");
        std::process::exit(1);
    }
    let path = &args[0];
    let yaml = fs::read_to_string(path).unwrap_or_else(|e| {
        eprintln!("Error reading {path}: {e}");
        std::process::exit(1);
    });
    model::parse_model_yaml(&yaml).unwrap_or_else(|e| {
        eprintln!("YAML parse error: {e}");
        std::process::exit(1);
    })
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
    let model = read_model(args);
    match compiler::eval_model(&model) {
        Ok(result) => {
            let grid = model.grid.as_ref();
            println!("Evaluation complete.");
            if let Some(g) = grid {
                println!("  Grid: {} bins x {} {}", g.bins, g.bin_size, g.bin_unit);
            }
            // Print each named (non-temp) series
            for (idx, name) in result.column_map.iter() {
                if name.starts_with("__temp_") {
                    continue;
                }
                let series = eval::extract_column(&result.state, idx, result.bins);
                let preview: Vec<String> = series.iter().take(8).map(|v| format!("{v:.2}")).collect();
                let suffix = if series.len() > 8 { " ..." } else { "" };
                println!("  {name}: [{}{}]", preview.join(", "), suffix);
            }
        }
        Err(e) => {
            eprintln!("{e}");
            std::process::exit(1);
        }
    }
}
