---
id: G-013
title: E-18 Model Calibration Needs Crystal Ball Design Input
status: open
---

### Why this is a gap

E-18 mentions "model fitting against real telemetry" in scope but defers it to the analysis layer. The crystal ball's prediction accuracy depends fundamentally on calibration — how well the model's capacity, failure rates, and retry kernels match reality.

Unresolved questions that should be design inputs when E-18 fitting milestones are specced:
- Which model parameters are fittable? (Capacity, failure rate, retry kernel coefficients — but not topology or grid resolution.)
- What is the objective function? (Minimize series-level MSE between model output and observed telemetry? Per-node? Per-class?)
- How often does recalibration happen? (Per-run? Sliding window? Triggered by drift detection from anomaly epic?)
- Should the calibrated model carry provenance about its fit quality? (Residuals, confidence, calibration timestamp.)

### Immediate implications

- E-18 spec work should reference the crystal ball note when designing the fitting/optimization layer.
- Anomaly detection should consider "model-vs-reality divergence" as a calibration trigger, not just an alert.

### Reference

- `work/epics/E-18-headless-pipeline-and-optimization/spec.md`
- `docs/notes/crystal-ball-predictive-projection.md`
- `docs/notes/predictive-systems-and-uncertainty.md`

---
