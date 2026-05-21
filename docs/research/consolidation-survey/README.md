---
title: Consolidation Survey
status: complete
purpose: Code-grounded survey of FlowTime's current architecture (snapshot 2026-05-06) plus the forward-looking redesign proposal that builds on it. Foundation for the Consolidation epic series.
---

# Consolidation Survey

This bundle has two parts:

1. **Survey of the architecture as-of 2026-05-06** (`00`–`11`) — a code-grounded snapshot of how FlowTime is actually built today. Code is treated as truth; documentation is treated as a hint that may have drifted. Every claim cites `path:line`.
2. **Redesign proposal** (`redesign-proposal.md`) — the forward-looking standalone proposal for FlowTime's target architecture and the Consolidation epic series that delivers it.

The survey informs the proposal. The proposal does not reference the survey directly; it stands on its own and is addressed to forward-looking implementation work.

## Document map

### Survey (architecture as-of 2026-05-06)

| File | Scope |
|---|---|
| `00-overview.md` | Executive summary, key findings, reading order |
| `01-process-and-deployment.md` | Binaries, services, ports, CLIs, UIs |
| `02-code-graph.md` | Project dependencies, library surface boundaries |
| `03-template-pipeline.md` | Template → resolved model, parameter substitution |
| `04-engine-runtime.md` | Evaluator, node kinds, expression model |
| `05-validation-stack.md` | Validators, analysers, gates, error contracts |
| `06-run-lifecycle.md` | End-to-end sequences for each entry point |
| `07-storage-and-artifacts.md` | Artifact shapes, formats, storage layout |
| `08-telemetry-and-time-machine.md` | Synthetic adapters, telemetry ingestion, time machine |
| `09-rust-engine.md` | engine/ directory survey, parity status |
| `10-doc-drift.md` | Documented intent vs. code reality |
| `11-redesign-substrate.md` | Architectural seams, change surfaces |

### Proposal

| File | Scope |
|---|---|
| `redesign-proposal.md` | Consolidation — target architecture, principles, eight-epic decomposition, sequencing, definition of done |

## Reading order

For a redesign-oriented read: `redesign-proposal.md` first; dive into `00-overview.md` and `11-redesign-substrate.md` for the substrate behind the proposal; sample `01`–`09` as the proposal references specific subsystems.

For an architecture overview of the current code: `00` → `01` → `02` → `03` → `06`.

For an engine-focused read: `04` → `05` → `03` → `06` → `09`.

For an adjacencies read: `07` → `08` → `09`.

For a "what's broken" read: `10-doc-drift.md` end-to-end.

## Conventions (survey docs)

- Every architectural claim cites `path:line` (or a `path` if it's a whole-file claim).
- Mermaid diagrams: `flowchart` for structure, `sequenceDiagram` for data flow, `classDiagram` for type relationships.
- Doc drift is flagged inline with `> **Drift:** ...` callouts AND aggregated in `10-doc-drift.md`.
- "I couldn't determine X" is acceptable — better than guessing.
- The survey docs document reality only; the redesign proposal is in `redesign-proposal.md`.
