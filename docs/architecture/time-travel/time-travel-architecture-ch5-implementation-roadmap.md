# FlowTime Architecture: KISS Approach - Chapter 5

## Chapter 5: Implementation Roadmap

This chapter provides detailed milestones with acceptance criteria, dependencies, testing strategies, and risk assessment.

---

### 5.1 Overview

**Total Implementation Time:** 10 working days (2 calendar weeks)

**Team Size:** 2-3 engineers (1 senior, 1-2 mid-level)

**Milestones:**
- M3.0 (2 days): Foundation + Fixtures
- M3.1 (3 days): Time-Travel APIs
- M3.2 (3 days): TelemetryLoader + Templates
- M3.3 (2 days): Validation + Polish

**Dependencies:**
- M3.0 has no dependencies (can start immediately)
- M3.1 requires M3.0 (fixtures + schema)
- M3.2 requires M3.0 (schema) and informs M3.1 consumers
- M3.3 requires M3.1+M3.2 (integrates all components)

---

### 5.2 Milestone M3.0: Foundation (2 days)

#### M3.0.1 Goal

Extend Engine to support file sources for const nodes and enforce explicit initial conditions for self-referencing expressions.

#### M3.0.2 Scope

**Deliverables:**
1. File source support for const nodes
2. Initial condition validation for SHIFT
3. Updated model parser
4. Unit tests for new features
5. Documentation updates

**Non-Goals:**
- Template system (M3.2)
- Telemetry integration (M3.1)
- API changes (M3.3)

#### M3.0.3 Acceptance Criteria

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

#### M3.0.4 Technical Design

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

#### M3.0.5 Testing Strategy

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

#### M3.0.6 Risks and Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| CSV parsing edge cases (encoding, line endings) | Medium | Medium | Use well-tested library (CsvHelper for C#, pandas for Python), add tests for CRLF/LF |
| File path security (directory traversal) | Low | High | Validate paths don't contain "..", restrict to model directory tree |
| Large file memory usage | Low | Medium | Stream CSVs row-by-row, don't load entire file at once |
| Initial condition type confusion | Medium | Low | Clear error messages, add examples to docs |

#### M3.0.7 Deliverables Checklist

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

### 5.3 Milestone M3.1: Time-Travel APIs (3 days)

#### M3.1.1 Goal

Deliver `/state` and `/state_window` endpoints that expose bin-level snapshots, derived metrics, and node coloring so the UI can ship the first time-travel experience.

#### M3.1.2 Scope

**Deliverables:**
1. `GET /v1/runs/{runId}/state?binIndex={idx}` single-bin snapshot endpoint
2. `GET /v1/runs/{runId}/state_window?startBin={s}&endBin={e}` range endpoint
3. Derived metrics pipeline (utilization, latency_min, throughput ratio)
4. Node coloring engine (service utilization, queue latency bands, fallback)
5. API contract types (`StateResponse`, `StateWindowResponse`)
6. Provenance + window metadata surfaced in responses
7. Instrumentation (structured logging, duration metrics) for the new endpoints
8. Comprehensive tests (unit, integration, golden) covering success and failure modes

**Non-Goals:**
- Telemetry ingestion (TelemetryLoader lives in M3.2)
- Template system enhancements (M3.2)
- Validation severity changes or observability polish (M3.3)
- Aggregation or rollup endpoints beyond the base `/state_window`

#### M3.1.3 Acceptance Criteria

**AC1: /state Single Bin**
```
GET /v1/runs/run_abc123/state?binIndex=42

Response:
{
  "runId": "run_abc123",
  "mode": "simulation",
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "timezone": "UTC"
  },
  "grid": {
    "bins": 288,
    "binSize": 5,
    "binUnit": "minutes",
    "binMinutes": 5
  },
  "bin": {
    "index": 42,
    "startUtc": "2025-10-07T03:30:00Z",
    "endUtc": "2025-10-07T03:35:00Z"
  },
  "nodes": {
    "OrderService": {
      "kind": "service",
      "arrivals": 150,
      "served": 145,
      "errors": 5,
      "capacity": null,
      "utilization": null,
      "throughputRatio": 0.97,
      "color": "gray"
    },
    "OrderQueue": {
      "kind": "queue",
      "arrivals": 145,
      "served": 140,
      "queue": 8,
      "latency_min": 0.286,
      "sla_min": 5.0,
      "color": "green"
    }
  }
}
```

**AC2: /state_window Time Series**
```
GET /v1/runs/run_abc123/state_window?startBin=0&endBin=144

Response:
{
  "runId": "run_abc123",
  "window": {...},
  "grid": {...},
  "slice": {
    "startBin": 0,
    "endBin": 144,
    "bins": 144
  },
  "timestamps": [
    "2025-10-07T00:00:00Z",
    "2025-10-07T00:05:00Z",
    ...
  ],
  "nodes": {
    "OrderService": {
      "kind": "service",
      "series": {
        "arrivals": [...],
        "served": [...],
        "errors": [...],
        "utilization": [...]
      }
    }
  }
}
```

**AC3: Derived Metrics**
- Utilization = `served / capacity` (null when capacity missing)
- Latency_min = `queue / served × binMinutes` (zero or null when denominator zero)
- Throughput ratio = `served / arrivals` (null when arrivals zero)
- Metrics respect bin window (start exclusive, end exclusive)

**AC4: Node Coloring**
- Services: green <0.7, yellow 0.7–0.9, red ≥0.9 utilization
- Queues: compare latency_min against SLA (green ≤1×, yellow ≤1.5×, red otherwise)
- No capacity → gray, missing SLA → teal, errors escalate to warning header

**AC5: Performance**
- `/state` completes in <50 ms for 288-bin runs on dev hardware
- `/state_window` (≤144 bins) completes in <200 ms
- Responses include `Cache-Control: no-store` and structured timing logs

#### M3.1.4 Technical Design

**API Surface:**
- Add two minimal GET handlers in `FlowTime.API` using endpoint routing
- Input validation: `binIndex >= 0`, `startBin < endBin`, both ≤ `grid.bins`
- Shared pipeline for loading run manifests, window metadata, and series slices

**Derived Metric Pipeline:**
- Introduce `FlowTime.Core.Metrics` namespace with `StateMetricComputer`
- Compute metrics lazily per node kind, using shared helpers for safe division
- Store intermediate results in `StateSnapshot` domain model for reuse across endpoints

**Node Coloring Engine:**
- Move thresholds into `NodeColoringRules` with mode-based configuration (telemetry vs simulation)
- Support fallback colors (`gray`, `teal`) when inputs missing
- Return both `color` and `colorReason` to help UI debugging

**Data Access:**
- Reuse existing run artifact reader to hydrate baseline series
- Provide lightweight caching layer (in-memory dictionary scoped to request) to avoid duplicate CSV reads
- Expose provenance + schema_version from run manifest in responses

**Observability:**
- Structured logs (`StateRequestStarted`, `StateRequestCompleted`)
- Metrics: `flowtime_api_state_duration_ms`, `flowtime_api_state_window_duration_ms`
- Include warning headers when derived metrics fall back (e.g., division by zero)

#### M3.1.5 Testing Strategy

**Unit Tests (approximately 15):**
- `StateMetricComputerTests` (utilization, latency, throughput, null handling)
- `NodeColoringRulesTests` (service/queue thresholds, fallbacks)
- `StateRequestValidatorTests` (bin ranges, negative indices, window bounds)
- `StateResponseBuilderTests` (metadata population, provenance propagation)

**Integration Tests (8):**
- `StateEndpoint_ReturnsSingleBinSnapshot`
- `StateEndpoint_InvalidBin_Returns400`
- `StateWindowEndpoint_ReturnsDenseSlice`
- `StateWindowEndpoint_InvalidRange_Returns400`
- `StateEndpoint_ComputesDerivedMetrics`
- `StateEndpoint_MissingCapacity_UsesNullsAndGray`
- `StateWindowEndpoint_PartialNodes_FiltersUnavailableSeries`
- `StateEndpoint_PerformanceWithinBudget` (measured via test harness)

**Golden Tests (2):**
- Fixed run + bin index → compare serialized JSON to golden artifact
- Fixed run + window slice → compare JSON (stable ordering, tolerances for floats)

**Performance / Load Checks:**
- Benchmark for 10 concurrent `/state` requests (target <75 ms p95)
- Smoke test for `/state_window` with 500-bin slice to verify graceful error (413/400)

#### M3.1.6 Risks and Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| UI contract drift | Medium | High | Pair with UI mocks, publish OpenAPI spec, add contract tests |
| Derived metric correctness | Medium | High | Double-entry unit tests with hand-computed fixtures |
| Large run artifacts | Low | Medium | Stream CSV slices, limit requested window size |
| Missing capacity/SLA data | High | Medium | Clear null semantics, fallback colors, surface warnings |
| Performance regressions | Medium | Medium | Add benchmark gate, profile serialization hotspots |

#### M3.1.7 Deliverables Checklist

- [ ] `/v1/runs/{id}/state` endpoint implemented with validation
- [ ] `/v1/runs/{id}/state_window` endpoint implemented with validation
- [ ] Derived metric helpers (utilization, latency, throughput)
- [ ] Node coloring rules with configuration hooks
- [ ] Response contracts (`StateResponse`, `StateWindowResponse`) documented
- [ ] Structured logging + duration metrics in place
- [ ] 15+ unit tests passing for metrics/validation
- [ ] 8 integration tests exercising HTTP surface
- [ ] Golden snapshots checked in for regression coverage
- [ ] API reference updated (docs/api/state-endpoints.md)
- [ ] UI contract review completed
- [ ] Code review complete / merged to main

---

### 5.4 Milestone M3.2: TelemetryLoader + Templates (3 days)

#### M3.2.1 Goal

Ship the telemetry ingestion pipeline (TelemetryLoader) and reusable template system so telemetry-first runs can be generated from ADX data and fed into the engine consistently.

#### M3.2.2 Scope

**Deliverables:**
- **TelemetryLoader**
  1. ADX connection + KQL query builder
  2. Dense bin filling with zero/NaN strategies
  3. CSV writer for arrivals/served/errors/queue_depth/external_demand
  4. Manifest generator (window, grid, files[], warnings[], provenance, checksums)
  5. Warning collection + configuration surface
  6. CLI command / SDK hook to run the loader from dev machines
- **Template System**
  1. Template schema + authoring guide
  2. Template parser with include resolution and cycle detection
  3. Parameter validation + substitution pipeline
  4. Template instantiation producing canonical `model.yaml`
  5. Example templates (order-system, microservices, http-service)
  6. CLI command for template instantiation
- **Integration glue** between loader output directories and template parameters

**Non-Goals:**
- Public API endpoints (`/state`, `/state_window`) – completed in M3.1
- Validation / observability polish – deferred to M3.3
- Advanced templating constructs (conditionals, loops) – out of scope
- Full production ADX deployment automation – tracked separately

#### M3.2.3 Acceptance Criteria

**TelemetryLoader**

**AC-TL1: ADX Connectivity**
```
Given: Valid ADX connection settings
When: TelemetryLoader initializes
Then: Connection succeeds

Given: Invalid connection string
When: TelemetryLoader.Load() executes
Then: Clear error is raised and retried per policy
```

**AC-TL2: Dense Bin Filling**
```
Given: Results missing bins [12, 13]
When: zeroFill=true
Then: Missing bins are filled with 0 and warning recorded

Given: zeroFill=false
Then: Missing bins use NaN and warning recorded
```

**AC-TL3: CSV + Manifest Output**
```
Given: Telemetry data for OrderService
When: Loader finishes
Then: CSV files exist (arrivals/served/errors/queue/external_demand if present)
And: manifest.json includes window, grid, files[], warnings[], provenance
```

**AC-TL4: Error Handling**
```
Given: ADX throttling or timeout
When: Query executes
Then: Loader retries with exponential backoff and surfaces failure after retry limit
```

**Template System**

**AC-TPL1: Template Parsing**
```
Given: Valid template YAML
When: TemplateParser.Parse(path) is called
Then: Template object matches schema

Given: Invalid YAML
Then: Parser raises error with line and column details
```

**AC-TPL2: Parameter Validation + Substitution**
```
Given: Required parameter missing
When: Instantiate(template, parameters) executes
Then: Error: "Required parameter 'q0' missing"

Given: "{{telemetry_dir}}" placeholder
Then: Instantiated model resolves to provided path
```

**AC-TPL3: Include Resolution**
```
Given: Template includes shared/common.yaml
When: Parser loads template
Then: Nodes/edges from include merged, template overrides win

Given: Circular include
Then: Error: "Circular include detected: A → B → A"
```

**AC-TPL4: Model Generation**
```
Given: order-system template + parameters
When: Instantiate runs
Then: Output model.yaml contains window, topology, nodes, provenance, validation rules
```

#### M3.2.4 Technical Design

**TelemetryLoader Architecture**

```
TelemetryLoader
  + Load(request: LoadRequest): LoadResult
  - ValidateRequest(request)
  - BuildQuery(request): string
  - ExecuteQuery(query): QueryResult
  - DenseFill(results, grid): DenseArrays
  - WriteCsv(node, metric, data, outputDir): string
  - GenerateManifest(files, warnings): Manifest

LoadRequest
  + window: TimeWindow
  + selection: NodeSelection
  + outputDir: string
  + options: LoadOptions

TelemetryManifest
  + window: TimeWindow
  + grid: Grid
  + files: FileInfo[]
  + warnings: Warning[]
  + provenance: Provenance
```

- Configurable via `appsettings.json` (zeroFill, maxBins, retryCount, queryTimeout, gapWarningThreshold).
- Supports Managed Identity, Service Principal, or AAD token auth.
- Streams ADX query results row-by-row to avoid large allocations.
- Warning system captures gaps, missing nodes, and schema mismatches for M3.3 validation.

**Template System Architecture**

```
Template
  + type: TemplateType (telemetry | simulation)
  + version: string
  + description: string
  + parameters: ParameterDefinition[]
  + includes: Include[]
  + topology: TopologyDefinition
  + nodes: NodeDefinition[]
  + validation: ValidationRule[]

TemplateParser
  + Parse(filePath): Template
  - LoadYaml(filePath)
  - ResolveIncludes(template)
  - ValidateSyntax(template)
  - ValidateSemantics(template)

TemplateInstantiator
  + Instantiate(template, parameters): Model
  - ValidateParameters(template.parameters, input)
  - SubstitutePlaceholders(template, parameterSet)
  - BuildModel(template, substitutions)

IncludeResolver
  + Resolve(basePath, include): TemplateFragment
  - DetectCircularIncludes(pathStack)
```

- Include resolution allows shared topology fragments with override semantics.
- Double-curly substitution supports escaping (`{{{{` → `{`).
- Provenance recorded in output model (`template.name`, `template.version`, `parameters`).
- CLI command (`flowtime template instantiate`) drives instantiation end-to-end.

#### M3.2.5 Testing Strategy

**TelemetryLoader Unit Tests (≈20):**
```
Test_ValidateRequest_ValidInput_Passes
Test_ValidateRequest_NonUtc_Throws
Test_BuildQuery_SingleNode_CorrectKql
Test_BuildQuery_TimeWindow_CorrectBounds
Test_DenseFill_WithGaps_FillsWithZeros
Test_DenseFill_NaNOption_FillsWithNaN
Test_WriteCsv_ChecksumMatchesExpected
Test_GenerateManifest_IncludesWarnings
Test_ExecuteQuery_Timeout_RetriesThenFails
Test_ExecuteQuery_NodeNotFound_AddsWarning
```

**TelemetryLoader Integration Tests (5):**
```
Test_LoadFromMockAdx_ProducesFiles
Test_LoadFromMockAdx_WithGaps_AddsWarning
Test_LoadFromMockAdx_MultipleNodes_AllProcessed
Test_LoadFromMockAdx_NodeNotFound_Continues
Test_LoadFromMockAdx_LargeWindow_PerformanceOk
```

**Template System Unit Tests (≈25):**
```
Test_TemplateParser_ValidTemplate_Parses
Test_TemplateParser_InvalidYaml_Throws
Test_ParameterValidator_MissingRequired_Throws
Test_ParameterValidator_InvalidType_Throws
Test_TemplateInstantiator_SubstitutesParameters
Test_TemplateInstantiator_IncludesMerged
Test_TemplateValidator_SemanticsMissingSeries_Throws
Test_TemplateValidator_SelfRefWithoutInitial_Throws
Test_TemplateCli_InstantiateCommand_GeneratesModel
Test_TemplateRegression_OrderSystem
Test_TemplateRegression_Microservices
```

**Template Integration Tests (6):**
```
Test_TemplateToModel_EndToEnd_OrderSystem
Test_TemplateToModel_EndToEnd_Microservices
Test_TemplateCli_Workflow_GeneratesAndRunsModel
Test_TemplateValidation_WarningsCaptured
Test_TemplateInclude_SharedNodesMerged
Test_TelemetryTemplate_WithLoaderOutput
```

#### M3.2.6 Risks and Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| ADX authentication or throttling issues | Medium | High | Support multiple auth modes, exponential backoff, smoke scripts |
| Large result sets (memory/IO pressure) | Medium | Medium | Stream results, cap `maxBins`, warn when limits exceeded |
| CSV formatting inconsistencies | Medium | Medium | Strict validation, golden loader outputs, checksum verification |
| Template complexity overwhelms authors | Medium | Medium | Keep syntax minimal, ship curated examples, provide docs |
| Parameter substitution bugs | Medium | Medium | Extensive unit + golden tests, clear escaping rules |
| Include resolution errors | Low | Medium | Cycle detection, max depth guard, clear error messages |

#### M3.2.7 Deliverables Checklist

- [ ] TelemetryLoader class + configuration committed
- [ ] ADX retry + dense-fill logic implemented
- [ ] CSV + manifest outputs validated with golden fixtures
- [ ] Warning collection + provenance recorded
- [ ] Template schema + authoring guide published
- [ ] TemplateParser, IncludeResolver, ParameterValidator implemented
- [ ] TemplateInstantiator + CLI command delivered
- [ ] Example templates (order-system, microservices, http-service) checked in
- [ ] Loader ↔ template glue documented (how to wire telemetry_dir parameters)
- [ ] 20 TelemetryLoader unit tests + 5 integration tests passing
- [ ] 25 Template unit tests + 6 integration tests + 2 golden tests passing
- [ ] Code reviews complete / merged to main

### 5.5 Milestone M3.3: Polish (2 days)

#### M3.3.1 Goal

Integrate all components, add observability, complete documentation, and prepare for production.

#### M3.3.2 Scope

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
- Multi-tenancy (post-M3.3)

#### M3.3.3 Acceptance Criteria

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

#### M3.3.4 Technical Design

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

#### M3.3.5 Testing Strategy

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

#### M3.3.6 Observability Configuration

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

#### M3.3.7 Documentation Deliverables

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

#### M3.3.8 Deliverables Checklist

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
| ADX integration complexity | Medium | High | Early M3.1 spike, fallback to mock for development |
| Parameter substitution bugs | Medium | Medium | Comprehensive test coverage, fuzzing |
| Performance issues with large models | Low | Medium | Load testing in M3.3, optimization if needed |
| Team member unavailability | Medium | Medium | Clear documentation, pair programming |
| Scope creep (advanced features) | High | Medium | Strict milestone boundaries, defer to post-M3.3 |

**Mitigation Strategies:**

1. **Early Integration:** Test end-to-end flow early in M3.1
2. **Automated Testing:** High test coverage (target >80%)
3. **Documentation:** Document as we build, not after
4. **Code Review:** All PRs require review before merge
5. **Demo Early:** Show working system after M3.1 and M3.2

---

**End of Chapter 5**

Continue to Chapter 6 for Decision Log and Appendices.
