# Release Notes — UI-M0

Date: 2025-08-30

## Summary
- Introduces FlowTime.UI as Blazor WASM single-page application
- Provides minimal observer interface for time-series visualization
- Implements API integration with simulation mode toggle
- Adds structural graph view and micro-DAG visualization
- Enables persistent user preferences and theme support

## Artifacts
- UI: `ui/FlowTime.UI`
- Tests: `ui/FlowTime.UI.Tests`
- Charts: Line charts for time-series data
- Graph: Structural graph view and micro-DAG SVG

## Usage
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project ui/FlowTime.UI`
- Access: Navigate to displayed localhost URL
- Demo: Toggle between API and Simulation modes

## Changes
- New: Blazor WASM SPA with responsive design
- New: Time-series line charts using chart libraries
- New: Structural graph view (table of nodes + degrees)
- New: Micro-DAG visualization (compact SVG rendering)
- New: API vs Simulation mode toggle with feature flag
- New: Persistent preferences (theme, simulation mode, selected model)
- New: Real-time chart updates from API responses
- New: Inline validation and evaluation error display

## Features Delivered
✅ SPA (Blazor WASM)  
✅ Load outputs from API runs (with simulation stub toggle)  
✅ Display time-series in line charts  
✅ Structural graph view (table of nodes + degrees) — pulled early  
✅ Micro-DAG visualization (compact SVG) — pulled early  
✅ Persistent preferences (theme, simulation mode, selected model) — pulled early  
✅ Simulation mode feature flag with deterministic stub — pulled early  

## Acceptance Criteria Met
✅ `dotnet run` for API + UI shows demand/served  
✅ Structural graph invariants test passes  
✅ Micro-DAG renders sources/sinks distinctly  
✅ Simulation vs API toggle switches data source  
✅ Theme + model selection persist across reloads  

## Compatibility
- Target: .NET 9.0 + Blazor WASM
- API Integration: Consumes FlowTime.API endpoints
- Progressive Enhancement: Graceful fallback to local CSV files
- Cross-Platform: Runs in modern web browsers

## Architecture
- Client-side Blazor WASM for responsive interaction
- API-first design consuming `/run` and `/graph` endpoints
- Componentized Razor pages for maintainability
- Local storage for user preferences persistence
- Deterministic simulation stub for offline development

## Next
- UI-M1: Editor basics with YAML schema validation and run button
- M3: Enhanced visualization for backlog and latency metrics
