# FlowTime rewrite

The specification for FlowTime's next version: a seeded restart in a new repository (decisions 1 and 2 in `spec.md`). This folder is the seed of that repository and moves there whole once it exists. Until then it lives here, outside `docs/`, because `docs/` describes the current system and this folder does not.

Nothing here is implemented, and nothing here is authority for this repository's code.

## Specs

| File | What it covers |
| --- | --- |
| `spec.md` | The seeded rewrite: decisions, the model language, verification, order of work and the measurements behind them. Figures in `spec/`. |
| `telemetry-analysis/spec.md` | Analysing a system from its telemetry: post-incident replay first, models derived rather than built, residuals as the main output. Figures in `telemetry-analysis/spec/`. |

## Pages

Published as private claude.ai artifacts; others can open them only once they are shared.

| Page | Link | Source |
| --- | --- | --- |
| Field Guide | https://claude.ai/artifact/VmM2JELcUJa6xVtRBZoH4w | `pages/field-guide.html`: a survey of this repository at commit 412467e0; its paths are as they were then |
| Restart Blueprint | https://claude.ai/artifact/86cuHmBJpxjY13m38VnKoV | `pages/restart-blueprint.html`: companion to `spec.md` |
| Building Blocks | https://claude.ai/artifact/PwGNkMKjdG8Kb7C2pySafj | `pages/building-blocks/` |
| Census Engine | https://claude.ai/artifact/4VE6Fo381wjFxvtJwojnvm | `pages/census-engine/` |
| Derived Models | https://claude.ai/artifact/HNqTFgnjGuhE8S3buNRnFd | `pages/derived-models/` |

`node pages/<page>/build.cjs` writes the page into `pages/out/`, which git ignores; each build links to the published artifacts by default. The Census Engine and Derived Models builds draw their figures from `data.json`, which `node pages/<page>/gen.js` regenerates from the toy model in `evidence/toy/`. Census Engine's `data.json` also records run times, which differ on every run.

## Evidence

Scripts and model files behind numbers the specs and pages cite. They run with Node 24; the scripts that call the Rust engine expect it built at `engine/target/release/flowtime-engine` in this repository.

### `spec.md`, Measurements

| Entry | Files | How it runs |
| --- | --- | --- |
| Closed-form property | `evidence/measurements/closed-form/spike-property.js`; `spike-property-broken.js` is the same generator against a model with the queue carry removed, which the property must catch | `node spike-property.js <work-dir>` |
| Simulation, spike model; simulation, steady load; Figures 4 and 5 | `evidence/measurements/simulation/fluid-limit.js` writes `fluid-limit.json`; `charts.js` draws the two figures from it with `svg-style.txt` | `node fluid-limit.js fluid-limit.json`, then `node charts.js fluid-limit.json svg-style.txt <svg-dir> <inline-dir>` |
| Simulation, two services in series | `evidence/measurements/tandem/run.js` | `node run.js <work-dir>` |
| Four ways to write the same queue | `evidence/measurements/queue-forms/*.yaml` | each file through `flowtime run` and `flowtime-engine eval` |
| Where each engine reads its own form | `evidence/measurements/engine-agreement/*.yaml` | the same |

The other entries (Rust test suite, every model through both engines, both C# paths, double counting, constraints and WIP, sizes) were run as the commands each entry describes; no script of theirs was kept.

### Pages

| Cited in | File | What it computes |
| --- | --- | --- |
| Building Blocks, live view | `evidence/toy/model2.js` | The toy model the live view runs, with both mechanism switches |
| Building Blocks, live view prose | `evidence/toy/check2.js`, `check3.js` | All eight switch combinations; the numbers the prose quotes |
| Building Blocks, decision 14 table | `evidence/toy/erlang.js` | The 1, 10 and 100 servers comparison, from Erlang C |
| Census Engine, Figure 5 table | `evidence/toy/limits/explicit.js` | Forward-only against solved inside the step, per step size |

`evidence/toy/model.js` is the toy's first version, without the switches; `limits/explicit.js` runs it.
