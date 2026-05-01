---
id: D-008
title: Run orchestration defaults to simulation mode
status: accepted
---

**Status:** active
**Context:** Telemetry mode requires capture CSV files on disk (under `examples/time-travel/{captureKey}/`). In dev environments these may not exist, causing 500 errors. Simulation mode always works.
**Decision:** M6 run orchestration defaults to simulation mode for all templates. Telemetry mode support deferred until capture generation workflow is in the UI.
**Consequences:** Runs always succeed but produce synthetic data. Telemetry mode (real CSV data) needs a separate workflow to generate captures first.
