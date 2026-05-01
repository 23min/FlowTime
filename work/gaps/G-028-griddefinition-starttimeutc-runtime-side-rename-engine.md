---
id: G-028
title: '`GridDefinition.StartTimeUtc` runtime-side rename (Engine)'
status: addressed
---

### Why this was a gap

Surfaced during M-050 step 2 — D-m-E24-02-03 ratified a deliberate wire/runtime naming asymmetry: the wire DTO `GridDto.StartTimeUtc` was renamed to `Start` (so the camelCase convention emits `start:`, matching production templates), but the runtime model `GridDefinition.StartTimeUtc` (in `src/FlowTime.Core/Models/ModelParser.cs:577`) kept its name. The boundary is `ModelService.ConvertToModelDefinition`: `StartTimeUtc = model.Grid.Start`.

The asymmetry is real but cosmetic. Same concept, two names across the wire/runtime boundary. Readers of `model.Grid.Start` (wire) and `runtimeModel.Grid.StartTimeUtc` (runtime) see different identifiers for the same semantic.

### Why out of E-24 scope

E-24's scope is the post-substitution wire shape. The runtime model is the consumer of that shape, not part of it. Renaming the runtime property would touch `ModelParser`, every Engine evaluator that reads grid start, and a handful of tests authoring `ModelDefinition` directly. Out of step-2 budget at the time; carved out per D-m-E24-02-03.

### Status

**Resolved 2026-04-25** via `chore/e-24-cleanup-wave` patch branch:
- `GridDefinition.StartTimeUtc` → `GridDefinition.Start` in `src/FlowTime.Core/Models/ModelParser.cs`.
- `ModelService.ConvertToModelDefinition` (`src/FlowTime.Contracts/Services/ModelService.cs`) updated; the wire/runtime asymmetry comment is now obsolete and was removed.
- `TelemetryCapture.BuildWindow` (`src/FlowTime.TimeMachine/TelemetryCapture.cs:200`) and `FixtureModelLoader` (`src/FlowTime.Core/Fixtures/FixtureModelLoader.cs:64`) updated to read `model.Grid.Start`.
- `ModelParserTopologyTests` (`tests/FlowTime.Core.Tests/Parsing/ModelParserTopologyTests.cs`) test fixtures construct `GridDefinition { Start = ... }` directly.
- `TelemetryManifestWindow.StartTimeUtc`, `FixtureWindow.StartTimeUtc`, and `CaptureManifestWriter` record-parameter `StartTimeUtc` left untouched per their gap-entry carve-out (different types; live under `window:` blocks).
- Bundled with the `ProvenanceEmbedder` delete and the Template-layer `Legacy*` cleanup as the rationale predicted.

---
