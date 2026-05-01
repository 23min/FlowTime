---
id: G-027
title: '`ProvenanceEmbedder` parallel CLI path (Sim, dead code)'
status: addressed
---

### Why this was a gap

Surfaced during M-050 step 4 (deferred); confirmed during step-6 wrap audit. `src/FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs` is a static helper that injects a `provenance:` block into a YAML string by inserting lines after `schemaVersion:`. It uses string-template emission (not `ProvenanceDto` serialization) and a separate `ProvenanceMetadata` type (in `src/FlowTime.Sim.Core/Models/`) with its own field names — including `source`, `schemaVersion`, `templateTitle` that the M-049 Q5/A4 ratified shape **drops**.

Status check (verified 2026-04-25):
- `grep -rn "ProvenanceEmbedder\." src/ tests/` — **zero hits**. The static method has no callers.
- `--embed-provenance` CLI flag in `Sim.Cli/Program.cs:744` carries help text: `"(Legacy) provenance is always embedded; flag retained for compatibility"`. The flag is parsed and stored, but it does not invoke `ProvenanceEmbedder` anywhere.
- The unified `SimModelBuilder` → `ModelDto` → `ProvenanceDto` path (M-050 step 4) is the sole live emission path.

So `ProvenanceEmbedder` is a latent **wrong-shape emitter** — if anything ever resurrects the call site, it would emit a provenance block that violates the post-m-E24-02 contract (snake_case hangover from earlier shape, `source`/`schemaVersion`/`templateTitle` keys that AC6 drops).

### Why deferred from M-050

Out of step-4 scope (parallel CLI path; the milestone owned the primary path). Confirmed dead during the step-6 wrap audit, after the milestone's order-of-work was already locked.

### Status

**Resolved 2026-04-25** via `chore/e-24-cleanup-wave` patch branch:
- `src/FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs` deleted.
- `src/FlowTime.Sim.Core/Models/ProvenanceMetadata.cs` retained — has live consumers via `IProvenanceService.CreateProvenance` (`ProvenanceService`, `RunOrchestrationService`, `ProvenanceServiceTests`); only the dead emitter helper was removable.
- `--embed-provenance` flag, `EmbedProvenance` field on `CliOptions`, mutual-exclusivity validation, help text, and example removed from `src/FlowTime.Sim.Cli/Program.cs`. Five CLI tests covering the now-removed flag (`ArgParser_ParsesEmbedProvenanceFlag`, `ArgParser_ParsesBothProvenanceOptions`, `Generate_WithEmbedProvenanceFlag_EmbedsProvenanceInModel`, `Generate_WithEmbedProvenance_NoSeparateProvenanceFile`, `Generate_WithBothProvenanceOptions_ReturnsError`) deleted from `tests/FlowTime.Sim.Tests/Cli/GenerateProvenanceTests.cs`. Service-side `?embed_provenance=` query-string shim left in place (separate concern, documented as legacy in `FlowTime.Sim.Service.http`).

---
