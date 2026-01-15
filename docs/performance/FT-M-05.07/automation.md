# Task 1.2 Automation Notes

- `package.json` + Playwright config scaffolded (`npm install` required locally; `node_modules/` is gitignored).
- RED spec `tests/ui/specs/topology-latency.spec.ts` asserts pointer INP and overlay counts; currently fails until Phase 1 tasks finish.
- Browsers installed via `npm run test-ui:install` (installs Chromium + deps inside the dev container).
- Running `npm run test-ui` requires the FlowTime stack to be running inside the dev container (`dotnet run` for FlowTime.API/Sim/UI).
  - If the UI is not up, Playwright will time out waiting for `canvas[data-topology-canvas]` (see `out/playwright-artifacts/topology-latency-*/error-context.md`).
  - Once the UI is available, expect the spec to fail on the latency thresholds until Tasks 1.3+ land.
