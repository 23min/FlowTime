# Devcontainer

This repo ships a minimal devcontainer for M-0–M-1 to keep startup fast and diffs clean. Optional tooling (Node, Azure CLI, Azurite) will be added per milestone.

# Dev Container Setup

This repository includes a dev container configuration optimized for FlowTime development with minimal overhead.

## What's Included

- .NET 9 SDK (via devcontainers/dotnet image)
- PowerShell 7 feature
- VS Code extensions: C# Dev Kit, C#, EditorConfig, GitLens, GitHub Actions, Git Graph, YAML, REST Client

## API-First Development with Shared Docker Network

We support cross-repository development where other services (e.g., flowtime-sim) can communicate with FlowTime over a Docker network.

### One-Time Setup: Create Shared Network

```bash
docker network create flowtime-dev
```

### FlowTime Dev Container Configuration

The dev container automatically:
- Joins the `flowtime-dev` network with stable name `flowtime-api`
- Forwards port 8080 to the host for convenience
- Does not auto-start the API (start manually via F5, VS Code tasks, or command line)

This is configured in `.devcontainer/devcontainer.json`:
```json
{
  "runArgs": ["--network=flowtime-dev", "--name", "flowtime-api"],
  "forwardPorts": [8080]
}
```

### Cross-Container Communication

From other containers on the `flowtime-dev` network, access the API using the service name:

```bash
# Health check
curl -s http://flowtime-api:8080/healthz

# API calls
export FLOWTIME_URL=http://flowtime-api:8080
curl -s -X POST "$FLOWTIME_URL/run" 
  -H "Content-Type: application/yaml" 
  --data-binary @model.yaml
```

### Host Access

When port forwarding is enabled:
```bash
curl -s http://localhost:8080/healthz
```

## Port Configuration

**For detailed port configuration, see [development-setup.md](development-setup.md)**.

Default ports:
- **FlowTime API**: 8080
- **FlowTime UI**: 5219 (development)
- **FlowTime-Sim**: 8081 (separate repository)

## Troubleshooting

### Network Issues
```bash
# Check network membership
docker network inspect flowtime-dev | jq '.[0].Containers | keys'

# Test name resolution (from sibling container)
getent hosts flowtime-api || ping -c1 flowtime-api

# Check API listening (inside FlowTime container)
ss -lntp | grep 8080
```

### Debugging Across Multiple Dev Containers

1. Open FlowTime in VS Code
2. Open other repository (e.g., flowtime-sim) in separate VS Code window
3. Start FlowTime API using F5 or launch configuration
4. Both containers communicate via `flowtime-dev` network

### Launch Configuration

Example VS Code launch configuration for API:
```json
{
  "name": "FlowTime.API",
  "type": "coreclr",
  "request": "launch",
  "program": "dotnet",
  "args": ["run", "--project", "src/FlowTime.API", "--urls", "http://0.0.0.0:8080"]
}
```

## Use
- Open in VS Code and choose "Reopen in Container" (or start a Codespace).
- Post-create runs `.devcontainer/init.ps1` to restore the solution.

## Next milestones
When a milestone needs extra tooling, we’ll add flags and a Dockerfile to enable:
- Node (elkjs, Monaco)
- Azure CLI + Bicep + azd
- Azurite (Blob/Queue/Table emulator)

We’ll mirror additions in CI so devcontainer and Actions stay in sync.
