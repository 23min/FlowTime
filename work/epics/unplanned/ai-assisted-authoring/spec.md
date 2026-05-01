# Epic: AI-Assisted Authoring (MCP Server-Side Pattern Enforcement)

**ID:** _unplanned (no E-NN assigned)_
**Status:** planning
**Created:** 2026-05-01
**Origin:** absorbs M-10.03 from E-12 Dependency Constraints (dropped on E-12 wrap, 2026-05-01) plus the original M-10.03 Plans 3–5 (resource pooling, compiler expansion, feedback loops). Recorded as the home for "what happens when an AI / external authoring caller asks FlowTime to produce a model."

## Intent

Harden FlowTime's MCP-server surface so any external authoring caller — Claude as a tool, future agent harnesses, scripted authoring pipelines — that asks FlowTime to produce a model receives a result that is **canonical** (matches engine capabilities), **pattern-enforced** (refuses unsupported behaviours with actionable guidance), and **deterministic** (same inputs → same model). The goal is enforcement, not authoring UI: the FlowTime UI is **out of scope**.

The seed scope is the dependency-pattern matrix (Option A vs Option B) M-10.03 was originally going to enforce. Future scope extends to resource pooling, compiler expansion patterns, and feedback-loop refusal — collectively the M-10.03 Plans 3–5 set.

## Why this is a separate epic

- **Different surface from FlowTime UI.** The Svelte workbench (E-21) is for human flow analysis. The expert-authoring-surface unplanned epic (`work/epics/unplanned/expert-authoring-surface/spec.md`) is for human textual model authoring (CodeMirror + inline lenses). Neither caller needs the MCP-pattern-enforcement layer the way an AI caller does — humans get visual feedback as they edit; AIs get pattern enforcement and refusal-with-rationale at the server boundary.
- **Different consumer assumptions.** AI callers will mass-produce model variants (sweeps, fits, what-ifs) and need pattern stability across calls more than humans do. They also need machine-readable refusal reasons to course-correct.
- **Different testing posture.** Pattern-enforcement assertions (refusal-on-feedback-loop, etc.) are deterministic server-side tests; they don't need a Playwright surface.

## Scope (initial)

### Absorbed from M-10.03

- Dependency pattern selector (maps user intent → Option A or Option B helper, refuses unsupported patterns).
- Canonical helper output contracts (Option A: dependency node + arrivals/served/errors + effort edge + derived retry pressure; Option B: constraint registry + node references + deterministic allocation).
- Engine-warning → MCP-hard-error promotion for `missing_dependency_arrivals`, `missing_dependency_served`, `dependency_missing_effort_edges`, `dependency_retry_pressure_missing`.

### Future scope (M-10.03 Plans 3–5, originally out-of-scope at M-10.03 draft time)

- **Plan 3 — Resource pooling:** model multiple services sharing one constrained resource via the constraint registry, including allocation-rule discovery from telemetry signals.
- **Plan 4 — Compiler expansion:** macro-style helpers that expand into canonical multi-node patterns (e.g. retry-with-circuit-breaker → circuit-breaker node + retry edges + recovery semantics).
- **Plan 5 — Feedback loops:** explicit support (or explicit refusal-with-alternatives) for feedback patterns the engine cannot evaluate today.

### Cross-cutting

- Refusal-with-rationale standard: every refused pattern returns a structured response naming the unsupported behaviour, the closest supported alternative, and a pointer to the canonical pattern doc.
- Determinism canary: pattern-helper outputs are byte-pinned for a fixed input set (matches the testing-rigor direction the m-E21-08 dogfooding gap argues for).
- Documentation pass: the `work/epics/completed/ai/mcp-modeling.md` and `work/epics/completed/E-12-dependency-constraints/spec.md` MCP-pattern-matrix section need re-anchoring under this epic's spec rather than two completed-epic locations.

## Out of Scope

- **FlowTime UI surfaces.** Per direction recorded 2026-05-01 — there is no anticipated AI-assist surface inside the FlowTime UI (Svelte or otherwise).
- **Human expert manual authoring.** That's the sibling `work/epics/unplanned/expert-authoring-surface/spec.md` (CodeMirror + inline lenses).
- **The Question-Driven Interface.** That's the sibling `work/epics/ui-question-driven/spec.md` (analytical query panel; LLM integration is a future motivation but not authoring).
- **Engine evaluation behaviour.** This epic enforces pattern shape at authoring time; it does not change how the engine evaluates models.

## Dependencies

- **E-12 Dependency Constraints (complete).** M-10.01 + M-10.02 land the Option A and Option B engine semantics this epic enforces canonically.
- **E-15 Telemetry Ingestion (planned).** Plan 3 (resource pooling) wants telemetry-derived allocation hints; Plan 5 (feedback loops) wants telemetry-validated refusal semantics. Plans 1–2 (the absorbed M-10.03 core) do **not** require E-15.
- **MCP modeling foundation (`work/epics/completed/ai/`, M-08.01–05, complete).** This epic builds on the MCP server work that landed there.

## Sequencing notes

- Not on the immediate critical path. The strategic critical path runs E-15 → Telemetry Loop & Parity → E-22 Model Fit. This epic is parallel-track and likely follows E-22 unless an authoring deadline pulls it in.
- If Plans 1–2 (the absorbed M-10.03 core) are scoped tightly, they could ship as a small standalone milestone at any point — they don't strictly need E-15 first.
- Plans 3–5 should wait for telemetry-driven validation signals so the canonical patterns reflect real-world topologies, not synthetic-template aesthetics.

## ADRs

None at draft time. When this epic gets an E-NN number and starts its first milestone, the dependency-pattern-matrix decision (which is currently inline prose in the absorbed M-10.03 spec) gets promoted to an ADR in `docs/decisions/`.

## Status surfaces

- `work/graph.yaml` — node `ai-assisted-authoring` with `kind: epic`, `status: planning`, `confidence: low`.
- `work/epics/epic-roadmap.md` — listed under UI Paradigm Epics… *no, not under UI Paradigm Epics — that's the wrong section.* Listed under a new **External Authoring Surfaces** sub-section (or appended to "Mid-Term / Aspirational Epics" with an explicit "external surface, not UI" note).
- `ROADMAP.md` — no entry until the epic gets an E-NN number and a sequenced position.
