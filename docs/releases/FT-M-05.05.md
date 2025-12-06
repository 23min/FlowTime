# Release FT-M-05.05 — Router Flow Solidification

**Release Date:** 2025-12-06  
**Type:** Milestone delivery  
**Canonical Runs:** `data/runs/run_transportation-basic-classes_0e29c545`, `data/runs/run_supply-chain-multi-tier-classes_ecc81d58`

## Overview

FT-M-05.05 removes the last manual percentage splits from the class-enabled transportation and supply-chain templates so routers become the single source of downstream inflows. All engine entry points (CLI, API, Sim analyzer) now re-evaluate graphs with router overrides, guaranteeing downstream queues/services consume the routed series without duplicating math. Template authoring guidance was refreshed and a dedicated release note documents the migration so future templates adopt the router pattern immediately.

## Key Changes

1. **Router-aware evaluation:** FlowTime.Cli, FlowTime.API `/v1/run`, and `TemplateInvariantAnalyzer` call the shared `RouterAwareGraphEvaluator`, ensuring router overrides apply before artifacts or analyzers run. CLI/API parity tests now cover router scenarios.
2. **Template retrofits:**  
   - `templates/transportation-basic-classes.yaml` v3.1.0 drops `splitAirport`/`splitIndustrial` parameters plus the derived `hub_dispatch_*` expressions; `HubDispatchRouter` feeds dispatch queues directly.  
   - `templates/supply-chain-multi-tier-classes.yaml` v3.1.0 removes the `restock/recover/scrap` split math so `ReturnsRouter` owns all downstream inflows.  
   Regenerated canonical specs (`data/models/...`) and deterministic runs exhibit `classCoverage: "full"` with zero router warnings.
3. **Docs & release collateral:** `templates/README.md` and `docs/templates/template-authoring.md` describe the router-driven pattern (targets reference queue demand nodes; downstream expressions stay zero). This release note captures the migration details for milestone tracking and future authors.

## Tests

- `dotnet build`
- `dotnet test --nologo`
- Targeted suites: `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter RouterTemplateRegressionTests`
- CLI/API parity tests (router scenarios) run as part of the full solution test pass.

## Verification

- `flow-sim generate --id transportation-basic-classes --mode simulation --out data/models/transportation-basic-classes-model.yaml`
- `flowtime run data/models/transportation-basic-classes-model.yaml --deterministic-run-id --seed 4242`
- `flow-sim generate --id supply-chain-multi-tier-classes --mode simulation --out data/models/supply-chain-multi-tier-classes-model.yaml`
- `flowtime run data/models/supply-chain-multi-tier-classes-model.yaml --deterministic-run-id --seed 6789`

Both canonical runs emit no router diagnostics, and RouterTemplateRegressionTests confirm downstream queues rely solely on router overrides.
