# AI-Assisted Development Framework

A portable, self-contained framework for AI-assisted software development using personas, skills, and structured workflows.

## Purpose

This framework defines:
- **Agents**: Role-based personas that guide AI behavior
- **Skills**: Reusable workflows for common development tasks
- **Instructions**: Global guardrails that apply to every session

The framework is generic and can be adapted to any project using milestone-driven development.

---

## Quick Start

### Starting a Session

Always begin with the **session-start** skill to establish context and choose your working mode.

**Trigger phrases:**
- "Start a session"
- "Begin work"
- "Let's start"
- "Initialize context"

The session start will ask you to choose:
1. **Agent role** - Which persona should guide the work?
2. **Task type** - What are you trying to accomplish?
3. **Context** - What epic, milestone, or feature?

### Common Workflows

**Planning a new epic:**
```
session-start → architect (epic-refine) → documenter (milestone-draft)
```

**Implementing a milestone:**
```
session-start → implementer (milestone-start) → tester (red-green-refactor) → documenter (milestone-wrap)
```

**Fixing a bug:**
```
session-start → implementer → red-green-refactor → code-review
```

**Preparing a release:**
```
session-start → documenter (milestone-wrap + release)
```

---

## Structure

### `/agents/`
Role-based personas that define focus areas and typical skill usage:
- **architect**: Design decisions, system boundaries, epic planning
- **implementer**: Coding with minimal risk and clear intent
- **tester**: Test planning, TDD workflow, regression safety
- **documenter**: Documentation quality, consistency, release notes
- **deployer**: Infrastructure, packaging, release execution

### `/skills/`
Reusable workflows organized by lifecycle stage:

**Epic Lifecycle:**
- `epic-refine`: Clarify scope and decisions before milestone planning
- `epic-start`: Initialize context when beginning epic work
- `epic-wrap`: Close out completed epic and sync documentation

**Milestone Lifecycle:**
- `milestone-draft`: Create milestone specifications
- `milestone-start`: Begin implementation work
- `milestone-wrap`: Complete milestone and update docs

**Development Workflows:**
- `session-start`: Initialize working session and choose role
- `red-green-refactor`: TDD cycle (write failing test → make it pass → improve)
- `code-review`: Review changes for correctness and regressions
- `branching`: Apply milestone-driven branching strategy
- `ui-debug`: Diagnose UI issues with deterministic tests

**Product & Release:**
- `roadmap`: Maintain epic lifecycle and planning documents
- `release`: Execute release ceremony
- `deployment`: Follow deployment procedures
- `gap-triage`: Handle discovered gaps during work

### `/instructions/`
Global guardrails that apply regardless of agent or skill:
- **ALWAYS_DO.md**: Core rules for every session

---

## When to Use Which Agent

### Starting Work
**Use session-start first** - it will help you choose the right agent and task.

### By Task Type
- **"I need to plan a new feature/epic"** → architect + epic-refine
- **"Let's implement milestone X"** → implementer + milestone-start
- **"Write tests for..."** → tester + red-green-refactor
- **"Update the documentation"** → documenter
- **"Prepare a release"** → documenter + release
- **"Debug the UI"** → implementer + ui-debug
- **"Deploy to production"** → deployer + deployment

### By Question Type
- **"How should we approach..."** → architect
- **"How do I test..."** → tester
- **"Where should this be documented?"** → documenter
- **"Should this be in scope?"** → architect + gap-triage

---

## Skill Trigger Phrases

Each skill has specific trigger phrases. Here's a quick reference:

### Epic Work
- **epic-refine**: "refine the epic", "clarify epic scope", "what are we building?"
- **epic-start**: "start epic X", "begin epic work", "initialize epic"
- **epic-wrap**: "close the epic", "epic is complete", "archive milestones"

### Milestone Work
- **milestone-draft**: "create milestone spec", "draft milestone", "plan milestone"
- **milestone-start**: "start milestone", "begin M-XX.XX", "implement milestone"
- **milestone-wrap**: "complete milestone", "close milestone", "wrap up"

### Development
- **session-start**: "start session", "begin work", "initialize"
- **red-green-refactor**: "write tests", "TDD", "test-driven", "implement feature"
- **code-review**: "review changes", "check the code", "look for issues"
- **branching**: "create branch", "branch strategy", "git workflow"

### Documentation & Release
- **roadmap**: "update roadmap", "plan epics", "epic lifecycle"
- **release**: "release", "tag version", "publish"
- **gap-triage**: "found a gap", "missing requirement", "unexpected issue"

---

## Core Principles

### 1. Milestone-Driven Development
Work is organized into:
- **Epics**: Coherent architectural or product themes
- **Milestones**: Concrete, scoped deliverables within epics
- **Features**: Individual changes within milestones

### 2. Test-Driven Development (TDD)
Always follow RED → GREEN → REFACTOR:
1. **RED**: Write failing tests first
2. **GREEN**: Implement minimum code to pass
3. **REFACTOR**: Improve structure with tests passing

### 3. Documentation as Code
- Milestone specs are authoritative and living documents
- Tracking documents record progress during implementation
- Release notes capture what shipped and why

### 4. No Time Estimates
Never include effort estimates, timelines, or target dates. Focus on:
- Clear requirements and acceptance criteria
- Dependency sequences
- Scope boundaries (in/out of scope)

### 5. Incremental Progress
- Keep changes small and focused
- Build and test before handoff
- Document decisions as you go

---

## Customization

This framework is designed to be portable. To adapt it to your project:

1. **Update agent responsibilities** to match your team structure
2. **Modify skills** to match your workflows and tooling
3. **Adjust ALWAYS_DO** for project-specific conventions
4. **Keep the structure** - the agent/skill separation is key

### Project-Specific Hooks
Look for these markers in skills that may need customization:
- File paths and directory structures
- Build and test commands
- Documentation locations
- Branch naming conventions
- Version number formats

---

## Anti-Patterns

### ❌ DON'T
- Skip session-start and jump into coding
- Include time or effort estimates
- Write code without tests
- Merge to main without review
- Skip documentation updates
- Use vague requirements ("make it better")

### ✅ DO
- Start every session with session-start
- Write tests first (RED → GREEN → REFACTOR)
- Keep milestone specs stable
- Update tracking docs frequently
- Make small, focused changes
- Document decisions inline

---

## Getting Help

If you're unsure which agent or skill to use:
1. Run **session-start** - it will guide you
2. Review the "When to Use" sections above
3. Check individual skill files for detailed guidance
4. Ask: "What should I do to [accomplish goal]?"

The framework is designed to guide you through context gathering and decision-making.
