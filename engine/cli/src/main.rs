use std::env;
use std::fs;
use flowtime_core::model;

fn main() {
    let args: Vec<String> = env::args().collect();

    if args.len() < 2 {
        eprintln!("Usage: flowtime-engine <model.yaml>");
        eprintln!("Parses a FlowTime model YAML and prints a summary.");
        std::process::exit(1);
    }

    let path = &args[1];
    let yaml = match fs::read_to_string(path) {
        Ok(content) => content,
        Err(e) => {
            eprintln!("Error reading {path}: {e}");
            std::process::exit(1);
        }
    };

    match model::parse_model_yaml(&yaml) {
        Ok(model) => {
            let grid = model.grid.as_ref();
            println!("Model parsed successfully.");
            if let Some(g) = grid {
                println!("  Grid: {} bins × {} {}", g.bins, g.bin_size, g.bin_unit);
            }
            println!("  Nodes: {}", model.nodes.len());
            if let Some(topo) = &model.topology {
                println!("  Topology nodes: {}", topo.nodes.len());
                println!("  Topology edges: {}", topo.edges.len());
                if !topo.constraints.is_empty() {
                    println!("  Constraints: {}", topo.constraints.len());
                }
            }
            if !model.classes.is_empty() {
                println!("  Classes: {}", model.classes.len());
            }
        }
        Err(e) => {
            eprintln!("Parse error: {e}");
            std::process::exit(1);
        }
    }
}
