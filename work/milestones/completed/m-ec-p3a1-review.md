# Review: m-ec-p3a1 — Analytical Projection Hardening

**Date:** 2026-04-03  
**Scope:** Review of the current `milestone/m-ec-p3a1` implementation  
**Verdict:** Approved for wrap

## Summary

The current branch satisfies the revised bridge scope for `m-ec-p3a1`. Analytical capabilities and the current state analytical computation surface now live in Core, the flow-latency parity and metadata honesty regressions found in the earlier review are fixed, the stationarity threshold is wired through an explicit runtime option, and analytical by-class DTO parity is present end-to-end.

The remaining purity issues are real, but they are no longer blockers for this milestone because they have been explicitly transferred to E-16: compiled semantic references, runtime analytical descriptors, class-truth separation, public analytical contract redesign, and consumer heuristic deletion.

## Wrap Assessment

### What This Milestone Now Delivers

- Core owns the current analytical capability/computation surface used by `/state` and `/state_window`.
- Snapshot/window analytical projection now consumes Core results for logicalType-resolved `serviceWithBuffer` cases, metadata honesty, and current by-class analytical values.
- Stationarity warning emission is tied to the actual presence of `queueTimeMs` in the window payload and uses an explicit runtime tolerance option.
- Analytical class DTO fields are present through the current API and Blazor time-travel models.

### What Is Explicitly Deferred To E-16

- Deleting semantic reference parsing from runtime API behavior.
- Replacing string-derived logical type with compiled semantic references and a runtime analytical descriptor.
- Separating real by-class truth from wildcard fallback.
- Publishing authoritative analytical facts in contracts and deleting current consumer heuristics.

## Validation Summary

1. `dotnet build`
   Result: green.
2. `dotnet test --nologo`
   Result: green.
3. Focused analytical regression command from the active terminal
   Result: green.

## Conclusion

`m-ec-p3a1` is ready to wrap as complete for its revised bridge scope. The remaining purification work should not be backfilled into this milestone; it now belongs wholly to E-16.