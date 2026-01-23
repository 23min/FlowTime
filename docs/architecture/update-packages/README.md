# Package Update Epic (Net 9)

Goal: update dependencies while staying on .NET 9 (no .NET 10 packages). UI updates are deferred to the final phase.

Last refreshed: 2026-01-23 (dotnet 9.0.306); targets unchanged from baseline.

Data source: `dotnet list package --outdated --highest-minor` for safe .NET 9 patch/minor updates, with `dotnet list package --outdated` to surface major-version opportunities.

Legend: packages marked **(major)** are major version jumps and should be scheduled after patch/minor updates and a focused review.

## Recommended Order

1. Core libraries: `FlowTime.Contracts`, `FlowTime.Core`, `FlowTime.Expressions` (if any), `FlowTime.Sim.Core`
2. Services/CLIs: `FlowTime.API`, `FlowTime.Cli`, `FlowTime.Sim.Service`, `FlowTime.Sim.Cli`
3. Generators/adapters: `FlowTime.Generator`, `FlowTime.Adapters.Synthetic`
4. Tests (non-UI): unit/integration tests after production projects are green
5. UI last: `FlowTime.UI` + `FlowTime.UI.Tests` (MudBlazor + WASM stack)

## Per-Project Package Targets

### src/FlowTime.Contracts/FlowTime.Contracts.csproj

| Package | Current | Next |
| --- | --- | --- |
| Microsoft.Data.Sqlite | 9.0.0 | 9.0.12 |
| Microsoft.Extensions.Configuration.Abstractions | 9.0.0 | 9.0.12 |
| Microsoft.Extensions.Configuration.Binder | 9.0.0 | 9.0.12 |
| Microsoft.Extensions.Logging.Abstractions | 9.0.0 | 9.0.12 |
| YamlDotNet | 16.1.3 | 16.3.0 |

### src/FlowTime.Core/FlowTime.Core.csproj

| Package | Current | Next |
| --- | --- | --- |
| JsonSchema.Net (major) | 5.5.1 | 8.0.5 |
| YamlDotNet | 15.1.6 | 15.3.0 |

### src/FlowTime.Sim.Core/FlowTime.Sim.Core.csproj

| Package | Current | Next |
| --- | --- | --- |
| Microsoft.Extensions.Logging.Abstractions | 9.0.0 | 9.0.12 |
| YamlDotNet | 16.1.3 | 16.3.0 |

### src/FlowTime.Cli/FlowTime.Cli.csproj

| Package | Current | Next |
| --- | --- | --- |
| JsonSchema.Net (major) | 5.5.1 | 8.0.5 |
| Microsoft.Extensions.Configuration | 9.0.0 | 9.0.12 |
| Microsoft.Extensions.Configuration.Abstractions | 9.0.0 | 9.0.12 |
| Microsoft.Extensions.Logging | 9.0.0 | 9.0.12 |
| Microsoft.Extensions.Logging.Abstractions | 9.0.0 | 9.0.12 |

### src/FlowTime.API/FlowTime.API.csproj

| Package | Current | Next |
| --- | --- | --- |
| Microsoft.AspNetCore.OpenApi | 9.0.7 | 9.0.12 |
| Parquet.Net | 5.2.0 | 5.4.0 |
| YamlDotNet | 16.3.0 | 16.3.0 |

### tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj

| Package | Current | Next |
| --- | --- | --- |
| coverlet.collector | 6.0.2 | 6.0.4 |
| Microsoft.NET.Test.Sdk | 17.12.0 | 17.14.1 |
| xunit | 2.9.2 | 2.9.3 |
| xunit.runner.visualstudio (major) | 2.8.2 | 3.1.5 |

### tests/FlowTime.Tests/FlowTime.Tests.csproj

| Package | Current | Next |
| --- | --- | --- |
| BenchmarkDotNet | 0.15.2 | 0.15.8 |
| coverlet.collector | 6.0.2 | 6.0.4 |
| Microsoft.Extensions.Configuration | 9.0.0 | 9.0.12 |
| Microsoft.NET.Test.Sdk | 17.12.0 | 17.14.1 |
| xunit | 2.9.2 | 2.9.3 |
| xunit.runner.visualstudio (major) | 2.8.2 | 3.1.5 |

### tests/FlowTime.Expressions.Tests/FlowTime.Expressions.Tests.csproj

| Package | Current | Next |
| --- | --- | --- |
| coverlet.collector | 6.0.2 | 6.0.4 |
| Microsoft.NET.Test.Sdk | 17.12.0 | 17.14.1 |
| xunit | 2.9.2 | 2.9.3 |
| xunit.runner.visualstudio (major) | 2.8.2 | 3.1.5 |

### tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj

| Package | Current | Next |
| --- | --- | --- |
| coverlet.collector | 6.0.2 | 6.0.4 |
| JsonSchema.Net (major) | 5.5.1 | 8.0.5 |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.7 | 9.0.12 |
| Microsoft.NET.Test.Sdk | 17.12.0 | 17.14.1 |
| xunit | 2.9.2 | 2.9.3 |
| xunit.runner.visualstudio (major) | 2.8.2 | 3.1.5 |

### tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj

| Package | Current | Next |
| --- | --- | --- |
| coverlet.collector | 6.0.2 | 6.0.4 |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.0 | 9.0.12 |
| Microsoft.NET.Test.Sdk | 17.11.1 | 17.14.1 |
| xunit | 2.9.2 | 2.9.3 |
| xunit.runner.visualstudio (major) | 2.8.2 | 3.1.5 |

### tests/FlowTime.Cli.Tests/FlowTime.Cli.Tests.csproj

| Package | Current | Next |
| --- | --- | --- |
| coverlet.collector | 6.0.2 | 6.0.4 |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.0 | 9.0.12 |
| Microsoft.NET.Test.Sdk | 17.12.0 | 17.14.1 |
| xunit | 2.9.2 | 2.9.3 |
| xunit.runner.visualstudio (major) | 2.8.2 | 3.1.5 |

### tests/FlowTime.Integration.Tests/FlowTime.Integration.Tests.csproj

| Package | Current | Next |
| --- | --- | --- |
| coverlet.collector | 6.0.2 | 6.0.4 |
| Microsoft.NET.Test.Sdk | 17.12.0 | 17.14.1 |
| xunit | 2.9.2 | 2.9.3 |
| xunit.runner.visualstudio (major) | 2.8.2 | 3.1.5 |

### tests/FlowTime.Adapters.Synthetic.Tests/FlowTime.Adapters.Synthetic.Tests.csproj

| Package | Current | Next |
| --- | --- | --- |
| Microsoft.NET.Test.Sdk | 17.11.1 | 17.14.1 |
| xunit | 2.9.0 | 2.9.3 |
| xunit.runner.visualstudio (major) | 2.8.2 | 3.1.5 |

### src/FlowTime.UI/FlowTime.UI.csproj

| Package | Current | Next |
| --- | --- | --- |
| Microsoft.AspNetCore.Components.WebAssembly | 9.0.7 | 9.0.12 |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 9.0.7 | 9.0.12 |
| Microsoft.Extensions.Http | 9.0.7 | 9.0.12 |
| Microsoft.NET.ILLink.Tasks | 9.0.10 | 9.0.10 |
| Microsoft.NET.Sdk.WebAssembly.Pack | 9.0.11 | 9.0.11 |
| MudBlazor | 8.14.0 | 8.15.0 |
| YamlDotNet | 16.1.3 | 16.3.0 |

### tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj

| Package | Current | Next |
| --- | --- | --- |
| bunit | 1.27.17 | 1.40.0 |
| Microsoft.NET.Test.Sdk | 17.12.0 | 17.14.1 |
| xunit | 2.9.2 | 2.9.3 |
| xunit.runner.visualstudio (major) | 2.8.2 | 3.1.5 |
| YamlDotNet | 16.1.3 | 16.3.0 |

## Notes and Guardrails

- Keep `TargetFramework` at `net9.0` across all projects.
- For Microsoft packages, stay on `9.0.x` (avoid `10.x`).
- Treat **(major)** rows as separate mini-epics: review changelogs, update code/tests, and stage behind patch/minor updates.
- UI updates are intentionally deferred; handle MudBlazor, WASM runtime packages, and `bunit` together at the end.
