# Task Completion Checklist

When completing a task in FlowTime:

## 1. Build
```bash
dotnet build
```
Ensure solution compiles without errors.

## 2. Run Tests
```bash
dotnet test --nologo
```
All tests must pass.

## 3. Verify Changes
- Check that code follows style guide (no underscore prefixes on private fields)
- Verify Conventional Commit format: `feat(scope):`, `fix(scope):`, etc.
- Ensure no backward-incompatible changes without discussion

## 4. API Changes
If API was modified:
- Update `.http` examples in `src/FlowTime.API/`
- Update API tests in `tests/FlowTime.API.Tests/`

## 5. Documentation
- Update relevant docs if behavior changed
- Follow milestone documentation guide (no time estimates)

## 6. Commit
- Stage changes: `git add .`
- Commit with conventional format
- DO NOT push without explicit user approval

## 7. Version Bumps (Only on merge to main)
Follow post-merge ceremony in `docs/development/release-ceremony.md`:
- Update all `.csproj` VersionPrefix together
- Create release doc
- Create git tag
- Push tag and main
