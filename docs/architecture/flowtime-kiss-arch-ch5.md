# FlowTime Architecture: KISS Approach - Chapter 5

## Chapter 5: Implementation Roadmap

This chapter provides detailed milestones with acceptance criteria, dependencies, testing strategies, and risk assessment.

---

### 5.1 Overview

**Total Implementation Time:** 10 working days (2 calendar weeks)

**Team Size:** 2-3 engineers (1 senior, 1-2 mid-level)

**Milestones:**
- M1 (2 days): Foundation - File sources and initial conditions
- M2 (3 days): TelemetryLoader - ADX integration
- M3 (3 days): Templates - System and instantiation
- M4 (2 days): Polish - Validation, observability, documentation

**Dependencies:**
- M1 has no dependencies (can start immediately)
- M2 requires M1 (file source support needed)
- M3 requires M1 (templates reference file sources)
- M4 requires M2+M3 (integrates all components)

---

### 5.2 Milestone 1: Foundation (2 days)

#### M1.1 Goal

Extend Engine to support file sources for const nodes and enforce explicit initial conditions for self-referencing expressions.

#### M1.2 Scope

**Deliverables:**
1. File source support for const nodes
2. Initial condition validation for SHIFT
3. Updated model parser
4. Unit tests for new features
5. Documentation updates

**Non-Goals:**
- Template system (M3)
- Telemetry integration (M2)
- API changes (M4)

#### M1.3 Acceptance Criteria

**AC1: File Source Support**
```
Given: A model with const node referencing file
When: Engine evaluates the model
Then: Series data is loaded from file and evaluation succeeds

Test Case:
  model.yaml:
    nodes:
      - id: test_series
        kind: const
        source: "file://data/test.csv"
  
  data/test.csv:
    10
    20
    30
    ...

  Expected: test_series = [10, 20, 30, ...]
```

**AC2: File Path Resolution**
```
Given: File source with relative path
When: Model is in /app/models/model.yaml
And: Source is "file://data/series.csv"
Then: Resolved path is /app/models/data/series.csv

Given: File source with absolute path
When: Source is "file:///absolute/path/series.csv"
Then: Resolved path is /absolute/path/series.csv
```

**AC3: File Validation**
```
Given: File source pointing to non-existent file
When: Engine attempts to load series
Then: Parser raises error: "File not found: {path}"

Given: CSV file with incorrect row count
When: grid.bins = 288 but file has 300 rows
Then: Parser raises error: "Series length mismatch: expected 288, got 300"
```

**AC4: Initial Condition Enforcement**
```
Given: Expression with self-referencing SHIFT
When: Initial condition is missing
Then: Parser raises error: "Self-referencing SHIFT requires explicit 'initial'"

Test Case:
  - id: queue
    kind: expr
    expr: "MAX(0, SHIFT(queue, 1) + inflow - outflow)"
    # Missing: initial

  Expected: ParseError with message and line number
```

**AC5: Initial Condition Evaluation**
```
Given: Expression with self-referencing SHIFT and initial=5
When: Engine evaluates at t=0
Then: SHIFT(queue, 1)[0] returns 5

Given: Initial condition references another series
When: initial: "q0_from_telemetry"
Then: Initial value is q0_from_telemetry[0]
```

#### M1.4 Technical Design

**File Source Implementation:**

```
Parser Changes:
  1. Add SourceType enum: Inline | File | HTTP
  2. Add FileSourceResolver class:
     - ResolveRelativePath(modelDir, relativePath)
     - ResolveAbsolutePath(absolutePath)
     - Validate file exists and readable
  3. Add CSVReader class:
     - Read(filePath) → float64[]
     - Parse single-column format (no header)
     - Validate row count

Evaluator Changes:
  1. Modify ConstNode.Eval():
     If source is File:
       - Resolve path
       - Read CSV
       - Cache in memory
     Else if source is Inline:
       - Use values array
```

**Initial Condition Implementation:**

```
Parser Changes:
  1. Add InitialCondition type:
     - Scalar(value: float)
     - Reference(seriesId: string)
     - Expression(expr: string)  # Future
  
  2. Modify ExprNode validation:
     - Detect self-referencing SHIFT in AST
     - If found and initial is absent: Error
     - Parse initial value

Evaluator Changes:
  1. Modify SHIFT operator:
     - If t-k < 0 and node is self-referencing:
       Return initial condition value
     - Else if t-k < 0:
       Return 0 (non-self-reference padding)
  
  2. Store initial conditions in evaluation context
```

#### M1.5 Testing Strategy

**Unit Tests (15 tests):**

```
Test_FileSource_ValidCSV_LoadsSuccessfully
Test_FileSource_RelativePath_ResolvesCorrectly
Test_FileSource_AbsolutePath_ResolvesCorrectly
Test_FileSource_FileNotFound_ThrowsError
Test_FileSource_IncorrectRowCount_ThrowsError
Test_FileSource_InvalidNumberFormat_ThrowsError
Test_FileSource_EmptyFile_ThrowsError

Test_InitialCondition_Scalar_ReturnsValue
Test_InitialCondition_Reference_ReturnsSeriesFirstValue
Test_InitialCondition_Missing_ThrowsError
Test_SelfReferencingShift_WithInitial_EvaluatesCorrectly
Test_SelfReferencingShift_WithoutInitial_ThrowsError
Test_NonSelfReferencingShift_NoInitialRequired_Success
Test_InitialCondition_NegativeValue_AcceptsValue
Test_InitialCondition_ZeroValue_AcceptsValue
```

**Integration Tests (3 tests):**

```
Test_EndToEnd_FileSourceAndInitial_ProducesExpectedOutput
  - Create model with file source and self-referencing expr
  - Evaluate
  - Compare output to golden CSV

Test_EndToEnd_MultipleFileSources_AllLoadCorrectly
  - Model references 5 different CSV files
  - All series load and evaluate

Test_EndToEnd_NestedDirectories_PathResolutionWorks
  - Model in /app/models/subfolder/model.yaml
  - References ../data/series.csv
  - Resolves to /app/models/data/series.csv
```

**Golden Tests (1 test):**

```
Test_FileSourceRegression_FixedInput_ConsistentOutput
  - Fixed model + fixed CSV files
  - Evaluate
  - Compare artifacts to golden artifacts (byte-for-byte)
```

#### M1.6 Risks and Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| CSV parsing edge cases (encoding, line endings) | Medium | Medium | Use well-tested library (CsvHelper for C#, pandas for Python), add tests for CRLF/LF |
| File path security (directory traversal) | Low | High | Validate paths don't contain "..", restrict to model directory tree |
| Large file memory usage | Low | Medium | Stream CSVs row-by-row, don't load entire file at once |
| Initial condition type confusion | Medium | Low | Clear error messages, add examples to docs |

#### M1.7 Deliverables Checklist

- [ ] File source parsing (ConstNode.source field)
- [ ] File path resolution (relative and absolute)
- [ ] CSV reader implementation
- [ ] File validation (existence, row count, format)
- [ ] Initial condition parsing (scalar and reference)
- [ ] Initial condition validation (required for self-ref SHIFT)
- [ ] SHIFT operator update (use initial when t-k < 0)
- [ ] 15 unit tests passing
- [ ] 3 integration tests passing
- [ ] 1 golden test passing
- [ ] Documentation: Model schema updated with source field
- [ ] Documentation: Initial condition semantics
- [ ] Code review completed
- [ ] Merged to main branch

---

### 5.3 Milestone 2: TelemetryLoader (3 days)

#### M2.1 Goal

Implement TelemetryLoader to extract telemetry from Azure Data Explorer and write to CSV files.

#### M2.2 Scope

**Deliverables:**
1. TelemetryLoader class with Load() method
2. ADX connection and query execution
3. Dense bin filling (zero-fill or NaN)
4. CSV file writing
5. Manifest generation
6. Warning collection
7. Configuration system
8. Unit and integration tests

**Non-Goals:**
- Template system (M3)
- API integration (M4)
- Advanced gap detection (post-M4)

#### M2.3 Acceptance Criteria

**AC1: ADX Connection**
```
Given: Valid ADX connection string in config
When: TelemetryLoader initializes
Then: Connection is established successfully

Given: Invalid connection string
When: TelemetryLoader.Load() is called
Then: Error is raised with clear message
```

**AC2: KQL Query Construction**
```
Given: Request with window and node selection
When: TelemetryLoader builds query
Then: Query has correct time bounds and node filter

Example:
  Input: window = [2025-10-07 00:00, 2025-10-08 00:00), nodes = ["OrderService"]
  Expected KQL:
    NodeTimeBin
    | where ts >= datetime(2025-10-07T00:00:00Z) and ts < datetime(2025-10-08T00:00:00Z)
    | where node in ("OrderService")
    | order by node asc, ts asc
```

**AC3: Dense Bin Filling**
```
Given: Query results missing bins [12, 13]
When: TelemetryLoader performs dense filling
Then: Output arrays have values at all bins 0..287

Expected behavior:
  If zeroFill=true:
    Missing bins filled with 0
    Warning added: "Zero-filled bins: 12-13"
  
  If zeroFill=false:
    Missing bins filled with NaN
    Warning added: "Missing bins: 12-13"
```

**AC4: CSV File Writing**
```
Given: Telemetry data for OrderService
When: Loader writes files
Then: Files are created:
  - OrderService_arrivals.csv (288 lines, one number per line)
  - OrderService_served.csv
  - OrderService_errors.csv
  - OrderService_demand.csv (if external_demand present)
  - OrderService_queue.csv (if queue_depth present)

File format:
  120
  135
  140
  ...
```

**AC5: Manifest Generation**
```
Given: Successful telemetry load
When: Loader completes
Then: manifest.json is created with:
  - window (start, end, timezone)
  - grid (bins, binSize, binUnit)
  - files[] (node, metric, path, rows, checksum)
  - warnings[] (data gaps, quality issues)
  - provenance (extraction_ts, source, loader_version)
```

**AC6: Error Handling**
```
Given: ADX query timeout
When: Loader executes query
Then: Retry 3 times with exponential backoff
And: If all retries fail, raise error with details

Given: Node not found in Gold
When: Loader processes results
Then: Warning added: "Node '{name}' not found in Gold"
And: Process continues with available nodes
```

#### M2.4 Technical Design

**Class Structure:**

```
TelemetryLoader
  + Load(request: LoadRequest): LoadResult
  - ValidateRequest(request): void
  - BuildQuery(request): string
  - ExecuteQuery(query): QueryResult
  - DenseFill(results, grid): DenseArrays
  - WriteCSV(node, metric, data, outputDir): string
  - GenerateManifest(files, warnings): Manifest

LoadRequest
  + window: TimeWindow
  + selection: NodeSelection
  + outputDir: string
  + options: LoadOptions

LoadResult
  + success: boolean
  + manifest: TelemetryManifest
  + warnings: Warning[]
  + errors: Error[]

TelemetryManifest
  + window: TimeWindow
  + grid: Grid
  + files: FileInfo[]
  + warnings: Warning[]
  + provenance: Provenance
```

**Configuration:**

```
appsettings.json:
{
  "adx": {
    "cluster": "https://cluster.region.kusto.windows.net",
    "database": "Telemetry",
    "auth": {
      "type": "ManagedIdentity"
    }
  },
  "telemetryLoader": {
    "zeroFill": true,
    "checksums": true,
    "maxBins": 10000,
    "queryTimeout": 30,
    "retryCount": 3,
    "gapWarningThreshold": 15
  }
}
```

#### M2.5 Testing Strategy

**Unit Tests (20 tests):**

```
# Validation
Test_ValidateRequest_ValidInput_Passes
Test_ValidateRequest_NonUTC_Throws
Test_ValidateRequest_MisalignedStart_Throws
Test_ValidateRequest_EmptyNodes_Throws

# Query Construction
Test_BuildQuery_SingleNode_CorrectKQL
Test_BuildQuery_MultipleNodes_CorrectKQL
Test_BuildQuery_TimeWindow_CorrectBounds

# Dense Filling
Test_DenseFill_NoGaps_ReturnsOriginal
Test_DenseFill_WithGaps_FillsWithZeros
Test_DenseFill_LargeGap_AddsWarning
Test_DenseFill_NaNOption_FillsWithNaN

# CSV Writing
Test_WriteCSV_ValidData_CreatesFile
Test_WriteCSV_Checksum_MatchesExpected

# Manifest
Test_GenerateManifest_AllFields_Present
Test_GenerateManifest_Warnings_Included

# Error Handling
Test_ExecuteQuery_Timeout_RetriesAndFails
Test_ExecuteQuery_NodeNotFound_ContinuesWithWarning
Test_ExecuteQuery_ConnectionFailed_Throws

# Configuration
Test_LoadConfig_ValidFile_ReturnsConfig
Test_LoadConfig_MissingFile_UsesDefaults
```

**Integration Tests (5 tests):**

```
Test_LoadFromMockADX_ValidWindow_ProducesFiles
  - Mock ADX with fixture data (288 bins)
  - Request OrderService
  - Verify all files created with correct rows

Test_LoadFromMockADX_WithGaps_FillsAndWarns
  - Mock ADX missing bins 12-13
  - Verify zero-filled
  - Verify warning in manifest

Test_LoadFromMockADX_MultipleNodes_AllProcessed
  - Mock ADX with OrderService and BillingService
  - Request both
  - Verify all files for both nodes

Test_LoadFromMockADX_NodeNotFound_ContinuesGracefully
  - Mock ADX has OrderService only
  - Request OrderService + NonExistent
  - Verify OrderService files created
  - Verify warning for NonExistent

Test_LoadFromMockADX_LargeWindow_HandlesCorrectly
  - Request 10,000 bins
  - Verify performance acceptable (<5s)
```

**Mock ADX Setup:**

```
Use in-memory data structure mimicking ADX results:
  - Fixture data with known timestamps and values
  - Ability to simulate gaps
  - Ability to simulate timeouts or errors
  - Configurable delay for performance testing
```

#### M2.6 Risks and Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| ADX authentication issues | Medium | High | Test with multiple auth methods (MI, SP, Azure CLI), clear error messages |
| Query throttling | Medium | Medium | Implement exponential backoff, add rate limit config |
| Large result sets (memory) | Low | Medium | Stream results, process row-by-row |
| CSV write failures (disk full) | Low | High | Check disk space before writing, atomic writes |
| Time zone confusion | Medium | High | Enforce UTC everywhere, validate in parser |

#### M2.7 Deliverables Checklist

- [ ] TelemetryLoader class implemented
- [ ] ADX connection handling (with auth)
- [ ] KQL query construction
- [ ] Query execution with retry logic
- [ ] Dense bin filling (zero-fill and NaN options)
- [ ] CSV file writing (single-column format)
- [ ] Manifest generation (JSON format)
- [ ] Warning collection system
- [ ] Configuration system (appsettings.json)
- [ ] 20 unit tests passing
- [ ] 5 integration tests passing
- [ ] Mock ADX infrastructure for tests
- [ ] Documentation: TelemetryLoader API
- [ ] Documentation: Configuration options
- [ ] Code review completed
- [ ] Merged to main branch

---

### 5.4 Milestone 3: Templates (3 days)

#### M3.1 Goal

Implement template system for reusable topology definitions and expression formulas.

#### M3.2 Scope

**Deliverables:**
1. Template schema definition
2. Template parser and validator
3. Parameter system (types, defaults, validation)
4. Template instantiation (parameter substitution)
5. Include mechanism (shared templates)
6. Template library (2-3 example templates)
7. Documentation and examples

**Non-Goals:**
- Advanced templating (conditionals, loops) - M4+
- Template versioning system - M4+
- Template marketplace - Post-M4

#### M3.3 Acceptance Criteria

**AC1: Template Parsing**
```
Given: Valid template YAML file
When: TemplateParser.Parse() is called
Then: Template object is returned with all fields populated

Given: Invalid YAML syntax
When: TemplateParser.Parse() is called
Then: Error is raised with line number and description
```

**AC2: Parameter Validation**
```
Given: Template with required parameter "q0"
When: Instantiation called without "q0"
Then: Error is raised: "Required parameter 'q0' missing"

Given: Template with number parameter "q0"
When: Instantiation called with q0="text"
Then: Error is raised: "Parameter 'q0' must be number, got string"

Given: Template with enum parameter "mode"
When: Instantiation called with mode="invalid"
Then: Error is raised: "Parameter 'mode' must be one of [telemetry, simulation]"
```

**AC3: Parameter Substitution**
```
Given: Template with "{{q0}}" in expression
When: Instantiation called with q0=5
Then: Generated model has "initial: 5" (substituted)

Given: Template with nested reference "{{params.q0}}"
When: Instantiation called with q0=10
Then: Substitution resolves to 10

Given: Template with "{{window.start}}"
When: Instantiation called with window.start="2025-10-07T00:00:00Z"
Then: Substitution resolves to "2025-10-07T00:00:00Z"
```

**AC4: Include Mechanism**
```
Given: Template A includes "shared/common.yaml"
When: Template A is loaded
Then: Nodes and edges from common.yaml are merged into Template A

Given: Template A and common.yaml define overlapping node IDs
When: Template A is loaded
Then: Template A definitions override common.yaml (later wins)
```

**AC5: Template Instantiation**
```
Given: Template "order-system.yaml" with parameters
When: ModelBuilder.Instantiate(template, parameters)
Then: Complete model.yaml is generated with:
  - schemaVersion: 1
  - window (from parameters)
  - grid (from parameters)
  - topology (from template)
  - nodes (from template expressions)
  - provenance (template name, version, parameters)
```

**AC6: Validation**
```
Given: Template with topology referencing non-existent series
When: Template is validated
Then: Error is raised: "Semantic 'arrivals' references unknown series 'orders_arrivals'"

Given: Template with circular include (A includes B, B includes A)
When: Template is loaded
Then: Error is raised: "Circular include detected: A → B → A"

Given: Template with self-referencing expression without initial
When: Template is validated
Then: Error is raised per M1 validation rules
```

#### M3.4 Technical Design

**Class Structure:**

```
Template
  + type: TemplateType (telemetry | simulation)
  + version: string
  + description: string
  + parameters: ParameterDefinition[]
  + includes: Include[]
  + topology: TopologyDefinition
  + expressions: ExpressionDefinition[]
  + validation: ValidationRule[]
  + outputs: OutputDefinition[]

ParameterDefinition
  + name: string
  + type: ParameterType (string | number | boolean | enum)
  + required: boolean
  + default: any
  + allowedValues: any[] (for enum)
  + min: number (for number)
  + max: number (for number)

TemplateParser
  + Parse(filePath): Template
  - LoadYaml(filePath): object
  - ResolveIncludes(template): Template
  - ValidateSyntax(template): ValidationResult
  - ValidateSemantics(template): ValidationResult

ModelBuilder
  + Instantiate(template, parameters, grid, window): Model
  - ValidateParameters(template.parameters, providedParams): void
  - SubstituteParameters(template, params): Template
  - GenerateModel(template): Model
```

**Parameter Substitution Algorithm:**

```
1. Walk template tree (topology, expressions, outputs)
2. For each string value:
   a. Check if contains "{{...}}"
   b. Extract parameter name
   c. Validate parameter exists
   d. Replace with value
   e. Convert to target type if needed
3. Return substituted template
```

#### M3.5 Testing Strategy

**Unit Tests (25 tests):**

```
# Parsing
Test_Parse_ValidTemplate_ReturnsObject
Test_Parse_InvalidYAML_ThrowsError
Test_Parse_MissingRequiredField_ThrowsError

# Parameters
Test_ValidateParameters_AllPresent_Passes
Test_ValidateParameters_MissingRequired_Throws
Test_ValidateParameters_WrongType_Throws
Test_ValidateParameters_OutOfRange_Throws
Test_ValidateParameters_InvalidEnum_Throws
Test_ApplyDefaults_MissingOptional_UsesDefault

# Substitution
Test_Substitute_ScalarParameter_Replaces
Test_Substitute_NestedParameter_Replaces
Test_Substitute_MultipleOccurrences_ReplacesAll
Test_Substitute_NoParameters_NoChange

# Includes
Test_ResolveIncludes_SingleInclude_Merges
Test_ResolveIncludes_MultipleIncludes_MergesAll
Test_ResolveIncludes_OverlappingNodes_LatestWins
Test_ResolveIncludes_CircularReference_Throws

# Validation
Test_ValidateSemantics_AllRefsExist_Passes
Test_ValidateSemantics_MissingRef_Throws
Test_ValidateSemantics_DuplicateNodeId_Throws

# Instantiation
Test_Instantiate_ValidTemplate_GeneratesModel
Test_Instantiate_WithIncludes_MergesCorrectly
Test_Instantiate_Provenance_Included

# Edge Cases
Test_Substitute_SpecialCharacters_HandlesCorrectly
Test_Substitute_NumberAsString_ConvertsCorrectly
Test_Parse_LargeTemplate_Succeeds
```

**Integration Tests (5 tests):**

```
Test_EndToEnd_TelemetryTemplate_GeneratesValidModel
  - Load order-system.yaml template
  - Instantiate with telemetry parameters
  - Validate generated model.yaml
  - Engine can parse and evaluate

Test_EndToEnd_SimulationTemplate_GeneratesValidModel
  - Load microservices.yaml template
  - Instantiate with simulation parameters
  - Validate generated model.yaml

Test_EndToEnd_TemplateWithIncludes_ProducesExpectedModel
  - Template includes shared topology
  - Instantiate
  - Verify merged nodes and edges

Test_EndToEnd_ParameterizedTemplate_DifferentParams_DifferentModels
  - Instantiate same template with q0=0
  - Instantiate same template with q0=10
  - Verify models differ only in initial value

Test_EndToEnd_ComplexTemplate_AllFeatures_Success
  - Template with parameters, includes, validation, outputs
  - Instantiate
  - Engine evaluates successfully
```

#### M3.6 Template Library

**Templates to Create:**

1. **order-system.yaml** (Telemetry, queue-based service)
   - OrderService node
   - OrderQueue node
   - Expression for queue depth
   - Capacity inference
   - Conservation validation

2. **http-service.yaml** (Telemetry, request-response)
   - API service node
   - No queue (stateless)
   - Utilization computation
   - SLA tracking

3. **microservices.yaml** (Simulation, stochastic)
   - Multiple services
   - PMF-based arrivals
   - Fixed capacities
   - Routing between services

#### M3.7 Risks and Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Parameter substitution edge cases | Medium | Medium | Extensive test coverage, clear error messages |
| Include resolution complexity | Medium | Medium | Limit include depth (max 3 levels), detect cycles |
| Template versioning conflicts | Low | High | Version in filename, validate compatibility |
| User confusion about parameters | High | Low | Good documentation, examples, UI help text |

#### M3.8 Deliverables Checklist

- [ ] Template schema documentation
- [ ] TemplateParser class implemented
- [ ] Parameter validation system
- [ ] Parameter substitution engine
- [ ] Include resolution mechanism
- [ ] ModelBuilder.Instantiate() method
- [ ] Template validation (syntax and semantics)
- [ ] 25 unit tests passing
- [ ] 5 integration tests passing
- [ ] 3 example templates (order-system, http-service, microservices)
- [ ] Documentation: Template authoring guide
- [ ] Documentation: Parameter reference
- [ ] Code review completed
- [ ] Merged to main branch

---

### 5.5 Milestone 4: Polish (2 days)

#### M4.1 Goal

Integrate all components, add observability, complete documentation, and prepare for production.

#### M4.2 Scope

**Deliverables:**
1. API orchestration (POST /v1/runs telemetry mode)
2. Post-evaluation validation runner
3. Enhanced provenance tracking
4. Observability (metrics, logs, traces)
5. Configuration management
6. Complete documentation
7. Deployment guide

**Non-Goals:**
- UI implementation (separate project)
- Advanced features (overlays, capacity prediction)
- Multi-tenancy (post-M4)

#### M4.3 Acceptance Criteria

**AC1: End-to-End Telemetry Flow**
```
Given: Valid telemetry request via API
When: POST /v1/runs is called
Then: Full flow completes:
  1. Template loaded
  2. Telemetry extracted (TelemetryLoader)
  3. Model instantiated
  4. Engine evaluates
  5. Artifacts written
  6. Response returned with runId and warnings
```

**AC2: Post-Evaluation Validation**
```
Given: Model with validation rules
When: Engine completes evaluation
Then: Validation checks run
And: Warnings are collected in run.json
And: Validation results are returned in API response
```

**AC3: Provenance Tracking**
```
Given: Telemetry run completes
When: run.json is generated
Then: Provenance section includes:
  - model.source (template name and version)
  - model.parameters (all parameters used)
  - telemetry.extraction_ts
  - telemetry.source (ADX cluster and database)
  - telemetry.loader_version
  - engine.version
  - engine.evaluation_time_ms
```

**AC4: Observability**
```
Given: Run is created and processed
When: Execution completes
Then: Metrics are emitted:
  - run_creation_total (counter)
  - telemetry_load_duration_ms (histogram)
  - model_evaluation_duration_ms (histogram)
  - run_status (gauge: pending|running|completed|failed)

And: Logs are written:
  - INFO: Run created with runId
  - INFO: Telemetry loaded (X rows)
  - INFO: Model evaluated (Y series)
  - WARN: Data gap detected
  - ERROR: Evaluation failed (if applicable)

And: Traces include spans:
  - POST /v1/runs
    - Load Template
    - Extract Telemetry
    - Instantiate Model
    - Evaluate Model
    - Write Artifacts
```

**AC5: Error Handling**
```
Given: Various failure scenarios
When: API is called
Then: Appropriate error responses are returned:
  - 400 for invalid requests (window, parameters)
  - 404 for template not found
  - 502 for ADX unavailable
  - 500 for internal errors

And: Error responses include:
  - error code
  - clear message
  - field name (if applicable)
  - suggested action (if applicable)
```

#### M4.4 Technical Design

**API Orchestration:**

```
POST /v1/runs handler:
  1. Validate request (window, template, parameters)
  2. Generate runId
  3. Create work directory
  4. Load template
  5. Call TelemetryLoader (if mode=telemetry)
  6. Instantiate model (ModelBuilder)
  7. Evaluate model (Engine)
  8. Run validation checks
  9. Write artifacts to blob storage
  10. Generate run.json with provenance and warnings
  11. Update run registry
  12. Return response

Error Handling:
  - Catch exceptions at each step
  - Map to appropriate HTTP status
  - Include context in error response
  - Log with full stack trace
```

**Validation Runner:**

```
PostEvaluationValidator.Run(model, evaluatedSeries):
  For each validation in model.validation:
    result = EvaluateValidation(validation, evaluatedSeries)
    If result.failed:
      warnings.Add(CreateWarning(validation, result))
  
  Return warnings

EvaluateValidation(validation, series):
  Switch validation.type:
    Case Conservation:
      For each bin t:
        residual = ComputeResidual(t)
        If abs(residual) > validation.tolerance:
          result.failedBins.Add(t)
    
    Case CapacityCheck:
      For each bin t:
        If series[validation.served][t] > series[validation.capacity][t]:
          result.failedBins.Add(t)
  
  Return result
```

#### M4.5 Testing Strategy

**Integration Tests (10 tests):**

```
Test_EndToEnd_TelemetryRequest_Success
  - POST /v1/runs with telemetry request
  - Verify 202 Accepted
  - Poll until completed
  - Verify artifacts exist
  - Verify run.json has provenance

Test_EndToEnd_WithValidation_WarningsIncluded
  - Model with conservation validation
  - Telemetry with known violation
  - Verify warning in run.json

Test_EndToEnd_ADXDown_ReturnsError
  - Mock ADX connection failure
  - POST /v1/runs
  - Verify 502 error
  - Verify error message includes "ADX unavailable"

Test_EndToEnd_InvalidTemplate_Returns404
  - POST with template="nonexistent"
  - Verify 404 error
  - Verify available templates listed

Test_EndToEnd_InvalidWindow_Returns400
  - POST with non-UTC window
  - Verify 400 error
  - Verify field name in error response

Test_EndToEnd_MissingParameter_Returns400
  - Template requires q0
  - POST without q0 parameter
  - Verify 400 error

Test_EndToEnd_MetricsEmitted_Success
  - POST /v1/runs
  - Verify metrics are recorded

Test_EndToEnd_LogsWritten_Success
  - POST /v1/runs
  - Verify log entries for each phase

Test_EndToEnd_TracesGenerated_Success
  - POST /v1/runs with tracing enabled
  - Verify trace spans created

Test_EndToEnd_LargeModel_Performance
  - Model with 100 nodes, 288 bins
  - POST /v1/runs
  - Verify completion <5s
```

**Stress Tests (3 tests):**

```
Test_Concurrent_MultipleRequests_AllSucceed
  - Submit 10 concurrent requests
  - Verify all complete successfully
  - Verify no resource contention issues

Test_LongRunning_NoTimeout
  - Model with 10,000 bins
  - Verify completion within reasonable time
  - No timeout errors

Test_RepeatedRequests_NoLeaks
  - Submit 100 requests sequentially
  - Monitor memory usage
  - Verify no memory leaks
```

#### M4.6 Observability Configuration

**Metrics (Prometheus format):**

```
# Counters
flowtime_run_creation_total{status="completed|failed"}
flowtime_telemetry_load_errors_total

# Histograms
flowtime_telemetry_load_duration_seconds
flowtime_model_evaluation_duration_seconds
flowtime_api_request_duration_seconds

# Gauges
flowtime_active_runs
flowtime_template_count
```

**Logging (Structured JSON):**

```
{
  "timestamp": "2025-10-07T14:30:00.123Z",
  "level": "INFO",
  "message": "Run created",
  "runId": "run_abc123",
  "template": "order-system",
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "bins": 288
  },
  "userId": "user@example.com"
}
```

**Tracing (OpenTelemetry):**

```
Span: POST /v1/runs
  Attributes:
    http.method: POST
    http.route: /v1/runs
    template: order-system
    window.start: 2025-10-07T00:00:00Z
  
  Child Spans:
    - Load Template (duration: 5ms)
    - Extract Telemetry (duration: 1200ms)
    - Instantiate Model (duration: 50ms)
    - Evaluate Model (duration: 450ms)
    - Write Artifacts (duration: 300ms)
```

#### M4.7 Documentation Deliverables

**Architecture Documentation:**
- This document (all 6 chapters)
- Architecture decision records (ADRs)

**API Documentation:**
- OpenAPI spec for all endpoints
- Request/response examples
- Error codes and meanings

**Template Documentation:**
- Template authoring guide
- Parameter reference
- Example templates with explanations

**Deployment Documentation:**
- Infrastructure requirements
- Configuration guide
- Deployment steps (Docker, Kubernetes)
- Troubleshooting guide

**User Documentation:**
- Getting started guide
- Common workflows (time-travel, what-if)
- FAQ

#### M4.8 Deliverables Checklist

- [ ] API orchestration implemented
- [ ] POST /v1/runs telemetry mode working end-to-end
- [ ] Post-evaluation validation runner
- [ ] Provenance tracking enhanced
- [ ] Metrics instrumentation (Prometheus)
- [ ] Logging (structured JSON)
- [ ] Tracing (OpenTelemetry)
- [ ] Configuration management
- [ ] 10 integration tests passing
- [ ] 3 stress tests passing
- [ ] Architecture documentation complete
- [ ] API documentation (OpenAPI spec)
- [ ] Template authoring guide
- [ ] Deployment guide
- [ ] User getting started guide
- [ ] Code review completed
- [ ] Merged to main branch
- [ ] Production deployment plan approved

---

### 5.6 Risk Summary and Mitigation

**Overall Project Risks:**

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| ADX integration complexity | Medium | High | Early M2 spike, fallback to mock for development |
| Parameter substitution bugs | Medium | Medium | Comprehensive test coverage, fuzzing |
| Performance issues with large models | Low | Medium | Load testing in M4, optimization if needed |
| Team member unavailability | Medium | Medium | Clear documentation, pair programming |
| Scope creep (advanced features) | High | Medium | Strict milestone boundaries, defer to post-M4 |

**Mitigation Strategies:**

1. **Early Integration:** Test end-to-end flow early in M2
2. **Automated Testing:** High test coverage (target >80%)
3. **Documentation:** Document as we build, not after
4. **Code Review:** All PRs require review before merge
5. **Demo Early:** Show working system after M2 and M3

---

**End of Chapter 5**

Continue to Chapter 6 for Decision Log and Appendices.
