# Milestone SIM-M1 — Service Times, Schema Version & RNG Hardening

Status: COMPLETE (tagged as `sim-m1`).

SIM-M0 established the deterministic arrivals + Gold/event contracts. SIM-M1 extends the simulator with groundwork for richer performance modeling while freezing and versioning the public contract for adapter consumption.

---

## Goals

1. Introduce `schemaVersion: 1` in simulation spec & outputs; enforce during validation (assume `0` if absent with warning; emit `1`).
2. Emit a metadata manifest (`metadata.json`) containing: `schemaVersion`, `seed`, SHA256 hashes (LF-normalized, lowercase hex) of `events` and `gold`, RNG kind, and `generatedAt` timestamp.
3. Upgrade deterministic RNG to a stable algorithm (PCG) decoupled from .NET implementation; allow `rng: legacy` opt-out for one milestone.
4. Extend spec with optional service time distribution (const or exponential) to prepare for backlog/served divergence in SIM-M2.
5. Adapter (SYN-M0) readiness: parity harness that roundtrips simulator outputs through adapter → engine model and verifies arrival counts match engine demand/served series (served == arrivals identity in SIM-M1).
6. Quality & observability: structured verbose output (machine-parsable key=value lines), hash stability tests, schema version drift detection.

Out of scope (defer to later milestones): backlog calculations, queue metrics, routing expansions, retries, PMF export (SIM-M2).

---

## Spec Additions

Root:
```yaml
schemaVersion: 1          # required (SIM-M1+); if missing treat as 0 with warning
rng: pcg | legacy         # optional; default pcg
service:                  # optional (foundation only; no output effect yet)
  kind: const | exp
  value: 5                # for const (abstract service time units — unit semantics TBD; no runtime effect yet)
  rate: 2.5               # for exp (mean = 1/rate)
```

Validation changes:
- Reject `schemaVersion` not equal to 1 (future: support list of supported versions).
- If both `value` and `rate` present or missing for their service `kind` → error.
- Units: Document as "abstract service time units" (conversion to real wall time deferred).
- `arrivals.kind`: required; must be `const` or `poisson`.
- For `const`: `arrivals.values` required, length must equal `grid.bins`, all values integer (after rounding) & >= 0.
- For `poisson`: exactly one of `rate` or `rates` required; if `rates` provided length must equal `grid.bins`; all (rate|rates) >= 0; cannot specify both.
- `service.kind` (when block present) must be `const` or `exp`.
- `service.value` required & `service.rate` forbidden for `kind=const`; `value >= 0`.
- `service.rate` required & `service.value` forbidden for `kind=exp`; `rate > 0`.
- All length / non-negative validations surface explicit errors (see validator implementation).

---

## Output Changes

1. Events NDJSON: (unchanged for SIM-M1) — service times not yet emitted.
2. Gold CSV: unchanged (no backlog / in_service columns until SIM-M2).
3. New `metadata.json` example (hash values shortened for illustration):
```json
{
  "schemaVersion": 1,
  "seed": 12345,
  "rng": "pcg",
  "events": { "path": "events.ndjson", "sha256": "abc123..." },
  "gold": { "path": "gold.csv", "sha256": "def456..." },
  "generatedAt": "2025-08-28T12:34:56Z"
}
```

Hash computation: normalize line endings to `\n` before hashing.

Notes:
- Hash hex is emitted lowercase (consumers should case-normalize defensively).

---

## Phased Plan

| Phase | Focus | Deliverables | Tests (actual classes) | Status |
|-------|-------|-------------|------------------------|--------|
| 0 | Planning & branch | SIM-M1 doc, branch scaffold | n/a | ✅ Done |
| 1 | schemaVersion & validation | Spec & validator updates; docs & samples bump | SimulationSpecParserTests, SimulationSpecValidator (indirect) | ✅ Done |
| 2 | RNG hardening | PCG implementation + opt-out flag | DeterminismTests (Poisson/Const), PcgRngSnapshotTests | ✅ Done |
| 3 | Metadata manifest | `metadata.json` write + CLI verbose print (`docs/metadata-manifest.md`) | DeterminismTests (SimMode_MetadataManifest_HashesStable) | ✅ Done |
| 4 | Service spec parsing | DTO + validation; no runtime effect | ServiceSpecParserTests | ✅ Done |
| 5 | Adapter parity harness (SYN-M0 tie‑in) | Engine parity, events aggregation, manifest parity, negative guard | AdapterParityTests | ✅ Done |
| 6 | Docs & release prep | Updated contracts + release notes | Manual doc review | ⏳ |

---

## Acceptance Criteria

- [x] Specs without `schemaVersion` accepted with warning; outputs include `schemaVersion: 1`.
- [x] Specs with unsupported version rejected.
- [x] RNG kind selectable; PCG default yields stable snapshot (first N samples direct RNG hash) across runs & OS.
- [x] Metadata manifest emitted with correct hashes (validated by tests).
- [x] Determinism tests updated to include metadata hash comparison.
- [x] Service time block parsed & validated (no change to events/gold yet).
- [x] Parity harness demonstrates adapter roundtrip with unchanged arrival counts (engine demand == gold arrivals; events aggregated == gold counts; manifest validated).
- [x] Release notes drafted & merged (`releases/SIM-M1.md`).
- [x] All simulation example specs upgraded with `schemaVersion: 1` (non-sim engine examples may differ).
- [ ] Tag created after merge.
- [x] Documentation updated (contracts, README, milestone) reflecting versioning & RNG change.

Stretch (optional):
- [ ] Coverage badge integration in README (if feasible from CI artifact).
- [ ] Optional gzip compression flag for events (verify determinism of uncompressed content hash).

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| RNG swap surprises users | Perceived regression | Provide `rng: legacy` fallback + release notes |
| schemaVersion omission common | Many warnings clutter output | Offer `--suppress-warnings` or escalate after one milestone |
| Hash instability (line endings) | False negative determinism failures | Normalize to LF prior to hashing |
| Adapter drift vs sim schema | Failing parity later | Add CI job executing roundtrip parity early |

---

## Metrics / Quality Gates
- Determinism runtime: parity test < 2s on CI (observed locally; not yet enforced in CI gate).
- RNG performance: PCG sampling within ±5% of legacy `Random` for Poisson generation (planned; benchmark not automated in SIM-M1).
- Code coverage: maintain or increase from SIM-M0 baseline (measurement pipeline not yet integrated; planned SIM-M2 task).

---

## Next Milestone Preview (SIM-M2)
- Backlog & served divergence (service time effect).
- Backpressure modeling (capacity placeholder).
- PMF export (daily/weekly) enabling engine PMF node.

---

## Test Coverage Map
| Area | Test Class(es) / Method Highlights |
|------|-------------------------------------|
| Versioning / Validation | SimulationSpecParserTests, ServiceSpecParserTests, ArgParserTests |
| RNG Determinism | DeterminismTests (ConstSpec_GeneratesIdenticalOutputs, PoissonSpec_ReproducibleGivenSeed), PcgRngSnapshotTests |
| Manifest Hash Stability | DeterminismTests (SimMode_MetadataManifest_HashesStable) |
| Parity Harness | AdapterParityTests (baseline, events aggregation, manifest parity, negative guard) |
| CLI End-to-End | CliSimModeTests |

## Revision History
| Date | Change | Author |
|------|--------|--------|
| 2025-08-27 | Initial SIM-M1 planning draft | AI Assistant |
| 2025-08-27 | Parity harness tests (engine, events, manifest, negative) added | AI Assistant |
| 2025-08-27 | Release prep branch & draft release notes created | AI Assistant |
| 2025-08-28 | Documentation refinements (validation details, test name alignment, acceptance criteria updates) | AI Assistant |
