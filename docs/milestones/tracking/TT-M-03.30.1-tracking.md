# TT‑M‑03.30.1 Implementation Tracking — Domain Metric Aliases

**Milestone:** `docs/milestones/TT-M-03.30.1.md`  
**Status:** ✅ Complete  
**Branch:** `feature/tt-m-0330-1-domain-aliases`

## Task Checklist

- [x] **Schema + Model ingestion** — Added `semantics.aliases` surface in template/schema docs (`docs/schemas/template-schema.md`, `docs/schemas/model.schema.yaml` reference) and thread through `ModelService`/`ModelParser` so alias dictionaries survive YAML ➜ engine ingestion.
- [x] **API + Contracts** — Extended `GraphNodeSemantics`, `NodeSnapshot`, and `NodeSeries` contracts plus `GraphService`/`StateQueryService` to emit aliases; refreshed Graph + State endpoint goldens/tests (`tests/FlowTime.Api.Tests`).
- [x] **UI Consumption** — Topology inspector, dependency tables, and canvas tooltips prefer domain aliases with canonical fallback; Blazor + JS models now carry alias metadata and unit/component tests cover alias output (`tests/FlowTime.UI.Tests`).
- [x] **Docs + Release** — Authored template maintainer guide (`docs/templates/metric-alias-authoring.md`), updated retry architecture + milestone doc, and published release note `docs/releases/TT-M-03.30.1.md`.

## Verification

| Command | Result | Notes |
| --- | --- | --- |
| `dotnet build FlowTime.sln` | ✅ | |
| `dotnet test FlowTime.sln` | ⚠️ | `FlowTime.Sim.Tests.NodeBased.ModelGenerationTests.GenerateModelAsync_WithConstNodes_EmbedsProvenance` still failing (pre-existing; documented). |

## References

- Milestone: `docs/milestones/TT-M-03.30.1.md`
- Schema guide: `docs/schemas/template-schema.md`
- Authoring guide: `docs/templates/metric-alias-authoring.md`
- Release note: `docs/releases/TT-M-03.30.1.md`
