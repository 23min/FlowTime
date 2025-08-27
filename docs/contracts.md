# FlowTime-Sim Contracts (SIM-M0)

Status: Near-complete (Phases 1–6 implemented). This document defines the simulation spec, event, and Gold output contracts for SIM-M0. Optional enhancements (hash logging, large-λ warning) are implemented; future evolution will version via `schemaVersion`.

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

Determinism: Given identical YAML (including seed) output events and Gold CSV must be byte-identical (except trailing newline tolerance) in SIM-M0. A test suite enforces this (hash comparisons). CLI verbose mode prints SHA256 hashes of generated files to aid reproducibility.

Poisson performance note: For λ > 1000 a warning is emitted (Knuth sampler may degrade). Future milestone will introduce an O(1) or transformed-rejection sampler.

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

## 4. Output Paths & CLI Modes

Default (when not overridden by `outputs` or CLI `--out` directory):
- `events.ndjson`
- `gold.csv`

If CLI `--out` points to a directory, outputs are written under that root (using spec `outputs` overrides if present). Current rule: treat provided path without extension as directory root; with an extension we still derive its directory as root (SIM-M0 simplicity). Two CLI modes:

| Mode | Purpose | Outputs |
|------|---------|---------|
| engine | Relay spec to FlowTime engine `/run` | Series CSV/JSON (legacy path) |
| sim | Local simulation described here | NDJSON events + Gold CSV |

Verbose (`--verbose`) additionally logs SHA256 hashes of both files.

## 5. Error Handling & Exit Codes

Validation failures (spec) must produce a clear aggregated error message listing all issues; exit code 2 for usage/spec errors (align with existing pattern). Runtime generation errors (unexpected exceptions) exit code 1.

## 6. Deterministic Randomness & Hashing

RNG seeded by `seed` (default 12345). All Poisson samples derive solely from this RNG. Switching to a fixed algorithm (e.g., PCG) is a future hardening task; for SIM-M0 we accept the .NET implementation but encapsulate it.

Hashing: Verbose mode prints SHA256 hashes so downstream processes (e.g., SYN-M0 adapter tests) can assert scenario reproducibility without shipping large fixtures.

## 7. Contract Evolution Strategy

Introduce an eventual `schemaVersion` field at root once first breaking change is contemplated (post SIM-M0). Prior to that, additive changes only. The Synthetic Adapter (SYN-M0) will pin to the SIM-M0 contract hash.

## 8. Open Questions (Track Before Finalizing SIM-M0)

| ID | Question | Tentative Answer | Resolve By |
|----|----------|------------------|------------|
| Q1 | Should `served` ever exceed `arrivals`? | No; enforced invariant. | Pre SIM-M1 |
| Q2 | Include per-event random offset within bin (jitter)? | Not in SIM-M0; keep deterministic counts only. | SIM-M1 scope review |
| Q3 | Support fractional expected counts for const arrivals? | No; const are integer counts. Use Poisson for stochastic. | SIM-M0 freeze |

## 9. Sample Outputs

Curated (truncated) sample outputs for quick reference live under `docs/examples/sim/`:

| Scenario | Events Sample | Gold CSV | Notes |
|----------|---------------|----------|-------|
| Const (5 each bin, 4 bins) | `docs/examples/sim/const.events.ndjson.sample` | `docs/examples/sim/const.gold.csv` | 20 total events |
| Poisson (λ=3.5 seed=999) | `docs/examples/sim/poisson.events.ndjson.sample` | `docs/examples/sim/poisson.gold.csv` | 3,4,3,4 counts |

Regenerate locally:
```
dotnet run --project src/FlowTime.Sim.Cli -- --mode sim --model examples/m0.const.sim.yaml --out out/const --verbose
dotnet run --project src/FlowTime.Sim.Cli -- --mode sim --model examples/m0.poisson.sim.yaml --out out/poisson --verbose
```
Copy (or truncate) the outputs into the docs examples if the contract changes; update hashes in milestone notes.

## 10. Change Log

| Date | Change |
|------|--------|
| 2025-08-27 | Initial draft extracted from milestone SIM-M0 Phases 1–2 |
| 2025-08-27 | Added sim CLI mode details, hashing, large-λ warning, sample outputs section |

---

Feedback welcome; refine before Phase 3 (generator implementation).
