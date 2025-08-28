# Release: SIM-M1 (Draft)

Status: DRAFT (not yet tagged)

## Summary
SIM-M1 builds on SIM-M0 by introducing explicit spec versioning, RNG hardening, a metadata manifest for reproducibility, a service time specification scaffold, and an adapter parity harness ensuring downstream integration stability.

## Highlights
- schemaVersion: `schemaVersion: 1` enforced (legacy specs without the field accepted with warning during transition).
- RNG Hardening: PCG32 portable deterministic default. Legacy .NET `Random` selectable via `rng: legacy` (deprecated; removal planned next milestone).
- Metadata Manifest: `metadata.json` with seed, rng, per-artifact SHA256 hashes (LF-normalized), generation timestamp.
- Service Spec Scaffold: `service` block (const|exp) parsed & validated; no runtime impact yet (backlog modeling deferred to SIM-M2).
- Parity Harness: End-to-end tests validate (a) reproducibility, (b) engine demand parity with Gold arrivals, (c) event aggregation correctness, (d) manifest integrity, (e) negative mismatch detection.
- Determinism: Stable hashes across platforms for identical specs.

## Breaking / Behavioral Changes
| Area | Change | Action Required |
|------|--------|-----------------|
| Spec | Must add `schemaVersion: 1` to silence warning | Update existing YAML specs |
| RNG | Default changed to PCG32 | Pin `rng: legacy` only if comparing historical outputs (temporary) |
| Output | New `metadata.json` file written | Ensure tooling ignores or ingests as needed |

## Migration from SIM-M0
1. Add `schemaVersion: 1` at top of each spec.
2. Remove any reliance on implicit legacy RNG ordering (unless explicitly specifying `rng: legacy`).
3. Optionally consume `metadata.json` for faster parity checks—compare its hashes instead of re-parsing large files.

## New Spec Fields
```yaml
schemaVersion: 1
rng: pcg            # or legacy (temporary)
service:
  kind: exp         # const | exp
  rate: 2.5         # or value: <non-negative>
```

## Metadata Manifest Example
```json
{
  "schemaVersion": 1,
  "seed": 12345,
  "rng": "pcg",
  "events": { "path": "events.ndjson", "sha256": "..." },
  "gold": { "path": "gold.csv", "sha256": "..." },
  "generatedAt": "2025-08-27T12:34:56Z"
}
```

## Deprecations
- `rng: legacy` flagged for removal after SIM-M1 (target SIM-M2). Release notes will include final notice.

## Testing & Quality
- 39 tests covering validation, RNG determinism snapshot, manifest hash stability, parity, negative guard.
- All tests pass on Linux container (.NET 9).
- Hash normalization ensures consistent results across OS line endings.

## Known Limitations
- Service times currently inert (no backlog/served divergence yet).
- No compression option for outputs (planned SIM-M2+).
- Single route id; multi-node routing deferred.

## Next (SIM-M2 Preview)
- Introduce backlog/served differentiation using service distribution.
- Metrics & summaries added to manifest (`metrics.*`).
- Optional gzip for large NDJSON.
- Potential capacity modeling.

## Checklist (Release Prep)
- [ ] Final review of docs (contracts, README, examples) reflects SIM-M1.
- [ ] All specs in examples contain `schemaVersion: 1`.
- [ ] Replace remaining legacy RNG usage in tests (if not explicitly intentional).
- [ ] Tag `sim-m1` after merge to main.

## Acknowledgements
Automated assistance implemented core RNG, manifest, and parity features.

---
(End of SIM-M1 draft release notes)
