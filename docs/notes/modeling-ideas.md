# Modeling Enhancements – Idea Parking Lot

> **Purpose:** Capture pattern ideas, future tooling concepts, and “would be nice” modeling aids that don’t yet have a concrete milestone. When a project needs one of these, promote it into the architecture roadmap or a formal milestone.

## 1. Template Helper Blocks
- **Context:** Advanced expressions (scheduled dispatch, SLA gating, hysteresis) can make templates hard to read.
- **Idea:** Document reusable helper blocks (“macros” by convention). Each block defines a small set of nodes (e.g., `bus_schedule_flag`, `bus_schedule_capacity`) that authors can copy/paste instead of embedding complex MOD/STEP logic inline.
- **Benefits:** Keeps individual expressions short; analyzers can reference well-known node IDs; docs can show graphical explanations for each block.
- **Next Steps:** Curate a “helper block catalog” in `docs/templates/`. Start with bus dispatch and SLA threshold gating once CL‑M‑04.03.02 lands.

## 2. Macro-like Syntax (Future)
- **Concept:** Allow template authors to declare macros with parameters:
  ```yaml
  macros:
    - name: scheduled_dispatch
      params: [source, capacity, period, phase]
      nodes:
        - id: "${source}_schedule_flag"
          kind: expr
          expr: "PULSE(${period}, ${phase}, 1)"
        - id: "${source}_dispatch"
          kind: expr
          expr: "MIN(${source}, ${source}_schedule_flag * ${capacity})"
  ```
- **Runtime Impact:** TemplateService expands macros before schema validation. Requires templating support (similar to parameter substitution) and guardrails against name collisions.
- **Status:** Idea only; revisit after helper blocks prove valuable.

## 3. Modeling Tutor / Authoring Assistant
- **Motivation:** As functions grow, humans need guidance. Long-term, an MCP-backed AI could surface suggestions (“use bus dispatch block here”) or auto-wire macros.
- **Interim approach:** Beef up analyzer messages and doc cross-links so warnings point to helper blocks (“See bus-dispatch helper in docs/templates/...”) before we build full AI assistance.

## 4. Pattern Catalog (Docs)
- **Proposal:** Create a `docs/templates/patterns/` section listing patterns such as:
  - Scheduled dispatch (bus stops)
  - SLA gates (IF/STEP)
  - Hysteresis (HOLD/SMOOTH once implemented)
  - Randomized routing (BERNOULLI/CHOICE)
- Each pattern includes description, YAML snippet, analyzer expectations, and UI screenshots.
- **Status:** To be kicked off after the first helper block ships (tie-in with CL‑M‑04.03.02).

## 5. Parameterized Snippets (IDE support)
- **Future idea:** Provide VS Code snippets or `flow-sim` subcommands (e.g., `flow-sim snippets bus-dispatch`) that output ready-to-use YAML blocks. Keeps duplication low without requiring macro syntax changes.

---

_Add new ideas here when they emerge. Reference this doc in milestone proposals so we don’t lose track of useful concepts._
