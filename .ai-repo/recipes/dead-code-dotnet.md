---
name: dotnet
fileExts: [.cs]
excludePaths: [obj/, bin/, "*.g.cs", .claude/worktrees/, packages/]
tool: roslynator
toolCmd: "roslynator analyze FlowTime.sln --severity-level info --output /tmp/roslynator-flowtime.xml"
---

# Dead-code recipe: .NET (Roslynator)

## Setup

`roslynator` CLI is required on PATH. Install once per devcontainer:

```bash
dotnet tool install -g Roslynator.DotNet.Cli
```

If the audit reports tool failure, this is the most likely cause.

## Things to look out for in this stack

- **DI registrations using string keys or type-by-name** (`AddSingleton<T>`, `AddScoped<T>`, `AddTransient<T>`). Resolved by the runtime; Roslynator does not see the consumer side. Check `Program.cs` in `FlowTime.API`, `FlowTime.Sim.Service`, `FlowTime.UI` before flagging a service-shaped class as dead.
- **xUnit `[Theory]` / `[MemberData]` / `[ClassData]` discovery** — test data sources are runtime-resolved; their producer methods may look unused but are called by the xUnit runner.
- **`WebApplicationFactory<Program>` integration tests** resolve types via DI — public types in `FlowTime.API` and `FlowTime.Sim.Service` may have their only callers via integration-test DI.
- **YamlDotNet serialization** — public properties on `ModelDto`, `ProvenanceDto`, `Template`, `SimModelArtifact` (deleted, but pattern persists) are read by reflection. Public DTO properties without explicit C# callers are usually serialization surfaces, not dead code.
- **Reflection / polymorphic JSON deserialization** — `System.Text.Json` polymorphic types invoked by `JsonSerializerOptions` configuration.
- **Blazor `@page` routes and Razor components** — `*.razor` files are discovered by attribute and resolved by route tables; Roslynator may not link C# code-behind to its `.razor` partner.
- **Source generators** — none currently used in FlowTime, but if one is added, look for `[GeneratedCode]` consumers before flagging code as dead.

## Public surface notes

- **`FlowTime.Contracts`** is consumed by sibling repo `flowtime-sim-vnext` — treat its public types (`ModelDto`, `ProvenanceDto`, response shapes) as **live unless cross-repo grep confirms no callers**. Roslynator only sees this repo.
- **Engine API `/v1/*` endpoints** in `FlowTime.API/Program.cs` are consumed by Blazor UI (`FlowTime.UI`), Svelte UI (`ui/`), and external Time Machine clients. Endpoint-handler methods and request/response DTOs may have no `.cs` caller in this repo but are part of an HTTP contract.
- **CLI public commands** in `FlowTime.Cli` and `FlowTime.Sim.Cli` are entry points; their `Run*` methods may look unused but are dispatched from the command-line parser.
- **`FlowTime.Adapters.Synthetic`** — public fixture-generator types are consumed by integration tests across multiple test projects.

## Tool-specific notes

- **High-signal codes:** `RCS1213` (unused private member), `RCS1170` (read-only auto property), `RCS1077` (unused parameter), `IDE0051` (unused private member), `IDE0052` (private field never read).
- **Suppress as too noisy:** `RCS1163` (unused parameter on event handlers / DI-injected dependencies — fires on idiomatic patterns), `RCS1058` (use compound assignment — pure style noise), `RCS1090` (call ConfigureAwait — fires across the whole codebase).
- **Output format:** XML at `/tmp/roslynator-flowtime.xml`. Parse the `<Diagnostic>` elements; each has `Id`, `Severity`, `Message`, and `Location` with file path + line.
- **Run scope:** the toolCmd analyzes the whole solution; the skill's per-recipe filter narrows results to the milestone change-set after the tool runs.
