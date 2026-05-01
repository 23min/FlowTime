---
id: G-029
title: Template-layer `Legacy*` aliases (Sim authoring-time)
status: addressed
---

### Why this was a gap

Surfaced during M-050 (D-m-E24-02-03, 2026-04-25). `src/FlowTime.Sim.Core/Templates/Template.cs` carries three `Legacy*` properties on the authoring-time `Template` types, each paired with a `ShouldSerialize*` shim:

| Property | YAML alias | C# canonical |
|---|---|---|
| `Template.Node.LegacyExpression` (line 264) | `expression:` | `Expr` (emits as `expr:`) |
| `TemplateOutput.LegacySource` (line 339) | `source:` (in outputs) | `Series` (emits as `series:`) |
| `TemplateOutput.LegacyFilename` (line 348) | `filename:` | `As` (emits as `as:`) |

All three are **dead aliases**:
- `expression:` — zero hits in `templates/*.yaml` (production), zero hits in `tests/**/*.yaml`. Real templates use `expr:`. Note: `examples/*.yaml` and `engine/fixtures/*.yaml` have hits for `expression:`, but those go through the Engine's `ModelParser`, not through `Template`.
- `source:` (in outputs) — zero hits in production templates and tests. The `source:` matches in production templates (9 hits) are all on **nodes** (`nodes[].source: ${telemetryFooSource}`), a separate mechanism on `Template.Node`, not on `TemplateOutput`.
- `filename:` — zero hits in production templates or tests.

The paired `ShouldSerialize*` methods are no-ops at the YAML emitter — YamlDotNet does not honor the `ShouldSerialize{X}()` convention (it is a Json.NET / `XmlSerializer` pattern). See D-m-E24-02-03 for the YamlDotNet investigation.

### Why deferred from M-050

`Template` is authoring-time, pre-substitution. E-24's scope is the post-substitution model boundary (the unified `ModelDto`). Touching `Template` would scope-creep the milestone.

### Status

**Resolved 2026-04-25** via `chore/e-24-cleanup-wave` patch branch:
- `Template.Node.LegacyExpression` (and `ShouldSerializeLegacyExpression`) deleted.
- `TemplateOutput.LegacySource` (and `ShouldSerializeLegacySource`) deleted.
- `TemplateOutput.LegacyFilename` (and `ShouldSerializeLegacyFilename`) deleted.
- Pre-delete sweep confirmed zero live consumers in production templates and Sim tests; the one `filename:` hit in `tests/FlowTime.Sim.Tests/ArtifactHashingTests.cs` flows through `ModelHasher` (raw YAML deserialization to `object?`), not through `Template`. The `LegacyExpression`-named test in `tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs:182` exercises Engine-side `ModelValidator`, not Sim's `Template`, and is unaffected.

---
