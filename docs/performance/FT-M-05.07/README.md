# FT-M-05.07 Performance Artifacts

This directory stores reference data, HUD captures, Playwright summaries, and Chrome trace notes that support milestone FT-M-05.07.

| File/Folder | Purpose |
|-------------|---------|
| `playwright-plan.md` | End-to-end automation plan for the hover/pan/scrub latency suite (Task 1.2). |
| `automation.md` | Running notes for the Playwright harness (commands, known failures, CI hooks). |
| `captures/` | Raw diagnostics dumps (`hover-diagnostics_*.json`, `canvas-diagnostics_*.csv`) captured during validation. |
| `traces/` | Chrome performance trace exports (`.json.gz`) once Phase 4 validation runs. |
| `reports/` | Summaries of before/after metrics used in milestone release notes. |

> Keep raw dumps lightweight (≤10 MB per file). Compress Chrome traces if they exceed the repo’s recommended size limits.

## Playwright Harness (Task 1.2)

1. Ensure the FlowTime stack is running locally (API + SIM + UI). By default the UI listens on `http://localhost:5219`. Override `FLOWTIME_UI_BASE_URL` if needed.
2. Install Node deps and Playwright browsers (once per container):
   ```bash
   npm install
   npm run test-ui:install
   ```
3. Run the RED suite (will currently fail because the latency budgets are not met yet):
   ```bash
   npm run test-ui
   ```
   Use `npm run test-ui:debug` for headed debugging with the Playwright inspector.
4. Artifacts (traces, screenshots) land under `out/playwright-artifacts/`.
