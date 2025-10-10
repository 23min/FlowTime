# FlowTime Code Style and Conventions

## Private Field Naming (STRICT)
- ✅ Use **camelCase** WITHOUT underscore prefix: `dataDirectory`, `indexLock`, `registry`
- ❌ NEVER use underscore prefix: `_dataDirectory`, `_indexLock`, `_registry`
- Example: `private readonly string dataDirectory;` NOT `private readonly string _dataDirectory;`

## C# Style
- .NET 9, C# with nullable reference types enabled
- Implicit usings enabled
- Invariant culture for parsing/formatting

## API Patterns
- Use Minimal APIs (`.MapPost()`, `.MapGet()`) in Program.cs
- NO controller classes
- Route pattern: Group routes using `app.MapGroup("/v1")` for versioning
- Dependency injection: Inject services directly into route handlers

## Testing Conventions
- Test pattern: `ClassName` → `ClassNameTests`
- Core tests: No web dependencies, deterministic, fast
- API tests: Use `WebApplicationFactory<Program>` only, avoid mocking Core
- Include at least one negative test case

## Commits
- Conventional Commits: `feat(api):`, `fix(core):`, `chore(repo):`, `docs:`, `test(api):`
