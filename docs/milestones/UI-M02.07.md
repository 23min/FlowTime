# UI-M2.7 — Artifacts Registry UI

**Status:** ✅ Complete (with documented deferrals)  
**Dependencies:** M2.7 ✅, M2.8 ✅, UI-M2.6 ✅  
**Target:** User interface for artifacts registry browsing and management  
**Completed:** 2025-09-24

---

## Goal

Implement **artifacts registry user interface** that enables users to browse, search, filter, and manage artifacts from the FlowTime Engine registry. This milestone creates the UI foundation for the charter's "never forget" principle by making all artifacts discoverable and accessible.

## Context & Charter Alignment

The **M2.7/M2.8 Enhanced Artifacts Registry** provides comprehensive API capabilities for artifact discovery and management. **UI-M2.7** implements the user-facing interfaces that make this registry accessible for daily workflows.

**Charter Role**: Enables the **[Artifacts]** tab in charter navigation and provides artifact browsing throughout the charter workflow.

## Functional Requirements

### **FR-UI-M2.7-1: Artifacts Browser Interface**
Users can browse all artifacts through an intuitive interface with comprehensive search and filtering capabilities.

**Core Capabilities:**
- **Browse**: View all artifacts in organized list/grid with pagination
- **Search**: Text search across artifact titles, descriptions, and metadata
- **Filter**: By type (runs/models/telemetry), date ranges, tags, file size
- **Sort**: By date, name, size, type with ascending/descending options
- **Actions**: View details, compare, download for each artifact
### **FR-UI-M2.7-2: Artifact Detail View**
Users can view comprehensive artifact details including metadata, files, and relationships.

**Core Capabilities:**
- **Metadata Display**: Complete artifact information (ID, created date, type, tags, source)
- **File Listing**: All files within artifact with sizes and download options
- **Relationships**: Links to related artifacts when available *(Deferred to UI-M2.9—see Deferred Scope)*
- **Actions**: Compare, download, edit metadata functionality
- **Navigation**: Breadcrumb navigation back to registry browser
                                
### **FR-UI-M2.7-3: API Integration & Services**
UI consumes M2.7/M2.8 enhanced artifacts registry APIs for all functionality.

**Integration Requirements:**
- **Registry API**: Consume `/v1/artifacts` and `/v1/artifacts/{id}`. UI integration for `/v1/artifacts/{id}/relationships` is deferred to UI-M2.9.
- **Enhanced Queries**: Support M2.8 advanced filtering (date ranges, file sizes, full-text search)
- **Error Handling**: Graceful degradation for API unavailability with user feedback
- **Performance**: Efficient loading with pagination, caching for large artifact collections
- **Real-time**: Registry refresh functionality to detect new artifacts

### **FR-UI-M2.7-4: Charter Workflow Integration**
Artifacts UI integrates seamlessly with charter navigation and workflow context.

**Integration Points:**
- **Navigation**: Artifacts accessible via main navigation (prepare for future [Artifacts] tab)
- **Reusable Components**: Artifact selector components for use in other charter workflows
- **Contextual Actions**: Compare, download actions integrate with charter workflow progression
- **State Management**: Preserve user context when navigating between charter sections


## Integration Points

### **Enhanced Registry API (M2.7/M2.8)**
- Consumes complete artifacts registry API with advanced querying capabilities
- Leverages M2.8 enhancements: date filtering, file size filtering, full-text search, relationships
- Error handling and performance optimization for large collections (1000+ artifacts)

### **Charter Workflow Context**
- Integrates with existing ExpertLayout navigation structure
- Prepares foundation for future UI-M2.8 charter tab system
- Provides reusable components for cross-workflow artifact selection

## Acceptance Criteria

### **Artifacts Browser Functionality**
- ✅ Users can browse all artifacts with search, filter, and sort capabilities
- ✅ Artifact detail view shows complete metadata and file listings
- ✅ File download and preview functionality works for all artifact types
- ✅ Registry refresh and import functionality accessible from UI
- ⏳ Related-artifact linking deferred to UI-M2.9 (interim placeholder banner acceptable)

### **Charter Workflow Integration**
- ✅ Artifact selector components work in Runs wizard input selection
- ✅ Contextual actions (Compare, Download) launch appropriate workflows
- ✅ Artifacts browser integrates seamlessly with charter navigation
- ✅ UI performance remains responsive with 1000+ artifacts

### **User Experience**
- ✅ Artifact browsing is intuitive and requires minimal training
- ✅ Search and filtering help users find artifacts quickly
- ✅ Error states and loading indicators provide clear feedback
- ✅ Mobile-responsive design works on tablets and smaller screens

## Implementation Approach

### **Iterative Development**
1. **Core Browser**: Basic /artifacts page with search/filter/list functionality
2. **Detail Views**: Individual artifact pages with metadata and file listings  
3. **API Integration**: Connect to M2.7/M2.8 registry endpoints with error handling
4. **Charter Integration**: Add to navigation, create reusable components
5. **Polish**: Performance optimization, responsive design, accessibility

---

## Deferred Scope

- **Related Artifact Links (UI Exposure)**: The backend relationships endpoint (`/v1/artifacts/{id}/relationships`) remains available, but the UI surfacing and navigation affordances are postponed to **UI-M2.9 – Compare Workflow UI Integration**. Interim UI should indicate that relationships are coming soon.
- **Compare-from-Detail Shortcut**: Compare actions that depend on the relationships panel ship alongside the deferred work above.

---

## Success Metrics

**User Experience:**
- Users can discover and access any artifact within 3 clicks
- Search/filter operations complete within 2 seconds with 1000+ artifacts
- Artifact detail views load completely within 1 second
- Download functionality works reliably for all artifact types and sizes

**Technical Integration:**
- All M2.7/M2.8 registry API endpoints consumed successfully
- UI maintains responsiveness on mobile/tablet devices
- Error states provide clear user guidance and recovery options
- Components integrate cleanly with existing MudBlazor design system

---

## Next Steps

1. **UI-M2.8**: Charter navigation structure and tab migration  
2. **UI-M2.9**: Compare workflow UI integration with artifact selection
3. **Cross-platform integration**: Coordination with Sim UI development

This milestone establishes the **UI foundation** for the charter's "never forget" principle, making all FlowTime artifacts discoverable and manageable through intuitive interfaces.
