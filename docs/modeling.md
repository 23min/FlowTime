# Modeling Documentation Map

> **Purpose:** Explain where different kinds of modeling information live in `docs/`, so concepts, ideas, notes, and guidance are coherent and discoverable.

FlowTime’s documentation is split by **intent**, not by feature area. Use this map when adding new docs or looking for existing guidance.

---

## 1. Concepts (`docs/concepts/`)

Defines the **core modeling abstractions**:

- Time grid, bins, and units.
- Stocks and flows (levels vs rates).
- Node kinds at the idea level (e.g., `service`, `serviceWithBuffer`, expression nodes, routers).
- How queues/buffers fit into the mental model, without committing to specific epics or milestones.

Think of Concepts as the “vocabulary and mental model” section. When you introduce a new core abstraction, define it here.

---

## 2. Architecture (`docs/architecture/`)

Describes **why the engine and UI look the way they do**:

- Whitepaper and foundational design decisions.
- Epic folders (e.g., `service-with-buffer/`, `classes/`, `ptolemy/`).
- Deeper comparisons to other systems and non-goals/guardrails.

Architecture docs assume the reader knows the basic concepts and want to see rationale, trade-offs, and epic-level change plans.

---

## 3. Reference (`docs/reference/`)

Authoritative description of **shipped capabilities and contracts**:

- `engine-capabilities.md` and related capability snapshots.
- Schema and contract descriptions that are not already in `docs/schemas/`.

Reference answers "what is implemented and what is it called?" It should avoid roadmap content and domain-specific modeling advice.

---

## 4. Guides (`docs/guides/`)

Task- and domain-oriented **how-to guides**:

- End-to-end examples for specific domains (e.g., warehouses, cloud queues, call centers).
- Authoring flows (e.g., "from template to run", "interpreting UI topology").

Guides should:

- Use terms defined in Concepts.
- Link to Architecture docs when design rationale matters.
- Link to Notes when there is deeper background that is useful but not required.

---

## 5. Notes (`docs/notes/`)

Opinionated or exploratory **design and modeling notes**:

- Comparisons to other systems (e.g., FlowTime vs Ptolemy).
- Modeling patterns and recommendations that may evolve.
- Idea parking lots and scratchpads that have enough structure to be shared.

Notes are allowed to be essay-like. When a Note becomes stable and broadly applicable, promote parts of it into Concepts and Guides, keeping the Note as a deep-dive reference.

---

## 6. Milestones and Roadmap

- `docs/milestones/` – Concrete implementation slices (e.g., CL-M-04.03.02, SB-M-01) that move Architecture forward.
- `docs/ROADMAP.md` – High-level roadmap view.
- `docs/architecture/epic-roadmap.md` – Architecture-focused epic index.

When a milestone lands, ensure that Concepts, Architecture, Reference, Guides, and relevant Notes are updated or linked, so modeling guidance remains coherent.
