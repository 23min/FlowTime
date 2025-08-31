# UI Milestone UI-M1 (Planned – Adjusted)

## Scenarios

- Edit and re‑run: Author a model in the browser and instantly see updated charts & structure.
- Example: Adjust scalar (0.8 → 0.7) and re‑run; observe chart + structural graph consistency.

## How it works

- YAML editor component with basic schema & semantic validation (client-side + fallback server errors).
- Run posts model via existing run client (API or simulation depending on flag).
- On success: update chart, structural table, micro-DAG.
- On error: inline markers + snackbar; stale results hidden or clearly marked.
- Toggle between current static selector mode and editor mode (simple switch or tab).

## Why it’s useful

- Tightens feedback loop; eliminates manual edits to example files.
- Ensures structural visualization remains accurate as models evolve.
- Builds foundation for future scenario and template features.

## Out of Scope (push to later)

- Multi-file includes / templating.
- Model diffing or version history.
- Real-time collaborative editing.

## Acceptance Criteria (draft)

- Editor initializes with currently selected (persisted) model text.
- Successful run updates all three views (chart, table, micro-DAG).
- Invalid YAML: no update to prior results; clear inline error.
- Semantic error (unknown node): highlighted and message shown.
- Works identically in simulation and API modes.
