# FlowTime-Sim Contracts (SIM-M0 Draft)

Status: DRAFT (Phases 1–2). This document defines the simulation spec, event, and Gold output contracts targeted in SIM-M0. It will be versioned when stabilized.

## 1. Simulation Spec (YAML)

Root keys:

```yaml
grid:
  bins: 24                # required > 0
  binMinutes: 60          # required > 0
  start: 2025-01-01T00:00:00Z   # optional (UTC). Default 1970-01-01T00:00:00Z if omitted.
seed: 12345               # optional; default 12345
arrivals:                 # required
  kind: const | poisson   # required
  # const variant
  values: [10, 10, 12, ...]   # length == bins
  # poisson variant (one of the following)
  rate: 8.5               # single lambda applied to each bin
  rates: [8.0, 9.5, ...]  # per-bin lambda values; length == bins
route:                    # required (SIM-M0 single node)
  id: nodeA
outputs:                  # optional; override default output paths
  events: out/events.ndjson
  gold: out/gold.csv
```

Validation summary:
- `grid`, `arrivals`, and `route` sections are mandatory.
- `grid.bins > 0`, `grid.binMinutes > 0`.
- `grid.start` if present must be UTC (`Z`); local offsets rejected.
- `arrivals.kind` in {`const`,`poisson`}.
- For `const`: `values` required; length == `grid.bins`.
- For `poisson`: exactly one of `rate` or `rates`; if `rates`, length == `grid.bins`.
- `route.id` non-empty.
- No mixing `rate` and `rates`.

Determinism: Given identical YAML (including seed) output events and Gold CSV must be byte-identical (except trailing newline tolerance) in SIM-M0.

Future reserved fields (not active yet):
- `flows:` (multiple flow classes)
- `routing:` (multi-stage graphs)
- `retries:` (retry distributions)
- `pmf:` (PMF emission mode)

## 2. Event Stream (NDJSON)

One JSON object per line; UTF-8; newline-delimited (\n). No outer array. Order: ascending by bin, then sequence of generation within bin.

Example lines:
```json
{"entity_id":"e1","event_type":"arrival","ts":"2025-01-01T00:00:00Z","node":"nodeA","flow":"*"}
{"entity_id":"e2","event_type":"arrival","ts":"2025-01-01T00:00:00Z","node":"nodeA","flow":"*"}
```

Fields:
- `entity_id`: string, deterministic sequential (prefix `e`).
- `event_type`: "arrival" (only type for SIM-M0).
- `ts`: ISO8601 UTC timestamp of bin start.
- `node`: route id.
- `flow`: "*" placeholder (future: flow class).

Reserved (not emitted yet): `attrs`, `correlation_id`, `retry_seq`.

## 3. Gold Aggregated Series (CSV)

Header:
```
timestamp,node,flow,arrivals,served,errors
```

Each row corresponds to a grid bin (start timestamp). Example:
```
timestamp,node,flow,arrivals,served,errors
2025-01-01T00:00:00Z,nodeA,*,20,20,0
2025-01-01T01:00:00Z,nodeA,*,18,18,0
```

Rules:
- `timestamp`: bin start (ISO8601 UTC).
- `arrivals`: count of events in that bin.
- `served`: equals arrivals (identity in SIM-M0; future capacity model may reduce).
- `errors`: 0 (future: count of failed / dropped events).
- `flow`: "*".

## 4. Output Paths

Default (when not overridden by `outputs` or CLI `--out` directory):
- `events.ndjson`
- `gold.csv`

If CLI `--out` points to a directory, outputs written under that root unless spec overrides. If CLI `--out` sets an explicit file (future decision), we will treat it as root directory (SIM-M0 simplest rule: if it has no extension treat as dir; else still treat as dir unless spec overrides). Exact CLI semantics to be finalized in Phase 5.

## 5. Error Handling & Exit Codes

Validation failures (spec) must produce a clear aggregated error message listing all issues; exit code 2 for usage/spec errors (align with existing pattern). Runtime generation errors (unexpected exceptions) exit code 1.

## 6. Deterministic Randomness

RNG seeded by `seed` (default 12345). All Poisson samples derive solely from this RNG. Switching to a fixed algorithm (e.g., PCG) is a future hardening task; for SIM-M0 we accept the .NET implementation but encapsulate it.

## 7. Contract Evolution Strategy

Introduce an eventual `schemaVersion` field at root once first breaking change is contemplated (post SIM-M0). Prior to that, additive changes only. The Synthetic Adapter (SYN-M0) will pin to the SIM-M0 contract hash.

## 8. Open Questions (Track Before Finalizing SIM-M0)

| ID | Question | Tentative Answer | Resolve By |
|----|----------|------------------|------------|
| Q1 | Should `served` ever exceed `arrivals`? | No; enforced invariant. | Pre SIM-M1 |
| Q2 | Include per-event random offset within bin (jitter)? | Not in SIM-M0; keep deterministic counts only. | SIM-M1 scope review |
| Q3 | Support fractional expected counts for const arrivals? | No; const are integer counts. Use Poisson for stochastic. | SIM-M0 freeze |

## 9. Change Log

| Date | Change |
|------|--------|
| 2025-08-27 | Initial draft extracted from milestone SIM-M0 Phases 1–2 |

---

Feedback welcome; refine before Phase 3 (generator implementation).
