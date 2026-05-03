---
id: D-014
title: Split post-p3a projection hardening into its own milestone
status: accepted
---

**Status:** active
**Context:** Review of M-056 found the core analytical primitive is sound, but the state projection and contract surfaces still have duplicated capability logic, metadata drift, finite-value hardening gaps, and incomplete client symmetry. Folding that cleanup back into p3a would blur the milestone boundary and make later Phase 3 work harder to sequence.
**Decision:** Track the post-review cleanup as a dedicated follow-on milestone, `m-ec-p3a1`, before continuing to p3b/p3c/p3d. p3a remains the primitive-introduction milestone; p3a1 owns analytical projection and contract hardening.
**Consequences:** Phase 3 order becomes p3a → p3a1 → p3b → p3c → p3d. Future analytical milestones should build on the hardened projection surface rather than duplicating ad hoc snapshot/window logic.
