# Service API Foundation

**Status:** Retrospective completed epic grouping.

This folder groups the earliest FlowTime service milestones that established the minimal HTTP surface and then extended it to serve run artifacts without introducing drift from the CLI execution path.

## Themes

- Expose the core engine through a thin, host-agnostic API surface.
- Keep CLI and API behavior aligned through a single evaluation path.
- Make persisted run artifacts readable by UI and external HTTP clients.

## Milestones

- `SVC-M-00.00` — minimal API host with `/run`, `/graph`, and `/healthz` foundations.
- `SVC-M-01.00` — artifact-serving endpoints for run indexes and series streams.

## Notes

- These service milestones depend on the same engine truth as the CLI and deliberately avoid duplicate execution logic.