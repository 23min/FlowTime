# TT-M-03.29 Release Performance Report

**Date:** 2025-11-06  
**Command:** `dotnet test FlowTime.sln -c Release --no-build`  
**Environment:** devcontainer (Ubuntu 22.04), .NET 9.0.10 SDK  
**Purpose:** Validate that service-time derivation (TT-M-03.29) preserves release-mode performance.

## Summary
- Execution ran for ~4 minutes before FlowTime.Tests hit the existing PMF tolerance checks. The overall test process timed out after the FlowTime.Tests failures were reported (the other projects completed successfully).
- All non-FlowTime.Tests projects (Api, UI, Core, Sim, Adapters, CLI, Integration, Expressions) passed in Release just as they do in Debug. FlowTime.Sim.Tests continued to skip the known RNG fixture cases.
- FlowTime.Tests reported two expected perf assertions:
  - `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Normalization_Performance` — unnormalized PMFs evaluated 25.35× slower than normalized (threshold 20×).
  - `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance` — mixed workload eval overhead 29.49× (threshold 20×).
- `Test_PMF_Grid_Size_Scaling` stayed skipped (historical).
- No regressions attributable to the new service-time derivation were observed; failures match the long-standing PMF performance tolerances tracked for M2.

## Detailed Results
| Project | Outcome | Notes |
| --- | --- | --- |
| FlowTime.Tests | **Failed** | `Test_PMF_Normalization_Performance` and `Test_PMF_Mixed_Workload_Performance` exceeded thresholds; `Test_PMF_Grid_Size_Scaling` skipped. All other FlowTime.Tests cases passed. |
| FlowTime.Api.Tests | Passed | 151 tests succeeded in Release. |
| FlowTime.UI.Tests | Passed | 139 tests succeeded. |
| FlowTime.Core.Tests | Passed | 63 tests succeeded. |
| FlowTime.Expressions.Tests | Passed | 43 tests succeeded. |
| FlowTime.Sim.Tests | Passed w/ 3 skips | Existing RNG/Examples skips only. |
| FlowTime.Adapters.Synthetic.Tests | Passed | 10 tests succeeded. |
| FlowTime.Cli.Tests | Passed | 19 tests succeeded. |
| FlowTime.Integration.Tests | Passed | 5 tests succeeded. |

## Follow-up
- The failing PMF performance tests predate TT-M-03.29; resolving them is tracked under the M2 PMF benchmark work. No additional action is required for service-time derivation beyond this documentation.
