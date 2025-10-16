# FlowTime Time-Travel UI Visualizations
**Date**: October 7, 2025  
**Purpose**: UX specifications for time-travel visualization components  
**API Contract**: See `TIME-TRAVEL-ARCHITECTURE-PLAN.md` → "UI Contract" section

---

## Design Principles

1. **Time-Travel First**: Every view can be scrubbed through time
2. **SLA-Centric**: Highlight SLA compliance/breaches prominently
3. **Flow-Oriented**: Organize by flows, not individual nodes
4. **Progressive Disclosure**: Summary → Detail on demand
5. **Data Density**: Show trends (mini graphs) without overwhelming

### Implementation Notes — October 2025
- Live ADX ingestion is deferred; UX demos should rely on canonical run bundles produced by the telemetry capture + bundling workflow from M-03.02.
- Run creation currently happens through CLI orchestration (`flowtime telemetry capture` / `bundle` + `flowtime run`); UI-facing run APIs will be introduced in a future milestone.
- Initial visualisations can use built-in chart components or off-the-shelf chart libraries. Full topology graph layout will depend on an external layout engine in a future milestone (TBD) rather than bespoke client-side positioning.

---

## View 1: SLA Dashboard

**Purpose**: At-a-glance SLA compliance for all flows

### Layout

```
+------------------------------------------------------------+
|  FlowTime - SLA Dashboard                   [Time Range v] |
+------------------------------------------------------------+
|                                                            |
|  +-------------+  +-------------+  +-------------+         |
|  |   Orders    |  |   Billing   |  |  Inventory  |         |
|  |             |  |             |  |             |         |
|  |    95.8%    |  |    89.2%    |  |   100.0%    |         |
|  |   SLA Met   |  |   SLA Met   |  |   SLA Met   |         |
|  |             |  |             |  |             |         |
|  |  23/24 bins |  |  21/24 bins |  |  24/24 bins |         |
|  |   v Green   |  |   ! Yellow  |  |   v Green   |         |
|  +-------------+  +-------------+  +-------------+         |
|                                                            |
|  Click any flow to see detailed graph -->                  |
+------------------------------------------------------------+
```

### SLA Box Spec

**Data Source**: `GET /v1/runs/{runId}/metrics?start={start}&end={end}`

**Box Contents**:
- **Flow Name**: (e.g., "Orders")
- **SLA %**: Large font, 2 decimal places (e.g., "95.8%")
- **Label**: "SLA Met" or "SLA Breached"
- **Bin Count**: "{bins_meeting_sla}/{bins_total} bins"
- **Visual Indicator**: ✓ or ⚠ icon

**Color Coding**:
```yaml
Green:  sla_pct >= 95.0
Yellow: 90.0 <= sla_pct < 95.0
Orange: 80.0 <= sla_pct < 90.0
Red:    sla_pct < 80.0
```

**Interaction**:
- Click box → navigate to Flow Graph view for that flow
- Hover → tooltip with worst latency, avg latency, total errors

**Example Tooltip**:
```
Orders Flow (last 24h)
─────────────────────
SLA Met: 23/24 bins (95.8%)
Worst Latency: 6.2 min
Avg Latency: 1.4 min
Total Errors: 12
```

---

## View 2: Flow Graph

**Purpose**: Visualize topology with real-time state and trends

### Layout

```
┌────────────────────────────────────────────────────────────────────┐
│  Orders Flow                          [Time] ━━●━━━━━━━ 14:00     │
│  [SLA: 95.8%]                         [Play ▶] [◀ Prev] [Next ▶]  │
├────────────────────────────────────────────────────────────────────┤
│                                                                     │
│   ┌─────────────────┐                                              │
│   │  OrderService   │                                              │
│   │  ┌───────────┐  │         ┌──────────────────┐                │
│   │  │  Latency  │  │         │   OrderQueue     │                │
│   │  │  0.5 min  │  │  ─────▶ │  ┌────────────┐  │                │
│   │  │  ▂▃▅▇▅▃▂  │  │         │  │  Queue: 25 │  │                │
│   │  └───────────┘  │         │  │  Latency   │  │                │
│   │                 │         │  │  10.7 min  │  │                │
│   │  Arr: 150       │         │  │  ▁▃▅▇██▇▅  │  │                │
│   │  Srv: 145       │         │  └────────────┘  │                │
│   │  Util: 72.5%    │         │                  │                │
│   │  Err: 2         │         │  Arr: 145        │                │
│   └─────────────────┘         │  Srv: 140        │                │
│         GREEN                 │  Util: 93.3%     │                │
│                               └──────────────────┘                │
│                                     RED (SLA breach!)              │
│                                                                     │
│   [Detail Panel →]                                                 │
└────────────────────────────────────────────────────────────────────┘
```

### Node Spec

**Data Source**: `GET /v1/runs/{runId}/flow/{flowName}?ts={timestamp}&window_start={start}&window_end={end}`

**Node Components**:

1. **Header**: Node name (e.g., "OrderService")
2. **Mini Graph**: Sparkline showing primary metric trend (7-10 bins)
   - Queue nodes: show queue depth
   - Service nodes: show latency
   - Bar chart style: `▁▃▅▇██▇▅`
3. **Key Metrics**: 4-6 values
   - Arrivals (Arr)
   - Served (Srv)
   - Utilization (Util) - percentage
   - Errors (Err)
   - Queue (if queue node)
   - Latency (Latency) - minutes
4. **Border Color**: Based on SLA/utilization status

**Node Colors**:

```yaml
By SLA (for nodes with sla_min defined):
  Green:  latency <= sla_min
  Yellow: sla_min < latency <= sla_min * 1.5
  Red:    latency > sla_min * 1.5

By Utilization (for nodes without SLA):
  Green:  utilization < 0.7
  Yellow: 0.7 <= utilization < 0.9
  Red:    utilization >= 0.9

Gray: No data for current bin
```

**Edge Spec**:
- Arrow from source → target
- Label: flow rate (served count)
- Width: proportional to flow rate (optional)
- Color: same as source node (optional)

### Mini Graph Spec

**Data**: From `miniGraph` field in `/flow` response
**Bins**: 7-10 most recent bins (configurable)
**Style**: Bar chart using Unicode block characters
**Height**: 3-5 levels

**Unicode Blocks** (for vertical bars):
```
Empty:  ▁ (1/8)
Low:    ▃ (3/8)
Medium: ▅ (5/8)
High:   ▇ (7/8)
Full:   █ (8/8)
```

**Color**: Match node border color

**Example Rendering**:
```
Latency over last 7h:
▂▃▅▇▅▃▂
```

### Time Scrubber

**Position**: Top of view, horizontal slider

**Components**:
- **Slider**: Draggable handle for current bin
- **Range**: Start time ← → End time
- **Current Time**: Display "14:00" (timestamp of current bin)
- **Play Button**: Auto-advance through bins (1 bin/sec)
- **Prev/Next**: Step by single bin

**Interaction**:
- Drag slider → update all node states in real-time
- Play → animate through time (update every 500ms-1s)
- Keyboard: ← / → arrow keys to step

---

## View 3: Node Detail Panel

**Purpose**: Deep-dive into single node metrics over time window

### Layout

```
┌────────────────────────────────────────────────────────────┐
│  OrderQueue Details                            [Close ✕]   │
├────────────────────────────────────────────────────────────┤
│  Time Window: 2025-10-07 08:00 - 16:00 (8 hours)          │
│                                                             │
│  SLA: 3.0 min   |   SLA Met: 6/8 bins (75.0%)   ⚠ Yellow  │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  Latency (min)                                      │  │
│  │                                                     │  │
│  │   12 ┤                          ██                  │  │
│  │   10 ┤                      ████                    │  │
│  │    8 ┤                  ████                        │  │
│  │    6 ┤              ████                            │  │
│  │    4 ┤          ████                                │  │
│  │    2 ┤      ████                                    │  │
│  │    0 ┼──┴──┴──┴──┴──┴──┴──┴──                      │  │
│  │       08 09 10 11 12 13 14 15 16                   │  │
│  │                                                     │  │
│  │  [Red zone: > 4.5 min] ─────────────               │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌────────────┬────────────┬────────────┬────────────┐    │
│  │  Arrivals  │  Served    │  Queue     │  Errors    │    │
│  │            │            │            │            │    │
│  │    145     │    140     │     25     │      2     │    │
│  │   ▃▅▇█▇▅▃  │   ▃▅▇█▇▅▃  │   ▁▃▅▇██▇  │   ▁▁▁▃▁▁▁  │    │
│  │            │            │            │            │    │
│  │  Total:    │  Total:    │  Peak:     │  Total:    │    │
│  │  1,160     │  1,120     │  45        │  12        │    │
│  └────────────┴────────────┴────────────┴────────────┘    │
│                                                             │
│  Utilization: 93.3%  ▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇░░░ (93%)          │
│                                                             │
└────────────────────────────────────────────────────────────┘
```

### Panel Components

**Data Source**: `GET /v1/runs/{runId}/state_window?start={start}&end={end}&aggregate=true`

1. **Header**:
   - Node name
   - Time window range
   - Close button

2. **SLA Summary Bar**:
   - SLA target (sla_min)
   - Bins meeting SLA
   - Color indicator

3. **Primary Metric Chart**:
   - Full-size time series chart (latency for queues, utilization for services)
   - X-axis: time (hourly ticks)
   - Y-axis: metric value
   - SLA threshold line (red horizontal)
   - Bars colored by SLA breach

4. **Metric Grid** (2x2 or 4x1):
   - Current value (large font)
   - Mini sparkline (7-10 bins)
   - Aggregate (total/avg/peak)
   - Metrics: Arrivals, Served, Queue, Errors

5. **Utilization Bar**:
   - Horizontal progress bar
   - Percentage label
   - Color by utilization thresholds

**Interaction**:
- Hover chart bar → tooltip with exact values
- Click metric box → expand to full chart
- Click outside panel → close

**Example Tooltip** (on chart bar):
```
2025-10-07 12:00
──────────────
Latency: 10.7 min
Queue: 25
Arrivals: 145
Served: 140
SLA: 3.0 min (BREACH!)
```

---

## View 4: Time Range Selector

**Purpose**: Choose analysis window (last 24h, last 7d, custom)

### Layout

```
┌────────────────────────────────────────────────┐
│  Time Range                                    │
│                                                 │
│  ○ Last 24 hours                               │
│  ○ Last 7 days                                 │
│  ● Custom Range                                │
│                                                 │
│    Start: [2025-10-07 00:00] 📅               │
│    End:   [2025-10-07 23:59] 📅               │
│                                                 │
│    Bin Size: [1 hour ▾]                        │
│                                                 │
│    [Apply]  [Cancel]                           │
└────────────────────────────────────────────────┘
```

**Options**:
- Last 24 hours (hourly bins)
- Last 7 days (hourly bins)
- Last 30 days (daily bins)
- Custom range (choose bin size)

**Validation**:
- End > Start
- Bin count <= 1000 (prevent overload)
- Warn if bin count > 200 (slow rendering)

---

## Interaction Flows

### Flow 1: View SLA Compliance

```
1. User opens SLA Dashboard
   └─> GET /metrics?start={-24h}&end={now}
   └─> Render SLA% boxes per flow

2. User hovers "Orders" box
   └─> Show tooltip (worst latency, errors)

3. User clicks "Orders" box
   └─> Navigate to Flow Graph view
```

### Flow 2: Time-Travel Through Flow

```
1. User opens Flow Graph (Orders)
   └─> GET /flow/Orders?ts={now}&window_start={-6h}&window_end={now}
   └─> Render topology with current state + mini graphs

2. User drags time scrubber to 10:00
   └─> GET /state?ts=2025-10-07T10:00:00Z
   └─> Update all node metrics (arrivals, served, queue, latency)
   └─> Re-color nodes based on new state

3. User clicks "Play" button
   └─> Loop: advance 1 bin every 1 second
   └─> Call /state?ts={t} for each bin
   └─> Animate node color changes
```

### Flow 3: Inspect Node Details

```
1. User clicks "OrderQueue" node in Flow Graph
   └─> Open Node Detail Panel (overlay)
   └─> GET /state_window?start={-6h}&end={now}&aggregate=true
   └─> Render full time series charts + sparklines

2. User hovers chart bar at 12:00
   └─> Show tooltip with exact values

3. User clicks "Latency" metric box
   └─> Expand to full-screen chart (optional)
```

---

## Technical Specs

### Browser Support
- Chrome 100+
- Firefox 100+
- Safari 15+
- Edge 100+

### Performance Targets
- Initial load: < 2s
- Time scrub update: < 200ms
- Play animation: 60 FPS (smooth)
- Node detail panel: < 500ms

### Data Volume Assumptions
- Max nodes per flow: 20
- Max bins per query: 200
- Max flows in dashboard: 10
- Mini graph bins: 7-10

### Responsive Breakpoints
- Desktop: 1280px+ (3-column SLA boxes)
- Tablet: 768-1279px (2-column SLA boxes)
- Mobile: < 768px (1-column SLA boxes, simplified graph)

---

## Wireframe Annotations

### SLA Box States

```
Normal (Green):              Warning (Yellow):           Critical (Red):
┌─────────────┐             ┌─────────────┐            ┌─────────────┐
│   Orders    │             │  Billing    │            │  Payments   │
│             │             │             │            │             │
│   95.8%     │             │   91.2%     │            │   78.5%     │
│  SLA Met    │             │  SLA Met    │            │ SLA Breach  │
│             │             │             │            │             │
│ 23/24 bins  │             │ 22/24 bins  │            │ 19/24 bins  │
│  ✓ Green    │             │  ⚠ Yellow   │            │  ✕ Red      │
└─────────────┘             └─────────────┘            └─────────────┘
```

### Node States in Flow Graph

```
Healthy (Green):            Degraded (Yellow):          Critical (Red):
┌─────────────────┐        ┌─────────────────┐         ┌─────────────────┐
│  OrderService   │        │  OrderQueue     │         │  PaymentAPI     │
│  ┌───────────┐  │        │  ┌───────────┐  │         │  ┌───────────┐  │
│  │ Lat 0.5m  │  │        │  │ Lat 4.2m  │  │         │  │ Lat 12.5m │  │
│  │ ▂▃▅▃▂     │  │        │  │ ▃▅▇▇▅     │  │         │  │ ▇██████   │  │
│  └───────────┘  │        │  └───────────┘  │         │  └───────────┘  │
│  Arr: 150       │        │  Arr: 145       │         │  Arr: 200       │
│  Srv: 145       │        │  Srv: 140       │         │  Srv: 120       │
│  Util: 72%      │        │  Util: 87%      │         │  Util: 98%      │
└─────────────────┘        └─────────────────┘         └─────────────────┘
   (Border: Green)            (Border: Yellow)            (Border: Red)
```

---

## Color Palette

```yaml
SLA Colors:
  Green:  #10B981 (Tailwind green-500)
  Yellow: #F59E0B (Tailwind amber-500)
  Orange: #F97316 (Tailwind orange-500)
  Red:    #EF4444 (Tailwind red-500)

Background:
  Panel:  #F9FAFB (Tailwind gray-50)
  Node:   #FFFFFF (White)
  Border: #E5E7EB (Tailwind gray-200)

Text:
  Primary:   #111827 (Tailwind gray-900)
  Secondary: #6B7280 (Tailwind gray-500)
  Success:   #059669 (Tailwind green-600)
  Error:     #DC2626 (Tailwind red-600)

Charts:
  Axis:     #9CA3AF (Tailwind gray-400)
  Grid:     #E5E7EB (Tailwind gray-200)
  Bar:      Match node color (green/yellow/red)
  Sparkline: #6366F1 (Tailwind indigo-500)
```

---

## Implementation Notes

### Priority 1 (MVP)
- SLA Dashboard (boxes + click-through)
- Flow Graph (static layout, colored nodes)
- Time scrubber (manual drag)
- Basic metrics display (no mini graphs)

### Priority 2
- Mini graphs (sparklines on nodes)
- Node detail panel (full charts)
- Play/pause animation
- Hover tooltips

### Priority 3
- Responsive layout (mobile)
- Custom time range picker
- Export/share views
- Keyboard shortcuts

---

## API Dependencies

See `TIME-TRAVEL-ARCHITECTURE-PLAN.md` for:
- `GET /v1/runs/{runId}/metrics` (SLA aggregates)
- `GET /v1/runs/{runId}/flow/{flowName}` (topology + state)
- `GET /v1/runs/{runId}/state` (single-bin snapshot)
- `GET /v1/runs/{runId}/state_window` (time series)

---

**End of Specification**
