---
id: G-033
title: 'Tests are too weak: surveyed-output-only canaries cannot detect drift; need deterministic golden-output assertions'
status: open
---

### Why this is a gap

The `transportation-basic` regression above (`edge_flow_mismatch_incoming` × 3 after E-24 unification) was caught **by accident**: only because four prior run artifacts from 2026-04-24 happened to still be on disk for byte-comparison against today's run. **If those runs had been pruned, the regression would have been invisible to the test suite** — the build-time canary `Survey_Templates_For_Warnings` would still have reported `val-err == 0` and passed.

This exposes a structural weakness in the testing rigor:

1. **The canary asserts existence-of-error-class, not output-equivalence.** It says "no template produces a validator error". It does NOT say "this template produces this specific output for this specific parameter set." A regression that shifts the output without producing a validator error slips through.
2. **The canary gates on `val-err == 0` only.** Analyser warnings, run-time warnings, and per-bin numeric output drift are all unscored. A model could go from "12 warnings" to "0 warnings" or vice-versa, and from "arrivals=10.0" to "arrivals=10.5", and the canary would not budge.
3. **No golden-output fixtures.** The repo has 12 shipped templates that are surveyed-but-not-asserted-against. There is no fixture that says "for `transportation-basic` with `splitAirport=0.4`, `splitIndustrial=0.3`, `hubRetryRate=0.2`, the `arrivals[Router]` series at bin 12 is exactly X.XX, and the warning set is exactly {Y, Z}."

The user-stated intent is correct: **tests need to create models for which the test deterministically knows what the output should be, and compare against that.** Today's tests largely don't do that for the engine-template integration surface.

### What "stronger" looks like

A golden-output canary for each shipped template, with:

- A pinned parameter set (chosen for both numeric-stability and analytical-coverage — including some that should produce known warnings, some that should be clean).
- A pinned expected output bundle: per-series per-bin numeric values (or a tight tolerance), full warning set with codes + message + nodeId + edgeIds + severity, run manifest fields.
- A byte-identical (or tight-tolerance numeric) assertion. A drift fails the build with a clear diff showing which series at which bin moved by how much, and which warnings appeared / disappeared.
- Forward-only regeneration when a deliberate engine change shifts numeric output: a `--regenerate` mode the engineer runs after a sanctioned change, producing a reviewable diff that becomes the new pinned expected output. (Pattern matches `dotnet test --update-snapshots` style.)

This is the standard "approval testing" / "golden master" pattern. The `Survey_Templates_For_Warnings` canary is the lightweight version of this; it needs to be promoted to the strict version.

### Where the bar is already set higher (precedents in this repo)

- `tests/FlowTime.Adapters.Synthetic.Tests/FileSeriesReaderTests.cs` and similar — assert exact CSV byte content against pinned fixtures.
- `tests/FlowTime.Sim.Tests/Templates/RouterTemplateRegressionTests.cs`, `TransitNodeTemplateTests.cs`, `EvaluationIntegrityTemplateTests.cs` — assert specific structural properties per template.
- M-044 AC1 real-bytes fixture (`FLOWTIME_E2E_TEST_RUNS=1`) — pinned wire-format round-trip on `state_window` warnings.

These cover narrow surfaces. They do NOT cover the **end-to-end** "render template at default params → run engine → byte-compare full output bundle" loop that would have caught the regression above.

### Status

**Open.** Worth planning into a near-term milestone. Strongly suggest scoping into an explicit testing-rigor milestone before further engine evolution (E-22 Time Machine: Model Fit + Chunked Evaluation, E-15 Telemetry Ingestion, future Cloud Deployment work). Each of those will introduce more compilation paths, more analytical surfaces, more places where silent drift can hide.

### Proposed shape (for milestone planning)

**Milestone scope (rough; needs proper planning):**

1. **Pick a representative parameter set per template.** Default params are usually fine but some templates may need explicit "happy path" + "deliberately-broken" pairs to lock both the clean-output and the warning-emission paths.
2. **Generate the expected output bundle** by running the engine **once** at a sanctioned point in time (post-E-24, post-E-23, post-E-21 wrap is a reasonable baseline). Capture: full per-series per-bin numeric table (or tight-tolerance representation), full warning set, manifest summary fields.
3. **Pin the expected bundles** under `tests/fixtures/golden-templates/<template-id>/` with documentation of the parameter set + capture date + capture commit hash.
4. **Write the test:** parameterized over the 12 templates, each runs the engine and byte-compares (or tolerance-compares numeric series) against the pinned bundle.
5. **Add a `--regenerate` mode** for sanctioned-change workflow: after a deliberate engine change shifts output, the engineer regenerates the bundles and the diff becomes the PR review artifact.
6. **Make the canary fail on val-warn delta**, not just val-err. Even before the full golden pinning lands, a per-template `val-warn` count delta gate would have caught today's regression: 0→3 on `transportation-basic` would have failed the build.

**Out of scope (deliberately):**

- Floating-point exact equality across platforms. Tolerance-based numeric comparison is fine and is what every other comparable engine canary uses.
- Pinning intermediate compilation-IR. The pinned artifact is the **observable engine output** (series + warnings), not the intermediate compiled DAG. The IR can change freely as long as the output stays equivalent.

### Reference

- Today's regression case: `data/runs/run_20260428T165413Z_6ed5974e` vs `data/runs/run_20260424T150244Z_b2f4c995` (same template, default params, divergent warning counts).
- Existing canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs::Survey_Templates_For_Warnings`.
- Pattern precedents in this repo: see "Where the bar is already set higher" above.
- User-stated intent (this conversation, 2026-04-28): "Tests need to create models for which the test deterministically know what the output should be and compare against that."

### Immediate implications

- Do **not** delete the per-template run artifacts in `data/runs/` until a golden-output canary is in place — they are the only reproduction surface for retroactive drift detection.
- Treat any new analyser-warning regression on a previously-clean template as a **hard regression** until the canary is strict enough to catch it automatically.
- Plan this milestone into the near-term sequence — preferably before E-22 (Model Fit) starts, since model-fit work will compare engine output against telemetry and silently-drifting output would corrupt fit results without warning.

---
