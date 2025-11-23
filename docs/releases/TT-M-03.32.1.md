# TT‑M‑03.32.1 — First-Class DLQ Nodes & Template Retrofits

**Status:** ✅ Complete  
**Date:** November 23, 2025  
**Branch:** `feature/tt-m-03321`

---

## Overview
Follow-up to TT‑M‑03.32 that finished retry governance. TT‑M‑03.32.1 made DLQs first-class in the schema/runtime and then retrofitted every canonical template so DLQs only appear on services we operate while downstream dependencies surface losses via terminal queues. Analyzer/UI/Sim docs were updated to reflect the new semantics.

## Key Deliverables
1. `kind: dlq` nodes throughout schema/contracts; analyzer enforces terminal-only edges and queue-latency rules for DLQs.
2. Topology canvas renders DLQs distinctly (badge + queue depth) with feature-bar toggle; chips/inspector show alias text.
3. Templates (`it-system-microservices`, `manufacturing-line`, `network-reliability`, `supply-chain-incident-retry`, `transportation-basic`, `supply-chain-multi-tier`) now use backlog-aware queues + operator-owned DLQs/terminal sinks. `templates/` YAMLs updated along with goldens/tests.
4. Docs: milestone & tracking files, template authoring/testing guides, release notes, and new roadmap entries reflect DLQ lens + remaining gaps.
5. New status doc (`docs/architecture/time-travel/status-2025-11-23.md`) captures epic closure + future gaps (EdgeTimeBin, TelemetryLoader, expressions, etc.).

## Tests
| Command | Result |
| --- | --- |
| `dotnet build FlowTime.sln` | ✅ |
| `dotnet test FlowTime.sln` | ✅ (Sim tests skip expected perf cases) |
| `dotnet run --project src/FlowTime.Sim.Cli -- generate ...` (per template) | ✅ |
| `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter TemplateBundleValidationTests` | ✅ |

## Notes
- Analyzer cross-node checks (queue arrivals vs upstream served) deferred to next epic.
- Template catalog remains on FlowTime.Sim API; Engine focuses on model artifacts.  
- Expression extension roadmap + deferred TelemetryLoader work captured in `docs/ROADMAP.md` for future planning.
