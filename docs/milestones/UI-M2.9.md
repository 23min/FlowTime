# UI-M2.9 — Schema Migration for UI

**Status:** � In Progress  
**Dependencies:** ✅ M2.9 (Engine Schema Evolution Complete), ✅ SIM-M2.6 (Sim Schema Evolution Complete)  
**Target:** Update UI to parse new schema format (binSize/binUnit) from Engine and Sim services

---

## Overview

Update FlowTime UI to correctly parse the new schema format (`binSize`/`binUnit`) introduced in M2.9. The UI currently expects the OLD schema format (`binMinutes`) and **cannot function** with Engine v0.6.1+ or Sim v0.6.0+.

**CRITICAL**: The UI is currently **completely broken** - all API calls fail due to schema mismatch. See [`docs/ui/UI-CURRENT-STATE-ANALYSIS.md`](../ui/UI-CURRENT-STATE-ANALYSIS.md) for comprehensive analysis.

### Strategic Context
- **Motivation**: Engine M2.9 completed schema evolution from `binMinutes` to `binSize`/`binUnit` format
- **Impact**: UI must be updated to parse new schema or remains non-functional
- **Dependencies**: Engine and Sim have already completed their migrations - UI is the last component needing update

### Current State

**Engine API (`/v1/run`)** returns:
```json
{
  "grid": {
    "bins": 8,
    "binSize": 1,
    "binUnit": "hours"
  }
}
```

**UI expects** (OLD schema):
```csharp
public record GridInfo(
    int Bins,
    int BinMinutes);  // ⬅️ MISSING in Engine response
```

**Result:** Deserialization exception → UI cannot execute models or browse artifacts

## Scope

### In Scope ✅

1. **UI Response Models Update**
   - Update `GridInfo` record to parse `binSize`/`binUnit`
   - Update `SimGridInfo` class to parse `binSize`/`binUnit`
   - Add computed `BinMinutes` properties for backward compatibility with UI pages

2. **Service Layer Updates**
   - Update `FlowTimeApiClient` to handle new schema from Engine
   - Update `FlowTimeSimApiClient` to handle new schema from Sim
   - Update `ApiRunClient` business logic to preserve schema information

3. **UI Page Updates**
   - Update `TemplateRunner.razor` to display semantic units (e.g., "1 hour" not "60 minutes")
   - Update `Simulate.razor` to display grid info correctly
   - Update `Artifacts.razor` to browse artifacts with new schema

4. **Testing**
   - Unit tests for schema parsing
   - Integration tests for API client
   - Manual E2E testing of all workflows

### Out of Scope ❌

- ❌ Template system restructuring (separate milestone)
- ❌ Demo template updates (handled separately)
- ❌ Advanced template validation
- ❌ UI redesign or new features
- ❌ Performance optimization

### Dependencies

**External (Complete):**
- ✅ M2.9: Engine schema evolution complete
- ✅ SIM-M2.6: Sim schema evolution complete

**Internal:**
- Comprehensive UI state analysis ([`docs/ui/UI-CURRENT-STATE-ANALYSIS.md`](../ui/UI-CURRENT-STATE-ANALYSIS.md))

---

## Implementation Plan

### Phase 1: Critical Schema Fixes

**Goal**: Restore basic UI functionality by fixing deserialization failures

#### Task 1.1: Update SimGridInfo Model

**File:** `ui/FlowTime.UI/Services/FlowTimeSimApiClient.cs`

**Current (BROKEN):**
```csharp
public class SimGridInfo
{
    public int Bins { get; set; }
    public int BinMinutes { get; set; }  // ⬅️ Engine doesn't send this
    public string Timezone { get; set; } = "UTC";
    public string Align { get; set; } = "left";
}
```

**Fixed:**
```csharp
public class SimGridInfo
{
    public int Bins { get; set; }
    public int BinSize { get; set; }
    public string BinUnit { get; set; } = "minutes";
    public string Timezone { get; set; } = "UTC";
    public string Align { get; set; } = "left";
    
    // INTERNAL ONLY: Computed property for UI display convenience
    // NOT serialized to/from JSON (binMinutes removed from all external schemas)
    [JsonIgnore]
    public int BinMinutes => BinUnit.ToLowerInvariant() switch
    {
        "seconds" => BinSize / 60,
        "minutes" => BinSize,
        "hours" => BinSize * 60,
        "days" => BinSize * 1440,
        _ => BinSize
    };
}
```

**Success Criteria:**
- [ ] UI can deserialize `series/index.json` from artifacts
- [ ] Artifact browsing page loads without exceptions

#### Task 1.2: Update GridInfo Model

**File:** `ui/FlowTime.UI/Services/FlowTimeApiModels.cs`

**Current (BROKEN):**
```csharp
public record GridInfo(
    [property: JsonPropertyName("bins")] int Bins,
    [property: JsonPropertyName("binMinutes")] int BinMinutes);
```

**Fixed:**
```csharp
public record GridInfo(
    [property: JsonPropertyName("bins")] int Bins,
    [property: JsonPropertyName("binSize")] int BinSize,
    [property: JsonPropertyName("binUnit")] string BinUnit)
{
    // INTERNAL ONLY: Computed property for UI display convenience
    // NOT serialized to/from JSON (binMinutes removed from all external schemas)
    [JsonIgnore]
    public int BinMinutes => BinUnit.ToLowerInvariant() switch
    {
        "seconds" => BinSize / 60,
        "minutes" => BinSize,
        "hours" => BinSize * 60,
        "days" => BinSize * 1440,
        _ => BinSize
    };
}
```

**Success Criteria:**
- [ ] UI can deserialize Engine `/v1/run` responses
- [ ] Model execution completes without exceptions

#### Task 1.3: Update GraphRunResult

**File:** `ui/FlowTime.UI/Services/RunClientContracts.cs`

**Current (DATA LOSS):**
```csharp
public sealed record GraphRunResult(
    int Bins,
    int BinMinutes,  // ⬅️ Loses semantic info
    ...);
```

**Fixed:**
```csharp
public sealed record GraphRunResult(
    int Bins,
    int BinSize,
    string BinUnit,
    IReadOnlyList<string> Order,
    IReadOnlyDictionary<string, double[]> Series,
    string? RunId = null)
{
    // INTERNAL ONLY: Computed property for UI display convenience
    // NOT for serialization (binMinutes removed from all external schemas)
    public int BinMinutes => BinUnit.ToLowerInvariant() switch
    {
        "seconds" => BinSize / 60,
        "minutes" => BinSize,
        "hours" => BinSize * 60,
        "days" => BinSize * 1440,
        _ => BinSize
    };
}
```

**Update:** `ui/FlowTime.UI/Services/ApiRunClient.cs`
```csharp
// Change from:
var gr = new GraphRunResult(r.Grid.Bins, r.Grid.BinMinutes, r.Order, r.Series, r.RunId);

// To:
var gr = new GraphRunResult(r.Grid.Bins, r.Grid.BinSize, r.Grid.BinUnit, r.Order, r.Series, r.RunId);
```

**Success Criteria:**
- [ ] UI preserves semantic information (binSize/binUnit)
- [ ] Pages can display "1 hour" instead of "60 minutes"

### Phase 2: UI Page Display Updates

**Goal**: Update UI pages to display semantic grid information

#### Task 2.1: Update TemplateRunner Display

**File:** `ui/FlowTime.UI/Pages/TemplateRunner.razor`

**Changes:**
- Display grid as `{BinSize} {BinUnit}` instead of `{BinMinutes} minutes`
- Example: "24 bins × 1 hour" not "24 bins × 60 minutes"

**Success Criteria:**
- [ ] Template results show semantic units
- [ ] User sees original parameter values preserved

#### Task 2.2: Update Simulate Display

**File:** `ui/FlowTime.UI/Pages/Simulate.razor`

**Changes:**
- Display execution results with semantic units
- Handle both user-provided schemas gracefully

**Success Criteria:**
- [ ] Direct execution shows correct grid format
- [ ] Results display semantic time units

#### Task 2.3: Update Artifacts Display

**File:** `ui/FlowTime.UI/Pages/Artifacts.razor`

**Changes:**
- Display artifact grid info with new schema
- Show `{BinSize} {BinUnit}` format in artifact list

**Success Criteria:**
- [ ] Artifact browsing shows correct grid format
- [ ] Users see semantic time units in artifact metadata

### Phase 3: Testing & Validation

**Goal**: Ensure all workflows work end-to-end

#### Test Case 1: Template Workflow
1. Select template with `binSize: 1, binUnit: hours`
2. Generate model
3. Execute in Engine
4. Verify UI displays "1 hour" NOT "60 minutes"

#### Test Case 2: Direct Execution
1. Paste NEW schema YAML
2. Execute
3. Verify UI parses and displays correctly

#### Test Case 3: Artifact Browsing
1. View run created by Engine v0.6.1+
2. Fetch `series/index.json`
3. Verify grid info displays correctly
4. Fetch series CSV

#### Test Case 4: Error Handling
1. Attempt to paste OLD schema YAML (`binMinutes` only) if user has legacy examples
2. Engine should reject with clear error message
3. Verify UI displays error appropriately

---

## Test Plan

### Test-Driven Development Approach

**Strategy:** Write failing tests first, then implement fixes

### Phase 1 Tests (Unit Tests)

#### Test: SimGridInfo Deserialization
```csharp
[Fact]
public void SimGridInfo_Deserializes_NewSchema()
{
    var json = @"{
        ""bins"": 288,
        ""binSize"": 5,
        ""binUnit"": ""minutes"",
        ""timezone"": ""UTC""
    }";
    
    var grid = JsonSerializer.Deserialize<SimGridInfo>(json);
    
    Assert.Equal(288, grid.Bins);
    Assert.Equal(5, grid.BinSize);
    Assert.Equal("minutes", grid.BinUnit);
    Assert.Equal(5, grid.BinMinutes); // Computed property
}
```

#### Test: GridInfo Deserialization
```csharp
[Fact]
public void GridInfo_Deserializes_NewSchema()
{
    var json = @"{
        ""bins"": 8,
        ""binSize"": 1,
        ""binUnit"": ""hours""
    }";
    
    var grid = JsonSerializer.Deserialize<GridInfo>(json);
    
    Assert.Equal(8, grid.Bins);
    Assert.Equal(1, grid.BinSize);
    Assert.Equal("hours", grid.BinUnit);
    Assert.Equal(60, grid.BinMinutes); // 1 hour = 60 minutes
}
```

#### Test: BinMinutes Computation
```csharp
[Theory]
[InlineData("seconds", 60, 1)]     // 60 seconds = 1 minute
[InlineData("minutes", 5, 5)]      // 5 minutes = 5 minutes
[InlineData("hours", 1, 60)]       // 1 hour = 60 minutes
[InlineData("days", 1, 1440)]      // 1 day = 1440 minutes
public void BinMinutes_ComputedInternally_ForDisplayOnly(string unit, int size, int expectedMinutes)
{
    var grid = new SimGridInfo
    {
        Bins = 10,
        BinSize = size,
        BinUnit = unit
    };
    
    Assert.Equal(expectedMinutes, grid.BinMinutes);
}
```

### Phase 2 Tests (Integration Tests)

#### Test: Engine API Client
```csharp
[Fact]
public async Task FlowTimeApiClient_ParsesNewSchema()
{
    // Arrange: Start test server with Engine
    var client = _factory.CreateClient();
    
    // Act: Call /v1/run endpoint
    var response = await client.PostAsync("/v1/run", yamlContent);
    var result = await response.Content.ReadFromJsonAsync<RunResponse>();
    
    // Assert: New schema parsed correctly
    Assert.Equal(8, result.Grid.Bins);
    Assert.Equal(1, result.Grid.BinSize);
    Assert.Equal("hours", result.Grid.BinUnit);
}
```

### Phase 3 Tests (E2E Manual Tests)

See "Phase 3: Testing & Validation" section above for manual test cases

## Success Criteria

### Milestone Complete When:
- [ ] All UI response models parse new schema (binSize/binUnit)
- [ ] All tests passing (unit + integration)
- [ ] UI can execute models via Engine
- [ ] UI can browse artifacts with new schema
- [ ] UI displays semantic time units (e.g., "1 hour")
- [ ] No deserialization exceptions
- [ ] Documentation updated

### Phase 1 Complete (Critical Fixes)
- [ ] `SimGridInfo` updated with binSize/binUnit
- [ ] `GridInfo` updated with binSize/binUnit
- [ ] `GraphRunResult` preserves semantic information
- [ ] `ApiRunClient` passes through schema fields
- [ ] Unit tests passing for schema parsing

### Phase 2 Complete (Display Updates)
- [ ] TemplateRunner displays semantic units
- [ ] Simulate page displays grid correctly
- [ ] Artifacts page displays grid correctly
- [ ] All UI pages use new schema

### Phase 3 Complete (Validation)
- [ ] Template workflow tested end-to-end
- [ ] Direct execution tested
- [ ] Artifact browsing tested
- [ ] Backward compatibility verified (if Engine accepts OLD schema)

---

## File Impact Summary

### Files to Modify (Critical - Phase 1)

**Priority: P0 (Blocking)**
- `ui/FlowTime.UI/Services/FlowTimeSimApiClient.cs`
  - Line 256-264: Update `SimGridInfo` class
  - Add computed `BinMinutes` property
  
- `ui/FlowTime.UI/Services/FlowTimeApiModels.cs`
  - Line 94-96: Update `GridInfo` record
  - Add computed `BinMinutes` property
  
- `ui/FlowTime.UI/Services/RunClientContracts.cs`
  - Line 3-10: Update `GraphRunResult` record
  - Add binSize/binUnit fields
  - Add computed `BinMinutes` property
  
- `ui/FlowTime.UI/Services/ApiRunClient.cs`
  - Line 22-27: Update `RunAsync()` method
  - Pass binSize/binUnit to GraphRunResult

### Files to Modify (Display - Phase 2)

**Priority: P1 (User Experience)**
- `ui/FlowTime.UI/Pages/TemplateRunner.razor`
  - Update grid display to show semantic units
  
- `ui/FlowTime.UI/Pages/Simulate.razor`
  - Update results display with semantic units
  
- `ui/FlowTime.UI/Pages/Artifacts.razor`
  - Update grid info display with semantic units

### Files to Update (Documentation)

**Priority: P2 (Reference)**
- `docs/ui/UI-CURRENT-STATE-ANALYSIS.md` - Mark as resolved
- `docs/ui/template-integration-spec.md` - Update examples
- `README.md` - Update schema references if present

### Files to Create

**Priority: P1 (Tracking)**
- `docs/milestones/tracking/UI-M2.9-tracking.md` - Implementation tracking document

---

## Migration Notes

### NO Backward Compatibility

**IMPORTANT:** The UI must NOT support OLD schema (`binMinutes`):
- Engine M2.9 (v0.6.0+) uses NEW schema exclusively - OLD schema rejected
- Sim SIM-M2.6 (v0.5.0+) uses NEW schema exclusively - OLD schema rejected
- `binMinutes` removed from ALL external schemas (API, artifacts, models)
- `binMinutes` exists ONLY internally in Engine for computation (NOT exposed)
- No production deployments exist with OLD schema

**Validation:**
- UI must parse ONLY `binSize`/`binUnit` from JSON
- UI must NOT accept or serialize `binMinutes` in any external format
- UI may compute `binMinutes` internally for display convenience only

### Schema Evolution Complete

| Component | Version | Schema | Status |
|-----------|---------|--------|--------|
| **Engine** | v0.6.1+ | binSize/binUnit | ✅ Complete (M2.9) |
| **Sim** | v0.6.0+ | binSize/binUnit | ✅ Complete (SIM-M2.6) |
| **UI** | Current | binMinutes (OLD) | ❌ **THIS MILESTONE** |

### Breaking Changes

**For UI Users:**
- Grid display changes from "60 minutes" to "1 hour" (semantic)
- More readable time units
- Better alignment with template parameters

**For UI Developers:**
- `GridInfo` and `SimGridInfo` models changed
- Must use new fields or computed properties
- All UI pages referencing grid info need updates

---

## Related Documents

- [UI Current State Analysis](../ui/UI-CURRENT-STATE-ANALYSIS.md) - Comprehensive breakage analysis
- [M2.9 Milestone](M2.9.md) - Engine schema evolution (complete)
- [SIM-M2.6 Milestone](../../flowtime-sim-vnext/docs/milestones/SIM-M2.6.md) - Sim schema evolution
- [Milestone Documentation Guide](../development/milestone-documentation-guide.md) - Documentation standards

