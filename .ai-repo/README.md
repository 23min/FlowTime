# Project-Specific AI Configuration

This directory holds AI framework extensions specific to **this repository**.
The shared framework lives in `.ai/` (submodule). This directory is repo-local.

## Structure

```
.ai-repo/
├── config/    ← structured repo-owned artifact layout config
├── skills/    ← project-specific skill checklists
├── rules/     ← project-specific conventions and constraints
└── README.md  ← you are here
```

## Config

The canonical repo-owned artifact layout lives in `.ai-repo/config/artifact-layout.json`.
It defines the effective roadmap path, epic spec filename, milestone spec path template,
tracking doc path template, completed epic archive path, and naming patterns for this repo.

Generated assistant surfaces should mirror the resolved values from this file; they should not be the source of truth for layout.

## Skills

Add a `.md` file in `skills/` with a checklist format (see `.ai/skills/` for examples).
Project skills are automatically picked up by `bash .ai/setup.sh` and distributed
to platform adapters (Copilot, Claude, Codex).

Examples of project-specific skills:
- `deploy-to-azure.md` — deployment runbook for this project
- `run-pipeline.md` — how to trigger the CI/CD pipeline
- `data-export.md` — how to export fixture data from production

## Rules

Add a `.md` file in `rules/` for project-specific conventions:
- `tech-stack.md` — "Use xUnit, NSubstitute, .NET 9"
- `naming.md` — "Services use I{Name} interface pattern"
- `testing.md` — "All tests use [Theory] for parameterized cases"

These are referenced by platform adapters so the AI reads them automatically.
