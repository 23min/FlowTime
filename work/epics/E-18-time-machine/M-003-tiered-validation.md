---
id: M-003
title: Tiered Validation
status: done
parent: E-18
acs:
  - id: AC-1
    title: TimeMachineValidator.Validate(yaml, ValidationTier.Schema) returns
    status: met
  - id: AC-2
    title: TimeMachineValidator.Validate(yaml, ValidationTier.Compile) catches
    status: met
  - id: AC-3
    title: TimeMachineValidator.Validate(yaml, ValidationTier.Analyse) returns
    status: met
  - id: AC-4
    title: POST /v1/validate responds 200 with { isValid, tier, errors, warnings
    status: met
  - id: AC-5
    title: Invalid tier value → 400 Bad Request
    status: met
  - id: AC-6
    title: Empty/null yaml → 400 Bad Request
    status: met
  - id: AC-7
    title: Rust session validate_schema returns { is_valid, errors } without
    status: met
  - id: AC-8
    title: rg "FlowTime\.Generator" src/ tests/ still zero (no regressions)
    status: met
  - id: AC-9
    title: dotnet test FlowTime.sln all green; Rust cargo test all green
    status: met
---

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

## Acceptance criteria

### AC-1 — TimeMachineValidator.Validate(yaml, ValidationTier.Schema) returns

`TimeMachineValidator.Validate(yaml, ValidationTier.Schema)` returns errors for invalid YAML
### AC-2 — TimeMachineValidator.Validate(yaml, ValidationTier.Compile) catches

`TimeMachineValidator.Validate(yaml, ValidationTier.Compile)` catches structural errors (bad node refs, bad expressions)
### AC-3 — TimeMachineValidator.Validate(yaml, ValidationTier.Analyse) returns

`TimeMachineValidator.Validate(yaml, ValidationTier.Analyse)` returns warnings from invariant analyzer
### AC-4 — POST /v1/validate responds 200 with { isValid, tier, errors, warnings

`POST /v1/validate` responds 200 with `{ isValid, tier, errors, warnings }` for all three tiers
### AC-5 — Invalid tier value → 400 Bad Request

### AC-6 — Empty/null yaml → 400 Bad Request

### AC-7 — Rust session validate_schema returns { is_valid, errors } without

Rust session `validate_schema` returns `{ is_valid, errors }` without full compile
### AC-8 — rg "FlowTime\.Generator" src/ tests/ still zero (no regressions)

`rg "FlowTime\.Generator" src/ tests/` still zero (no regressions)
### AC-9 — dotnet test FlowTime.sln all green; Rust cargo test all green

`dotnet test FlowTime.sln` all green; Rust `cargo test` all green
