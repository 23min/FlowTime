# UI Milestone UI-M1

## Scenarios

- Edit and re‑run: Author a model in the browser and instantly see updated charts.
- Example: Adjust the scalar (e.g., 0.8 → 0.7) and re‑run to compare.

## How it works

- Add a YAML editor with basic schema validation.
- “Run” posts the model to a backend or triggers the CLI proxy, then reloads the CSV.
- Show errors inline if validation or evaluation fails.

## Why it’s useful now

- Tightens the feedback loop for modeling.
- Prepares the UI to consume the API once it’s available.
