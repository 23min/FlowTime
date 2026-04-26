# Dead-code Audit — 2026-04-26

**Scope:** E-24 Schema Alignment merge (`0a146ce^1...0a146ce`) — proof-of-concept run for `/wf-dead-code-audit` outside a wrap-milestone context. 71 files changed, 41 `.cs` files in scope (39 added/modified, 2 deleted), 0 `.ts/.tsx/.svelte` files in scope, 0 `.rs` files in scope.

**Recipes:** dotnet (active), typescript (no files in scope), rust (no files in scope)

**Tool exits:** dotnet (**ok** — Roslynator analyzers via `dotnet build /p:RoslynatorAnalyze=true`), typescript (skipped), rust (skipped)

**Note:** This run was a deliberate scope override — the v0 skill defaults to `git diff --name-only main...HEAD` which would have produced an empty change-set on `main`. Manual override to E-24's diff range was used to validate skill wiring against a real, completed milestone.

---

## Recipe: dotnet

**Tool run:** `dotnet build FlowTime.sln /p:RoslynatorAnalyze=true /p:TreatWarningsAsErrors=false --verbosity normal /flp:logfile=/tmp/roslynator-build.log;verbosity=normal /nologo`

**Result:** Build succeeded in 49 s with 0 errors, 41 unique analyzer warnings emitted by Roslynator.Analyzers v4.13.1 + Roslyn IDE rules. **Zero of the 41 warnings land inside E-24's change-set** — the in-scope dotnet section below is therefore empty by every finding class. Solution-wide warnings appear in the wider-than-scope informational section at the end of this recipe block.

### Confirmed-dead suspects

(None in scope.)

### Tool-flagged-but-live

(None in scope.)

### Intentional public surface

(None in scope.)

### Needs judgement

- **`tests/FlowTime.Adapters.Synthetic.Tests/FileSeriesReaderTests.cs:132-133`** — sets `model_id` and `template_id` (snake_case) inside a JSON manifest fixture at `provenance.has_provenance/.model_id/.template_id`. Not the *model schema's* `provenance` block (which is camelCase per E-24 m-E24-03); this is the *telemetry-run manifest*'s provenance sub-object on disk. **Question for human:** is the telemetry-run manifest schema deliberately snake_case while the model schema is camelCase, or has the manifest schema drifted out of step with E-24's camelCase convergence? If deliberate, this is intentional dual-convention; if not, it's drift the next telemetry-touching milestone should fix.

### Blind-spot sweep

**Cleanups verified landed (no regression risk):**

- ✅ **`ProvenanceEmbedder` deleted** — `src/FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs` is gone (commit `5a2a31d`); zero in-tree references in any `.cs` file.
- ✅ **`SimModelArtifact` + 6 satellites deleted** — `SimModelArtifact`, `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern` — zero in-tree class definitions or references.
- ✅ **`GridDto.StartTimeUtc` → `Start` rename landed** — `src/FlowTime.Contracts/Dtos/ModelDtos.cs` declares `public string? Start { get; set; }` only.
- ✅ **`Template` `Legacy*` aliases deleted** — `grep` for `class Legacy*` and `[YamlMember(Alias = "legacy*")]` returns zero hits in `src/FlowTime.Sim.Core/Templates/`.
- ✅ **snake_case provenance keys absent from emission** — every `.cs` reference to `generated_at` / `model_id` / `template_id` / `template_version` (model-schema sense) is inside a negative `Assert.DoesNotContain(...)` assertion; the keys are proven absent in `ModelDto`-emitted YAML.
- ✅ **Leaked-state schema fields absent** — `grep` for top-level `^window:` / `^generator:` / `^mode:` / `^metadata:` in `docs/schemas/model.schema.yaml` returns zero hits.
- ✅ **Orphan fixtures clean** — `tests/fixtures/templates/loop-parity-template.yaml` is referenced by `ClassesLoopTests.cs:180` (`TemplateId = "loop-parity-template"`).

**Tool-blind drift findings (real, but most are pre-existing or owned by other epics):**

- ⚠️ **`tests/FlowTime.Tests/ApiIntegrationTests.cs:93`** — declares `public sealed class Grid { public int bins { get; init; } public int binMinutes { get; init; } }`. Uses `binMinutes` — the deprecated schema field. Pre-existing drift (file not in E-24 change-set; never touched during E-24). The class is a deserialization target for `RunResponse.grid`; the API now emits `bins` + `binSize` + `binUnit`, so `binMinutes` deserializes to its default value (0) and is silently unused. **Owned by:** E-23 m-E23-02 (test migration phase) or a small standalone patch. Either way, not a blocker for any active milestone — file as `work/gaps.md` entry.
- ⚠️ **`tests/FlowTime.Tests/Schema/{TargetSchemaValidationTests,SchemaVersionTests,SchemaErrorHandlingTests}.cs`** assert on `ModelValidator`-specific error message phrasing (e.g., `Assert.Contains("binMinutes is no longer supported", e)`). These tests are **already enumerated** in the E-23 m-E23-02 spec as the four test files to migrate from `ModelValidator.Validate` to `ModelSchemaValidator.Validate`. **Owned by:** E-23 m-E23-02 (already-tracked work).

**Stale ADR / decision sweep:**

- ✅ `work/decisions.md`'s extensive `SimModelArtifact` / satellite-type references are all in historical decision records (`D-2026-04-24-036` E-23 paused, `D-2026-04-24-037` Option E ratified, `D-2026-04-25-038` E-24 closed). These are intentional historical context describing what WAS decided, not stale ADRs. Clean.

**Schema fields with no consumers:** No findings — the schema declares only consumed fields per `ADR-E-24-03`.

**Helpers retained "for stability":** No findings — E-24's three follow-up gaps (`ProvenanceEmbedder`, `GridDefinition.StartTimeUtc` rename, Template `Legacy*` aliases) were all cleaned up in commit `5a2a31d` before this audit.

### Wider-than-scope informational (solution-wide noise floor)

Listed for context, not as E-24 findings. 41 unique analyzer warnings exist in the rest of the solution; none touch E-24's change-set. Distribution by code:

| Code | Hits | What it flags |
|---|---|---|
| `RCS1213` | 18 | Unused private member |
| `RCS1194` | 12 | Implement public exception types correctly (missing standard ctors) |
| `RCS1155` | 7 | Use `StringComparison` overload |
| `RCS1075` | 2 | Avoid empty catch clause |
| `RCS1215` | 1 | Expression comparing `double` to `NaN` |
| `RCS1102` | 1 | Make class static |

Top-five files by warning count:

| File | Warnings | Notes |
|---|---|---|
| `src/FlowTime.Contracts/Services/FileSystemArtifactRegistry.cs` | 7 | All `RCS1155` `StringComparison` + 1 `RCS1075` empty-catch |
| `src/FlowTime.Sim.Core/Templates/Exceptions.cs` | 6 | All `RCS1194` — exception types missing standard ctors |
| `src/FlowTime.TimeMachine/TelemetryBundleBuilder.cs` | 3 | All `RCS1213` unused private member |
| `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs` | 2 | Both `RCS1213` unused private member |
| `src/FlowTime.Core/Pmf/Pmf.cs` | 2 | Both `RCS1213` unused private member |

These are real findings worth filing as gaps, but they're out of scope for an E-24-specific audit. Recommended: when E-21 m-E21-08 polish or a dedicated cleanup milestone runs, this list is the starting punch list.

---

## Recipe: typescript

**No files in scope this milestone.** E-24 was pure .NET — zero `.ts/.tsx/.svelte/.js/.mjs/.cjs` files in the change-set. Recipe wiring untested by this run.

---

## Recipe: rust

**No files in scope this milestone.** E-24 was pure .NET — zero `.rs` files in the change-set. Recipe wiring untested by this run.

---

## Wiring evaluation (this run, not the audit's content)

- **Stack detection during bootstrap**: ✅ correct — three stacks identified (`.NET`, TypeScript via `tsconfig.json`, Rust via `Cargo.toml`).
- **Recipe authoring**: ✅ all three recipes parsed cleanly, frontmatter shape correct.
- **Devcontainer install**: ✅ rustup + clippy + Roslynator analyzer NuGet package wiring landed via `init.sh` + `Directory.Build.props`.
- **Change-set resolution**: ✅ `git diff --name-only <base>...<head>` works; per-recipe `fileExts` + `excludePaths` filtering works.
- **Tool invocation (.NET)**: ✅ **fixed in this run** — pivot from standalone `roslynator analyze` (which failed on .NET 9 workspace loading with 148k `CS0518`) to `dotnet build /p:RoslynatorAnalyze=true` running analyzers inside the SDK's compilation pipeline. Build succeeded, 41 unique analyzer findings emitted, zero in scope for E-24.
- **Tool invocation (TS / Rust)**: untested in this run (no in-scope files).
- **Blind-spot sweep**: ✅ produces real value-add findings beyond what the analyzer can see.
- **Soft-signal contract**: ✅ never mutates code, never fails the build, the report is the output.

## Recommended follow-ups (file as gap entries before the next audit)

1. ✅ ~~Fix the .NET recipe's `toolCmd` so Roslynator runs against FlowTime.sln.~~ **Done in this run.**
2. **Test the TypeScript recipe** when E-21 resumes (m-E21-07 Validation Surface or m-E21-08 Polish will produce `.ts/.svelte` change-sets).
3. **Test the Rust recipe** when an E-18 or future Rust-engine change-set lands.
4. **Triage the `binMinutes` drift in `ApiIntegrationTests.cs:93`** — small standalone patch or absorb into E-23 m-E23-02 test migration.
5. **Confirm telemetry-run-manifest snake_case stays distinct from model-schema camelCase** — answer the open question on `FileSeriesReaderTests.cs:132-133`.
6. **Backlog: solution-wide RCS1155 / RCS1194 / RCS1213 cleanups** — 41 warnings worth a dedicated cleanup pass when capacity allows. Top targets: `FileSystemArtifactRegistry.cs` (7), `Exceptions.cs` (6 — should add the standard exception-type ctors), `TelemetryBundleBuilder.cs` (3 unused private members).
