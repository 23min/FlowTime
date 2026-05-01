---
name: dead-code-audit
description: Recipe-driven dead-code report at milestone close. Two paths — `bootstrap` (no recipe found; detect stacks, ask the human to pick a tool, write `.claude/skills/dead-code-audit/recipes/dead-code-<stack>.md`, exit) and `audit` (one or more recipes found; for each, run the recipe's `toolCmd` over the milestone change-set, apply judgement, sweep for tool-blind findings, emit a per-stack section to `work/dead-code-report.md`). Soft signal — never mutates code, never fails the build, always exits 0 from the audit path. Invoked from `wrap-milestone` as a non-blocking step.
---

# Skill: Dead Code Audit

End-of-milestone soft-signal pass that surfaces unused symbols, orphan fixtures, ADRs describing reverted decisions, helpers retained "for stability" with no callers, and deprecated aliases. KISS v0: **recipe-driven, tool-grounded, LLM-orchestrated**.

The skill is generic; per-stack audit profiles ("recipes") describe how to audit one stack — which tool to invoke, which paths to exclude, which blind spots the LLM should watch. Recipes are first-class and live at `.claude/skills/dead-code-audit/recipes/dead-code-*.md`. A repo can have one or many recipes; polyglot repos run them sequentially in one audit.

**Soft-signal contract:** never mutates code. Never fails the build. Always exits 0 from the audit path. Findings turn into `work/gaps.md` entries by hand if real — auto-gap-filing is out of scope for v0.

## When to Use

- Wrap-milestone step (automatic, non-blocking).
- On demand to scan the current milestone change-set for dead code.
- First-time setup in a repo that has no recipes yet — the bootstrap conversation produces them.

## Two paths, one entry point

```
human invokes /<prefix>-dead-code-audit
  │
  ▼
skill checks .claude/skills/dead-code-audit/recipes/dead-code-*.md
  │
  ├─ none found     ──▶  BOOTSTRAP path (write recipes, exit; no audit)
  │
  └─ one or more    ──▶  AUDIT path (run each recipe sequentially, write report)
```

The two paths are intentionally separated. Bootstrap is a conversation that produces the recipe as its primary deliverable — humans review the recipe before any audit runs against it. After bootstrap exits, the human re-invokes the skill to perform the first audit.

## Path A — Bootstrap (no recipes found)

Triggered when `.claude/skills/dead-code-audit/recipes/dead-code-*.md` matches zero files.

### 1. Detect stacks present in the repo

Scan for stack signals at known paths. Treat each hit as evidence of one stack:

| Signal | Stack |
|---|---|
| `*.csproj`, `*.sln` | `.NET` |
| `pyproject.toml`, `setup.py`, `requirements*.txt` | Python |
| `Cargo.toml` | Rust |
| `package.json` (with TypeScript signal: `tsconfig.json`) | TypeScript |
| `package.json` (no `tsconfig.json`) | JavaScript |
| `go.mod` | Go |
| `pom.xml`, `build.gradle*` | Java/JVM |
| `Gemfile` | Ruby |
| `composer.json` | PHP |

Add stacks the team explicitly mentions even if signal is absent (e.g. an external `.proto` schema repo).

### 2. Present detected stacks; ask which to audit

Show the list. Ask: "Which stacks should this repo audit for dead code? (one or many)". Capture the human's answer.

### 3. For each chosen stack, propose 2–3 tool options

Each option is one short sentence with the **tradeoff that matters for picking** — install cost, scope (cross-project vs. file-local), strictness, false-positive rate. Don't recommend a winner; the human picks.

Reference table for common stacks (extend as needed):

| Stack | Option A | Option B | Option C |
|---|---|---|---|
| .NET | **Roslynator** — runs in `dotnet build`; misses cross-project unused publics; zero install cost. | **JetBrains InspectCode** (free CLI) — strongest cross-project unused-public-symbol detection; ~100 MB to install. | **`dotnet format analyzers`** — IDE0051/IDE0052 only; weakest of the three; zero install cost. |
| Python | **vulture** — fast, low-noise; misses dynamic dispatch. | **pyflakes** (via flake8) — already in many toolchains; only file-local unused. | **deadcode** (newer) — broader sweep, more false positives. |
| Rust | **`cargo udeps`** — unused crate dependencies; nightly-only. | **clippy `dead_code` lint** — built-in; file-local. | **`cargo machete`** — unused dependencies; stable toolchain. |
| TypeScript | **knip** — broadest (unused files, exports, deps). | **ts-prune** — unused exports; tsconfig-aware. | **eslint `no-unused-vars` + `import/no-unused-modules`** — already in any TS toolchain; weak. |
| Go | **`staticcheck`** (U1000) — unused identifiers; standard. | **`deadcode`** (golang.org/x) — reachability analysis; main-package-aware. | **`go vet`** — minimal; stdlib only. |
| Java | **PMD `UnusedPrivate*`** — fast; file-local. | **IntelliJ Inspect (CLI)** — cross-project; heavy install. | **SpotBugs** — broader bug surface; partial overlap. |

For stacks not in the table, propose 2–3 options based on what the language ecosystem actually offers — don't invent tools.

### 4. Capture the human's choice for each stack

One choice per stack. Confirm before writing.

### 5. Write the recipe(s)

For each chosen stack, write `.claude/skills/dead-code-audit/recipes/dead-code-<name>.md` using the recipe shape below. Populate frontmatter with the human's choice; populate the body with stack-specific blind-spot hints (the table in step 3 of the audit path lists the standard blind-spot families to seed).

Create the `.claude/skills/dead-code-audit/recipes/` directory if absent.

### 6. Exit

Print: `Recipes written. Re-run /<prefix>-dead-code-audit to perform the audit.` — and stop. **Do not audit on the same invocation.** The two-step shape is deliberate: it keeps the bootstrap conversation reviewable and recovers cleanly if the wrong stack got picked.

## Path B — Audit (one or more recipes found)

For each recipe in `.claude/skills/dead-code-audit/recipes/dead-code-*.md`, run the four steps below sequentially. Polyglot repos produce one report with multiple per-stack sections.

### 1. Resolve the milestone change-set

Compute the file list touched on the milestone branch since it diverged from its base (epic branch, integration branch, or `main` — whichever the wrap path uses). Use `git diff --name-only <base>...HEAD`. If invoked outside a wrap context with no obvious base, default to `git diff --name-only main...HEAD` and note the scope assumption in the report header.

### 2. Filter to recipe-relevant files

Apply the recipe's `fileExts` filter, then exclude any path matching the recipe's `excludePaths`. If the filtered set is empty, write a per-stack section noting "no files in scope this milestone" and move on to the next recipe.

### 3. Invoke the recipe's `toolCmd`; capture output

Run `toolCmd` exactly as written in the recipe frontmatter. Capture stdout, stderr, and exit code. The tool may emit XML, JSON, plain text, or SARIF — read the recipe body for any format hints. **Tool failure is a finding, not a wrap blocker** — emit a per-stack section noting the failure (with the captured stderr) and continue to the next recipe.

### 4. Apply judgement and sweep for tool-blind findings

Read the tool output alongside the recipe body's blind-spot hints. Produce four classes of finding per recipe:

- **confirmed-dead-suspects** — `file:line` + reason. Tool flagged it; the change-set evidence supports the flag (no live caller, no DI registration, no fixture reference, no public-surface rationale).
- **tool-flagged-but-live** — tool flagged it; grep / structural read showed a live caller. Cite the caller (`<caller-file:line>`) so the next reviewer can verify in seconds.
- **intentional-public-surface** — tool flagged it; the symbol is part of a documented public surface (cross-repo consumer, exported API, schema-derived contract). Cite the surface and rationale.
- **needs-judgement** — the LLM cannot decide. Flag for human triage with the specific question that needs answering.

Then sweep for findings the tools structurally cannot see. Standard blind-spot families (recipe body should expand these per stack):

- **Orphan fixtures** — fixture files with no test referencing them.
- **Stale ADRs** — decision records describing reverted decisions (search for symbols / paths cited in the ADR; if absent in the change-set's HEAD tree, flag).
- **Helpers retained "for stability"** — exported helpers with zero callers in the change-set's HEAD tree, with a comment or commit history indicating "kept for compat" / "kept for stability."
- **Deprecated aliases** — symbols tolerated by parsers / emitters but not flagged by the tool because they parse cleanly.
- **Schema fields with no consumers** — schema / DTO / proto fields that no producer or consumer code references.

### 5. Emit the per-stack section to `work/dead-code-report.md`

Overwrite `work/dead-code-report.md` on each run. The report has one header section, then one section per recipe. Each recipe section has the four finding classes plus the blind-spot sweep results. See [Output format](#output-format) below.

### 6. Exit 0

Always. The report is the output; downstream tooling reads it.

## Recipe shape

`.claude/skills/dead-code-audit/recipes/dead-code-<name>.md`. One recipe per stack. ~20–40 lines.

**Required frontmatter:**

| Field | Type | Meaning |
|---|---|---|
| `name` | string | Stack name (matches the filename suffix). Used in report section headers. |
| `fileExts` | array of strings | File extensions to include (e.g. `[.cs]`, `[.ts, .tsx]`). |
| `tool` | string | The binary or command to invoke (display name, e.g. `roslynator`, `knip`). |
| `toolCmd` | string | The actual invocation, including args and output capture (e.g. `"roslynator analyze FlowTime.sln --severity-level info --output /tmp/roslynator.xml"`). |

**Optional frontmatter:**

| Field | Type | Meaning |
|---|---|---|
| `excludePaths` | array of strings | Glob-ish path prefixes to exclude (e.g. `[obj/, bin/, "*.g.cs"]`). |

**Body (free-text):** hints used as LLM prompt context. Recommended sections:

- **Things to look out for in this stack** — DI tricks, runtime-resolved dispatch, source generators, reflection, fixture-discovery conventions, codegen.
- **Public surface notes** — which directories or types are part of an external contract; cross-repo consumers; exported APIs.
- **Tool-specific notes** — high-signal codes / rules to weight; noisy codes to suppress.

## Worked example — `.claude/skills/dead-code-audit/recipes/dead-code-dotnet.md`

This is the canonical reference recipe. First-time bootstrap can use it as a template.

~~~markdown
---
name: dotnet
fileExts: [.cs]
excludePaths: [obj/, bin/, "*.g.cs", .claude/worktrees/]
tool: roslynator
toolCmd: "roslynator analyze FlowTime.sln --severity-level info --output /tmp/roslynator.xml"
---

# Dead-code recipe: .NET (Roslynator)

## Things to look out for in this stack
- DI registrations using string keys or type-by-name (`AddSingleton<T>`, `AddScoped<T>`)
- xUnit `[Theory]` / `[MemberData]` / `[ClassData]` discovery — callers are runtime-resolved
- Source generators producing callers (look for `[GeneratedCode]` consumers)
- Reflection-based instantiation (`Activator.CreateInstance`, JSON polymorphic deserialization)

## Public surface notes
`FlowTime.Contracts` is consumed by sibling repo `flowtime-sim-vnext` — treat its public types as live unless cross-repo grep confirms no callers.

## Tool-specific notes
Roslynator's `RCS1213` (unused private member) and `RCS1170` (read-only auto property) are the highest-signal codes. Suppress `RCS1163` (unused parameter) — too noisy on event handlers and DI-injected dependencies.
~~~

## Output format — `work/dead-code-report.md`

Overwritten each audit run. Section order is fixed; per-recipe sections appear in alphabetical order of recipe `name`.

```markdown
# Dead-code Audit — {YYYY-MM-DD}
**Scope:** milestone change-set since `<base-ref>` (<N> files)
**Recipes:** dotnet, typescript
**Tool exits:** dotnet (ok), typescript (ok)

## Recipe: dotnet

### Confirmed-dead suspects
- `src/Foo/Bar.cs:42` — `private bool _legacyFlag` set but never read; introduced 2025-12 for compat with retired migration path.
- `src/Foo/Helpers/RetryShim.cs:1-87` — public class flagged by Roslynator RCS1213; no callers in change-set HEAD tree; CHANGELOG never released a public retry API.

### Tool-flagged-but-live
- `src/Pack/PackLoader.cs:118` `LoadPack(string)` — flagged unused; live caller at `tests/Pack/PackLoaderTests.cs:34` via xUnit `[Theory]` data source.

### Intentional public surface
- `src/Contracts/IPackEntry.cs:12-40` — `IPackEntry` flagged as unreferenced; consumed by sibling repo `flowtime-sim-vnext` (see recipe public-surface notes).

### Needs judgement
- `src/Migration/V03Mapper.cs` — referenced only by V02→V03 migration test fixtures. Question for human: V02 retired in M-PACK-A-01; is V03Mapper still needed?

### Blind-spot sweep
- **Orphan fixtures:** `tests/fixtures/legacy-pack-v01.json` — no test references it.
- **Stale ADRs:** `docs/decisions/ADR-0023.md` cites `Foo.LegacyResolver` which no longer exists in the tree.
- **Schema fields with no consumers:** `Pack.schema.cs:Manifest.deprecatedHint` — no producer or consumer.

## Recipe: typescript
…
```

Section omitted when empty (e.g. no `Tool-flagged-but-live` items). The report's Markdown shape is stable — downstream tooling can grep for `### Confirmed-dead suspects`.

## Integration with `wrap-milestone`

`wrap-milestone` invokes this skill as a non-blocking step (see `wrap-milestone.md`). The integration shape:

- **Bootstrap state** (no recipes): wrap surfaces a one-liner — *"dead-code audit not configured — run `/<prefix>-dead-code-audit` to set up recipes"* — and continues. The wrap is not blocked.
- **Audit produced a report**: wrap adds a one-line link from the milestone tracking doc to `work/dead-code-report.md`. The wrap is not blocked.
- **Audit produced no report** (e.g. all recipes had empty filter sets): wrap notes "dead-code audit: no files in scope" and continues.

The skill never produces a wrap gate. Findings flow through human review at PR time.

## What's not in v0 (explicitly deferred)

Listed here so consumers don't expect them. Each is filed as an enhancement-class follow-up if real evidence demands it.

- **Mechanical-vs-semantic two-pass design.** v0 is one orchestrated pass per recipe.
- **`full` mode (solution-wide audit).** v0 is scoped to the milestone change-set only.
- **CI integration.** CI hygiene is a separate per-repo concern, not part of this skill.
- **Auto-gap-filing into `work/gaps.md`.** Humans file gaps if needed.
- **Cost caps, sampling, file-density ranking.** No `max-files` / `max-tokens` enforcement.
- **`wrap-epic` integration.** v0 hooks into `wrap-milestone` only.
- **Per-recipe parallelism.** Recipes run sequentially.
- **Auto-detection of stacks at audit time.** Stack detection happens at bootstrap. Re-run bootstrap if the repo's stack mix changes.
- **Multi-language pre-bundled starter recipes.** Bootstrap conversation is the v0 onboarding.
- **Per-project recipes within a stack.** A monorepo with three .NET projects gets one `dead-code-dotnet.md`; per-project exclusion goes in `excludePaths`.

## Anti-patterns

- Auditing on the same invocation that wrote the recipe — bootstrap exits before auditing on purpose. Re-run.
- Running with no recipe and a forced `--audit` flag — the skill takes no flags; bootstrap is the only thing that happens when no recipes exist.
- Hand-editing `work/dead-code-report.md` — it's overwritten on every audit run. Findings worth keeping go to `work/gaps.md`.
- Treating the report as a build gate — it's a soft signal. The whole point.
- Adding a recipe that names a tool the repo doesn't have on PATH — the audit step will fail and emit a `tool-failed` section instead of producing useful findings. Install the tool, or amend the recipe to name an installed alternative.

## Invocation

```
/<prefix>-dead-code-audit
```

(Replace `<prefix>` with the framework skill prefix — `wf` by default.)

No arguments. The skill picks bootstrap or audit based on the presence of recipes.
