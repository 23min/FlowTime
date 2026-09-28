# When working in C# / .NET

**Version & house style:** read `global.json` and the `.csproj` (`<TargetFramework>`,
`<LangVersion>`) for the versions and use idioms available there; the repo's own conventions
win.

## Project defaults
- Track latest .NET + latest C# `LangVersion`. `<Nullable>enable</Nullable>` and
  `<ImplicitUsings>enable</ImplicitUsings>` always on; `<TreatWarningsAsErrors>true</…>`.
- File-scoped namespaces; `using` declarations; `var` for obvious types.

## Naming (deliberately against the common .NET house style)
- **Private fields are camelCase with NO leading underscore**
  (`private readonly string dataDirectory;`).
- JSON on the wire is **camelCase**.

## Async & types (added best practice — prune any you don't want)
- **async/await all the way** — no `.Result` / `.Wait()` / `async void` (except event
  handlers); `ConfigureAwait(false)` in library code.
- **records** for DTOs and value objects; `required` members; expression-bodied members and
  switch expressions / pattern matching where they read cleanly.
- Return `IReadOnlyList<T>` / `IEnumerable<T>`, not concrete `List<T>`. ⚖️ Prefer `sealed`
  classes by default.

## Testing
- **xUnit** unit; **bUnit** (`RenderComponent<T>()`) for Blazor; **Playwright** E2E.
  `WebApplicationFactory<Program>` for API tests, `NullLogger<T>.Instance` for services; name
  tests `<ClassName>Tests`.
- Parse/format with **invariant culture**; exact numeric assertions unless a tolerance is
  required. `dotnet build` + `dotnet test` green before handing work back.
