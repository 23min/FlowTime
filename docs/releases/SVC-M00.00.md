# Release Notes — SVC-M00.00

Date: 2025-08-25

## Summary
- Introduces FlowTime.API with minimal HTTP surface
- Enables API-driven runs without CLI dependency
- Provides foundation for UI integration and automation
- Maintains CLI ↔ API parity via shared FlowTime.Core

## Artifacts
- API: `src/FlowTime.API`
- Endpoints: `/run`, `/graph`, `/healthz`
- Tests: `tests/FlowTime.Api.Tests`

## Usage
- Build: `dotnet build`
- Test: `dotnet test`
- Run API: `dotnet run --project src/FlowTime.API`
- Health check: `GET http://localhost:8080/healthz`
- Run model: `POST http://localhost:8080/run` (YAML body)

## Changes
- New: Minimal ASP.NET Core API host
- New: POST `/run` with YAML model input and JSON output
- New: GET `/graph` for compiled node graph introspection  
- New: GET `/healthz` health check endpoint
- New: Parity tests ensuring CLI ↔ API consistency
- New: JSON response schema with series arrays and metadata

## Compatibility
- Target: .NET 9.0
- Single source of truth: Both CLI and API use FlowTime.Core
- Deterministic: Same model produces identical results across CLI/API
- Content-wise byte parity for equivalent outputs

## API Contract
```
POST /run
Content-Type: text/plain (YAML model)
Response: JSON with grid, series arrays, optional CSV download

GET /graph  
Response: JSON with node/edge structure for debugging

GET /healthz
Response: 200 OK for service health
```

## Next
- UI-M00.00: Blazor WASM integration with API backend
- M1: Contracts parity and artifact alignment
