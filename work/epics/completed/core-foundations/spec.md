# Core Foundations

**Status:** Retrospective completed epic grouping.

This folder groups the earliest pre-charter engine milestones that established the deterministic DAG runtime, the expression language, the core temporal built-ins, and the benchmarking baseline used to harden that foundation.

## Themes

- Establish deterministic time-binned DAG evaluation as the engine's core execution model.
- Add spreadsheet-style expressions and temporal built-ins without abandoning single-pass evaluation.
- Formalize performance measurement for the foundational engine surface.

## Milestones

- `M-00.00` — initial deterministic DAG and time-bin execution foundation.
- `M-01.00` — expression parser and spreadsheet-style formula authoring.
- `M-01.05` — built-ins and SHIFT support with minimal stateful node infrastructure.
- `M-01.06` — BenchmarkDotNet-based performance benchmarking infrastructure.

## Notes

- These milestones predate the current numbered epic workflow and are grouped retrospectively.
- `M-02.00` is kept separate because PMF support formed its own probabilistic modeling thread.