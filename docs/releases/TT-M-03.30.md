# TT‑M‑03.30 — UI Overlays for Retries + Service Time

**Status:** ✅ Complete  
**Date:** November 8, 2025  
**Branch:** `feature/ui-m-0330-edge-overlays`

---

## 🎯 Milestone Outcome

Edge overlays are now first-class in the topology view. Operators can toggle Retry Rate or Attempts coloring, see a matching legend + inline labels, and persist those settings per run. Inspector ↔ canvas linking works both ways (hover/click on dependencies highlights the canvas edge and centers it), and overlay choices survive reloads via run-state storage. Playback controls were also cleaned up so timelines scrub smoothly with clear 1×/2×/4×/8× speeds.

## ✅ Completed Highlights

### Canvas & Overlays
- Added `EdgeOverlayMode` + label toggles in the Feature Bar, persisted via run-state/localStorage.
- Canvas derives retry overlays client-side (graph metadata + `/state_window` node series), colors edges using the SLA palette, doubles overlay edge width, and renders legends/labels with hover hitboxes.
- Inspector dependency list links to the canvas: hover drives edge highlighting, click centers/focuses the edge, and JS notifies .NET of canvas hovers.

### Persistence & Hotkeys
- Overlay settings serialize in the run-state payload alongside section expansion + viewport snapshots; legacy storage migration updated accordingly.
- Playback chips now bind to clean 1×/2×/4×/8× factors, and the playback loop advances multiple bins so higher multipliers are visibly faster.

### Documentation & Tracking
- Milestone doc notes the current limitation (Attempts overlay reflects upstream retries until `/state_window.edges` ships in TT‑M‑03.31).
- Roadmap + tracking files updated to reflect the client-side derivation decision and completion status.

## 📊 Validation

| Command | Outcome |
| --- | --- |
| `dotnet build FlowTime.sln` | ✅ (warnings: existing NU1603 + nullable diagnostics) |
| `dotnet test FlowTime.sln` | ⚠️ *Skipped.* Suite currently fails at `FlowTime.Sim.Tests.NodeBased.ModelGenerationTests.GenerateModelAsync_WithConstNodes_EmbedsProvenance`; tracked separately for TT‑M‑03.31. |

## ⚠️ Known Issues / Follow-Up

- Attempts overlay still shows upstream effort (retries) even if downstream throughput is lower; fix deferred to TT‑M‑03.31 when API edge slices become available.
- `FlowTime.Sim.Tests.NodeBased.ModelGenerationTests.GenerateModelAsync_WithConstNodes_EmbedsProvenance` continues to fail; run skipped for this milestone and needs resolution before the next release.
- Screenshots for docs were intentionally deferred to keep scope tight; capture them when we demo the client-side overlay derivation.

---

TT‑M‑03.30 delivers the UX for retry/service-time overlays; the remaining API work lands in TT‑M‑03.31.
