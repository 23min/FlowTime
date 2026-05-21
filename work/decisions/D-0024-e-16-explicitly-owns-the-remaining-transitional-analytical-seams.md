---
id: D-0024
title: E-0016 explicitly owns the remaining transitional analytical seams
status: accepted
---

**Status:** active
**Context:** After the E-0016-01 and E-0016-02 cleanup, three transitional seams still remained visible: `RunManifestReader` could recover telemetry-source facts by reparsing raw YAML text, class ingestion still translated legacy `*` / `DEFAULT` fallback markers, and `MetricsService` still carried a second analytical execution path via model-evaluation fallback when state-window resolution failed.
**Decision:** These are not acceptable permanent bridges. E-0016 explicitly owns removing all three: M-0012 removes raw-model-text telemetry-source fallback readers, M-0013 removes legacy class-fallback translation helpers once regenerated runtime metadata carries explicit fallback labeling, and M-0015 removes the duplicate `MetricsService` analytical fallback path in favor of one Core evaluator surface.
**Consequences:** Review and wrap should treat any of these helpers surviving beyond their owning milestone as an incomplete E-0016 implementation, not a tolerable compatibility layer.
