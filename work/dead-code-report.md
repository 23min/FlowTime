# Dead-code Audit — 2026-05-05

**Milestone:** M-066 — Flow-Authority Policy Spike (E-25 Engine Truth Gate)

**Scope:** `git diff --name-only main...milestone/M-066-edge-flow-authority-decision` filtered through the dotnet / typescript / rust recipe `fileExts` lists. M-066 is a doc-only milestone — no engine code, no template edits, no test changes, no schema edits, no `ExpectedRunWarnings` adjustments (per the milestone's explicit "Constraints" section).

**Recipes:** dotnet (no files in scope), typescript (no files in scope), rust (no files in scope)

**Tool exits:** All recipes skipped — change-set contains zero source files matching any recipe's `fileExts`.

**Result:** **No-op audit.** The audit is produced for M-066 wrap completeness; no source-file analysis was warranted by the change-set.

---

## Change-set summary

M-066's diff against `main` consists of:

- **New documentation files**
    - `docs/architecture/flow-authority-policy.md` — the three-class taxonomy doc (AC-1).
    - `docs/adr/ADR-0001-flow-authority-policy.md` — the ratified ADR (AC-5/AC-6/AC-9).
- **In-place documentation revisions** (AC-4)
    - `docs/flowtime-v2.md` — split the universal fan-out row into class-3 + class-1 rows.
    - `docs/reference/flow-theory-coverage.md` — change "Not planned" verdict to "Deferred" with forward refs to ADR-0001 and G-038.
- **Planning-tree entities** (aiwf-managed)
    - `work/epics/E-25-engine-truth-gate/epic.md` — open-questions update (AC-10).
    - `work/epics/E-25-engine-truth-gate/M-066-edge-flow-authority-decision.md` — the milestone spec itself (frontmatter status, ACs, inlined footprint analysis, inlined doc-sweep, work log, wrap sections).
    - `work/gaps/G-032-…md` — promoted `open → addressed`; reference to ADR-0001 (AC-10).
    - `work/gaps/G-038-…md` — new, the class-2 capacity-aware allocator deferred-follow-up gap (AC-7).
    - `work/gaps/G-035-…md`, `work/gaps/G-037-…md` — sibling gap surface tracked alongside the milestone (audit-only promotions).
- **Repo housekeeping**
    - `.gitignore` — `STATUS.md` added (commit `7e9cc97`).

No `.cs` / `.ts` / `.tsx` / `.svelte` / `.rs` files were added, modified, or deleted by this milestone. Templates, schema YAML, and test code were not touched.

---

## Recipe: dotnet

**No files in scope this milestone.** M-066's change-set contains zero `.cs` / `.csproj` / `.sln` files. Recipe not invoked.

### Confirmed-dead suspects

(None in scope.)

### Tool-flagged-but-live

(None in scope.)

### Intentional public surface

(None in scope.)

### Needs judgement

(None in scope.)

### Blind-spot sweep

The milestone changed only documentation, the planning tree, and `.gitignore`. There is no live-code surface added or removed, no helper retained "for stability," and no test-only seam to evaluate. The blind-spot sweep is not applicable to this change-set.

---

## Recipe: typescript

**No files in scope this milestone.** M-066's change-set contains zero `.ts` / `.tsx` / `.svelte` / `.js` / `.mjs` / `.cjs` files. Recipe not invoked.

---

## Recipe: rust

**No files in scope this milestone.** M-066's change-set contains zero `.rs` files. Recipe not invoked.

---

## Notes for the next audit

- The **enforcement implementation** is M-069 (Schema + Compile + Analyse Enforcement). That milestone *will* land `.cs` changes in `ModelSchemaValidator`, `ModelCompiler` / `TimeMachineValidator`, and `InvariantAnalyzer` — a real dotnet-recipe run is expected at M-069 wrap.
- The **engine + template alignment** is M-067. That milestone *will* land both `.cs` changes (engine surface) and template-side edits — a real dotnet-recipe run is expected at M-067 wrap.
- M-066 itself produces no follow-ups for this audit. The audit is a no-op by design.
