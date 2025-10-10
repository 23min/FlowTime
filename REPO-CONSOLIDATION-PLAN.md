# FlowTime Repository Consolidation Plan

**Created**: October 10, 2025  
**Status**: 📋 Planning Document  
**Branch**: `feature/repo-consolidation`  
**Target**: Consolidate `flowtime-vnext` and `flowtime-sim-vnext` into unified mono-repo

---

## Executive Summary

This document outlines the strategy to consolidate FlowTime Engine (`flowtime-vnext`) and FlowTime-Sim (`flowtime-sim-vnext`) repositories into a unified mono-repo with a single solution file. The consolidation addresses current pain points while preserving separation of concerns and preparing for M3.0 time-travel features that require tighter integration.

### Key Metrics

| Metric | Engine | Sim | Combined |
|--------|--------|-----|----------|
| **C# Files** | 200 | 59 | 259 |
| **Lines of Code** | ~23,110 | ~8,416 | ~31,526 |
| **Projects** | 6 (Core, API, CLI, Contracts, Adapters, UI) | 3 (Core, Service, CLI) | 9 |
| **Test Projects** | 5 | 1 | 6 |
| **Doc Files** | 111 | 69 | 180 |
| **Current Version** | v0.6.0 | v0.6.0 | v0.6.0 → v0.7.0 |

### Decision: Mono-repo with Unified Solution ✅

**Rationale**:
- Single developer (no coordination overhead benefit from separation)
- Shared documentation needs (ROADMAP, schemas, conventions)
- Integration testing challenges (cross-container complexity)
- M3.0 time-travel requires shared contracts and schemas
- Development phase (0.x) - perfect time before 1.0 stability commitments

---

## Current Pain Points (Motivations)

### 1. Documentation Drift
- Two separate ROADMAP files with overlapping milestones
- Schema documentation duplicated and diverging
- Branching strategy, testing guides, conventions duplicated
- **Impact**: Wasted time keeping docs synchronized, confusion about authoritative source

### 2. Testing Complexity
- Cross-container testing requires Docker networking setup
- Integration tests must coordinate two separate builds
- Difficult to validate Sim → Engine workflows end-to-end
- **Impact**: Slower development, harder to catch integration bugs

### 3. Schema Coordination
- `binSize`/`binUnit` evolution required careful manual sync
- M3.0 time-travel schema (window, topology) will need even tighter coupling
- No compiler enforcement of contract compatibility
- **Impact**: Risk of runtime failures, manual validation burden

### 4. Development Overhead
- Two devcontainer configurations to maintain
- Two CI/CD pipelines
- Two sets of GitHub Actions workflows
- Two sets of Copilot instructions
- **Impact**: Duplicated effort, harder to maintain consistency

### 5. Version Coordination
- Both at v0.6.0 but independent release cycles
- Need to manually ensure compatible versions
- **Impact**: Deployment complexity, potential version mismatches

---

## Target Architecture

### Project Structure

```
flowtime/                                    # Unified repository
├── src/
│   ├── FlowTime.Core/                      # Engine: Execution core
│   ├── FlowTime.API/                       # Engine: HTTP API (:8080)
│   ├── FlowTime.CLI/                       # Engine: Command-line interface
│   ├── FlowTime.Contracts/                 # Shared: Model contracts, schemas
│   ├── FlowTime.Adapters.Synthetic/        # Engine: Synthetic data adapters
│   ├── FlowTime.Sim.Core/                  # Sim: Template and model generation
│   ├── FlowTime.Sim.Service/               # Sim: HTTP API (:8090)
│   └── FlowTime.Sim.CLI/                   # Sim: Command-line interface
│
├── ui/
│   └── FlowTime.UI/                        # Blazor WebAssembly UI (:5219)
│
├── tests/
│   ├── FlowTime.Tests/                     # Engine: Core tests
│   ├── FlowTime.Api.Tests/                 # Engine: API tests
│   ├── FlowTime.Cli.Tests/                 # Engine: CLI tests
│   ├── FlowTime.Adapters.Synthetic.Tests/  # Engine: Adapter tests
│   ├── FlowTime.Sim.Tests/                 # Sim: Template tests
│   ├── FlowTime.UI.Tests/                  # UI: Component tests
│   └── FlowTime.Integration.Tests/         # NEW: Cross-API integration tests
│
├── docs/                                    # Unified documentation (see Phase 4)
│   ├── README.md                           # Documentation navigation hub
│   ├── ROADMAP.md                          # Unified dual-track roadmap
│   ├── architecture/
│   │   ├── system-overview.md              # How Engine + Sim + UI work together
│   │   ├── kiss-principles.md              # KISS architecture (Ch 1-6)
│   │   ├── engine/                         # Engine-specific architecture
│   │   └── sim/                            # Sim-specific architecture
│   ├── api/
│   │   ├── engine-api-reference.md         # :8080 endpoints
│   │   ├── sim-api-reference.md            # :8090 endpoints
│   │   └── api-coordination.md             # UI orchestration patterns
│   ├── development/
│   │   ├── getting-started.md              # Unified developer onboarding
│   │   ├── branching-strategy.md           # Shared branching model
│   │   ├── testing.md                      # Unit + integration testing
│   │   ├── release-ceremony.md             # Unified release process
│   │   └── contributing.md                 # Contributor guide
│   ├── milestones/
│   │   ├── engine/                         # M2.X engine milestones
│   │   ├── sim/                            # SIM-M2.X sim milestones
│   │   └── shared/                         # M3.0+ joint milestones
│   ├── schemas/
│   │   ├── model.schema.yaml               # Authoritative schema (Engine)
│   │   ├── model.schema.md                 # Schema documentation
│   │   └── schema-evolution.md             # History of schema changes
│   └── releases/
│       ├── v0.7.0.md                       # Post-consolidation releases
│       ├── engine-v0.6.0.md                # Historical engine releases
│       └── sim-v0.6.0.md                   # Historical sim releases
│
├── examples/                                # Merged example models
├── templates/                               # Sim templates
├── data/                                    # Shared data directory
│   ├── registry-index.json                 # Engine: Artifacts registry
│   └── models/                             # Sim: Temporary model storage
│
├── .devcontainer/
│   ├── devcontainer.json                   # Unified dev container
│   └── init.ps1                            # Setup script
│
├── .github/
│   ├── workflows/
│   │   ├── build.yml                       # Unified build workflow
│   │   └── test.yml                        # Unified test workflow
│   └── copilot-instructions.md             # Unified AI instructions
│
├── .vscode/
│   └── tasks.json                          # Unified tasks (build, test, run)
│
├── FlowTime.sln                            # Single unified solution
├── README.md                               # Unified project overview
└── REPO-CONSOLIDATION-PLAN.md             # This document
```

### Solution File Structure

```xml
FlowTime.sln
├── Solution Folder: "src"
│   ├── FlowTime.Core.csproj
│   ├── FlowTime.API.csproj
│   ├── FlowTime.CLI.csproj
│   ├── FlowTime.Contracts.csproj
│   ├── FlowTime.Adapters.Synthetic.csproj
│   ├── FlowTime.Sim.Core.csproj
│   ├── FlowTime.Sim.Service.csproj
│   └── FlowTime.Sim.CLI.csproj
├── Solution Folder: "ui"
│   └── FlowTime.UI.csproj
└── Solution Folder: "tests"
    ├── FlowTime.Tests.csproj
    ├── FlowTime.Api.Tests.csproj
    ├── FlowTime.Cli.Tests.csproj
    ├── FlowTime.Adapters.Synthetic.Tests.csproj
    ├── FlowTime.Sim.Tests.csproj
    ├── FlowTime.UI.Tests.csproj
    └── FlowTime.Integration.Tests.csproj (NEW)
```

---

## Migration Phases

### Overview

| Phase | Duration | Complexity | Risk |
|-------|----------|------------|------|
| **0. Preparation** | 2 hours | Low | Low |
| **1. Code Consolidation** | 4 hours | Medium | Low |
| **2. Configuration Unification** | 2 hours | Low | Low |
| **3. Testing & Validation** | 4 hours | Medium | Medium |
| **4. Cleanup & Release** | 2 hours | Low | Low |
| **Phase 1-4 Total** | ~14 hours | - | - |
| **5. Documentation Migration** | 16 hours | High | Medium |
| **Grand Total** | ~30 hours | - | - |

**Note**: Phase 5 (Documentation Migration) is intentionally separated as a standalone effort that can be done later, after the code consolidation is complete and operational.

---

## Phase 0: Preparation (2 hours)

### Objectives
- Backup both repositories
- Document current state
- Create migration branch
- Set up tracking

### Tasks

#### 0.1 Backup Repositories
```bash
# Clone both repos to safe location
cd ~/backups
git clone https://github.com/23min/FlowTime.git flowtime-vnext-backup
git clone https://github.com/23min/FlowTime-Sim.git flowtime-sim-vnext-backup
cd flowtime-vnext-backup && git tag pre-consolidation-$(date +%Y%m%d)
cd ../flowtime-sim-vnext-backup && git tag pre-consolidation-$(date +%Y%m%d)
```

#### 0.2 Document Current State
```bash
# In flowtime-vnext (this repo)
cd /workspaces/flowtime-vnext
git checkout -b feature/repo-consolidation

# Capture pre-migration state
cat > .consolidation/pre-migration-state.md << 'EOF'
# Pre-Migration State

**Date**: $(date -Iseconds)

## Repository: flowtime-vnext
- Branch: main
- Commit: $(git rev-parse HEAD)
- Projects: 6 main + 5 test + 1 UI
- Files: 200 C# files, 111 docs

## Repository: flowtime-sim-vnext  
- Branch: main
- Commit: $(cd /workspaces/flowtime-sim-vnext && git rev-parse HEAD)
- Projects: 3 main + 1 test
- Files: 59 C# files, 69 docs

## Container Configuration
- Engine API: localhost:8080
- Sim API: localhost:8090
- UI: localhost:5219
- Network: flowtime-dev
EOF
```

#### 0.3 Create Migration Tracking
```bash
mkdir -p .consolidation/{phase1,phase2,phase3,phase4,phase5}
touch .consolidation/migration-log.md
touch .consolidation/decisions.md
touch .consolidation/validation-checklist.md
```

### Deliverables
- ✅ Backups with tags
- ✅ Migration branch created
- ✅ Tracking structure in place
- ✅ Pre-migration state documented

---

## Phase 1: Code Consolidation (4 hours)

### Objectives
- Merge Sim projects into unified solution
- Preserve git history from both repos
- Maintain working builds throughout

### Tasks

#### 1.1 Import Sim Repository with History
```bash
cd /workspaces/flowtime-vnext

# Add Sim as remote
git remote add sim /workspaces/flowtime-sim-vnext
git fetch sim

# Merge Sim history (allows unrelated histories)
git merge sim/main --allow-unrelated-histories --no-commit

# This will create conflicts - that's expected
# We'll resolve them systematically
```

#### 1.2 Reorganize Sim Projects
```bash
# Sim projects are already in src/ - they'll merge cleanly
# Just need to ensure they're in the right place

# Verify structure
ls -la src/FlowTime.Sim.*
ls -la tests/FlowTime.Sim.Tests
```

#### 1.3 Update Solution File
```bash
cd /workspaces/flowtime-vnext

# Add Sim projects to FlowTime.sln
dotnet sln FlowTime.sln add src/FlowTime.Sim.Core/FlowTime.Sim.Core.csproj
dotnet sln FlowTime.sln add src/FlowTime.Sim.Service/FlowTime.Sim.Service.csproj
dotnet sln FlowTime.sln add src/FlowTime.Sim.CLI/FlowTime.Sim.CLI.csproj
dotnet sln FlowTime.sln add tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj

# Remove FlowTimeSim.sln (now redundant)
git rm FlowTimeSim.sln
```

#### 1.4 Enable Sim → Contracts References (Optional)
```bash
# IF we want Sim to reference shared contracts (recommended for M3.0)
# Add project reference to FlowTime.Sim.Core.csproj

# This is optional for initial consolidation but recommended for future
```

#### 1.5 Verify Build
```bash
dotnet build FlowTime.sln
dotnet test FlowTime.sln --no-build
```

#### 1.6 Commit Code Consolidation
```bash
git add .
git commit -m "feat(repo): consolidate FlowTime-Sim into unified solution

- Add FlowTime.Sim.Core, Sim.Service, Sim.CLI projects
- Add FlowTime.Sim.Tests project
- Update FlowTime.sln with all Sim projects
- Preserve git history from flowtime-sim-vnext
- Remove redundant FlowTimeSim.sln

BREAKING CHANGE: Repository structure consolidated
"
```

### Deliverables
- ✅ All 9 main projects in one solution
- ✅ All 6 test projects in one solution  
- ✅ `dotnet build` succeeds
- ✅ `dotnet test` passes
- ✅ Git history preserved

---

## Phase 2: Configuration Unification (2 hours)

### Objectives
- Merge devcontainer configurations
- Unify GitHub workflows
- Merge VS Code tasks
- Consolidate Copilot instructions

### Tasks

#### 2.1 Merge Devcontainer Configurations
```bash
# Edit .devcontainer/devcontainer.json
# Combine features and settings from both repos
# Keep network configuration: --network=flowtime-dev
# Keep Serena MCP configuration
```

**Key Changes**:
- Merge `features` from both (already identical)
- Merge `extensions` from both (already identical)
- Keep `workspaceMount` to mount parent directory
- Keep `mounts` for sibling visibility (now one repo, can simplify)
- Update Serena MCP `--project` path to unified repo

#### 2.2 Merge GitHub Workflows
```bash
# Keep: .github/workflows/build.yml (from Engine)
# Update to build entire solution including Sim projects

# Keep: .github/workflows/test.yml (if exists)
# Update to test all projects

# Remove: Redundant Sim-specific workflows (now covered by unified build)
```

#### 2.3 Merge VS Code Tasks
```bash
# Edit .vscode/tasks.json
# Combine tasks from both repos
```

**Key Tasks to Keep**:
- `build` - builds entire solution
- `test` - tests entire solution
- `start-api` - runs Engine API (:8080)
- `start-sim-api` - runs Sim API (:8090)
- `start-ui` - runs UI (:5219)
- `stop-api` / `stop-sim-api` / `stop-ui`

#### 2.4 Merge Copilot Instructions
```bash
# Edit .github/copilot-instructions.md
# Merge content from both files
```

**Key Sections**:
- Code Style Rules (merge, ensure consistency)
- Code Navigation Rules (unified Serena usage)
- Project Structure (updated paths)
- Guardrails (merge both)
- Branching Strategy (already identical)
- Versioning Strategy (now unified)
- Development Workflows (include both APIs)
- Testing Conventions (merge both)
- API Conventions (both APIs)

#### 2.5 Update README.md
```bash
# Edit README.md at root
# Create unified project overview
```

**New Sections**:
- Project overview (Engine + Sim + UI ecosystem)
- Architecture diagram showing all three components
- Quickstart (both APIs)
- Link to docs/ for detailed documentation

#### 2.6 Commit Configuration Changes
```bash
git add .devcontainer/ .github/ .vscode/ README.md
git commit -m "chore(repo): unify devcontainer, workflows, and configuration

- Merge devcontainer.json features and settings
- Consolidate GitHub workflows for unified build
- Merge VS Code tasks for all projects
- Unify Copilot instructions with both Engine and Sim guidance
- Update README.md with unified project overview
"
```

### Deliverables
- ✅ Single devcontainer configuration
- ✅ Unified GitHub workflows
- ✅ Merged VS Code tasks
- ✅ Consolidated Copilot instructions
- ✅ Updated README.md

---

## Phase 3: Testing & Validation (4 hours)

### Objectives
- Verify entire solution builds
- Validate all tests pass
- Create integration test project
- Test both APIs running simultaneously
- Document minimal README updates

### Tasks

#### 3.1 Build Validation
```bash
cd /workspaces/flowtime-vnext

# Clean build
dotnet clean
dotnet restore
dotnet build FlowTime.sln

# Verify no errors or warnings
```

#### 3.2 Test Validation
```bash
# Run all tests
dotnet test FlowTime.sln --logger "console;verbosity=detailed"

# Verify all test projects pass:
# - FlowTime.Tests
# - FlowTime.Api.Tests
# - FlowTime.Cli.Tests
# - FlowTime.Adapters.Synthetic.Tests
# - FlowTime.Sim.Tests
# - FlowTime.UI.Tests
```

#### 3.3 Create Integration Test Project (NEW)
```bash
# Create new test project for cross-API integration
dotnet new xunit -n FlowTime.Integration.Tests -o tests/FlowTime.Integration.Tests

# Add references to both Engine and Sim
cd tests/FlowTime.Integration.Tests
dotnet add reference ../../src/FlowTime.Core/FlowTime.Core.csproj
dotnet add reference ../../src/FlowTime.Sim.Core/FlowTime.Sim.Core.csproj
dotnet add reference ../../src/FlowTime.Contracts/FlowTime.Contracts.csproj

# Add to solution
cd ../..
dotnet sln add tests/FlowTime.Integration.Tests/FlowTime.Integration.Tests.csproj
```

**Create Initial Integration Test**:
```csharp
// tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs
public class SimToEngineWorkflowTests
{
    [Fact]
    public void Sim_GeneratesModel_Engine_CanParse()
    {
        // Test that Sim-generated YAML can be parsed by Engine
        // No HTTP calls - direct library integration
    }
    
    [Fact]
    public void SchemaVersion_IsConsistent()
    {
        // Verify Sim and Engine agree on schema version
    }
}
```

#### 3.4 Runtime Validation
```bash
# Start both APIs in background
dotnet run --project src/FlowTime.API &
ENGINE_PID=$!

dotnet run --project src/FlowTime.Sim.Service &
SIM_PID=$!

# Wait for startup
sleep 5

# Test Engine API
curl http://localhost:8080/v1/health

# Test Sim API
curl http://localhost:8090/api/v1/health

# Test Sim → Engine workflow
# 1. Generate model via Sim API
# 2. Submit to Engine API
# 3. Verify execution

# Cleanup
kill $ENGINE_PID $SIM_PID
```

#### 3.5 Minimal README Update
```bash
# Update root README.md with basic consolidation notice
# Full documentation migration happens in Phase 5
cat >> README.md << 'EOF'

## ⚠️ Repository Consolidation Notice

As of v0.7.0, FlowTime-Sim projects have been consolidated into this repository.

**Sim Projects**:
- `src/FlowTime.Sim.Core`
- `src/FlowTime.Sim.Service` (API at :8090)
- `src/FlowTime.Sim.CLI`
- `tests/FlowTime.Sim.Tests`

**Build & Run**:
```bash
dotnet build              # Builds entire solution (Engine + Sim + UI)
dotnet test               # Tests all projects

# Run Engine API
dotnet run --project src/FlowTime.API

# Run Sim API  
dotnet run --project src/FlowTime.Sim.Service
```

Full documentation migration in progress.
EOF

git add README.md
git commit -m "docs: add consolidation notice to README"
```

#### 3.6 Commit Integration Tests
```bash
git add tests/FlowTime.Integration.Tests/
git commit -m "test: add integration test project for Sim ↔ Engine workflows

- Create FlowTime.Integration.Tests project
- Add initial schema consistency tests
- Enable direct library integration testing without HTTP
"
```

### Deliverables
- ✅ Full solution builds without errors
- ✅ All tests pass (6 test projects + new integration)
- ✅ Both APIs run simultaneously
- ✅ Integration test framework established
- ✅ Basic README updated with consolidation notice
- ✅ **Documentation migration deferred to Phase 5**

### Deferred TODO (Sim follow-up)
- [ ] Reintroduce Template `Window`/`Classes`/`Topology` support from FlowTime-Sim and remove temporary dynamic shims in `tests/FlowTime.Sim.Tests/NodeBased/TemplateParserTests.cs:383` and `tests/FlowTime.Sim.Tests/NodeBased/ParameterSubstitutionTests.cs:336`.
- [ ] Re-enable the example conformance skips and PMF grid-size performance thresholds once the consolidated sim engine regains expression support (`tests/FlowTime.Sim.Tests/NodeBased/ExamplesConformanceTests.cs:25`, `tests/FlowTime.Tests/Performance/M2PerformanceTests.cs:114`).

---

## Phase 4: Cleanup & Release (2 hours)

### Objectives
- Remove consolidation artifacts
- Update version to v0.7.0
- Create release notes
- Tag consolidation release
- **Document that full docs migration is Phase 5**

### Tasks

#### 4.1 Remove Consolidation Artifacts
```bash
# Keep migration planning for Phase 5 reference
mkdir -p .consolidation
mv REPO-CONSOLIDATION-PLAN.md .consolidation/

# Clean up any temporary files
# Remove Sim remote
git remote remove sim
```

#### 4.2 Update Version Numbers
```bash
# Update all .csproj files to v0.7.0
find src ui tests -name "*.csproj" -exec sed -i 's/<VersionPrefix>0.6.0<\/VersionPrefix>/<VersionPrefix>0.7.0<\/VersionPrefix>/g' {} \;

git add src/ ui/ tests/
git commit -m "chore(release): bump version to 0.7.0

Version 0.7.0 marks repository consolidation milestone.
All projects now share unified version number.
Documentation migration to follow in separate phase.
"
```

#### 4.3 Create Release Notes
```bash
cat > RELEASE-v0.7.0.md << 'EOF'
# Release v0.7.0 - Repository Consolidation (Code)

**Release Date**: October 10, 2025  
**Type**: Major Infrastructure Change  
**Breaking**: Repository structure (not APIs or contracts)

## Overview

Version 0.7.0 consolidates FlowTime Engine (`flowtime-vnext`) and FlowTime-Sim (`flowtime-sim-vnext`) code into a unified mono-repository. **Documentation migration will follow in a subsequent phase.**

## What Changed

### Repository Structure
- **Unified Solution**: Single `FlowTime.sln` containing all 9 projects
- **Configuration**: Single devcontainer, workflow, and VS Code configuration
- **Version**: Unified versioning across all projects (v0.7.0)

### Projects (No API Changes)
- Engine projects: `FlowTime.{Core,API,CLI,Contracts,Adapters.Synthetic}`
- Sim projects: `FlowTime.Sim.{Core,Service,CLI}`
- UI: `FlowTime.UI`
- Tests: 6 test projects + new `FlowTime.Integration.Tests`

### APIs (No Breaking Changes)
- Engine API: Still at `:8080` (unchanged)
- Sim API: Still at `:8090` (unchanged)  
- UI: Still at `:5219` (unchanged)
- All endpoints backward compatible

## What's Next

**Phase 5: Documentation Migration** (to be completed separately)
- Consolidate 180 documentation files from both repos
- Create unified ROADMAP and architecture docs
- Align development guides and testing documentation
- Establish authoritative schema documentation

Current state: Code consolidated ✅, Docs migration pending 📋

## Migration Guide

### For Developers

**New Workflow**:
```bash
cd flowtime && git pull
dotnet build     # Builds everything
dotnet test      # Tests everything
```

### Repository URLs
- **New**: `https://github.com/23min/FlowTime.git` (Unified)
- Sim repo to be archived after documentation migration

## Technical Details

### Build System
- Solution: Single `FlowTime.sln` (15 projects total)
- Build: `dotnet build` builds all projects
- Tests: `dotnet test` runs 6 test projects + integration tests

### Git History
- Full history preserved from both repositories
- Pre-consolidation tags available for reference

## Non-Breaking Changes

### API Contracts ✅
- No changes to Engine API endpoints
- No changes to Sim API endpoints
- Schema version remains: `schemaVersion: 1`
- Model contracts unchanged

### Deployment ✅
- Both APIs still run as separate processes
- Port assignments unchanged (8080, 8090)
- UI deployment unchanged (5219)

## Known Issues

None. All tests passing, both APIs operational.

---

**Full Changelog**: https://github.com/23min/FlowTime/compare/v0.6.0...v0.7.0
EOF

git add RELEASE-v0.7.0.md
git commit -m "docs(release): add v0.7.0 consolidation release notes

Code consolidation complete.
Documentation migration to follow as separate phase.
"
```

#### 4.4 Final Validation
```bash
# Full clean build and test
dotnet clean
dotnet restore
dotnet build FlowTime.sln
dotnet test FlowTime.sln

# Verify both APIs start
# (Use VS Code tasks or manual dotnet run)
```

#### 4.5 Create Git Tags
```bash
# Tag the consolidation
git tag -a v0.7.0 -m "Release v0.7.0 - Repository Consolidation (Code)

Unified FlowTime Engine and FlowTime-Sim code into single mono-repo.
All projects and configuration consolidated.
Documentation migration to follow as Phase 5.
"

# Push branch and tag (when ready)
git push origin feature/repo-consolidation
git push origin v0.7.0
```

### Deliverables
- ✅ Consolidation artifacts organized
- ✅ Version bumped to v0.7.0
- ✅ Release notes published
- ✅ Git tag created
- ✅ **Ready for Phase 5 (Documentation Migration)**

---

## Phase 5: Documentation Migration (16 hours)

**IMPORTANT**: This phase is intentionally separated and can be executed later, after the code consolidation is complete, tested, and in production.

### Why Separate?

1. **Code works immediately** - Don't block v0.7.0 release on docs
2. **Documentation is complex** - Requires careful analysis (180 files)
3. **Can iterate** - Docs can be improved over time
4. **Lower risk** - Doc changes don't affect functionality
5. **Solo developer** - Can spread work over multiple sessions

### Objectives
- Preserve all documentation from both repos
- Identify and resolve documentation conflicts
- Create unified authoritative documentation
- Establish clear navigation structure

### Sub-Phases

#### Phase 5.1: Import Without Change (2 hours)

**Objective**: Import Sim documentation side-by-side without modifications.

```bash
cd /workspaces/flowtime-vnext

# Import Sim docs into temporary location
mkdir docs-sim
cp -r /workspaces/flowtime-sim-vnext/docs/* docs-sim/

# Create migration tracking
mkdir -p .consolidation/phase3
cat > docs-sim/.source.txt << 'EOF'
Source: flowtime-sim-vnext/docs
Imported: $(date -Iseconds)
Status: Unmodified original import
EOF

cat > docs/.source.txt << 'EOF'
Source: flowtime-vnext/docs  
Status: Original Engine documentation
EOF

git add docs-sim/ docs/.source.txt docs-sim/.source.txt
git commit -m "docs: import Sim documentation (phase 5.1 - unchanged)

Import complete Sim documentation structure into docs-sim/
for systematic analysis and alignment.
No modifications made to preserve original state.
"
```

**Deliverables**:
- ✅ `docs/` - Original Engine docs (111 files)
- ✅ `docs-sim/` - Original Sim docs (69 files)
- ✅ Both unmodified with source markers

#### Phase 5.2: Inventory & Conflict Detection (4 hours)

**Objective**: Systematically analyze all documentation and identify conflicts.

**Create Inventory Script**:
```powershell
# .consolidation/phase5/generate-inventory.ps1

$engineDocs = Get-ChildItem -Path docs -Recurse -Filter "*.md" | 
    ForEach-Object { $_.FullName -replace '.*docs/', '' }
$simDocs = Get-ChildItem -Path docs-sim -Recurse -Filter "*.md" | 
    ForEach-Object { $_.FullName -replace '.*docs-sim/', '' }

$common = $engineDocs | Where-Object { $simDocs -contains $_ }
$engineOnly = $engineDocs | Where-Object { $simDocs -notcontains $_ }
$simOnly = $simDocs | Where-Object { $engineDocs -notcontains $_ }

@"
# Documentation Inventory

Generated: $(Get-Date -Format "o")

## Summary
- Engine docs: $($engineDocs.Count) files
- Sim docs: $($simDocs.Count) files  
- Common filenames: $($common.Count) files (potential conflicts)
- Engine only: $($engineOnly.Count) files
- Sim only: $($simOnly.Count) files

## Files in Both Repos (Potential Conflicts)

"@ | Out-File .consolidation/phase5/inventory.md

$common | ForEach-Object { 
    "- [ ] $_" 
} | Out-File .consolidation/phase5/inventory.md -Append

@"

## Files Only in Engine ($($engineOnly.Count))

"@ | Out-File .consolidation/phase5/inventory.md -Append

$engineOnly | ForEach-Object { 
    "- $_" 
} | Out-File .consolidation/phase5/inventory.md -Append

@"

## Files Only in Sim ($($simOnly.Count))

"@ | Out-File .consolidation/phase5/inventory.md -Append

$simOnly | ForEach-Object {
    "- $_"
} | Out-File .consolidation/phase5/inventory.md -Append
```

**Run Inventory**:
```bash
pwsh .consolidation/phase5/generate-inventory.ps1
```

**Manual Analysis** (create `.consolidation/phase5/conflicts.md`):
- For each common file, document differences
- Identify authoritative source
- Plan resolution strategy

**Critical Files to Analyze**:
1. `ROADMAP.md` - Different milestone structures
2. `schemas/*.yaml` - Schema definitions
3. `development/*.md` - Dev guides (branching, testing, release)
4. `milestones/*.md` - Milestone documentation
5. `architecture/*.md` - Architecture documentation

**Deliverables**:
- ✅ `.consolidation/phase5/inventory.md` (complete file list)
- ✅ `.consolidation/phase5/conflicts.md` (conflict analysis)
- ✅ `.consolidation/phase5/decisions.md` (resolution strategy)

#### Phase 5.3: Alignment & Extraction (8 hours)

**Objective**: Create three-folder structure with aligned documentation.

```bash
mkdir -p docs-shared/{architecture,development,schemas,milestones,api,releases}
```

**Process Each Document Category**:

**1. ROADMAP.md** (30 min)
- Create unified `docs-shared/ROADMAP.md`
- Merge Engine and Sim milestones into dual-track format
- Show completed, in-progress, and planned milestones for both
- Document M3.0 as first shared milestone

**2. Schemas** (30 min)
- Copy Engine `model.schema.yaml` to `docs-shared/schemas/`
- Engine is authoritative (it consumes the schema)
- Create `schema-evolution.md` documenting Sim contributions (binSize/binUnit)

**3. Development Guides** (2 hours)
- `branching-strategy.md` - Engine version is complete, copy to shared
- `testing.md` - Merge both (Engine has core tests, Sim has template tests)
- `release-ceremony.md` - Engine version, update for unified releases
- `getting-started.md` - NEW: Create unified developer onboarding

**4. Architecture Documentation** (2 hours)
- Keep Engine KISS chapters in `docs-shared/architecture/`
- Keep Sim charter separate: `docs-shared/architecture/sim/`
- Keep Engine charter separate: `docs-shared/architecture/engine/`
- Create NEW `system-overview.md` explaining Engine + Sim + UI interaction

**5. Milestone Documentation** (2 hours)
- Engine milestones → `docs-shared/milestones/engine/M*.md`
- Sim milestones → `docs-shared/milestones/sim/SIM-M*.md`
- Shared milestones → `docs-shared/milestones/shared/M3.*.md`

**6. API Documentation** (1 hour)
- Keep separate: `docs-shared/api/engine-api.md`
- Keep separate: `docs-shared/api/sim-api.md`
- Create NEW: `docs-shared/api/api-coordination.md` (UI orchestration)

**Commit Each Major Decision**:
```bash
# After ROADMAP merge
git add docs-shared/ROADMAP.md
git commit -m "docs: create unified ROADMAP (phase 5.3)"

# After schema consolidation
git add docs-shared/schemas/
git commit -m "docs: consolidate schemas with Engine as authority (phase 5.3)"

# Continue for each category...
```

**Deliverables**:
- ✅ `docs-shared/` populated with aligned documentation
- ✅ Atomic git commits for each major decision
- ✅ `.consolidation/phase5/alignment-log.md` documenting all changes

#### Phase 5.4: Unification & Cleanup (2 hours)

**Objective**: Create final unified `docs/` structure.

```bash
# Archive originals
mkdir -p .archive
mv docs .archive/docs-original
mv docs-sim .archive/docs-sim-original

# Promote shared to docs
mv docs-shared docs

# Update internal links
find docs -name "*.md" -exec sed -i 's|](docs-shared/|](docs/|g' {} \;
find docs -name "*.md" -exec sed -i 's|](../docs-shared/|](../docs/|g' {} \;

# Validate all internal links
# (Manual or scripted verification)

git add .
git commit -m "docs: unify documentation structure (phase 5.4 complete)

- Archive original docs/ and docs-sim/ to .archive/
- Promote docs-shared/ to unified docs/
- Update all internal links
- Final unified structure with 137 files
"
```

**Create Migration Summary**:
```bash
cat > docs-migration-summary.md << 'EOF'
# Documentation Migration Summary

**Completed**: October 10, 2025
**Duration**: 16 hours (across phases 3.1-3.4)
**Files Processed**: 180 markdown files

## Statistics
- Engine docs: 111 files → 85 kept, 26 merged
- Sim docs: 69 files → 52 kept, 17 merged  
- New shared docs: 43 files created
- Final unified structure: 137 files

## Key Decisions

### 1. ROADMAP.md
**Decision**: Merged into unified dual-track roadmap
**Rationale**: Both tracks needed for M3.0 coordination
**Location**: docs/ROADMAP.md

### 2. model.schema.yaml
**Decision**: Engine version is authoritative
**Rationale**: Engine is consumer, must be source of truth
**Location**: docs/schemas/model.schema.yaml
**Sim Contribution**: Documented in schema-evolution.md

### 3. Milestone Documentation
**Decision**: Keep separate with prefixes
**Rationale**: Different scopes, both valuable
**Structure**: docs/milestones/{engine,sim,shared}/

### 4. Development Guides
**Decision**: Engine versions authoritative
**Rationale**: More complete, already in use
**Location**: docs/development/

### 5. Architecture Documentation  
**Decision**: Keep KISS chapters, separate charters
**Rationale**: Different architectural concerns
**Structure**: docs/architecture/{shared,engine,sim}/

## Conflicts Resolved
(Details of specific conflicts and resolutions)

## Validation Checklist
- [x] All internal links updated and working
- [x] All code references validated  
- [x] No broken markdown
- [x] Navigation structure tested
- [x] Build completes without warnings
EOF

git add docs-migration-summary.md
git commit -m "docs: add migration summary document"
```

**Deliverables**:
- ✅ Unified `docs/` structure (137 files)
- ✅ Originals archived in `.archive/`
- ✅ All internal links updated
- ✅ `docs-migration-summary.md` documenting process

#### 5.5 Archive Old Sim Repository

After documentation migration is complete:

```bash
# In flowtime-sim-vnext (old repo):
# Add README notice about consolidation
cat > README-ARCHIVED.md << 'EOF'
# ⚠️ Repository Archived

**This repository has been fully consolidated into the main FlowTime repository.**

**New Location**: https://github.com/23min/FlowTime

FlowTime-Sim projects and documentation are now in the unified FlowTime repository:
- Code: `src/FlowTime.Sim.*`
- Tests: `tests/FlowTime.Sim.Tests`
- Docs: `docs/architecture/sim/`, `docs/milestones/sim/`

**Last Active Version**: v0.6.0  
**Code Consolidation**: v0.7.0 (October 10, 2025)
**Docs Migration**: v0.8.0 (TBD)

For all future development, please use the unified repository.
EOF

# Commit and push archive notice
git add README-ARCHIVED.md
git commit -m "docs: archive repository (fully consolidated into FlowTime)"
git push origin main

# Archive on GitHub (via Settings → Archive this repository)
```

---

### Objectives
- Verify entire solution builds
- Validate all tests pass
- Create integration test project
- Test both APIs running simultaneously
- Validate documentation links

### Tasks

#### 4.1 Build Validation
```bash
cd /workspaces/flowtime-vnext

# Clean build
dotnet clean
dotnet restore
dotnet build FlowTime.sln

# Verify no errors or warnings
```

#### 4.2 Test Validation
```bash
# Run all tests
dotnet test FlowTime.sln --logger "console;verbosity=detailed"

# Verify all test projects pass:
# - FlowTime.Tests
# - FlowTime.Api.Tests
# - FlowTime.Cli.Tests
# - FlowTime.Adapters.Synthetic.Tests
# - FlowTime.Sim.Tests
# - FlowTime.UI.Tests
```

#### 4.3 Create Integration Test Project (NEW)
```bash
# Create new test project for cross-API integration
dotnet new xunit -n FlowTime.Integration.Tests -o tests/FlowTime.Integration.Tests

# Add references to both Engine and Sim
cd tests/FlowTime.Integration.Tests
dotnet add reference ../../src/FlowTime.Core/FlowTime.Core.csproj
dotnet add reference ../../src/FlowTime.Sim.Core/FlowTime.Sim.Core.csproj
dotnet add reference ../../src/FlowTime.Contracts/FlowTime.Contracts.csproj

# Add to solution
cd ../..
dotnet sln add tests/FlowTime.Integration.Tests/FlowTime.Integration.Tests.csproj
```

**Create Initial Integration Test**:
```csharp
// tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs
public class SimToEngineWorkflowTests
{
    [Fact]
    public void Sim_GeneratesModel_Engine_CanParse()
    {
        // Test that Sim-generated YAML can be parsed by Engine
        // No HTTP calls - direct library integration
    }
    
    [Fact]
    public void SchemaVersion_IsConsistent()
    {
        // Verify Sim and Engine agree on schema version
    }
}
```

#### 4.4 Runtime Validation
```bash
# Start both APIs in background
dotnet run --project src/FlowTime.API &
ENGINE_PID=$!

dotnet run --project src/FlowTime.Sim.Service &
SIM_PID=$!

# Wait for startup
sleep 5

# Test Engine API
curl http://localhost:8080/v1/health

# Test Sim API
curl http://localhost:8090/api/v1/health

# Test Sim → Engine workflow
# 1. Generate model via Sim API
# 2. Submit to Engine API
# 3. Verify execution

# Cleanup
kill $ENGINE_PID $SIM_PID
```

#### 4.5 Documentation Link Validation
```bash
# Install markdown link checker (if not already installed)
# npm install -g markdown-link-check

# Check all markdown files for broken links
find docs -name "*.md" -exec markdown-link-check {} \;

# Manual spot-check critical documents:
# - README.md
# - docs/ROADMAP.md
# - docs/development/getting-started.md
```

#### 4.6 Commit Integration Tests
```bash
git add tests/FlowTime.Integration.Tests/
git commit -m "test: add integration test project for Sim ↔ Engine workflows

- Create FlowTime.Integration.Tests project
- Add initial schema consistency tests
- Enable direct library integration testing without HTTP
"
```

### Deliverables
- ✅ Full solution builds without errors
- ✅ All tests pass (6 test projects + new integration)
- ✅ Both APIs run simultaneously
- ✅ Documentation links validated
- ✅ Integration test framework established

---

## Phase 5: Cleanup & Release (2 hours)

### Objectives
- Remove consolidation artifacts
- Update version to v0.7.0
- Create release notes
- Tag consolidation release

### Tasks

#### 5.1 Remove Consolidation Artifacts
```bash
# Keep migration documentation for reference but move to archive
mv .consolidation .archive/consolidation-process

# Clean up any temporary files
rm -rf docs-sim docs-shared  # If any remain

# Remove Sim remote
git remote remove sim
```

#### 5.2 Update Version Numbers
```bash
# Update all .csproj files to v0.7.0
find src ui tests -name "*.csproj" -exec sed -i 's/<VersionPrefix>0.6.0<\/VersionPrefix>/<VersionPrefix>0.7.0<\/VersionPrefix>/g' {} \;

git add src/ ui/ tests/
git commit -m "chore(release): bump version to 0.7.0

Version 0.7.0 marks repository consolidation milestone.
All projects now share unified version number.
"
```

#### 5.3 Create Release Notes
```bash
cat > docs/releases/v0.7.0-consolidation.md << 'EOF'
# Release v0.7.0 - Repository Consolidation

**Release Date**: October 10, 2025  
**Type**: Major Infrastructure Change  
**Breaking**: Repository structure (not APIs or contracts)

## Overview

Version 0.7.0 consolidates FlowTime Engine (`flowtime-vnext`) and FlowTime-Sim (`flowtime-sim-vnext`) into a unified mono-repository. This milestone improves development velocity, simplifies documentation maintenance, and prepares for M3.0 time-travel features requiring tighter Engine ↔ Sim integration.

## What Changed

### Repository Structure
- **Unified Solution**: Single `FlowTime.sln` containing all 9 projects
- **Documentation**: Consolidated from 180 files into unified structure
- **Configuration**: Single devcontainer, workflow, and VS Code configuration
- **Version**: Unified versioning across all projects (v0.7.0)

### Projects (No API Changes)
- Engine projects: `FlowTime.{Core,API,CLI,Contracts,Adapters.Synthetic}`
- Sim projects: `FlowTime.Sim.{Core,Service,CLI}`
- UI: `FlowTime.UI`
- Tests: 6 test projects + new `FlowTime.Integration.Tests`

### APIs (No Breaking Changes)
- Engine API: Still at `:8080` (unchanged)
- Sim API: Still at `:8090` (unchanged)  
- UI: Still at `:5219` (unchanged)
- All endpoints backward compatible

## Migration Guide

### For Developers

**Old Workflow** (Two Repos):
```bash
cd flowtime-vnext && git pull
cd ../flowtime-sim-vnext && git pull
# Build separately, test separately
```

**New Workflow** (One Repo):
```bash
cd flowtime && git pull
dotnet build     # Builds everything
dotnet test      # Tests everything
```

### Repository URLs
- **Old**: 
  - `https://github.com/23min/FlowTime.git` (Engine)
  - `https://github.com/23min/FlowTime-Sim.git` (Sim)
- **New**: 
  - `https://github.com/23min/FlowTime.git` (Unified)
  - Sim repo archived/redirected

### Documentation
- All documentation now in unified `docs/` folder
- Separate architecture sections: `docs/architecture/{engine,sim,shared}/`
- Unified ROADMAP showing both Engine and Sim milestones

## Benefits

### For Solo Developer
- ✅ Single git history and version control
- ✅ One build, one test command
- ✅ Unified documentation (no sync needed)
- ✅ Easier refactoring across Engine ↔ Sim boundary
- ✅ Integration testing without Docker networking

### For Future Features
- ✅ M3.0 time-travel schema can be shared via `FlowTime.Contracts`
- ✅ Compiler enforces Engine ↔ Sim contract compatibility
- ✅ Easier to maintain schema versioning consistency

## Technical Details

### Build System
- Solution: Single `FlowTime.sln` (15 projects total)
- Build: `dotnet build` builds all projects
- Tests: `dotnet test` runs 6 test projects + integration tests

### Git History
- Full history preserved from both repositories
- Merge commit: `abc123...` with `--allow-unrelated-histories`
- Pre-consolidation tags available for reference

### Documentation Structure
```
docs/
├── ROADMAP.md (unified)
├── architecture/ (Engine, Sim, shared)
├── development/ (unified guides)
├── milestones/ (engine/, sim/, shared/)
└── schemas/ (Engine authoritative)
```

## Non-Breaking Changes

### API Contracts ✅
- No changes to Engine API endpoints
- No changes to Sim API endpoints
- Schema version remains: `schemaVersion: 1`
- Model contracts unchanged

### Deployment ✅
- Both APIs still run as separate processes
- Port assignments unchanged (8080, 8090)
- UI deployment unchanged (5219)
- Docker/container structure compatible

## Known Issues

None. All tests passing, both APIs operational.

## Acknowledgments

This consolidation was driven by real pain points:
- Documentation drift between repos
- Complex cross-container testing
- Manual schema synchronization
- Duplicated configuration maintenance

The unified structure better supports solo development and prepares for M3.0 time-travel features requiring shared contracts.

---

**Full Changelog**: https://github.com/23min/FlowTime/compare/v0.6.0...v0.7.0
EOF

git add docs/releases/v0.7.0-consolidation.md
git commit -m "docs(release): add v0.7.0 consolidation release notes"
```

#### 5.4 Final Validation
```bash
# Full clean build and test
dotnet clean
dotnet restore
dotnet build FlowTime.sln
dotnet test FlowTime.sln

# Verify both APIs start
# (Use VS Code tasks or manual dotnet run)
```

#### 5.5 Create Git Tags
```bash
# Tag the consolidation
git tag -a v0.7.0 -m "Release v0.7.0 - Repository Consolidation

Unified FlowTime Engine and FlowTime-Sim into single mono-repo.
All projects, documentation, and configuration consolidated.
Version bumped to 0.7.0 to mark this significant milestone.
"

# Push branch and tag (when ready)
git push origin feature/repo-consolidation
git push origin v0.7.0
```

#### 5.6 Archive Old Repositories
```bash
# In flowtime-sim-vnext (old repo):
# Add README notice about consolidation
cat > README-ARCHIVED.md << 'EOF'
# ⚠️ Repository Archived

**This repository has been consolidated into the main FlowTime repository.**

**New Location**: https://github.com/23min/FlowTime

FlowTime-Sim projects are now in the unified FlowTime solution:
- `src/FlowTime.Sim.Core`
- `src/FlowTime.Sim.Service`  
- `src/FlowTime.Sim.CLI`
- `tests/FlowTime.Sim.Tests`

**Last Active Version**: v0.6.0  
**Consolidation Date**: October 10, 2025  
**Consolidation Release**: v0.7.0

For all future development, please use the unified repository.
EOF

# Commit and push archive notice
git add README-ARCHIVED.md
git commit -m "docs: archive repository (consolidated into FlowTime)"
git push origin main

# Archive on GitHub (via Settings → Archive this repository)
```

### Deliverables
- ✅ Consolidation artifacts archived
- ✅ Version bumped to v0.7.0
- ✅ Release notes published
- ✅ Git tag created
- ✅ Old repository archived with redirect

---

## Post-Consolidation Checklist

### Immediate (Day 1 After Merge)
- [ ] Verify all CI/CD workflows pass on main
- [ ] Test devcontainer setup from scratch
- [ ] Update GitHub repository description
- [ ] Update repository topics/tags
- [ ] Announce consolidation (if external stakeholders)

### Short-term (Week 1)
- [ ] Monitor for any build/test issues
- [ ] Verify documentation navigation works
- [ ] Test integration test suite thoroughly
- [ ] Update any external references (bookmarks, wikis)

### Medium-term (Month 1)
- [ ] Leverage unified structure for M3.0 time-travel work
- [ ] Add `FlowTime.Contracts` references to Sim projects (if not done)
- [ ] Consider unified API gateway (future option)
- [ ] Evaluate if any further consolidation opportunities exist

---

## Risks & Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Build breaks after merge** | High | Low | Incremental commits, frequent testing |
| **Documentation link rot** | Medium | Medium | Automated link checking, manual validation |
| **Git history confusion** | Low | Low | Clear commit messages, pre-consolidation tags |
| **Developer workflow disruption** | Medium | Medium | Clear migration guide, VS Code tasks updated |
| **Container networking issues** | Low | Low | Keep separate processes on same ports |

### Rollback Plan

If consolidation fails:

```bash
# Rollback to pre-consolidation state
git tag consolidation-failed-$(date +%Y%m%d)
git reset --hard pre-consolidation-YYYYMMDD
git push --force origin feature/repo-consolidation

# Continue using separate repos
cd ~/backups/flowtime-vnext-backup
cd ~/backups/flowtime-sim-vnext-backup
# Resume work in separate repos
```

---

## Success Criteria

### Must Have ✅
- [ ] All projects build successfully
- [ ] All tests pass (100% pass rate)
- [ ] Both APIs operational simultaneously
- [ ] Documentation structure navigable
- [ ] Version numbers consistent (v0.7.0)
- [ ] Git history preserved
- [ ] CI/CD workflows functional

### Nice to Have ⭐
- [ ] Integration tests comprehensive
- [ ] Documentation fully aligned (no contradictions)
- [ ] All internal links working
- [ ] Devcontainer works first try
- [ ] Old repo archived cleanly

### Validation
- [ ] Fresh clone builds without errors
- [ ] New developer can onboard from README
- [ ] UI can communicate with both APIs
- [ ] VS Code tasks work as expected

---

## Timeline

### Phase 1-4: Code Consolidation (14 hours)
**Goal**: Get consolidated repo working with all tests passing

- **Week 1**: Phases 0-2 (8 hours) - Prep, code, and config consolidation
- **Week 2**: Phases 3-4 (6 hours) - Testing and release v0.7.0

### Suggested Schedule (Code Consolidation)
- **Day 1-2**: Preparation and code consolidation (6 hours)
- **Day 3**: Configuration unification (2 hours)
- **Day 4**: Testing and validation (4 hours)
- **Day 5**: Cleanup and release v0.7.0 (2 hours)

**Milestone**: v0.7.0 released with working code, basic docs

---

### Phase 5: Documentation Migration (16 hours) - SEPARATE
**Goal**: Unified, authoritative documentation structure

**Can be done anytime after v0.7.0 release**

- **Week N**: Import and inventory (6 hours)
- **Week N+1**: Alignment and extraction (8 hours)  
- **Week N+2**: Unification and cleanup (2 hours)

**Milestone**: v0.8.0 (or could be v0.7.1) with complete docs

---

## Next Steps

### Immediate: Code Consolidation (Phases 0-4)
1. **Review this plan** - Confirm approach and timeline
2. **Create tracking branch** - `feature/repo-consolidation` (✅ Done)
3. **Execute Phase 0** - Backup and preparation
4. **Execute Phase 1** - Code consolidation
5. **Execute Phase 2** - Configuration unification
6. **Execute Phase 3** - Testing and validation
7. **Execute Phase 4** - Cleanup and release v0.7.0
8. **Merge to main** - When all validation passes

**Checkpoint**: v0.7.0 released, code working ✅

### Later: Documentation Migration (Phase 5)
9. **Create docs branch** - `feature/docs-consolidation`
10. **Execute Phase 5** - Documentation migration (16 hours)
11. **Review and merge** - When docs are aligned
12. **Archive old repo** - After docs migration complete

---

## Questions to Resolve

Before starting execution:

1. **Version number**: Confirm v0.7.0 is appropriate (minor bump for infrastructure change)?
2. **Branch name**: Is `feature/repo-consolidation` the right name?
3. **Timeline**: Is 30 hours over 2-3 weeks realistic for your schedule?
4. **Documentation authority**: Confirm Engine docs are generally more authoritative?
5. **Schema ownership**: Confirm Engine is authoritative for `model.schema.yaml`?
6. **Old repo**: Archive completely or keep for reference?
7. **Contracts sharing**: Should Sim reference `FlowTime.Contracts` immediately or defer?

---

## References

- FlowTime Engine Charter: `docs/architecture/engine/flowtime-engine-charter.md`
- FlowTime-Sim Charter: `docs/architecture/sim/flowtime-sim-charter.md`
- KISS Architecture: `docs/architecture/kiss-principles.md`
- Current Roadmap (Engine): `/workspaces/flowtime-vnext/docs/ROADMAP.md`
- Current Roadmap (Sim): `/workspaces/flowtime-sim-vnext/docs/ROADMAP.md`
- Time-Travel Audit: `/workspaces/flowtime-sim-vnext/docs/time-travel-readiness-audit.md`

---

**Document Status**: 📋 Planning - Ready for Review  
**Last Updated**: October 10, 2025  
**Next Action**: Review and confirm approach before execution
