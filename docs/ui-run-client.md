# UI Run Client / API Demo Summary (M0)

This document summarizes the initial implementation of the run client exposed via `ApiDemo.razor`.

## Goals
- Provide a minimal interactive surface to exercise `/run` and `/graph` endpoints.
- Keep UI decoupled from API deployment topology.
- Avoid premature editor complexity (read‑only models for M0).

## Architecture Highlights
- Static YAML models in `wwwroot/models` fetched over the UI origin.
- Separate `HttpClient` for API calls (configurable base URL) to prevent origin bleed.
- `FlowTimeApiClient` no longer mutates its `HttpClient.BaseAddress`; DI supplies a preconfigured instance.
- MudBlazor 8.11.0 upgrade (chart API changes, popover defaults used for selects).

## Component Behavior
- On initialize: load preferences → load models → restore selected model → ready state.
- Model change clears prior run/graph results and saves preference.
- Run: POST `/run` with YAML; capture series, populate table + chart.
- Graph: POST `/graph`; build node view list (source/sink roles) + stats tuple.

## Error Handling
- API or model load failures surface via MudBlazor `Snackbar` with captured message.
- Basic catch around model fetch aggregates any exception into `lastError` field.

## Known Limitations (to address later)
- No YAML validation beyond API response errors.
- No concurrency control (rapid repeated clicks may interleave). Simple busy flags would mitigate.
- No caching beyond in‑memory dictionary of loaded models.
- Chart re-render forced via version integer; could evolve into memoized dataset diffing.

## Next Steps (Optional)
- Add unit / bUnit tests: model load path, preference restore, run success, graph stats correctness.
- Introduce cancellation token support for in-flight run/graph requests.
- Add copy/download affordances for YAML and CSV.
- Persist panel expansion preferences.

## Merge Checklist
- [ ] Docs updated (this file + `docs/UI.md`).
- [ ] All tests pass (`dotnet test`).
- [ ] Working directory clean.
- [ ] Confirm no remaining tab characters in YAML models.
- [ ] Consider adding `.gitattributes` normalization before wider contribution.

---
Generated at M0 UI wrap-up.