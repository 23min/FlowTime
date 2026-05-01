# MCP Server Best Practices (FlowTime Guidance)

**Status:** Draft  
**Source:** Derived from provided slide captures and general MCP/agent tooling practice.  
**Note:** This document reflects principles shown in the attached images. It does **not** quote or summarize the full talk because the source video transcript was not provided. If you want a precise mapping to the talk, share a transcript or timestamped notes.

---

## Why this document exists

FlowTime’s MCP server should be designed for **agent workflows**, not just as a thin wrapper around APIs. The slides you shared emphasize that if we expose low‑level operations and force agents to stitch together multi‑step chains, we get fragile systems, ambiguous outputs, and brittle prompts.

This document captures the **target design standard** for FlowTime’s MCP server (how we *should* have designed it, and how to evolve it now).

---

## Core Principles (from the slides)

### 1) Outcomes, not operations

**Trap:** Low‑level CRUD tools: `get_user()`, `list_orders()`, `get_status()`  
**Fix:** High‑level outcome tools like `track_latest_order(email)`

**Implication for FlowTime**
- Provide tools that map to **agent tasks**, not engine endpoints.
- Each tool should complete a user story: *“explain backlog cause for run X”*, not *“fetch series CSV”*.

### 2) Design top‑down from workflows

**Trap:** Designing tools by mirroring existing API endpoints  
**Fix:** Start from workflows, then create tools that complete them in 1–2 calls

**Implication for FlowTime**
- Define MCP tools around workflows like:  
  - “Explain why throughput dropped between bins 100–120”  
  - “Summarize dependency constraints for service A”  
  - “Generate a canonical model pattern (Option A/B)”
- MCP should compose internally; the agent shouldn’t chain multiple low‑level calls.

### 3) Flatten arguments

**Trap:** Deep nested configs or “mystery meat” arguments  
**Fix:** Simple top‑level args, typed enums, strong defaults

**Implication for FlowTime**
- Prefer explicit, typed fields (e.g., `runId`, `nodeId`, `windowStart`, `windowEnd`) over nested config blobs.
- Provide enums for known options: `pattern: "optionA" | "optionB"`
- Defaults should be safe and obvious; advanced options should be optional.

### 4) Instructions are context

**Trap:** Empty docstrings, vague errors, no examples  
**Fix:** Docstrings + schema that explain *what/when/return*, with examples that define contract

**Implication for FlowTime**
- Each tool must have:
  - Clear description of inputs and outputs
  - Example invocations
  - Concrete error guidance (what to do next)
- Errors should be framed as *prompts*: “Missing constraint series; use Option B with constraint registry.”

### 5) Respect the token budget

**Trap:** Raw JSON dumps, full DB rows, long error text  
**Fix:** Summaries + IDs, expandable detail on demand

**Implication for FlowTime**
- MCP tools should return **summaries by default**, not full time‑series arrays.
- Provide optional `verbose` or `includeSeries` flags for deep data.
- Return “IDs + key stats” rather than full raw payloads.

---

## Concrete MCP Design Guidelines for FlowTime

### A) Tool design

**Prefer tools like:**
- `explain_constraint_impact(runId, nodeId, window)`
- `summarize_run_health(runId, window)`
- `generate_dependency_model(pattern, parameters)`

**Avoid tools like:**
- `get_state_window(runId, nodeId, start, end)`
- `get_series_csv(seriesId)`

These low‑level calls should be internal implementation details.

### B) Stable contracts

Each MCP tool should include:
- **Purpose:** The user story it solves.
- **Input schema:** Flat, typed, minimal.
- **Output schema:** Summaries first, detail optional.
- **Examples:** At least one success, one error.

### C) Guardrails and validation

FlowTime should encode known constraints and enforce them:
- For dependency modeling, enforce Option A / Option B only.
- Convert engine *info warnings* into **hard MCP errors** when they indicate model contract violations.

### D) Composition belongs to the server

If an answer requires multiple API calls, the MCP server should do that composition, not the agent.
This is a hard requirement to avoid fragile agent chains.

---

## Applying this to FlowTime MCP (current gaps)

Based on the current FlowTime MCP work:

### Likely gaps
- Too many tools that mirror API endpoints.
- Insufficient “workflow tools” that produce explanations or summaries.
- Lack of explicit contracts and example‑driven docs.
- Token‑heavy responses (raw payloads) instead of summaries.

### How we should evolve it

1) **Add workflow‑level tools**  
2) **Consolidate low‑level tools** (keep internal only)  
3) **Add docstrings + examples** for all tools  
4) **Add default summarization** + optional deep data  
5) **Convert engine warnings into MCP hard errors** where appropriate  

---

## If you want full alignment with the talk

I don’t have direct access to the video transcript. To ground this doc in the talk precisely, you can provide:
- A transcript
- Timestamped notes
- A slide deck (PDF)

With that, I can revise this doc to explicitly map FlowTime guidance to exact talk points.

---

## Next Steps (optional)

- Add a **checklist** in `work/epics/completed/ai/mcp-modeling.md` for tool design.
- Define a **minimal MCP tool taxonomy**: workflow tools vs internal tools.
- Create a **tool review rubric** based on these principles.
