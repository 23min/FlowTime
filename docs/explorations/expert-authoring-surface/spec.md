# Epic: Expert Authoring Surface

**Status:** draft
**Depends on:** E-18 Time Machine session and validation foundations, [ui-workbench](../ui-workbench/spec.md), [ui-analytical-views](../ui-analytical-views/spec.md), and the Svelte-first surface direction already captured in current planning
**Architecture:** [docs/research/expert-authoring-surface.md](../../../../docs/research/expert-authoring-surface.md)
**Reference:** [reference/session-patch-model.md](reference/session-patch-model.md)

---

## Intent

Add an expert-only authoring surface that mixes compact textual authoring with
inline analytical feedback, while remaining fully aligned with the Time Machine,
the DAG/workbench, and the canonical FlowTime export boundary.

This is not a replacement for the existing UI direction. It is a specialized
surface for expert modelers and AI-assisted iteration.

## Why This Epic Exists

FlowTime already has several prerequisites for an expert authoring loop:

- a deterministic execution substrate in the Time Machine
- tiered validation and reevaluation work in E-18
- an interactive what-if loop in the Svelte UI
- a DAG/workbench direction for broader analytical use

What is missing is the expert shell that puts compact authored changes and
their first visible consequences in the same field of view.

## Goals

### G1: Expert text surface over a structured session model

Provide an expert editor that is compact, composable, and fast to iterate in,
while compiling to a structured session model instead of becoming execution
truth directly.

Requirements:

- editor text parses into a session AST with stable statement and entity IDs
- source spans are preserved so runtime facts can point back to authored statements
- direct manipulation actions rewrite source or session patches instead of creating hidden state
- any JavaScript-like shell remains an authoring convenience only; arbitrary user code never becomes Time Machine truth

### G2: Long-lived session plus deterministic patch application

The editor must talk to a long-lived session that supports small changes
efficiently.

Requirements:

- edits lower to deterministic patch operations
- patches apply to a long-lived Time Machine session
- reevaluation reuses session state wherever valid
- validation and diagnostics can run cheaply on every edit cycle

### G3: Inline analytical lenses

The expert surface must provide local analytical feedback without duplicating
the workbench or DAG.

Requirements:

- default inline lenses after key semantic statements such as capacity, arrivals, routing, and retry
- explicit lens requests for richer views at a statement site
- inline warnings, deltas, and downstream impact summaries
- all inline views use the same evaluation result as the DAG/workbench

### G4: DAG and workbench synchronization

The expert surface must stay connected to the rest of the analytical UI.

Requirements:

- selecting a node in the DAG highlights its authored statement(s)
- editing a statement highlights affected nodes and edges in the DAG
- inline lenses can pin into the workbench
- workbench cards can link back to the authored statement that drives them

### G5: Explicit export boundary

The expert surface must end in the canonical FlowTime world, not a second
parallel truth system.

Requirements:

- users can snapshot the session into a canonical model
- exported models can be diffed against the current canonical version
- export runs through the normal validation and artifact pipeline
- session-only metadata such as lenses or pinning does not pollute canonical output

### G6: Shared human + AI session protocol

The session model must support both human and AI iteration.

Requirements:

- AI operates on structured patches, not raw text as its primary protocol
- source spans and stable IDs let AI proposals map back to the editor cleanly
- AI and human changes flow through the same validation and reevaluation substrate

## First-Cut User Experience

The first cut should be intentionally narrow.

### First-cut browser shell

The exploratory browser implementation should use **CodeMirror 6** as the
editor shell.

Why this is the right first cut:

- inline and block-attached visuals are central to the concept
- CodeMirror is a better fit than Monaco for DOM-heavy decorations and embedded visual widgets
- proving code-local analytical feedback is more important than matching a VS Code-like editor feel in the first experiment

This is a spike-level UI choice, not a permanent contract decision. The session
AST, patch model, and export boundary remain editor-agnostic.

### Expert editing flow

1. Open an expert session from a model or template.
2. Edit capacity, retry, routing, or telemetry bindings in a compact authoring area.
3. See inline sparkline, delta, and warning feedback update almost immediately.
4. Observe downstream impact in the DAG and pin affected nodes into the workbench.
5. Export the chosen state back to the canonical model.

### Direct manipulation flow

1. Select a node in the DAG.
2. Drag or edit a capacity control.
3. The session emits a patch and rewrites the owning statement or managed source block.
4. The edited statement, inline lens, and DAG all update from the same session result.

### AI-assisted flow

1. An AI agent receives an objective.
2. It submits structured patches against the same expert session.
3. The user sees the proposed changes as source edits and inline effects.
4. The user accepts, rejects, or modifies the proposal before export.

## Architecture Constraints

### One runtime

Do not build a second execution runtime for this surface. Reuse the Time
Machine session and validation infrastructure.

### One analytical result stream

Inline lenses, DAG overlays, and workbench cards must project from the same
evaluation result. No lens-only analytics path.

### One truth boundary

The canonical model and run artifacts remain the durable truth. The expert
session is ephemeral until export.

### One structured session contract

The editor shell is replaceable. The stable asset is the session AST, patch
model, source-span mapping, and export boundary.

### Preferred spike stack

For the first browser experiment, prefer:

- CodeMirror 6 as the editor
- inline widgets for sparklines, warning chips, and small badges
- block widgets or equivalent line-attached panels for larger local visuals
- a scroll-synced background or overlay rendering layer for broader analytical context

## Non-Goals

- making this the default UI for all FlowTime users
- replacing the DAG with code-only authoring
- shipping a general-purpose scripting runtime inside the Time Machine
- storing lens state, pin state, or editor-only layout in canonical exports
- building free-form chatbot behavior as part of the first cut
- solving every possible authoring syntax before the session substrate is proven

## Open Questions

1. What is the smallest useful expert syntax that still feels materially better than forms?
2. How much reparsing should happen incrementally versus statement-group recompilation?
3. Which inline lenses are mandatory for the first cut: capacity, queue depth, utilization, downstream impact?
4. Should direct manipulation always rewrite authored source, or may some actions live in a managed patch layer first?
5. Does the first cut need TUI support, or should it stay browser-only until the session model stabilizes?

## Likely Milestones

1. **Session substrate spike**
   Expert editor shell, session AST, stable IDs, source spans, patch transport.

2. **Inline lens MVP**
   Default sparkline lenses, warning chips, downstream impact summary.

3. **DAG/workbench sync**
   Selection mapping, pinning, source-to-node round-tripping.

4. **Export + diff boundary**
   Snapshot to canonical model, compare against current model, run validation.

5. **AI patch loop**
   Shared structured operations for human and AI-assisted iteration.

6. **Syntax hardening**
   Only after the workflow proves useful should the authoring syntax itself become a stable product surface.