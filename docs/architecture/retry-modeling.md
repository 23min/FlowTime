# FlowTime Engine Retry Modeling Architecture

**Version:** 1.0  
**Date:** September 9, 2025  
**Purpose:** Define how FlowTime engine processes, analyzes, and visualizes retry behaviors and their downstream effects from a data modeling and computational perspective.

---

## Executive Summary

FlowTime engine's retry modeling enables **comprehensive analysis and visualization** of retry patterns, failure propagation, and temporal feedback loops in complex systems. This document outlines the computational architecture, data processing requirements, and visualization needs for modeling retry behaviors that create downstream effects.

### Key Principle: Single-Pass Evaluation with Causal Delays

FlowTime maintains **deterministic, single-pass evaluation** while handling complex retry patterns through:
- **Causal temporal operators** (`CONV`, `SHIFT`, `DELAY`) that only reference past states
- **Stateful nodes** with bounded history buffers for temporal computations
- **Conservative flow accounting** that tracks attempts, successes, failures, and retry echoes
- **Forward-only DAG evaluation** with explicit handling of temporal dependencies

> **Implementation update (Mar 2025):** The expression engine now ships `CONV` with inline kernel literals (e.g., `CONV(failures, [0.0, 0.6, 0.3, 0.1])`). The template `templates/supply-chain-incident-retry.yaml` demonstrates end-to-end wiring—throughput vs effort edges, attempts/failures/retryEcho series, and UI toggles.

---

## Reference Assets

- **Template:** `templates/supply-chain-incident-retry.yaml` (24-bin IT ops incident workflow with deterministic kernel)
- **Fixture:** `fixtures/time-travel/retry-service-time/` — deterministic 4-bin telemetry bundle used by API/UI tests; copy under `data/runs/<id>/model` to replay server-computed edges locally.
- **API Goldens:** `tests/FlowTime.Api.Tests/Golden/state-window-*.json`, `graph-run_graph_fixture.json`
- **UI Tests:** `tests/FlowTime.UI.Tests/TimeTravel/*` covering chips, edge payloads, and inspector toggles

### Domain Terminology Aliases (TT‑M‑03.30.1)

To reduce cognitive load for operators, templates can now declare domain-specific aliases for retry metrics (e.g., “Ticket Submissions” instead of `attempts`). Authors add an `aliases` dictionary under `topology.nodes[].semantics`, the engine preserves it end-to-end, and `/graph` + `/state_window` expose the mapping. The topology UI consumes aliases for inspector chips, dependency lists, and canvas tooltips while still retaining canonical field names for automation. See `docs/templates/metric-alias-authoring.md` for authoring guidance.

### Kernel Governance & Artifact Precompute

- Retry kernels are subject to lightweight policy checks during load:
  - Maximum length: `32` coefficients (longer kernels are trimmed with a warning).
  - Maximum mass: coefficients are scaled to keep ∑coefficients ≤ `1.0`.
  - Negative or non-finite values are clamped to `0`.
- Warnings surface through `/state`/`/state_window` as `retry_kernel_policy` telemetry messages so UI surfaces can reflect policy adjustments.
- Simulation artifacts automatically precompute retry echo series when `semantics.retryEcho` is mapped but no series is present, mirroring the queue depth precompute path. This keeps runtime evaluation causal and removes the need for ad-hoc CONV nodes in generated models.
- When templates reference retry metrics but the underlying telemetry is absent, the API surfaces structured warnings (`attempts_series_missing`, `failures_series_missing`, `retry_echo_series_missing`). `retryEcho` is derived on the fly via the configured kernel when missing, but the warning remains so operators can backfill telemetry if needed.

### Retry Governance & Terminal Disposition

Retries require explicit guardrails so models capture both the **retry pressure** and the **failure termination path**. We distinguish three layers of governance:

1. **Attempt Budget** – Maximum number of retries permitted per work item (including the initial attempt).  
2. **Decision Strategy** – What happens when an attempt fails (retry immediately, backoff, escalate).  
3. **Terminal Disposition** – Where permanently failed work lands after exhausting the budget.

#### Modeling Max-Attempt Budgets

- Templates MAY introduce `maxAttempts` on service nodes.  
- The simulation engine should track per-item attempt counts and divert failures that reach the limit into an `exhaustedFailures` series.  
- `attempts = served + failures`, but once `attemptCount >= maxAttempts`, additional failures increment both `failures` and `exhaustedFailures` while **no longer feeding the retry kernel**.  
- Effort edges use total attempts; throughput edges continue to reflect only successful work.

```yaml
nodes:
  - id: incident_intake
    kind: service
    semantics:
      attempts: series:incident_attempts
      failures: series:incident_failures
      retryEcho: series:incident_retry_echo
      exhaustedFailures: series:incident_exhausted
      maxAttempts: 4
```

#### Terminal Disposition (DLQ / Escalation)

- Exhausted items MUST be routed to a dedicated node (e.g., `incident_dlq`) via a **terminal edge**:
  - `type: terminal`
  - `measure: exhaustedFailures`
  - Optional `lag` to delay escalation.
- The terminal node can be modeled as:
  - A queue (async DLQ) with depth/latency metrics.
  - A service that represents an escalation team.  
- APIs should surface `exhaustedFailures` (count) and optional `escalated` series (success downstream of DLQ).
- UI chips/inspector blocks mirror this by adding “Exhausted” and “Escalated” metrics.

```yaml
edges:
  - from: incident_intake
    to: incident_dlq
    type: terminal
    measure: exhaustedFailures
    lag: 0
```

#### Contract Implications

- `/v1/runs/{id}/graph` adds `terminal` to `EdgeType`.  
- `/v1/runs/{id}/state_window` includes:
  - `exhaustedFailures(t)` (service node)  
  - `escalated(t)` or `dlqDepth(t)` for downstream node  
  - Optional `retryBudgetRemaining(t)` for visualization.  
- Telemetry schema must document the new series plus governance metadata:
  - `maxAttempts`
  - `backoffStrategy` (optional descriptive enum/string)
  - `exhaustedPolicy` (e.g., `escalate`, `drop`, `dead-letter`).

#### UI Considerations

- Canvas: terminal edges use a distinct stroke (e.g., dotted crimson) and badges display retry budget.  
- Inspector: service stack shows Attempts, Served, Failures, Retry Echo, **Exhausted**, and, if present, **Budget Remaining**.  
- Feature bar gains toggles for “Show Retry Budget” and “Show Terminal Edges”.

> **Gap (TT‑M‑03.28)**: Current milestone implements attempts/failures/retryEcho but does not yet enforce max-attempt budgets or terminal destinations. Governance support is slated for a follow-up milestone (recommended TT‑M‑03.30 or TT‑M‑03.31 depending on backlog). See _Delivery Roadmap_ for scheduling guidance.

---

## Core Retry Modeling Concepts

### 1. Dual Edge Types

FlowTime distinguishes between two fundamental edge types that capture different aspects of system behavior:

#### **Throughput Edges (Success-Driven)**
- **Purpose**: Model what flows downstream after successful completion
- **Semantics**: `Service A —served→ Queue B`
- **Driver**: Success count drives arrivals at downstream components  
- **Conservation**: `served_A = arrivals_B` (with potential time lag)

```yaml
# YAML representation
edges:
  - from: serviceA
    to: queueB
    type: throughput
    measure: served      # Only successes flow downstream
    lag: 0               # Immediate or delayed transfer
```

#### **Effort Edges (Attempt-Driven)**  
- **Purpose**: Model synchronous dependencies regardless of success/failure
- **Semantics**: `Service A —attempts→ Database D`
- **Driver**: Total attempts (successes + failures) drive dependency load
- **Conservation**: `attempts_A = load_D` (with potential amplification)

```yaml
# YAML representation  
edges:
  - from: serviceA
    to: databaseD
    type: effort
    measure: attempts    # All attempts create dependency load
    multiplier: 2.5      # Each service attempt = 2.5 DB calls
```

#### **Key Computational Rule**
```
Downstream arrivals = f(successes)    # Throughput edges
Dependency load = f(attempts)         # Effort edges
Retries = f(failures)                 # Create future attempts
```

### 2. Retry-Induced Temporal Effects

Retries create **temporal echoes** where past failures influence future system behavior:

#### **Internal Retries (Capacity Tax)**
```yaml
nodes:
  - id: serviceA
    kind: expr
    expr: |
      # Internal retry modeling - failures consume capacity but don't flow downstream
      attempts := MIN(capacity, arrivals)
      successes := attempts * (1 - internal_failure_rate)
      served := successes                    # Only successes go downstream
      capacity_tax := attempts - successes   # Failed attempts waste capacity
```

#### **Externalized Retries (Re-queuing)**
```yaml
nodes:
  - id: serviceA  
    kind: expr
    expr: |
      # External retry modeling - failures create future arrivals
      arrivals_total := arrivals + retries
      attempts := MIN(capacity, arrivals_total)
      failures := attempts * failure_rate
      successes := attempts - failures
      
      # Temporal convolution creates retry echoes
      retries := CONV(failures, [0.0, 0.6, 0.3, 0.1])
      served := successes
```

### 3. Conservation Laws with Retry Accounting

FlowTime enforces **multi-dimensional conservation** that accounts for retry effects:

#### **Primary Conservation (Flow Balance)**
```
arrivals + retries - served - ΔQ - dlq ≈ 0
```

#### **Effort Conservation (Attempt Accounting)**  
```
attempts = successes + failures
attempts ≥ served (always)
served = successes (by definition)
```

#### **Temporal Conservation (Retry Mass)**
```
∑retries[t+k] = ∑failures[t] * retry_fraction
```

---

## Computational Architecture

### 1. Forward-Only DAG Evaluation

FlowTime maintains **causal evaluation order** despite temporal feedback:

#### **Evaluation Algorithm**
```csharp
// Pseudo-code for retry-aware evaluation
foreach (int timeBin in 0..grid.Bins-1)
{
    // Phase 1: Compute current-time values
    foreach (var node in topologicalOrder)
    {
        if (node is StatefulNode stateful)
        {
            // Access historical values via bounded buffers
            var historicalInputs = stateful.GetHistoricalInputs(timeBin);
            var currentValue = stateful.Evaluate(timeBin, historicalInputs);
            stateful.UpdateHistory(timeBin, currentValue);
        }
        else
        {
            // Standard evaluation - no temporal dependencies
            node.Evaluate(timeBin);
        }
    }
    
    // Phase 2: Validate conservation laws
    conservationValidator.ValidateTimeBin(timeBin);
}
```

#### **Bounded History Management**
```csharp
public class StatefulNode : INode
{
    private readonly CircularBuffer<double> _history;
    private readonly int _maxLag;
    
    public double Evaluate(int currentBin, Dictionary<string, double> inputs)
    {
        // Access past values with bounds checking
        var laggedValues = GetLaggedInputs(currentBin, inputs);
        var result = ComputeWithHistory(laggedValues);
        
        // Update history for future temporal operations
        _history.Add(currentBin, result);
        return result;
    }
    
    private Dictionary<string, double> GetLaggedInputs(int currentBin, Dictionary<string, double> inputs)
    {
        var laggedInputs = new Dictionary<string, double>();
        foreach (var input in inputs)
        {
            // SHIFT(input, k) retrieves input[currentBin - k] 
            // CONV operations access multiple historical bins
            laggedInputs[input.Key] = AccessHistoricalValue(input.Key, currentBin);
        }
        return laggedInputs;
    }
}
```

### 2. Temporal Operators for Retry Modeling

#### **CONV Operator (Retry Convolution)**
```yaml
# Retry echo modeling
retries := CONV(failures, [0.0, 0.6, 0.3, 0.1])
# failures[t-0] * 0.0 + failures[t-1] * 0.6 + failures[t-2] * 0.3 + failures[t-3] * 0.1
```

**Implementation:**
```csharp
public class ConvNode : StatefulNode
{
    private readonly double[] _kernel;
    private readonly CircularBuffer<double> _inputHistory;
    
    public override double Evaluate(int currentBin, Dictionary<string, double> inputs)
    {
        var inputValue = inputs["input"];
        _inputHistory.Add(currentBin, inputValue);
        
        double result = 0.0;
        for (int k = 0; k < _kernel.Length; k++)
        {
            if (currentBin - k >= 0)
            {
                result += _inputHistory.Get(currentBin - k) * _kernel[k];
            }
        }
        return result;
    }
}
```

#### **SHIFT Operator (Time Delay)**
```yaml
# Simple time shifting for lag modeling
previous_failures := SHIFT(failures, 1)  # failures[t-1]
capacity_adjustment := base_capacity + SHIFT(overload_factor, 2)
```

#### **EMA Operator (Exponential Moving Average)**
```yaml
# Smoothed failure rate estimation
smoothed_failure_rate := EMA(observed_failures / attempts, 0.1)
adjusted_capacity := capacity * (1 - smoothed_failure_rate)
```

### 3. Algebraic Loop Handling

#### **Design Philosophy: Avoid Algebraic Loops**

FlowTime's architecture **deliberately avoids** algebraic loops to maintain:
- **Deterministic evaluation** order
- **Performance predictability** (O(nodes × bins) complexity)
- **Clear causal semantics** for temporal modeling

#### **Prohibited Patterns**
```yaml
# ❌ FORBIDDEN: Direct algebraic dependency
nodes:
  - id: serviceA
    expr: "capacity - BACKPRESSURE(serviceB)"   # serviceB depends on serviceA
  - id: serviceB  
    expr: "MIN(arrivals_from_A, capacity)"     # Creates algebraic loop
```

#### **Permitted Alternatives**
```yaml
# ✅ ALLOWED: Time-delayed feedback  
nodes:
  - id: serviceA
    expr: "capacity - SHIFT(backpressure_signal, 1)"  # Uses previous bin's backpressure
  - id: serviceB
    expr: "MIN(arrivals_from_A, capacity)"
  - id: backpressure_signal
    expr: "CLAMP(queue_depth_B / max_depth, 0, 1)"
```

#### **Loop Detection Algorithm**
```csharp
public class AlgebraicLoopDetector
{
    public void ValidateDAG(Graph graph)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        
        foreach (var node in graph.Nodes)
        {
            if (!visited.Contains(node.Id))
            {
                if (HasCycle(node, visited, recursionStack, graph))
                {
                    throw new AlgebraicLoopException($"Loop detected involving node: {node.Id}");
                }
            }
        }
    }
    
    private bool HasCycle(Node node, HashSet<string> visited, HashSet<string> recursionStack, Graph graph)
    {
        visited.Add(node.Id);
        recursionStack.Add(node.Id);
        
        foreach (var dependency in node.GetCurrentTimeDependencies()) // Only same-time-bin dependencies
        {
            if (!visited.Contains(dependency) || recursionStack.Contains(dependency))
            {
                return true; // Cycle found
            }
            
            if (HasCycle(graph.GetNode(dependency), visited, recursionStack, graph))
            {
                return true;
            }
        }
        
        recursionStack.Remove(node.Id);
        return false;
    }
}
```

---

## Complex System Examples

### 1. Service with Internal Retries

**Scenario**: Web service that retries database calls internally

```yaml
grid: { bins: 24, binMinutes: 60 }
nodes:
  # External arrivals to web service
  - id: web_arrivals
    kind: const
    values: [100, 120, 110, ...]
  
  # Web service processing with internal DB retries
  - id: web_service
    kind: expr
    expr: |
      # Internal retry capacity tax
      base_attempts := MIN(web_capacity, web_arrivals)
      db_calls_per_request := 2.5                    # Effort edge multiplier
      db_attempts := base_attempts * db_calls_per_request
      
      # Database success rate affects web service success
      db_success_rate := 0.95
      web_success_rate := db_success_rate ^ db_calls_per_request  # Compound failure probability
      
      web_successes := base_attempts * web_success_rate
      web_failures := base_attempts - web_successes
      served := web_successes  # Only successes flow downstream

edges:
  # Throughput edge: only successful web requests go downstream  
  - from: web_service
    to: downstream_queue
    type: throughput
    measure: served
    
  # Effort edge: all web attempts create database load
  - from: web_service  
    to: database
    type: effort
    measure: db_attempts
```

**Visualization Needs**:
- **Capacity utilization** with retry tax overlay
- **Success vs attempt rates** trending over time
- **Database load** correlation with web service attempts
- **Efficiency metrics** (successes per attempt)

### 2. Externalized Retry Queue System

**Scenario**: Message queue system with explicit retry queues

```yaml
grid: { bins: 24, binMinutes: 60 }
nodes:
  # Main processing queue
  - id: main_queue
    kind: backlog
    expr: "arrivals + retry_arrivals - served"
    
  # Service processing with externalized retries  
  - id: message_processor
    kind: expr
    expr: |
      processing_attempts := MIN(capacity, queue_depth)
      successes := processing_attempts * success_rate
      failures := processing_attempts - successes
      
      # Retry policy: 3 attempts max, exponential backoff
      retry_candidates := failures * transient_failure_fraction
      retry_pattern := [0.0, 0.6, 0.3, 0.1]  # 0% immediate, 60% next hour, etc.
      retries := CONV(retry_candidates, retry_pattern)
      
      # DLQ for exhausted retries (simplified)
      dlq_additions := failures * permanent_failure_fraction
      
      served := successes

  # Retry feedback loop
  - id: retry_arrivals  
    kind: expr
    expr: "retries"  # From CONV operation above
    
  # Dead letter queue accumulation
  - id: dlq_depth
    kind: expr  
    expr: "CUMSUM(dlq_additions)"

edges:
  # Throughput: successes flow to downstream systems
  - from: message_processor
    to: downstream_service
    type: throughput
    measure: served
    
  # Retry feedback: failed messages re-enter main queue with delay
  - from: message_processor
    to: main_queue
    type: throughput  
    measure: retry_arrivals
    lag: 1  # Minimum 1-bin delay for retry processing
```

**Visualization Needs**:
- **Queue depth** with retry vs new message breakdown
- **Retry echo patterns** showing temporal failure propagation  
- **DLQ growth rate** and exhaustion trends
- **System efficiency** (end-to-end success rate including retries)

### 3. Multi-Service Retry Propagation

**Scenario**: Microservice chain where failures cascade and create retry storms

```yaml
grid: { bins: 24, binMinutes: 60 }
nodes:
  # Service A: Frontend service
  - id: service_a
    kind: expr
    expr: |
      attempts_a := MIN(capacity_a, arrivals)
      failures_a := attempts_a * failure_rate_a
      successes_a := attempts_a - failures_a
      
      # Service A retries internally before giving up
      internal_retries_a := CONV(failures_a, [0.0, 0.8, 0.2])
      served_a := successes_a  # Only successes go to Service B

  # Service B: Downstream service affected by A's retry patterns
  - id: service_b  
    kind: expr
    expr: |
      # Arrivals include original successes plus any retry effects
      total_arrivals_b := served_a + SHIFT(retry_storm_effect, 1)
      attempts_b := MIN(capacity_b, total_arrivals_b)
      
      # Service B failure rate increases under retry storm pressure
      pressure_factor := CLAMP(total_arrivals_b / normal_capacity_b, 1.0, 3.0)
      effective_failure_rate_b := base_failure_rate_b * pressure_factor
      
      failures_b := attempts_b * effective_failure_rate_b
      successes_b := attempts_b - failures_b
      served_b := successes_b

  # Retry storm detection
  - id: retry_storm_effect
    kind: expr
    expr: |
      # Detect when Service A retries create overload at Service B
      retry_load := internal_retries_a * retry_amplification_factor
      MAX(0, retry_load - normal_retry_tolerance)

edges:
  - from: service_a
    to: service_b
    type: throughput
    measure: served_a
    
  # Effort edge: Service B makes database calls for each attempt
  - from: service_b
    to: shared_database
    type: effort  
    measure: attempts_b
    multiplier: 1.5
```

**Visualization Needs**:
- **Cascading failure** visualization across service chain
- **Retry storm** detection and amplitude measurement
- **Cross-service** pressure correlation analysis
- **Database load** amplification from upstream retry behavior

---

## Advanced Computational Considerations

### 1. When Algebraic Loops Might Be Needed (Future Research)

#### **Theoretical Cases Requiring Iteration**
While FlowTime avoids algebraic loops, certain complex scenarios might benefit from iterative solvers:

**Immediate Backpressure Systems:**
```yaml
# Hypothetical: Immediate capacity sharing between services
service_a_capacity := total_capacity * (1 - service_b_pressure)
service_b_capacity := total_capacity * (1 - service_a_pressure)  
service_a_pressure := queue_depth_a / max_depth
service_b_pressure := queue_depth_b / max_depth
# This creates an algebraic loop requiring iteration
```

**Real-Time Load Balancing:**
```yaml
# Hypothetical: Dynamic traffic splitting based on current performance
traffic_to_a := total_traffic * (response_time_b / (response_time_a + response_time_b))
traffic_to_b := total_traffic - traffic_to_a
response_time_a := f(traffic_to_a, current_load_a)  # Circular dependency
response_time_b := f(traffic_to_b, current_load_b)
```

#### **Proposed ITERATE Function (Very Low Priority)**
```yaml
# Hypothetical future syntax for algebraic loop handling
nodes:
  - id: equilibrium_solution
    kind: iterate
    maxIterations: 10
    tolerance: 0.001
    variables: [service_a_capacity, service_b_capacity]
    constraints:
      - "service_a_capacity + service_b_capacity <= total_capacity"
      - "service_a_pressure = f(service_a_capacity)"
      - "service_b_pressure = f(service_b_capacity)"
```

#### **Why Defer Algebraic Loops**
1. **Complexity explosion**: Iterative solvers add significant implementation complexity
2. **Performance impact**: Non-linear solving is much slower than linear DAG evaluation  
3. **Determinism loss**: Convergence issues can lead to non-deterministic results
4. **Rare practical need**: Most real systems have inherent delays that break algebraic loops
5. **Alternative modeling**: Time-delayed feedback usually captures system behavior adequately

### 2. Memory and Performance Optimization

#### **Bounded History Management**
```csharp
public class HistoryBuffer
{
    private readonly double[] _buffer;
    private readonly int _maxHistory;
    private int _currentIndex = 0;
    
    // Memory usage: O(maxLag) per stateful node, not O(totalBins)
    public void Add(int timeBin, double value)
    {
        _buffer[timeBin % _maxHistory] = value;
    }
    
    public double Get(int lagBins)
    {
        var index = (_currentIndex - lagBins) % _maxHistory;
        return _buffer[index >= 0 ? index : index + _maxHistory];
    }
}
```

#### **Computational Complexity**
- **Target**: O(nodes × bins) evaluation time
- **Memory**: O(nodes × maxLag) space for temporal operations
- **Optimization**: Vectorized operations for large bin counts

---

## Data Analysis and Visualization Requirements

### 1. Retry Pattern Analysis

#### **Temporal Echo Visualization**
- **Heatmaps** showing retry wave propagation over time
- **Correlation plots** between failure spikes and subsequent retry volumes
- **Kernel visualization** showing retry distribution patterns

#### **Conservation Tracking**
- **Flow balance** charts with retry accounting
- **Attempt vs success** efficiency trending
- **DLQ accumulation** rate monitoring

### 2. Performance Impact Analysis

#### **Capacity Tax Visualization**
- **Utilization breakdown**: successful vs failed capacity consumption
- **Retry overhead** as percentage of total system capacity
- **Efficiency trending** with retry rate correlation

#### **Downstream Effect Analysis**
- **Propagation delay** visualization across service chains
- **Amplification factor** measurement for retry storms
- **Cross-service** correlation analysis

### 3. Operational Insights

#### **Retry Policy Optimization**
- **What-if analysis** for different retry kernel configurations
- **Policy comparison** showing trade-offs between attempt limits and DLQ rates
- **Sensitivity analysis** for failure rate fluctuations

#### **System Health Monitoring**  
- **Early warning** indicators for retry storm conditions
- **Capacity pressure** visualization across service dependencies
- **SLA impact** assessment from retry behavior

---

## Implementation Roadmap Alignment

### Phase 1: Foundation (M-01.05 - M-3)
- Basic expression language with SHIFT operator
- Simple stateful node architecture
- Conservation law validation

### Phase 2: Temporal Operators (M-09.05)
- CONV operator for retry convolution
- DELAY operator for arbitrary time shifting  
- EMA operator for smoothed metrics
- Comprehensive retry modeling

### Phase 3: Advanced Analysis (M-10+)
- Multi-class retry behavior
- Cross-system integration validation
- Performance optimization for large-scale models

### Phase 4: Research Topics (M-15+)
- Algebraic loop investigation (if practical need emerges)
- Advanced iterative solvers (very low priority)
- Real-time streaming analysis capabilities

---

## Conclusion

FlowTime engine's retry modeling architecture provides **comprehensive computational capabilities** for analyzing and visualizing retry behaviors while maintaining deterministic, forward-only evaluation. The dual edge type system (throughput vs effort) combined with temporal operators enables rich modeling of complex retry patterns and their downstream effects.

By **avoiding algebraic loops** and using **bounded history buffers**, FlowTime maintains predictable performance characteristics while supporting sophisticated retry analysis. The architecture is designed to scale from simple internal retry modeling to complex multi-service retry propagation scenarios.

This foundation enables data-driven optimization of retry policies, early detection of retry storms, and comprehensive understanding of how failure patterns propagate through complex distributed systems.
- **Retry Edge Slice (TT‑M‑03.31)**  
  `/v1/runs/{runId}/state_window` now emits a server-computed `edges` collection (when requested) describing each retry-relevant dependency:
  - `id`, `from`, `to`, `edgeType`, `field`, `multiplier`, `lag`.
  - Series keys: `attemptsLoad`, `failuresLoad`, `retryRate` (aligned with `window.startBin`).  
  UI surfaces consume those series directly for overlay rendering; clients no longer derive retry metrics from node data.
