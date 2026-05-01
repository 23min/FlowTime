---
id: D-043
title: Matrix engine provenance — port basics, defer Sim-specific, explore plan hashing later
status: accepted
---

**Status:** active
**Context:** The C# engine has a provenance system (provenance.json, manifest.json with SHA256 hashes, template/parameter tracking) tightly coupled to the Sim→Engine→UI workflow. The Rust matrix engine (E-20) needs a provenance strategy. The plan-as-data representation opens new possibilities — plan hashing would make provenance structural rather than textual.
**Decision:** Port the basics now (SHA256 of model YAML, per-series hashes in manifest.json) as part of the .NET bridge work. Do NOT port Sim-specific provenance (template IDs, parameter bindings, generatedAt) until the bridge is wired up and Sim actually calls the Rust engine. Plan hashing (two models with different YAML but identical plans get the same hash) is deferred to E-17/E-18 where incremental evaluation makes it meaningful.
**Consequences:** The Rust engine can produce deterministic, verifiable output (model hash + series hashes) without coupling to the Sim authoring workflow. The bridge patch adds hashing. Sim-specific provenance fields are added when the bridge is exercised by real Sim runs. Plan hashing is a research item for the incremental evaluation milestone.
