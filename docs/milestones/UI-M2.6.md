# UI-M2.6 — Export/Import Workflow

> **📋 Charter Notice**: This milestone has been superseded by the [FlowTime-Engine Charter](../flowtime-engine-charter.md). Current development follows [M2.8 Incremental Charter UI](M2.8-UI-INCREMENTAL.md) which implements artifacts-centric workflow. This document is preserved for reference.

**Status:** 📋 Charter Superseded *(originally Planned)*  
**Dependencies:** M2.6 (Export/Import Loop), UI-M2.5 (Navigation)  
**Owner:** UI Team

---

## Goal

Enable users to close the modeling loop through UI-driven export and import workflows with persistent artifacts management. This milestone creates the UI components needed to make the core loop (M2.6) usable for external analysis and ensures the UI never "forgets" previous work through a comprehensive artifacts registry interface.

## Context & Problem

M2.6 provides the core export/import capabilities and artifacts registry, but users need UI workflows to:
- **Browse persistent artifacts** across models, runs, and imported telemetry
- **Export model results** for external analysis in multiple formats  
- **Import data back** to validate loop closure and create telemetry artifacts
- **Track and organize artifacts** with tags, search, and filtering
- **Resume previous work** without losing context across sessions

Without UI integration, users lose track of their models and runs, making FlowTime feel ephemeral rather than a professional modeling platform.

## Functional Requirements

### **FR-UI-M2.6-1: Persistent Artifacts Registry UI** (New!)
UI provides comprehensive artifacts management with search, filtering, and organization capabilities.

**Global Artifacts Interface:**
- **Artifacts Drawer/Tab**: Always-accessible global navigation to all artifacts
- **Cards-based Display**: Visual cards for Model, Run, and Telemetry artifacts
- **Advanced Filtering**: By kind, tags, time window, owner, and capabilities
- **Search & Sort**: Text search with sorting by date, name, duration, status

**Artifact Cards:**
- **Model Cards**: "Run in engine", "Open in editor", "Duplicate as new model"
- **Run Cards**: "Open in graph", "Export CSV/JSON", "Compare with..."
- **Telemetry Cards**: "Replay in engine", "View details", "Use as baseline"
- **Thumbnails**: DAG preview images for quick visual recognition

**Organization Features:**
- **Star & Pin**: Personal favorites and team defaults
- **Tagging**: Custom tags for project organization
- **Deep Links**: Direct URL access to any artifact
- **Resume Context**: Restore last opened artifact on app load

### **FR-UI-M2.6-2: Export Workflow Integration**
UI provides seamless export capabilities integrated with existing model runs.

**Export UI Components:**
- **Export Button**: One-click export from run results page
- **Format Selection**: Choose Gold CSV, NDJSON, Parquet, or complete package
- **Download Management**: Progress indication and download links
- **Export History**: Track exported runs for reference

**Integration Points:**
- Export button on simulation results page
- Format options: CSV (Excel-ready), NDJSON (streaming/programmatic), Parquet (analytics), Full Package
- Export status notifications with download links
- Integration with existing FlowTime API and FlowTime-Sim modes

### **FR-UI-M2.6-3: Import Workflow Integration**
UI enables import of external data back into FlowTime with automatic artifact creation.

**Import Capabilities:**
- **Import Dialog**: Upload CSV, NDJSON, or Parquet files
- **Format Detection**: Automatic format recognition and validation
- **Schema Validation**: Real-time feedback on data format compatibility
- **Artifact Creation**: Automatically create Telemetry artifacts from imported data
- **Metadata Entry**: Add tags, name, and description during import

**Workflow Integration:**
- "Import Data" option from artifacts drawer and main navigation
- Format-specific upload handlers for each supported type
- Import status notifications with validation results
- Integration with DAG reconstruction and artifacts registry
- Imported data immediately appears as discoverable Telemetry artifacts

### **FR-UI-M2.6-4: Loop Validation Display**
UI displays validation results for export/import round-trip integrity.

**Validation Display:**
- **Round-trip Status**: Visual confirmation of successful export→import cycle
- **Data Integrity Metrics**: Confirmation of 100% deterministic round-trip
- **Format Validation Results**: Schema compliance for each format type
- **DAG Reconstruction Status**: Confirmation that imported data reconstructs original DAG

**Visual Components:**
- Loop closure status indicator with success/failure states
- Data integrity verification with detailed results
- Format-specific validation feedback
- Quick actions: "Re-export", "Try Different Format", "View Import Details"

### **FR-UI-M2.6-5: Complete Loop Workflow**
UI orchestrates the full artifacts-aware export → import → validate cycle seamlessly.

**Guided Workflow:**
```
1. Browse Artifacts → Select model from persistent registry
2. Run Model → Creates new Run artifact automatically
3. Export Results → Choose format for intended use case  
4. External Analysis → User works with exported data outside FlowTime
5. Import Validation → Import creates new Telemetry artifact
6. Verification → Confirm round-trip integrity and DAG reconstruction
7. Persistent Storage → All artifacts remain discoverable in registry
```

**Workflow State Management:**
- All operations create persistent artifacts with full metadata
- Resume last context automatically on app load
- Track artifact relationships (model → run → telemetry lineage)
- Export/import operations maintain full audit trail in artifacts catalog

## Technical Architecture

### **Artifacts Registry Integration**
```typescript
// Artifacts registry service
export class ArtifactsService {
  async listArtifacts(filters: ArtifactFilters): Promise<ArtifactsList> {
    return await api.get('/v1/artifacts', { params: filters });
  }
  
  async getArtifact(id: string): Promise<ArtifactDetail> {
    return await api.get(`/v1/artifacts/${id}`);
  }
  
  async starArtifact(id: string, starred: boolean): Promise<void> {
    // Personal favorites management
  }
  
  async runArtifact(artifactId: string, options?: RunOptions): Promise<RunResult> {
    return await api.post('/v1/runs', { artifactId, ...options });
  }
}
```

### **Export Integration**
```typescript
// Export service integration  
export class ExportService {
  async exportRun(runId: string, format: 'gold' | 'ndjson' | 'parquet' | 'complete'): Promise<ExportResult> {
    return await api.get(`/v1/runs/${runId}/export/${format}`);
  }
  
  async trackExport(runId: string, format: string): Promise<void> {
    // Export operations are automatically tracked in Run artifact catalog
  }
}
```

### **Import Integration**
```typescript
// Import service integration
export class ImportService {
  async importData(
    file: File, 
    metadata: { name: string; tags: string[]; description?: string }
  ): Promise<ImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('metadata', JSON.stringify(metadata));
    // Creates Telemetry artifact automatically
    return await api.post('/v1/import', formData);
  }
  
  async validateSchema(file: File): Promise<ValidationResult> {
    // Validate data format before import
  }
}
```

### **Loop Validation**
```typescript
// Loop closure validation component  
export class LoopValidationView {
  @Input() exportResult: ExportResult;
  @Input() importResult: ImportResult;
  @Input() validationMetrics: ValidationResult;
  
  // Round-trip integrity display
  // DAG reconstruction status
  // Format validation results
}
```

## Implementation Phases

### **Phase 1: Artifacts Registry UI**
- Build global artifacts drawer/tab with cards-based display
- Implement search, filtering, and sorting capabilities
- Add artifact cards with type-specific actions (Model/Run/Telemetry)
- Create star/pin functionality and deep linking

### **Phase 2: Export UI Integration**
- Add export buttons to artifact cards and results pages
- Implement format selection (CSV, NDJSON, Parquet) and download management
- Progress indication and download link generation
- Integration with artifacts registry for tracking

### **Phase 3: Import UI Integration**  
- Build import dialog component with artifact metadata entry
- Implement format detection and schema validation
- Create import progress tracking with automatic artifact creation
- File format-specific upload handlers

### **Phase 4: Loop Validation & Context Persistence**
- Develop round-trip integrity validation visualization
- Implement "resume last context" functionality
- Add workflow state management and operation tracking
- End-to-end testing with full artifact persistence

## Coordination with M2.6

### **Parallel Development Strategy**
Since UI-M2.6 depends heavily on M2.6 APIs, coordination is critical:

#### **Phase-by-Phase Coordination**
- **Phase 1**: UI builds artifacts registry with M2.6 artifacts API
- **Phase 2**: Export UI integration with M2.6 export endpoints
- **Phase 3**: Import UI integration with M2.6 import and artifact creation
- **Phase 4**: End-to-end testing of complete persistent workflow

#### **API Integration Points**
- **Artifacts API**: Registry search, filtering, and artifact management
- **Export API**: Format selection and file generation
- **Import API**: File upload with automatic artifact creation
- **Testing**: Joint validation of persistence and loop closure

### **API Dependencies & Feedback Loop**
```typescript
// Critical API contracts that need early stabilization
interface ArtifactsAPI {
  GET /v1/artifacts?kind=&query=&tag=&from=&to=  // Registry search/filter
  GET /v1/artifacts/{id}                         // Get artifact details
  POST /v1/runs                                  // Run artifact by ID
  // Response: Artifact listings and details with file links
}

interface ExportAPI {
  GET /v1/runs/{id}/export/{format}  // Format: 'gold' | 'ndjson' | 'parquet'
  // Response: File download or JSON with download URL
}

interface ImportAPI {
  POST /v1/import  // Body: FormData with file + metadata
  // Response: Created telemetry artifact with ID and catalog
}

// UI provides feedback on:
// - Artifact metadata completeness for cards display
// - Search/filter performance and relevance
// - Error message clarity for import validation
// - Deep linking requirements for artifacts
```

## New UI Components

```
ui/FlowTime.UI/Components/Artifacts/   # NEW: Artifacts registry UI
  ArtifactsDrawer.razor                 # Global artifacts navigation drawer
  ArtifactCard.razor                    # Individual artifact display cards
  ArtifactsList.razor                   # Paginated artifacts listing
  ArtifactsSearch.razor                 # Search and filtering interface
  ArtifactActions.razor                 # Context actions per artifact type
  StarToggle.razor                      # Star/favorite functionality
  
ui/FlowTime.UI/Components/Export/
  ExportButton.razor                    # One-click export from artifacts/results
  ExportFormatSelector.razor            # Format selection dialog
  ExportProgressIndicator.razor         # Download progress and links
  
ui/FlowTime.UI/Components/Import/      # NEW: Import workflow
  ImportDialog.razor                    # File upload with metadata entry
  ImportProgressIndicator.razor         # Upload and processing status
  SchemaValidationFeedback.razor        # Real-time format validation
  
ui/FlowTime.UI/Components/Workflow/
  ContextResume.razor                   # Resume last artifact on load
  LoopValidationStatus.razor            # Round-trip integrity display
  WorkflowBreadcrumbs.razor             # Show current workflow state
  
ui/FlowTime.UI/Services/
  ArtifactsService.cs                   # NEW: Artifacts registry API
  ExportService.cs                      # Export API integration
  ImportService.cs                      # Import API integration  
  ContextPersistenceService.cs          # Save/restore UI context
```

## Acceptance Criteria

### **Artifacts Registry**
- ✅ Global artifacts drawer provides access to all models, runs, and telemetry
- ✅ Search and filtering work across artifact metadata, tags, and timestamps
- ✅ Artifact cards display appropriate actions based on type (Model/Run/Telemetry)
- ✅ Star/pin functionality enables personal and team artifact organization
- ✅ Deep links allow direct navigation to any artifact via URL

### **Export Workflow**
- ✅ Users can export any run artifact with 2 clicks (Export → Format)
- ✅ Export formats (CSV, NDJSON, Parquet) work correctly with external tools
- ✅ Export progress shows clearly with download links when complete
- ✅ Export operations are tracked in artifact metadata

### **Import Workflow**
- ✅ Users can import CSV/NDJSON/Parquet files with metadata entry
- ✅ Import creates persistent Telemetry artifacts automatically
- ✅ Schema validation provides clear feedback during upload
- ✅ Imported artifacts immediately appear in registry and are searchable

### **Context Persistence**
- ✅ App resumes last opened artifact automatically on load
- ✅ Browser refresh returns to same artifact context via deep links
- ✅ UI never "forgets" previous work - all artifacts remain discoverable
- ✅ Users can easily navigate between related artifacts (model → runs)

## Success Metrics

### **Artifacts Discovery & Usage**
- **Registry Adoption**: >80% of users use artifacts drawer within first session
- **Context Resume**: >60% of returning users successfully resume previous work
- **Artifact Reuse**: >40% of runs use previously created model artifacts
- **Search Effectiveness**: Users find target artifacts within 3 clicks/queries

### **Loop Closure Validation**  
- **Export Success**: >90% success rate opening exported files in external tools
- **Import Completion**: >70% of users who export also test import round-trip
- **Persistence Value**: <10% of users report "losing" previous work
- **Deep Link Usage**: >50% of shared artifact links successfully open target artifacts

## Risk Mitigation

### **API Dependency Risk**
**Risk**: M2.6 API changes break UI integration  
**Mitigation**: 
- Mock-first development with contract-driven design
- Weekly API contract reviews with M2.6 team
- Automated API contract testing

### **Complexity Risk** 
**Risk**: Artifacts registry and workflow too complex for initial users
**Mitigation**:
- Progressive disclosure: advanced search features hidden initially
- Guided tutorial for first-time artifact workflow completion
- Simple "Recent Artifacts" view for quick access to latest work

### **Performance Risk**
**Risk**: Export/import operations and artifact search too slow for good UX
**Mitigation**:
- Streaming progress indicators for long operations
- Background processing with notification when complete
- Optimize artifact search for catalogs with <1000 artifacts

## Questions for Review

1. **Artifacts Storage**: Should artifacts registry use local filesystem initially or require cloud storage (S3/ADLS)?

2. **Search Performance**: Is file-based catalog scanning sufficient or should we implement a proper search index?

3. **Permissions Model**: How detailed should artifact visibility controls be for initial release (private/team/public)?

4. **Context Persistence**: Should resume context use local storage + server sync or server-only state?

5. **Thumbnail Generation**: Should artifact cards show DAG previews, and what's the rendering strategy?

## Next Steps

1. **API Contract Finalization**: Lock down M2.6 artifacts API response formats and catalog schema
2. **UX Design**: Create detailed wireframes for artifacts drawer, cards, and search interface
3. **Mock Development**: Start artifacts registry UI with mock data while M2.6 develops APIs
4. **Storage Strategy**: Define artifacts storage layout and backup/sync requirements
5. **Performance Planning**: Define search response times and large registry handling

---

## Revision History

| Date | Change | Author |
|------|--------|--------|
| 2025-09-18 | Initial UI-M2.6 milestone specification created | Assistant |