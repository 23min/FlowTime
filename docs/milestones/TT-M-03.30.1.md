# TT‑M‑03.30.1 — Domain Terminology Mapping (Deferred)

Status: Deferred  
Owners: Platform (Templates) + UI  
References: docs/architecture/retry-modeling.md, docs/development/milestone-documentation-guide.md

---

## Overview

Introduce a lightweight mapping layer that links FlowTime’s generic retry/queue terminology (attempts, failures, retry echo, exhausted failures, etc.) to domain-specific language for each template. The goal is to help operators interpret metrics in health care, logistics, IT operations, and other verticals without diluting the platform-neutral data contract.

## Goals

- Allow template authors to declare per-node metric aliases (e.g., “Failures → Denied Claims”).
- Surface aliases via API metadata so UI surfaces (chips, inspector, tooltips) can present domain-friendly text alongside the canonical term.
- Provide documentation guidance for template maintainers on authoring and validating terminology mappings.

## Scope

### In Scope
- Schema extension for optional `semantics.aliases` (per metric, per node).
- Template authoring guide updates and exemplars (start with IT incident retry template).
- API payloads include alias metadata for each node’s metrics.
- UI inspector/canvas consume alias data for tooltips or secondary labels.

### Out of Scope
- Automated localization/internationalization of aliases.
- Bulk backfill for every template in one pass (tracked separately).
- Runtime customization by end users (admin-only within template files for now).

## Requirements

1. **Template Schema Update**  
   - Add optional alias structure: `aliases: { attempts: "Ticket Submissions", failures: "Escalations" }`.
   - Validation to ensure aliases do not exceed length limits and remain ASCII by default.

2. **API Contract Extension**  
   - `/v1/runs/{id}/graph` and `/state_window` include alias dictionaries so UI consumers can map canonical names to domain terms.
   - Golden tests updated to assert alias presence where configured.

3. **UI Consumption**  
   - Canvas chips and inspector tooltips display alias text (fallback to canonical labels when absent).
   - Feature toggle or developer tooling for showing canonical ↔ alias mapping during debugging.

4. **Documentation & Guides**  
   - Authoring checklist for template maintainers describing when/how to add aliases.
   - Architecture note referencing retry governance + alias usage for cross-domain clarity.

## Acceptance Criteria

- AC1: Template schema accepts alias block and rejects invalid entries.
- AC2: API responses include alias metadata for nodes that define it.
- AC3: UI surfaces (chips, inspector) render aliases when configured and fall back gracefully when absent.
- AC4: Documentation updated with a sample mapping table and governance guidance.

## Implementation Plan (Tentative)

1. **Schema & Validator Update** — Extend template schema, validators, and unit tests.  
2. **API Plumbing** — Thread alias data through graph/state builders; update goldens.  
3. **UI Integration** — Consume aliases in canvas + inspector; add tests.  
4. **Docs & Template Exemplars** — Update retry architecture doc, authoring guide, and seed template(s).

## Risks & Mitigations

- **Template Drift:** Some templates may lack owners to provide aliases. Mitigation: start with top-tier templates and document deferred backfill.  
- **UI Clutter:** Extra labels could crowd chips. Mitigation: default to tooltips/secondary text and keep toggles available for power users.  
- **Internationalization:** Aliases could be mistaken for i18n. Mitigation: clarify that aliases are domain labels, not translation strings.

## Status Notes

- Deferred until TT‑M‑03.28 closes and retry governance (TT‑M‑03.30) is underway.  
- Dependencies: retry governance semantics (exhausted failures, terminal edges) provide inputs worth aliasing.  
- Tracking item added to TT‑M‑03.28 follow-up list for roadmap visibility.
