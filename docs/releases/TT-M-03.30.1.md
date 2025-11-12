# TT‑M‑03.30.1 — Domain Metric Aliases

**Status:** ✅ Complete  
**Date:** November 10, 2025  
**Branch:** `feature/tt-m-0330-1-domain-aliases`

---

## 🎯 Milestone Outcome

Domain-specific aliases now flow from template semantics all the way to the UI. Template authors can map canonical metrics (`attempts`, `served`, `retryEcho`, …) to business-friendly labels, the API surfaces those mappings via `/graph` and `/state_window`, and the topology inspector/tooltips automatically prefer aliases with canonical fallbacks. This closes the terminology gap raised in TT‑M‑03.28 and makes retry/service telemetry legible for ops teams without compromising the standard contract.

## ✅ Highlights

- **Schema & ingestion:** Added `semantics.aliases` to the template schema, ModelService, and ModelParser so alias dictionaries persist once templates are generated (`docs/schemas/template-schema.md`, `src/FlowTime.Contracts/Services/ModelService.cs`, `src/FlowTime.Core/Models/ModelParser.cs`).
- **API contracts:** Extended `GraphNodeSemantics`, `NodeSnapshot`, and `NodeSeries` contracts plus `GraphService`/`StateQueryService` to emit aliases; refreshed Graph + State endpoint goldens/tests.
- **UI integration:** Topology inspector metric cards, dependency lists, and canvas tooltips now show the alias first (chips fall back to canonical labels when aliases are absent); JS helpers and Blazor DTOs carry the alias metadata.
- **Docs & guidance:** Authored `docs/templates/metric-alias-authoring.md`, updated the retry architecture note, milestone doc, tracking doc, and this release entry.

## 📊 Validation

| Command | Outcome | Notes |
| --- | --- | --- |
| `dotnet build FlowTime.sln` | ✅ | |
| `dotnet test FlowTime.sln` | ⚠️ | `FlowTime.Sim.Tests.NodeBased.ModelGenerationTests.GenerateModelAsync_WithConstNodes_EmbedsProvenance` still failing (pre-existing). |

## ⚠️ Known Issues

- The existing FlowTime.Sim provenance test failure remains; this work does not modify the Sim surface and the failure is documented for TT‑M‑03.31 follow-up.
- Alias adoption across every gallery template is staged; this milestone seeds the capability plus the IT incident templates, with broader backfill tracked separately.
