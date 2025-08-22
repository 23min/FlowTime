# Devcontainer

This repo ships a minimal devcontainer for M0–M1 to keep startup fast and diffs clean. Optional tooling (Node, Azure CLI, Azurite) will be added per milestone.

## What’s included (base)
- .NET 9 SDK (via devcontainers/dotnet image)
- PowerShell 7 feature
- VS Code extensions: C# Dev Kit, C#, EditorConfig, GitLens, GitHub Actions, Git Graph, YAML

## Use
- Open in VS Code and choose "Reopen in Container" (or start a Codespace).
- Post-create runs `.devcontainer/init.ps1` to restore the solution.

## Next milestones
When a milestone needs extra tooling, we’ll add flags and a Dockerfile to enable:
- Node (elkjs, Monaco)
- Azure CLI + Bicep + azd
- Azurite (Blob/Queue/Table emulator)

We’ll mirror additions in CI so devcontainer and Actions stay in sync.
