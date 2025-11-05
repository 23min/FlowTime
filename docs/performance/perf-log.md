# FlowTime Performance Log

> General log for notable performance test runs across milestones. Add new entries chronologically with command, duration, result summary, and context.

| Milestone / Run | Date | Summary / Grade | Details |
| --- | --- | --- | --- |
| M-01.05 Expression Language | September 10, 2025 | Performance Grade: A+ 🎉 | [docs/performance/M1.5-performance-report.md](docs/performance/M1.5-performance-report.md) |
| M-01.06 Baseline (Revised) | September 11, 2025 | Baseline established; see report | [docs/performance/M1.6-performance-report-revised.md](docs/performance/M1.6-performance-report-revised.md) |
| M-2 PMF Benchmark | — | Performance Grade: Incomplete ⏸️ | [docs/performance/M2-pmf-performance-report.md](docs/performance/M2-pmf-performance-report.md) |
| TT-M-03.27 Full Suite | 2025-11-04 | Full suite: 6m44s (190 pass / 1 skip) | [docs/performance/perf-log.md#tt-m-03-27-queues-first-class](docs/performance/perf-log.md#tt-m-03-27-queues-first-class) |

---

## Detailed Entries

### TT-M-03.27 (Queues First-Class)

- `dotnet test tests/FlowTime.Tests -c Release --no-build`
- Duration: ~6m44s; Passed 190 / Skipped 1 (`FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Grid_Size_Scaling`)
- No regression observed after enabling queue depth precompute and latency nulling.

