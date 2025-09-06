# Simulation Metadata Manifest (LEGACY)

> Deprecated as of SIM-M2 (2025-09-02). Replaced by dual `run.json` + `manifest.json` + `series/index.json` under `runs/<runId>/`. This document is retained for historical reference only and will be removed after one additional milestone unless needed for backward compatibility tooling.

Legacy content below (SIM-M1 reference):

---

# Simulation Metadata Manifest

Status: Implemented (SIM-M1 Phase 3 complete). Further extensions may add optional fields.

## 1. Purpose
The metadata manifest (`metadata.json`) is a compact, machine-readable summary of a completed simulation run. It enables:
- **Reproducibility**: Captures the exact inputs controlling stochastic behavior (`schemaVersion`, `seed`, `rng`).
- **Integrity & Drift Detection**: Provides SHA256 hashes of primary artifacts (events, gold) so downstream tooling can verify content without re-parsing large files.
- **Provenance**: Records generation timestamp and (future) simulator build identity for audit and traceability.
- **Single Success Sentinel**: Presence + valid schema indicates the run finished (avoids partial-artifact ambiguity).
- **Adapter / CI Integration**: Adapters and parity harnesses ingest one small JSON instead of scraping CLI output.
- **Extensibility**: A stable location to add future performance metrics (service/backlog stats) without mutating core event or gold file schemas.

## 2. Current Schema (SIM-M1)
```jsonc
{
  "schemaVersion": 1,          // integer (matches simulation spec version)
  "seed": 12345,               // integer seed actually used (after defaulting if omitted)
  "rng": "pcg",               // rng kind (pcg | legacy)
  "events": {
    "path": "events.ndjson", // relative path emitted
    "sha256": "<hex>"        // lowercase or uppercase hex (implementation chooses) of LF-normalized content
  },
  "gold": {
    "path": "gold.csv",
    "sha256": "<hex>"
  },
  "generatedAt": "2025-08-27T12:34:56Z" // UTC timestamp (ISO8601, 'Z')
}
```
Future optional top-level fields (reserved):
- `simulator`: { "version": "1.2.3", "commit": "abcdef" }
- `service`: summarised service time parameters when introduced
- `metrics`: aggregate counters (e.g., arrivalsTotal, servedTotal, errorsTotal)

## 3. Field Semantics
| Field | Required | Notes |
|-------|----------|-------|
| schemaVersion | Yes | Mirrors spec; consumers must verify supported version list. |
| seed | Yes | Final seed after default (12345 if absent in spec). |
| rng | Yes | Default `pcg` unless explicitly `legacy`. Stable across platforms. |
| events.path | Yes | Relative (or absolute if user specified path with directories). |
| events.sha256 | Yes | SHA256 of normalized events file contents. |
| gold.path | Yes | Same convention as events. |
| gold.sha256 | Yes | SHA256 of normalized gold file contents. |
| generatedAt | Yes | UTC. Indicates completion instant *after* file writes & hash computation. |

## 4. Hashing Rules
1. Read file bytes as written, convert Windows CRLF line endings to LF (`\n`) *before* hashing (idempotent for already LF files).
2. No trailing whitespace trimming; content fidelity preserved.
3. SHA256 computed over UTF-8 bytes of normalized content.
4. Hex output case: implementation-defined; consumers must case-normalize before comparison.
5. Hash mismatch indicates drift (re-run simulation to regenerate canonical artifacts).

## 5. Deterministic Replay Procedure
To verify reproducibility:
1. Re-run simulator with the original YAML spec (or reconstruct the spec values implied by manifest if spec unavailable: `schemaVersion`, `seed`, `rng`).
2. Compute new hashes (same normalization rules).
3. Compare to manifest hashes; equality => deterministic stability preserved.
4. For Poisson workloads, any mismatch implies change in RNG algorithm, sampling logic, or upstream spec differences.

## 6. Versioning & Evolution
- Additive fields: allowed without bumping `schemaVersion` (manifest consumers must ignore unknown fields).
- Backward-incompatible changes (renaming/removing existing fields or altering semantics) will accompany a spec `schemaVersion` increment.
- Deprecated fields will remain for at least one milestone with a release note before removal.

## 7. Example
```json
{
  "schemaVersion": 1,
  "seed": 777,
  "rng": "pcg",
  "events": { "path": "out/run/events.ndjson", "sha256": "A1B2C3..." },
  "gold": { "path": "out/run/gold.csv", "sha256": "D4E5F6..." },
  "generatedAt": "2025-08-27T09:15:30Z"
}
```

## 8. Validation Expectations
A valid manifest must satisfy:
- All required fields present with correct primitive types.
- `schemaVersion` matches supported set (currently {1}).
- Hash strings match `[0-9a-fA-F]{64}`.
- Target files exist and recomputed hashes match (optional deep validation phase).

## 9. Tooling Guidance
- CI parity tests: fail fast if manifest missing or schema invalid.
- Caching: use combined hash of (`events.sha256` + `gold.sha256`) as cache key for downstream analytics.
- Security: SHA256 chosen for ubiquity; not intended for cryptographic tamper guarantees (could extend to include signed digests if needed).

## 10. Roadmap (Future Additions)
| Candidate Field | Purpose | Milestone (Tentative) |
|-----------------|---------|-----------------------|
| simulator.version / commit | Traceability | SIM-M1 (stretch) / SIM-M2 |
| metrics.* | Quick totals without parsing CSV | SIM-M2 |
| service.* | Service time summary | SIM-M2 |
| capacity.* | Backpressure model description | SIM-M2+ |
| compression | Indicate compression algorithm if outputs gzipped | SIM-M2+ |

## 11. Cross References
- Core spec & contracts: `contracts.md` (section 6 / determinism).
- Milestone tracking: `milestones/SIM-M1.md` (Phase 3 deliverables).

---
Feedback welcome; refine before locking SIM-M1.
