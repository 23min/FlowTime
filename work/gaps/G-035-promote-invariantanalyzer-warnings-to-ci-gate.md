---
id: G-035
title: Promote InvariantAnalyzer warnings to CI gate
status: open
---

## What's missing

`InvariantAnalyzer` (`src/FlowTime.Core/Analysis/InvariantAnalyzer.cs`) and `TemplateInvariantAnalyzer` (`src/FlowTime.Sim.Core/Analysis/TemplateInvariantAnalyzer.cs`) run inside every model evaluation and emit `InvariantWarning[]` into the run artifact. There is no automated gate that fails CI when a representative-template evaluation produces warnings — only targeted unit tests in `InvariantAnalyzerTests` and `EvaluationIntegrityTemplateTests` cover specific scenarios.

Add a CI step (in `.github/workflows/build.yml`, alongside the existing per-project test runs) that:

1. Loads representative model templates (the same set the Sim regression suite uses).
2. Runs `TemplateInvariantAnalyzer.Analyze`.
3. Fails the job if `InvariantAnalysisResult.Warnings` is non-empty (or filters to a defined allowlist of expected-warning templates and fails on any unexpected warning).

## Why it matters

Conservation/sanity violations in evaluated series are the kind of regression that:

- Is silent at the test level — `dotnet test` passes even when a new template introduces an unintended warning, because no test asserts "no warnings."
- Surfaces only when someone reads the run artifact, which means it can ship to a release before being noticed.
- Has happened historically (the rule "run analyzers and document the outcome before wrapping" appeared in `milestone-rules-quick-ref.md` for exactly this reason).

Promoting the warnings to a build-failing gate makes the analyzer's existing output load-bearing instead of advisory, and removes the need for human discipline at milestone wrap.

## Discovered in

Surfaced 2026-05-03 during the docs-aiwf-cleanup audit when reviewing `docs/development/milestone-rules-quick-ref.md` line 122 — that rule prescribed manual analyzer runs at milestone wrap, which the audit recommended replacing with mechanical CI enforcement.
