# Devcontainer Maintenance

This guide replaces generic container-cleanup advice with FlowTime-specific rules.

## What Must Persist

In this repo, the durable development data lives inside the workspace:

- `/workspaces/flowtime-vnext/data`
- `/workspaces/flowtime-vnext/templates`
- `/workspaces/flowtime-vnext/templates-draft`
- any repo-local config such as `.flow-sim.yaml`

Current development defaults point there already:

- Engine API artifacts: `/workspaces/flowtime-vnext/data/runs`
- Sim service data root: `/workspaces/flowtime-vnext/data`
- Sim storage root: `/workspaces/flowtime-vnext/data/storage`

That means a normal devcontainer rebuild keeps the important FlowTime data because the workspace is bind-mounted from the host.

## What Exists Outside The Repo

The container may still contain files outside the workspace, but they are not FlowTime runtime truth by default:

- `/home/vscode/.claude` and `/home/vscode/.cache/claude-cli-nodejs` are assistant/tooling state
- `/tmp/claude-*` and similar paths are assistant scratch space
- `/tmp/flowtime_*` and `/tmp/flowtime-*` are usually test or command scratch output
- `~/.flow-sim.yaml` is optional user-level Sim CLI config if you create it intentionally
- `/var/lib/flowtime` and `/var/lib/flowtime-sim` are production-style examples, not active devcontainer defaults

Loose files under `/tmp` and `/var/tmp` should be treated as disposable unless you deliberately put something there.

## Safe Checks Before Cleaning

Use these first to see where space is going:

```bash
df -h /
du -sh /workspaces/flowtime-vnext/data /tmp /home/vscode/.nuget/packages /home/vscode/.cache 2>/dev/null
find /tmp -maxdepth 1 \( -name 'flowtime_*' -o -name 'flowtime-*' \) -printf '%y %p\n'
```

## Safe FlowTime Cleanup

### 1. Clean repo build outputs

```bash
dotnet clean FlowTime.sln
find /workspaces/flowtime-vnext \
  \( -path '*/bin' -o -path '*/obj' -o -path '*/TestResults' \) \
  -type d -prune -exec rm -rf {} +
```

This removes rebuildable .NET outputs only.

### 2. Clean package caches when space is tight

```bash
dotnet nuget locals all --clear
npm cache clean --force
pnpm store prune
```

These are safe to recreate, but do not run them in the middle of an install or restore.

### 3. Remove FlowTime temp scratch data

```bash
find /tmp -maxdepth 1 \( -name 'flowtime_*' -o -name 'flowtime-*' \) -exec rm -rf {} +
find /var/tmp -maxdepth 1 \( -name 'flowtime_*' -o -name 'flowtime-*' \) -exec rm -rf {} +
```

This targets temp directories left by tests and one-off commands without wiping unrelated tooling state.

### 4. Remove repo-local UI build caches if needed

```bash
rm -rf /workspaces/flowtime-vnext/ui/.svelte-kit
rm -rf /workspaces/flowtime-vnext/ui/dist
rm -rf /workspaces/flowtime-vnext/tools/mcp-server/dist
```

Only remove these when you want the next build to recreate them.

## Do Not Clean Blindly

Avoid these patterns in this container:

- `rm -rf /tmp/* ~/.cache/* /var/tmp/*`
- deleting `/home/vscode/.claude` during an active assistant session
- deleting `/home/vscode/.cache/claude-cli-nodejs` unless you explicitly want to clear tooling cache
- assuming `/var/lib/flowtime*` contains needed dev data in this container
- killing processes by raw port number or starting with `kill -9`

For FlowTime ports, verify the process first and kill only the relevant `dotnet` process.

## Rebuild And Persistence Rules

Use this rule of thumb:

1. Keep FlowTime artifacts inside the workspace unless there is a deliberate reason not to.
2. If you override `FLOWTIME_DATA_DIR` or `FLOWTIME_SIM_DATA_DIR` to an external path, make sure that path is bind-mounted or backed by a named volume.
3. Treat `/tmp` and `/var/tmp` as disposable working space only.
4. Treat home-directory tool caches as optional convenience state, not project truth.

## Host-Level Docker Cleanup

If Docker Desktop or the host VM is full, clean it from the host side rather than deleting random container paths:

```bash
docker system df
docker builder prune
docker container prune
```

Those commands are about host Docker storage, not FlowTime repo data.

## Practical One-Liner

If the container is low on disk and you want a conservative cleanup pass:

```bash
dotnet clean FlowTime.sln
dotnet nuget locals all --clear
find /workspaces/flowtime-vnext \
  \( -path '*/bin' -o -path '*/obj' -o -path '*/TestResults' \) \
  -type d -prune -exec rm -rf {} +
find /tmp -maxdepth 1 \( -name 'flowtime_*' -o -name 'flowtime-*' \) -exec rm -rf {} +
```

That reclaims the usual FlowTime-only disk usage without touching workspace data or assistant state.