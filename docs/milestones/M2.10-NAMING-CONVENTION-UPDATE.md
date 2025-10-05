# Private Field Naming Convention - Updated

**Date:** October 5, 2025  
**Updated By:** AI Assistant  
**Issue:** Private field naming convention was not clearly documented

---

## Summary of Changes

Updated `.github/copilot-instructions.md` to make the private field naming convention **crystal clear** and prominent.

### New Rule (Strict Convention)

**Private Field Naming Convention:**
- ✅ Use **camelCase** WITHOUT underscore prefix
- ❌ NEVER use underscore prefix
- Examples:
  - ✅ `private readonly string dataDirectory;`
  - ✅ `private readonly HttpClient client;`
  - ✅ `private static readonly SemaphoreSlim indexLock;`
  - ❌ `private readonly string _dataDirectory;`
  - ❌ `private readonly HttpClient _client;`
  - ❌ `private static readonly SemaphoreSlim _indexLock;`

### Why This Rule?

- Prevents analyzer warnings in test projects
- Consistent with majority of codebase
- Cleaner, more readable code

### Exceptions (Legacy Code Only)

- `Pcg32.cs` uses underscore prefixes (`_state`, `_increment`)
- **DO NOT replicate this pattern in new code**
- Legacy code will be refactored over time

---

## Files Updated

### 1. `.github/copilot-instructions.md`

Added prominent section at top:
```markdown
## Code Style Rules (Apply to ALL code generation, including Serena)

**Private Field Naming Convention (STRICT):**
- ✅ Use **camelCase** WITHOUT underscore prefix: `dataDirectory`, `indexLock`, `registry`
- ❌ NEVER use underscore prefix: `_dataDirectory`, `_indexLock`, `_registry`
- This prevents analyzer warnings in test projects
- Example: `private readonly string dataDirectory;` NOT `private readonly string _dataDirectory;`
```

Also updated the existing "Coding patterns and style" section to be more explicit.

### 2. `tests/FlowTime.Api.Tests/Provenance/ProvenanceQueryTests.cs`

Fixed newly created test file to follow convention:
- Changed: `private readonly HttpClient _client;` → `private readonly HttpClient client;`
- Changed: `private readonly TestWebApplicationFactory _factory;` → `private readonly TestWebApplicationFactory factory;`
- Updated all references throughout the file

---

## Inconsistencies Found

### Existing Test Files (Not Fixed Yet)

The following existing test files use underscore prefix and should be refactored:
- `tests/FlowTime.Api.Tests/Provenance/ProvenanceHeaderTests.cs`
- `tests/FlowTime.Api.Tests/Provenance/ProvenancePrecedenceTests.cs`
- `tests/FlowTime.Api.Tests/Provenance/ProvenanceStorageTests.cs`
- `tests/FlowTime.Api.Tests/Provenance/ProvenanceHashTests.cs`
- `tests/FlowTime.Api.Tests/Provenance/ProvenanceEmbeddedTests.cs`

These files use `_factory` pattern, which violates the convention but are part of existing M2.9 work.

**Recommendation:** Create a separate cleanup task to refactor these files after M2.10 is complete.

---

## Verification

Compiled test file to confirm changes:
```bash
dotnet build tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj
```

Result: Compilation errors are expected (RED state) for missing properties, but no naming convention errors.

---

## Action Items

- [x] Update `.github/copilot-instructions.md` with clear rule
- [x] Fix `ProvenanceQueryTests.cs` to follow convention
- [ ] Consider refactoring existing provenance test files (future task)
- [ ] Ensure all future code generation follows this rule (AI agents + developers)

---

**Status:** ✅ Complete - Rule is now clearly defined and applied to new code
