# Milestone SIM-M1 — Service Times, Schema Version & RNG Hardening

Status: ACTIVE (branch `feature/sim-m1/*`).

SIM-M0 established the deterministic arrivals + Gold/event contracts. SIM-M1 extends the simulator with groundwork for richer performance modeling while freezing and versioning the public contract for adapter consumption.

---

## Goals

1. Introduce `schemaVersion: 1` in simulation spec & outputs; enforce during validation (assume `0` if absent with warning; emit `1`).
2. Emit a metadata manifest (`metadata.json`) containing: `schemaVersion`, `seed`, SHA256 hashes of `events` and `gold`, generator RNG kind.
3. Upgrade deterministic RNG to a stable algorithm (PCG or XorShift128+) decoupled from .NET implementation; allow `rng: legacy` opt-out for one milestone.
4. Extend spec with optional service time distribution (const or exponential) to prepare for backlog/served divergence in SIM-M2.
5. Adapter (SYN-M0) readiness: parity test harness that roundtrips simulator outputs through adapter → engine model and verifies arrival counts match engine demand/served series.
6. Quality & observability: structured verbose output (machine-parsable key=value lines), hash stability tests, schema version drift detection.

Out of scope (defer to later milestones): backlog calculations, queue metrics, routing expansions, retries, PMF export (SIM-M2).

---

## Spec Additions (Draft)

Root:
```yaml
schemaVersion: 1          # required (SIM-M1+); if missing treat as 0 with warning
rng: pcg | legacy         # optional; default pcg
service:                  # optional (foundation only; no output effect yet)
  kind: const | exp
  value: 5                # for const (milliseconds or abstract units — TBD finalize unit)
  rate: 2.5               # for exp (mean = 1/rate)
```

Validation changes:
- Reject `schemaVersion` not equal to 1 (future: support list of supported versions).
- If both `value` and `rate` present or missing for their kind → error.
- Units: Document as "abstract service time units" (conversion to real wall time deferred).

---

## Output Changes

1. Events NDJSON: (unchanged for SIM-M1) — service times not yet emitted.
2. Gold CSV: unchanged (no backlog / in_service columns until SIM-M2).
3. New `metadata.json` example:
```json
{
  "schemaVersion": 1,
  "seed": 12345,
  "rng": "pcg",
  "events": { "path": "events.ndjson", "sha256": "..." },
  "gold": { "path": "gold.csv", "sha256": "..." },
  "generatedAt": "2025-09-01T12:34:56Z"
}
```

Hash computation: normalize line endings to `\n` before hashing.

---

## Phased Plan

| Phase | Focus | Deliverables | Tests | Status |
|-------|-------|-------------|-------|--------|
| 0 | Planning & branch | SIM-M1 doc, branch scaffold | n/a | ✅ Done |
| 1 | schemaVersion & validation | Spec & validator updates; docs & samples bump | VersionValidationTests | ✅ Done |
| 2 | RNG hardening | PCG implementation + opt-out flag | RngDeterminismTests, PcgRngSnapshotTests | ✅ Done |
| 3 | Metadata manifest | `metadata.json` write + CLI verbose print (`docs/metadata-manifest.md`) | MetadataHashTests | ✅ Done |
| 4 | Service spec parsing | DTO + validation; no runtime effect | ServiceSpecTests | 🟡 In Progress |
| 5 | Adapter parity harness (SYN-M0 tie‑in) | Test harness script/integration test | ParityRoundtripTests | ⏳ |
| 6 | Docs & release prep | Updated contracts + new release notes | DocLint | ⏳ |

---

## Acceptance Criteria

- [ ] Specs without `schemaVersion` accepted with warning; outputs include `schemaVersion: 1`.
- [ ] Specs with unsupported version rejected.
- [x] RNG kind selectable; PCG default yields stable snapshot (first N samples direct RNG hash) across runs & OS.
- [x] Metadata manifest emitted with correct hashes (validated by tests).
- [ ] Determinism tests updated to include metadata hash comparison.
- [x] Service time block parsed & validated (no change to events/gold yet).
- [ ] Parity harness demonstrates adapter roundtrip with unchanged arrival counts.
- [ ] Documentation updated (contracts, README, milestone) reflecting versioning & RNG change.

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
- Determinism runtime: parity test < 2s on CI.
- RNG performance: PCG sampling within ±5% of legacy Random for Poisson generation (micro benchmark optional).
- Code coverage: maintain or increase from SIM-M0 baseline (document current baseline once coverage artifact parsed).

---

## Next Milestone Preview (SIM-M2)
- Backlog & served divergence (service time effect).
- Backpressure modeling (capacity placeholder).
- PMF export (daily/weekly) enabling engine PMF node.

---

## Revision History
| Date | Change | Author |
|------|--------|--------|
| 2025-08-27 | Initial SIM-M1 planning draft | AI Assistant |
