* **Telemetry = just another input type**
* **Compare = a button on Run results**

---

# Main Menu

```
+--------------------------------------------------+
|  FlowTime UI                                     |
+--------------------------------------------------+
|  [Models]   [Runs]   [Artifacts]   [Learn]*      |
+--------------------------------------------------+
*Learn = separate surface, unchanged
```

---

# Models (FlowTime-Sim)

```
Models
----------------------------------------------------
[Templates]  [Stochastic Inputs]  [YAML Editor]

Template List:
  - Checkout Flow
  - API Fanout
  - Queue/Retry
  ...

[Preview DAG]  [Save as Model Artifact]
```

Artifacts saved here are selectable later in **Runs**.

---

# Runs (FlowTime-Engine)

### Wizard Tabs

```
Runs Wizard
----------------------------------------------------
[1. Select Input] -> [2. Configure Run] -> [3. Compute] -> [4. Results]
```

**1. Select Input**

```
Choose Input Source:
 (o) Model Artifact   [Browse Registry...]
 ( ) Telemetry Artifact [Browse Registry...]
 ( ) Upload Gold CSV   [Upload...]

[Next >]
```

**2. Configure Run**

```
Run Settings:
  Grid: [PT5M v]
  Horizon: [24h v]
  Overlay: [Select overlay.yaml] (optional)
  [ ] Generate Telemetry Output

[< Back]  [Next >]
```

**3. Compute**

```
[ Run Now ]
Progress...
✔ Engine run complete (Run ID: run_1234)

[Next >]
```

**4. Results**

```
+--------------------------------------------------+
| DAG View (nodes + edges)                         |
| Node color = latency/error; Edge width = volume  |
+--------------------------------------------------+
[Time Slider] [Metrics Panel]

Buttons:
 [Export CSV/JSON]   [Compare...]   [Back to Runs]
```

---

# Compare Flow (branched from Results)

**Click \[Compare...] → new wizard**

```
Compare Wizard
----------------------------------------------------
[1. Select Second Input] -> [2. Configure] -> [3. Results]
```

**1. Select Second Input**

```
Baseline = run_1234 (current run)

Select Comparison Input:
 (o) Model Artifact   [Browse Registry...]
 ( ) Telemetry Artifact [Browse Registry...]
 ( ) Upload Gold CSV   [Upload...]

[Next >]
```

**2. Configure**

```
Align Time Windows: [Auto v]
Normalize Metrics:  [None v]

[< Back]  [Next >]
```

**3. Results**

```
Side-by-side Charts (Baseline vs Comparison)
Delta DAG View (%Δ latency, throughput, errors)

Buttons:
 [Export diff.json]   [Back to Runs]
```

---

# Artifacts Registry

```
Artifacts
----------------------------------------------------
Filter: [All v]  Search: [________]

Cards:
  [Model] checkout_v1     Created: 2025-09-19
   Actions: [Run] [Edit] [Delete]

  [Run] run_1234          Created: 2025-09-20
   Actions: [Open Results] [Compare] [Export]

  [Telemetry] prod_w35    Created: 2025-09-18
   Actions: [Replay in Engine] [Compare] [Delete]
```

---

✅ This flow ensures:

* **Telemetry import is implicit** (just pick Telemetry/Upload in Runs).
* **Compare is contextual** (starts from a Run’s results or from an Artifact card).
* **Artifacts registry** is your persistent source of truth, so nothing “disappears” from the UI.
