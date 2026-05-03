---
id: G-034
title: Sim orchestration silently demotes dryRun:true when deterministic run already exists
status: addressed
discovered_in: M-062
addressed_by: [M-062]
---

## What's missing

`POST /api/v1/orchestration/runs` silently overrode `options.dryRun: true` and returned `{ isDryRun: false, wasReused: true, plan: null, metadata: <existing run> }` whenever `options.deterministicRunId: true` was set and a run with the computed id already existed on disk.

Reproducer (against `transportation-basic`):

```bash
# 1. Land a real run with deterministic id.
curl -X POST http://localhost:8090/api/v1/orchestration/runs \
  -H 'content-type: application/json' \
  -d '{"templateId":"transportation-basic","mode":"simulation",
       "rng":{"kind":"pcg32","seed":123},
       "options":{"dryRun":false,"deterministicRunId":true,"overwriteExisting":false}}'

# 2. Now request a dry-run with the same inputs — dryRun was ignored.
curl -X POST http://localhost:8090/api/v1/orchestration/runs \
  -H 'content-type: application/json' \
  -d '{"templateId":"transportation-basic","mode":"simulation",
       "rng":{"kind":"pcg32","seed":123},
       "options":{"dryRun":true,"deterministicRunId":true,"overwriteExisting":false}}' \
  | jq -c '{isDryRun, wasReused, planNull: (.plan == null)}'
# Before fix: {"isDryRun": false, "wasReused": true, "planNull": true}
# After fix:  {"isDryRun": true,  "wasReused": false, "planNull": false}
```

## Why it matters

Broke AC-5 of M-062 (Run Orchestration). The `/run` page's Preview button hits this trap as soon as the user has executed the same model once: the deterministic id collides, the API silently demotes dryRun, the page condition `phase === 'preview' && runResult?.isDryRun && runResult.plan` fails, and the page goes blank with no error feedback. More broadly: any client that uses dryRun to validate or estimate a run before committing it loses that guarantee silently. dryRun must be authoritative.

## Fix

`src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs` `CreateRunAsync()` — gate the `TryReuseExistingRunAsync` short-circuit on `!effectiveRequest.DryRun`. A dry-run now always falls through to the per-mode planning path which builds and returns a plan, regardless of whether a deterministic run already exists at the same id.

Test pinning the regression: `tests/FlowTime.TimeMachine.Tests/RunOrchestrationServiceTests.CreateRunAsync_DryRun_DoesNotReuseExistingDeterministicRun`.

## Out-of-scope debugging trail

While diagnosing this gap I initially mis-identified a "Trigger A" — full parameter set silently demoting dryRun. That was a phantom: subsequent dryRun requests with the full parameter set were hitting *this* gap because the prior real run with the same inputs had already created the deterministic-id run on disk. The full-parameter-set path itself is well-behaved; only the deterministic-reuse short-circuit was wrong.
