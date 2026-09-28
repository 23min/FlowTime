# Devcontainer

This repo ships a devcontainer for FlowTime development.

For repo-specific cleanup, disk-pressure recovery, and persistence rules, see [devcontainer-maintenance.md](./devcontainer-maintenance.md).

## What's Included

- .NET 9 SDK (via `mcr.microsoft.com/devcontainers/dotnet` image)
- GitHub CLI and Node 20 devcontainer features
- Claude Code CLI and Codex CLI (both installed in `.devcontainer/init.sh`; Codex via the pinned standalone installer in `.devcontainer/install-codex.sh`)
- tmux, for persistent sessions (e.g. running `dev left` / `dev right` from `ai-dotfiles` to open named panes)
- uv, Rust (stable + clippy), Go, and Roslynator CLI, for the `/wf-dead-code-audit` recipes
- aiwf (branch-tip pin during PoC)
- VS Code extensions: C# Dev Kit, C#, EditorConfig, GitHub Actions, Git Graph, YAML, REST Client, GitHub Copilot, Claude Code, Svelte

## Use

- Open in VS Code and choose "Reopen in Container" (or start a Codespace).
- `updateContentCommand` and `postCreateCommand` both run `.devcontainer/init.sh`, which restores the solution, installs the workload/tooling above, and (on post-create only) prints a "Ready" banner with next-step commands.
- Codex needs a one-time interactive login (`codex login`) after first create — its auth lives in a container-only, repo-scoped `~/.codex` (bind-mounted from a host-side `~/.codex-flowtime-vnext`, chmod 700), so it survives rebuilds but never mixes with a native host Codex install or another repo's.

## API-First Development with Shared Docker Network

We support cross-repository development where other services (e.g., flowtime-sim) can communicate with FlowTime over a Docker network.

### One-Time Setup: Create Shared Network

```bash
docker network create flowtime-dev
```

### FlowTime Dev Container Configuration

The dev container joins the `flowtime-dev` network (`runArgs: --network=flowtime-dev` in `.devcontainer/devcontainer.json`) and forwards its ports for host convenience. It does not auto-start the API — start manually via F5, VS Code tasks, or command line.

### Cross-Container Communication

From other containers on the `flowtime-dev` network, other services reach the Engine API via `FLOWTIME_API_BASEURL` (set in `containerEnv`, currently `http://flowtime-api:8081`).

```bash
curl -s http://flowtime-api:8081/healthz
```

### Host Access

Forwarded ports (see `forwardPorts` / `portsAttributes` in `devcontainer.json` for the current list): the Svelte UI (5173), the Engine and Sim APIs (8080/8081/8090/8091), and a Codex OAuth callback port (1455, used only during `codex login`).

```bash
curl -s http://localhost:8081/healthz
```

## Troubleshooting

### Network Issues
```bash
# Check network membership
docker network inspect flowtime-dev | jq '.[0].Containers | keys'

# Test name resolution (from sibling container)
getent hosts flowtime-api || ping -c1 flowtime-api

# Check API listening (inside FlowTime container)
ss -lntp | grep 8081
```

### Debugging Across Multiple Dev Containers

1. Open FlowTime in VS Code
2. Open other repository (e.g., flowtime-sim) in separate VS Code window
3. Start FlowTime API using F5 or launch configuration
4. Both containers communicate via `flowtime-dev` network

### Launch Configuration

Example VS Code launch configuration for the Engine API:
```json
{
  "name": "FlowTime.API",
  "type": "coreclr",
  "request": "launch",
  "program": "dotnet",
  "args": ["run", "--project", "src/FlowTime.API", "--urls", "http://0.0.0.0:8081"]
}
```

## `dev` / tmux compatibility

`.devcontainer/devcontainer.json` sets an explicit `--name`/`--hostname flowtime-vnext-dev` in `runArgs` (readable in Docker Desktop/Grafana; fails loudly on a stale-container name conflict instead of silently orphaning one), matching `deathcleaning`/`aiwf`/`yahoo-save`.

This has one consequence worth knowing: `ai-dotfiles`' `dev` script, run from a bare host terminal (`dev flowtime-vnext`), discovers containers by matching `^vsc-<repo>-` — VS Code's *default* auto-generated name. Since this container is explicitly named `flowtime-vnext-dev` instead, that host-invoked lookup won't find it and silently falls back to a plain tmux session on the host, cd'd into the repo directory (not inside the container).

This does **not** affect `dev left` / `dev right` run from a terminal already inside the container — that path (`dev`'s "already inside a container" branch) just runs `tmux new -A -D -s <name>` locally and never looks at the container's name at all. It only needs tmux (installed above) and the `dev` binary itself present in-container. That binary isn't wired in yet — `ai-dotfiles` is mid-flight on reworking its own integration story, so this repo isn't calling its `install.sh` until that lands; for now, use `docker exec -it flowtime-vnext-dev bash` (or VS Code's own integrated terminal, which already attaches you into the container) and run tmux directly.
