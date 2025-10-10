# Decisions Log

- 2025-10-10: Defer documentation merge; keep Engine docs under `docs/` and copy FlowTime-Sim documentation into new `docs-sim/` for Phase 5 follow-up.
- 2025-10-10: Rebuilt `.github/copilot-instructions.md` to cover unified Engine + Sim guidance in a single document.
- 2025-10-10: Replaced `README.md` with consolidated mono-repo overview covering Engine and Sim surfaces.
- 2025-10-10: Added FlowTime.Sim projects and tests to `FlowTime.sln` under existing solution folders.
- 2025-10-10: Removed standalone FlowTime-Sim root artifacts (`FlowTimeSim.sln`, `flowtime-sim.code-workspace`) now that the solution is unified.
- 2025-10-10: Renamed workspace file to `flowtime.code-workspace` and removed legacy `ft.code-workspace`.
- 2025-10-10: Temporarily skipped FlowTime.Sim time-travel template tests pending merge of `Template.Window/Classes/Topology` support.
