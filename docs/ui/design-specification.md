# FlowTime UI Design Specification (Engineering Draft)

**Version:** 1.0
**Audience:** UI engineers (Blazor, Power BI integration), architects, analysts
**Purpose:** Define a complete, end-to-end spec for the FlowTime UI, including modules, pages, functionality, and interactions. Covers **baseline functionality** (graph/time-travel) and **advanced PMF workflows** (library, editor, scenario integration).

---

## 1. Principles

1. **API-First**

   * All UI actions must map directly to FlowTime API endpoints (`/graph`, `/run`, `/state`, `/scenario/run`, etc).
   * No hidden client-only logic; outputs must be reproducible via API.

2. **Single Source of Truth**

   * Engine outputs (CSV/Parquet + run.json) are canonical.
   * UI only **renders or edits models/PMFs** — never alters results post hoc.

3. **Deterministic + Explainable**

   * PMFs, expressions, and scenarios must be **visualized alongside their YAML/JSON form** for auditability.
   * Analysts can copy/paste overlays and PMFs into git repos.

4. **Progressive Complexity**

   * Minimal viable UI (charts, runs) available from day 1.
   * Advanced PMF tools can be layered on later milestones.

---

## 2. Major UI Modules

### 2.1 Graph Explorer

* **Purpose:** Show system topology and flows, core of the “digital twin.”
* **Features:**

  * DAG visualization (elkjs layout, PixiJS rendering).
  * Node overlays: backlog, SLA color, utilization ring, queue badge.
  * Time scrubber: navigate bin-by-bin, animate runs.
  * Drill-down inspector with sparklines for arrivals, served, queue, latency.
  * Filter by class (VIP, Order, Refund, etc).
  * Collapse/expand subsystems.
* **Integration:** `/graph` + `/state?ts=` + `/state_window`.

---

### 2.2 Run Manager

* **Purpose:** Trigger, view, and compare engine runs.
* **Features:**

  * Upload/edit YAML model.
  * Run button → calls `/run`.
  * Status/progress indicator (async pattern).
  * Results viewer: charts, run.json metadata, scenario comparisons.
* **Integration:** `/run`, `/runs/{id}`, `/runs/{id}/export`.

---

### 2.3 Scenario Composer

* **Purpose:** Define and manage overlays (surge, outage, retry policy change).
* **Features:**

  * Visual overlay editor (time window picker, node/param selection).
  * PMF attachment (choose from Library or inline edit).
  * Side-by-side Baseline vs Scenario charts.
  * Delta chips (backlog-hours, SLA attainment, peak queue).
* **Integration:** `/scenario/run`, YAML preview/export.

---

### 2.4 PMF Library (new module)

* **Purpose:** Central repository of reusable PMFs.
* **Features:**

  * List all stored PMFs with metadata (name, description, version, source).
  * Tagging (retry, delay, arrivals, daily-shape).
  * Search/filter.
  * Versioning: history with diffs.
* **Data contract:** Stored as YAML/JSON arrays, normalized at save time.
* **Integration:** Backed by `/learn/retry`, `/learn/delay`, or manual upload.

---

### 2.5 PMF Editor

* **Purpose:** Create or edit PMFs visually.
* **Features:**

  * Histogram canvas: add/remove bins, drag sliders to adjust probabilities.
  * Auto-normalization (sum=1.0).
  * Stats panel: expected value, variance, percentiles.
  * Templates: geometric, uniform, weekday shape.
  * Telemetry fitting wizard: select telemetry window → compute empirical PMF.
* **Integration:** Save to Library; attach to scenarios or nodes.

---

### 2.6 PMF Comparison

* **Purpose:** Visualize changes between two PMFs.
* **Features:**

  * Side-by-side histograms.
  * Delta overlay (bar chart of differences).
  * Impact preview: apply both PMFs to same base arrivals/errors → show differences in retries/queues.
* **Integration:** Compare versions in Library or Scenario vs Baseline.

---

### 2.7 Telemetry Overlay

* **Purpose:** Compare model output vs real telemetry.
* **Features:**

  * Overlay telemetry CSV/Gold on charts.
  * Show drift (residuals).
  * Option to “fit PMF” from telemetry segment (send to PMF Editor).
* **Integration:** `/learn/capacity`, `/learn/routing`, `/learn/retry`.

---

### 2.8 Dashboard / Reports

* **Purpose:** Executive and SRE-friendly reporting.
* **Features:**

  * SLA attainment over time.
  * Bottleneck heatmaps.
  * Scenario cost/SLA trade-offs.
* **Integration:** Power BI integration with Gold telemetry + run outputs.

---

### 2.9 Development Mode

* **Purpose:** Support UI development and debugging when backend services are unavailable or incomplete.
* **Features:**

  * **Mock Data Generators:**
    * Generate synthetic run artifacts (run.json, series CSV, manifest.json) for UI testing
    * Parameterizable scenarios (node count, time range, PMF complexity)
    * Realistic data distributions matching production patterns
    * Edge case generators (empty runs, failed runs, large datasets)

  * **Debug Panels:**
    * API call inspector showing request/response payloads
    * Network timing and error logs
    * Schema validation results with clear error messages
    * State inspection for complex UI components (PMF editor, graph renderer)

  * **Development Helpers:**
    * Hot-reload for YAML model changes
    * Performance profiler for chart rendering and large dataset handling
    * Memory usage monitor for browser-based simulations
    * Component isolation mode for testing individual modules

  * **Testing Infrastructure:**
    * Golden dataset library for consistent UI testing
    * Screenshot regression testing for charts and graphs
    * PMF validation suite ensuring normalization and edge cases
    * Cross-browser compatibility indicators

* **Integration:** 
  * Toggle between live API and mock mode
  * Export captured API interactions as test fixtures
  * Development-only routes (`/dev/mock`, `/dev/debug`, `/dev/perf`)

---

## 3. User Workflows

### a) Developer (Model Authoring)

1. Open **Scenario Composer**.
2. Create overlay with retry PMF.
3. Use **PMF Editor** to design kernel visually.
4. Save to Library.
5. Run scenario → view results in Graph Explorer.

### b) Analyst (Telemetry-Driven)

1. Load telemetry overlay in **Telemetry Overlay** page.
2. Fit empirical PMF (e.g., retry distribution).
3. Compare fitted PMF vs baseline in **PMF Comparison**.
4. Run scenario with fitted PMF to validate.

### c) SRE (Ops)

1. Incident review: scrub Graph Explorer around 14:00–16:00.
2. Observe spike in errors → open **PMF Editor** with retry kernel.
3. Adjust retry policy (lighter/heavier tail).
4. Run scenario → confirm reduced backlog hours.
5. Export YAML overlay for rollout discussion.

---

## 4. Page-Level Functional Requirements

* **Graph Explorer**

  * FR-G1: Render DAG with per-node overlays.
  * FR-G2: Time scrubber with animation.
  * FR-G3: Drill-down inspector.

* **Run Manager**

  * FR-R1: Upload YAML, trigger run.
  * FR-R2: Async progress monitor.
  * FR-R3: Compare runs.

* **Scenario Composer**

  * FR-S1: Overlay editor with time window.
  * FR-S2: PMF attach from Library.
  * FR-S3: Baseline vs Scenario delta chips.

* **PMF Library**

  * FR-P1: List PMFs with metadata.
  * FR-P2: Tagging and search.
  * FR-P3: Version history.

* **PMF Editor**

  * FR-E1: Interactive histogram with auto-normalization.
  * FR-E2: Stats panel (mean, variance).
  * FR-E3: Template + telemetry fitting.

* **PMF Comparison**

  * FR-C1: Side-by-side histograms.
  * FR-C2: Delta visualization.
  * FR-C3: Apply to same base series and preview impact.

* **Telemetry Overlay**

  * FR-T1: Overlay telemetry vs model.
  * FR-T2: Fit PMFs from telemetry.
  * FR-T3: Drift visualization.

---

## 5. Non-Functional

* **Performance:** Handle \~300 nodes × 90 days × 5m bins in browser (buffer + downsampling).
* **Auditability:** Every PMF/scenario edit has a YAML preview and is versioned.
* **Usability:** Non-experts can design PMFs without touching YAML.
* **Consistency:** PMF values and model overlays must always normalize and align to canonical grid.

---

## 6. Future Extensions

* **Uncertainty bands:** Monte Carlo runs → UI shading for P50/P90.
* **Collaboration:** PMF library with comments, sharing.
* **Marketplace:** Import/export PMFs between domains (logistics, healthcare, IT).

---

✅ This spec is detailed enough that a **UI engineer could start implementing** the modules, while keeping in sync with the **engine** and **simulator** contracts.

