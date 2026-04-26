---
name: dotnet
fileExts: [.cs]
excludePaths: [obj/, bin/, "*.g.cs", .claude/worktrees/, packages/]
tool: roslynator-via-msbuild
toolCmd: "dotnet build FlowTime.sln /p:RoslynatorAnalyze=true /p:TreatWarningsAsErrors=false --verbosity normal /flp:logfile=/tmp/roslynator-build.log;verbosity=normal /nologo"
---

# Dead-code recipe: .NET (Roslynator analyzers via dotnet build)

## Setup

No global tool install needed. Roslynator runs as a NuGet analyzer package gated by the `RoslynatorAnalyze` MSBuild property. The package is wired in `Directory.Build.props` at the repo root with `Condition="'$(RoslynatorAnalyze)' == 'true'"`, so it only restores and runs when the audit asks for it — daily `dotnet build` is unaffected.

The earlier approach (standalone `roslynator analyze` CLI against `FlowTime.sln`) failed in this devcontainer with a 148k `CS0518` storm — Roslynator's `MSBuildWorkspace` could not resolve the .NET 9 targeting pack. Running analyzers inside the SDK's own build loop avoids that workspace-loading bug entirely; the SDK already has the right references loaded by definition.

## Things to look out for in this stack

- **DI registrations using string keys or type-by-name** (`AddSingleton<T>`, `AddScoped<T>`, `AddTransient<T>`). Resolved by the runtime; Roslynator does not see the consumer side. Check `Program.cs` in `FlowTime.API`, `FlowTime.Sim.Service`, `FlowTime.UI` before flagging a service-shaped class as dead.
- **xUnit `[Theory]` / `[MemberData]` / `[ClassData]` discovery** — test data sources are runtime-resolved; their producer methods may look unused but are called by the xUnit runner.
- **`WebApplicationFactory<Program>` integration tests** resolve types via DI — public types in `FlowTime.API` and `FlowTime.Sim.Service` may have their only callers via integration-test DI.
- **YamlDotNet serialization** — public properties on `ModelDto`, `ProvenanceDto`, `Template` are read by reflection. Public DTO properties without explicit C# callers are usually serialization surfaces, not dead code.
- **Reflection / polymorphic JSON deserialization** — `System.Text.Json` polymorphic types invoked by `JsonSerializerOptions` configuration.
- **Blazor `@page` routes and Razor components** — `*.razor` files are discovered by attribute and resolved by route tables; Roslynator may not link C# code-behind to its `.razor` partner.
- **Source generators** — none currently used in FlowTime, but if one is added, look for `[GeneratedCode]` consumers before flagging code as dead.

## Things to look out for in this stack

- **DI registrations using string keys or type-by-name** (`AddSingleton<T>`, `AddScoped<T>`, `AddTransient<T>`). Resolved by the runtime; Roslynator does not see the consumer side. Check `Program.cs` in `FlowTime.API`, `FlowTime.Sim.Service`, `FlowTime.UI` before flagging a service-shaped class as dead.
- **xUnit `[Theory]` / `[MemberData]` / `[ClassData]` discovery** — test data sources are runtime-resolved; their producer methods may look unused but are called by the xUnit runner.
- **`WebApplicationFactory<Program>` integration tests** resolve types via DI — public types in `FlowTime.API` and `FlowTime.Sim.Service` may have their only callers via integration-test DI.
- **YamlDotNet serialization** — public properties on `ModelDto`, `ProvenanceDto`, `Template`, `SimModelArtifact` (deleted, but pattern persists) are read by reflection. Public DTO properties without explicit C# callers are usually serialization surfaces, not dead code.
- **Reflection / polymorphic JSON deserialization** — `System.Text.Json` polymorphic types invoked by `JsonSerializerOptions` configuration.
- **Blazor `@page` routes and Razor components** — `*.razor` files are discovered by attribute and resolved by route tables; Roslynator may not link C# code-behind to its `.razor` partner.
- **Source generators** — none currently used in FlowTime, but if one is added, look for `[GeneratedCode]` consumers before flagging code as dead.

## Public surface notes

- **`FlowTime.Contracts`** types (`ModelDto`, `ProvenanceDto`, response shapes) are the wire-contract surface consumed by HTTP clients, CLI tooling, and test fixtures. Treat public types as **live unless explicit cross-surface inspection confirms no callers**. Roslynator may flag them as unreferenced.
- **Engine API `/v1/*` endpoints** in `FlowTime.API/Program.cs` are consumed by Blazor UI (`FlowTime.UI`), Svelte UI (`ui/`), and external Time Machine clients. Endpoint-handler methods and request/response DTOs may have no `.cs` caller in this repo but are part of an HTTP contract.
- **CLI public commands** in `FlowTime.Cli` and `FlowTime.Sim.Cli` are entry points; their `Run*` methods may look unused but are dispatched from the command-line parser.
- **`FlowTime.Adapters.Synthetic`** — public fixture-generator types are consumed by integration tests across multiple test projects.

## Tool-specific notes

- **High-signal codes:** `RCS1213` (unused private member), `RCS1170` (read-only auto property), `IDE0051` (unused private member), `IDE0052` (private field never read). Severity for these is set explicitly in `.editorconfig` at repo root.
- **Suppressed via `.editorconfig`:** `RCS1163` (unused parameter — fires on event handlers and DI-injected deps), `RCS1058` (use compound assignment — pure style), `RCS1090` (call ConfigureAwait — fires across the whole codebase). Suppression is repo-wide; the codes will not fire in IDE display either. Re-enable in `.editorconfig` if needed.
- **Output format:** MSBuild file logger at `/tmp/roslynator-build.log` (verbosity `normal`). Parse lines matching the regex `\b(RCS|IDE)\d+\b` — MSBuild emits warnings as `<file>(<line>,<col>): warning <CODE>: <message>`. Filter to in-scope files via the skill's change-set list.
- **Run scope:** the toolCmd builds the whole solution with analyzers enabled; the skill's per-recipe filter narrows results to the milestone change-set after the build completes.
- **Build cost:** with `RoslynatorAnalyze=true` the build is ~30–60 s slower than a normal build on this solution (21 projects). Daily dev builds (without the property) are unaffected.
- **Property gate:** `Directory.Build.props` only adds the `Roslynator.Analyzers` PackageReference when `$(RoslynatorAnalyze) == 'true'`. If you need to keep analyzers always-on (e.g., for CI), remove the property condition.
