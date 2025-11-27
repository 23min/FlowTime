# Service With Buffer Epic

This epic defines and tracks the introduction of **ServiceWithBuffer** as a first-class node type in the FlowTime engine, schema, and UI.

- Replace the legacy `kind: backlog` surface with a proper `kind: serviceWithBuffer` node that owns both queue/buffer and service behavior.
- Treat ServiceWithBuffer nodes as operational/service nodes in the topology, rendered like services with a small queue badge.
- Align engine, analyzers, templates, and UI so complex, cadence-driven flows are modeled as **services with buffers**, not queues with bolted-on behavior.

See `service-with-buffer-architecture.md` in this folder for the detailed proposal and `docs/milestones/SB-M-01-service-with-buffer.md` for the corresponding milestone spec.
