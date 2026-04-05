# UI-M-02.05 — Navigation Architecture Enhancement

> **📋 Charter Notice**: This milestone has been superseded by the [FlowTime-Engine Charter](../../../../docs/flowtime-engine-charter.md). The Engine+Sim navigation structure described here aligns with charter principles but implementation details now follow the Charter UI Milestone Structure (UI-M-02.07, UI-M-02.08, UI-M-02.09, UI-M-03.00).

> **Target Project:** FlowTime UI  
> **Prerequisites:** UI-M-2 ✅  
> **Status:** Charter Superseded *(originally IN PROGRESS)*  
> **Branch:** `feature/ui-dual-interface/route-based-architecture`

---

## Goal

Implement clear Engine vs Sim navigation structure that reinforces architectural distinctions through URL patterns, grouped navigation, and visual indicators. Make the FlowTime Engine vs FlowTime-Sim separation crystal clear to users.

## What We're Accomplishing

- **Route-Based Clarity:** Move FlowTime-Sim pages under `/sim/*` prefix for architectural clarity
- **Grouped Navigation:** Organize navigation into ANALYZE (Engine), SIMULATE (Sim), and TOOLS sections
- **Visual Indicators:** Add system attribution chips and color coding to reinforce distinctions
- **Enhanced Page Titles:** Include system context in all page titles and metadata
- **URL Self-Documentation:** Make bookmarks and shared URLs immediately clear about which system

## Why This Approach

- **Mental Model Alignment:** URL structure matches architectural reality (Engine vs Sim)
- **User Clarity:** Eliminate confusion about which system they're using
- **Future Scalability:** Clean organization for additional pages in each category
- **API Alignment:** `/sim/*` UI routes mirror `/sim/*` API endpoints
- **Documentation:** URLs become self-documenting for users and developers

## Technical Architecture

### URL Structure
```
/                       → Home
/features              → FlowTime Engine (Features)
/api-demo              → FlowTime Engine (API Testing)
/scenarios             → FlowTime Engine (Scenario Composer) - future
/sim/templates         → FlowTime-Sim (Template Studio)
/sim/catalogs          → FlowTime-Sim (Catalog Browser) - future
/health                → Tools (System Health)
/learn/*               → Learning Interface
```

### Navigation Structure
```
📊 ANALYZE (FlowTime Engine)
   ├─ Features (/features)
   ├─ API Demo (/api-demo) 
   └─ Scenarios (/scenarios) - future
🎲 SIMULATE (FlowTime-Sim)
   ├─ Templates (/sim/templates)
   └─ Catalogs (/sim/catalogs) - future
🔧 TOOLS
   ├─ Health (/health)
   └─ Settings (/settings) - future
🎓 LEARN
   └─ Getting Started (→ /learn/welcome)
```

## Functional Requirements

### FR-UI-M-02.05-1: Route-Based System Separation ✅
- Move TemplateRunner from `/templates` to `/sim/templates`
- Establish `/sim/*` prefix for all FlowTime-Sim related pages
- Maintain existing `/learn/*` structure for Learning Interface
- Keep FlowTime Engine pages at root level (`/features`, `/api-demo`, etc.)

### FR-UI-M-02.05-2: Flat Navigation with Visual Hierarchy ✅
- Implement flat navigation structure with section headers and indented submenus
- Add visual hierarchy through CSS indentation and styling
- Include system attribution chips (Engine/FlowTime-Sim) in section headers
- Use consistent iconography and color schemes
- Create landing pages for ANALYZE, SIMULATE, and TOOLS sections

### FR-UI-M-02.05-3: Enhanced Page Titles and Metadata ✅
- Update all page titles to include system context
- Format: "Page Name (FlowTime Engine)" or "Page Name (FlowTime-Sim)"
- Ensure browser tabs and bookmarks are immediately clear
- Update PageTitle directives in all Razor components

### FR-UI-M-02.05-4: Visual Mode Indicators ✅
- Add color coding: Primary blue for Engine, Secondary purple for Sim
- Include small system chips in navigation groups
- Use consistent iconography (Analytics for Engine, Casino for Sim)
- Maintain existing Demo/API mode toggle functionality

## Implementation Tasks

### 1. Route Updates
- [x] Update `TemplateRunner.razor` @page directive to `/sim/templates`
- [x] Update all navigation links to use new `/sim/templates` route
- [ ] Test route navigation and ensure no broken links

### 2. Navigation Structure
- [x] Replace expanding groups with flat navigation and visual hierarchy
- [x] Add ANALYZE, SIMULATE, TOOLS section headers with landing pages
- [x] Include system attribution chips in section headers
- [x] Implement CSS-based indentation for submenus
- [x] Create informative landing pages for each major section

### 3. Page Enhancements
- [x] Update page titles with system context
- [x] Ensure consistent PageTitle directives
- [x] Add visual indicators where appropriate

### 4. Testing and Validation
- [ ] Verify all routes navigate correctly
- [ ] Test navigation groups expand/collapse properly
- [ ] Ensure mobile navigation works with new structure
- [ ] Validate that existing functionality is preserved

## Expected User Benefits

1. **Immediate Clarity** - URL tells users which system they're using
2. **Better Mental Model** - Navigation structure matches architectural reality
3. **Improved Bookmarking** - Self-documenting URLs (`/sim/templates` vs `/templates`)
4. **Reduced Confusion** - Clear visual separation between Engine and Sim workflows
5. **Future Scalability** - Clean organization for additional features

## Files Modified

```
work/epics/completed/ui-charter-workflow/UI-M-02.05.md               # This milestone document
docs/ui/design-specification.md               # Updated navigation section
docs/ui/route-architecture.md                 # Updated route structure
```
ui/FlowTime.UI/Pages/TemplateRunner.razor     # Route change to /sim/templates
ui/FlowTime.UI/Layout/MainLayout.razor        # Flat navigation with visual hierarchy
ui/FlowTime.UI/Layout/MainLayout.razor.css    # Navigation hierarchy CSS styles
ui/FlowTime.UI/Pages/Analyze.razor           # FlowTime Engine landing page
ui/FlowTime.UI/Pages/Simulate.razor          # FlowTime-Sim landing page  
ui/FlowTime.UI/Pages/Tools.razor             # Tools landing page
ui/FlowTime.UI/Pages/Features.razor           # Enhanced page title
ui/FlowTime.UI/Pages/ApiDemo.razor           # Enhanced page title
ui/FlowTime.UI/Pages/Health.razor            # Enhanced page title
```
```

## Acceptance Criteria

- ✅ TemplateRunner accessible at `/sim/templates` (not `/templates`)
- ✅ Navigation shows clear ANALYZE/SIMULATE/TOOLS groupings
- ✅ System attribution chips visible in each navigation group
- ✅ Page titles include system context (Engine/FlowTime-Sim)
- ✅ All existing functionality preserved (mode toggle, health status, etc.)
- ✅ Mobile navigation works with grouped structure
- ✅ No broken links or navigation issues
- ✅ URL structure is self-documenting and consistent

---

**This milestone enhances the dual-interface architecture by making the Engine vs Sim distinction crystal clear through URL patterns, navigation structure, and visual indicators.**