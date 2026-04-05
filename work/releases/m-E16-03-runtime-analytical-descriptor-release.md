# Release Summary: m-E16-03 Runtime Analytical Descriptor

**Milestone:** m-E16-03-runtime-analytical-descriptor
**Completed:** 2026-04-05
**Status:** ready for commit and merge approval

## Delivered

- Added a compiled `AnalyticalDescriptor` to runtime nodes so analytical identity, category, queue origin, and parallelism are emitted as authoritative facts instead of adapter-time reconstruction.
- Replaced `AnalyticalCapabilities` with `AnalyticalDescriptorCompiler` and `AnalyticalEvaluator`, moving descriptor facts into Core and deleting adapter-side analytical identity helpers.
- Switched API graph and state projection paths to consume descriptor/logical-type truth for analytical behavior, queue-source dispatch fallback, computed-node inclusion, and promoted `serviceWithBuffer` parity.
- Updated Blazor and canvas topology consumers to use descriptor-derived logical type consistently through layout, inspector tabs, sparklines, provenance, and rendered node treatment.
- Added Core, API, and UI regression coverage for explicit vs reference-resolved `serviceWithBuffer` parity and promoted computed nodes that depend on runtime logical type.

## Validation

- `dotnet build` passed on 2026-04-05.
- `dotnet test --nologo --verbosity minimal` passed on 2026-04-05 with `1266` succeeded, `0` failed, and `10` skipped.
- Review follow-up checks confirmed the remaining graph `kinds` filter and topology canvas visual branches no longer fall back to authored `kind` for analytical/runtime category decisions.

## Deferred Work

- None from this milestone. Next milestone is `m-E16-04-core-analytical-evaluation`.