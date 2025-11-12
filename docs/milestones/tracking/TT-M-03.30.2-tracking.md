# TT‑M‑03.30.2 Tracking

Status: 🟡 Planned  
Milestone doc: [docs/milestones/TT-M-03.30.2.md](../TT-M-03.30.2.md)

## Checklist

- [ ] Audit existing templates for missing queues/buffers
- [ ] Add explicit consumer nodes to templates that gain queues
- [ ] Convert deterministic const series to PMFs (arrivals/demand/retry)
- [ ] Update API/UI goldens after template + UI refresh
- [ ] Refresh documentation + release note entry
- [ ] Implement retry loop arc + queue pill visuals (chips/focus/inspector updates)

## Notes

- Coordinate with template owners to stage queue additions per domain (IT ops, supply chain, transport, etc.)
- PMF derivation can reuse current arrays (normalize to preserve mean); document conversion approach for maintainers.
