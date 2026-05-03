# FT-M-05.16 — Topology Inspector Tabs

**Status:** ✅ Complete  
**Dependencies:** ✅ FT-M-05.14  
**Target:** Add a tabbed inspector UI so charts stay immediately visible and other sections are organized.

---

## Overview

The topology inspector currently stacks multiple sections in a long panel, which makes charts easy to lose. This milestone introduces tabs so charts are always the first view, with properties and other details accessible in additional tabs. The change improves readability without altering underlying data or metrics.

---

## Scope

### In Scope ✅
1. **Tabbed inspector UI**: replace the long stack with tabs.
2. **Default tab**: charts tab is default and remains the first tab.
3. **Content grouping**: properties, dependencies, warnings, and expression details move into dedicated tabs.
4. **State behavior**: keep the selected tab while the inspector remains open (reset on close).
5. **Preserve semantics**: all existing inspector metrics, tooltips, and chips behave as before.

### Out of Scope ❌
- ❌ New metrics or chart types.
- ❌ Changes to backend contracts.
- ❌ Visual redesign of the inspector beyond tab layout.

---

## Requirements

### FR1: Tabbed Layout
**Acceptance Criteria**
- [ ] Inspector uses tabs for major sections.
- [ ] Charts tab is first and selected by default.
- [ ] Tab labels are short and clear (e.g., Charts, Properties, Dependencies, Warnings, Expression).

### FR2: Content Mapping
**Acceptance Criteria**
- [ ] Charts tab contains existing chart blocks and sparklines.
- [ ] Properties tab contains the current properties list.
- [ ] Dependencies tab contains inbound/outbound dependency details.
- [ ] Warnings tab contains node warnings.
- [ ] Expression tab appears only when relevant to the node kind.

### FR3: State Preservation
**Acceptance Criteria**
- [ ] Tab selection persists while inspector is open.
- [ ] Closing the inspector resets to the default tab when reopened.

---

## Implementation Plan

### Phase 1: UI Layout
1. Add tab control to the inspector panel.
2. Map existing sections into tabs (charts first).

### Phase 2: State Handling
1. Track selected tab while inspector is open.
2. Reset tab on close.

### Phase 3: Validation
1. Verify charts are immediately visible on open.
2. Confirm all inspector content is reachable via tabs.

---

## Test Plan

### UI Tests
- `Topology_InspectorTabs_DefaultsToCharts`
- `Topology_InspectorTabs_PreservesSelectionWhileOpen`
- `Topology_InspectorTabs_ResetsOnClose`

---

## Success Criteria

- [ ] Charts are always directly visible on inspector open.
- [ ] All inspector content is reachable via tabs.
- [ ] No regression in tooltip/chip behavior.
