---
id: D-010
title: Epic numbering convention (E-xx)
status: accepted
---

**Status:** active
**Context:** Epics had no IDs, only slugs. Hard to see sequence at a glance in roadmap and folder listings.
**Decision:** Number epics sequentially starting at E-10. Affects folder name (`work/epics/E-{NN}-<slug>/`), branch name (`epic/E-{NN}-<slug>`), and milestone IDs (`m-E{NN}-<MM>-<slug>`). Forward-only — completed epics before E-10 stay unnumbered. Mid-term/aspirational epics get numbered when sequence is certain.
**Consequences:** All `.ai/` templates, skills, and paths updated. Active/planned epics need renaming when numbers assigned.
