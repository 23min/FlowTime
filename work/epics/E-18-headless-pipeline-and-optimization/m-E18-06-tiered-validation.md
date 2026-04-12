# m-E18-06 — Tiered Validation

**Epic:** E-18 Time Machine  
**Branch:** `milestone/m-E18-06-tiered-validation`  
**Status:** in-progress

## Goal

Expose model validation as a first-class, client-agnostic Time Machine operation.
Three tiers callable from the .NET SDK and from a new `POST /v1/validate` HTTP
endpoint. Tier 1 (schema) is also added to the Rust engine session protocol so
the Svelte What-If UI can get cheap per-edit feedback without a full compile.

## Scope

**Tier 1 — Schema:** YAML parses + JSON schema validates + class references resolve.
Backed by `ModelSchemaValidator.Validate` + `ModelValidator.Validate` in Core.
Cheap: no compile, no eval.

**Tier 2 — Compile:** Schema (tier 1) + model compiles into a Graph.
Backed by `ModelCompiler.Compile` + `ModelParser.ParseModel` in Core.
Catches structural errors (unresolved references, expression errors).

**Tier 3 — Analyse:** Compile (tier 2) + deterministic evaluation + invariant
checks. Backed by `TemplateInvariantAnalyzer.Analyze` in Sim.Core.
Catches semantic issues (conservation violations, capacity breaches).

**In scope:**
- `src/FlowTime.TimeMachine/Validation/` — `TimeMachineValidator` (static service),
  `ValidationResult`, `ValidationError`, `ValidationWarning`, `ValidationTier` enum
- `src/FlowTime.API/Endpoints/ValidationEndpoints.cs` — `POST /v1/validate`
- Rust engine session — new `validate_schema` command (tier 1 via session protocol)
- Unit tests: `tests/FlowTime.TimeMachine.Tests/Validation/`
- API tests: `tests/FlowTime.Api.Tests/ValidationEndpointsTests.cs`
- Rust integration tests: session `validate_schema` command

**Out of scope:**
- Line/column mapping in error messages
- Editor LSP integration
- Svelte UI changes (validate button) — separate UI milestone

## Contract

### HTTP Endpoint

```
POST /v1/validate
Content-Type: application/json

{
  "yaml": "...",
  "tier": "schema" | "compile" | "analyse"
}
```

Response (200 always, errors in body):

```json
{
  "tier": "schema",
  "isValid": false,
  "errors": [
    { "message": "Unknown class reference: 'premium'" }
  ],
  "warnings": []
}
```

Tier 3 analyse response includes warnings in addition to errors:

```json
{
  "tier": "analyse",
  "isValid": true,
  "errors": [],
  "warnings": [
    { "nodeId": "Queue", "code": "high_utilization", "message": "..." }
  ]
}
```

### Session Protocol Command (`validate_schema`)

```
request: { method: "validate_schema", params: { yaml: "..." } }
response (valid):   { result: { is_valid: true, errors: [] } }
response (invalid): { result: { is_valid: false, errors: ["..."] } }
```

Tier 2 (compile) is already served by the existing `compile` command, which
returns `error: { code: "compile_error", ... }` on failure.

## Acceptance Criteria

- [ ] `TimeMachineValidator.Validate(yaml, ValidationTier.Schema)` returns errors for invalid YAML
- [ ] `TimeMachineValidator.Validate(yaml, ValidationTier.Compile)` catches structural errors (bad node refs, bad expressions)
- [ ] `TimeMachineValidator.Validate(yaml, ValidationTier.Analyse)` returns warnings from invariant analyzer
- [ ] `POST /v1/validate` responds 200 with `{ isValid, tier, errors, warnings }` for all three tiers
- [ ] Invalid tier value → 400 Bad Request
- [ ] Empty/null yaml → 400 Bad Request
- [ ] Rust session `validate_schema` returns `{ is_valid, errors }` without full compile
- [ ] `rg "FlowTime\.Generator" src/ tests/` still zero (no regressions)
- [ ] `dotnet test FlowTime.sln` all green; Rust `cargo test` all green
