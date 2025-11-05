# FlowTime Performance Log

> General log for notable performance test runs across milestones. Add new entries chronologically with command, duration, result summary, and context.

| Milestone / Run | Date | Summary / Grade | Details |
| --- | --- | --- | --- |
| M-01.05 Expression Language | September 10, 2025 | Performance Grade: A+ 🎉 | [docs/performance/M1.5-performance-report.md](docs/performance/M1.5-performance-report.md) |
| M-01.06 Baseline (Revised) | September 11, 2025 | Baseline established; see report | [docs/performance/M1.6-performance-report-revised.md](docs/performance/M1.6-performance-report-revised.md) |
| M-2 PMF Benchmark | — | Performance Grade: Incomplete ⏸️ | [docs/performance/M2-pmf-performance-report.md](docs/performance/M2-pmf-performance-report.md) |
| TT-M-03.27 Full Suite | 2025-11-04 | Full suite: 6m44s (190 pass / 1 skip) | [docs/performance/perf-log.md#tt-m-03-27-queues-first-class](docs/performance/perf-log.md#tt-m-03-27-queues-first-class) |
| TT-M-03.28 Full Suite | 2025-03-18 | Full suite: 6m03s (299 pass / 4 skip) | [docs/performance/perf-log.md#tt-m-03-28-retries-first-class](docs/performance/perf-log.md#tt-m-03-28-retries-first-class) |

---

## Detailed Entries

### TT-M-03.27 (Queues First-Class)

- `dotnet test tests/FlowTime.Tests -c Release --no-build`
- Duration: ~6m44s; Passed 190 / Skipped 1 (`FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Grid_Size_Scaling`)
- No regression observed after enabling queue depth precompute and latency nulling.

### TT-M-03.28 (Retries First-Class)

- `dotnet test FlowTime.sln -c Release --no-build`
- Duration: ~6m03s end-to-end (aggregate across all test projects)
- Results:
  - `FlowTime.Tests` — 190 passed / 1 skipped (`FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Grid_Size_Scaling`)
  - `FlowTime.Api.Tests` — 149 passed
  - `FlowTime.UI.Tests` — 132 passed
  - `FlowTime.Sim.Tests` — 134 passed / 3 skipped (`ExpressionLibrarySmokeTests`, `ExamplesConformance` RNG checks)
  - Remaining test projects (`Core`, `Expressions`, `Adapters`, `CLI`, `Integration`) all green
- Notes: Performance skips match historical baseline; no new regressions observed after enabling retry semantics (CONV, effort edges, inspector toggles).
