# Devcontainer

FlowTime-Sim includes a specialized dev container configuration optimized for synthetic data generation development with cross-repository integration support.

## What's Included

- .NET 9 SDK (via devcontainers/dotnet image)
- PowerShell 7 feature
- GitHub CLI for repository management
- VS Code extensions: C# Dev Kit, C#, EditorConfig, GitLens, GitHub Actions, Git Graph, YAML, REST Client, Copilot, Markdown Preview Enhanced

## Cross-Repository Development with Shared Docker Network

FlowTime-Sim is designed to work alongside the main FlowTime API for comprehensive development scenarios.

### One-Time Setup: Create Shared Network

```bash
docker network create flowtime-dev
```

### FlowTime-Sim Dev Container Configuration

The dev container automatically:
- Joins the `flowtime-dev` network for cross-container communication
- Sets `FLOWTIME_API_BASEURL=http://flowtime-api:8080` for integration testing
- Configures DNS resolution for optimal network performance
- Does not auto-start the service (start manually via F5, VS Code tasks, or command line)

This is configured in `.devcontainer/devcontainer.json`:
```json
{
  "runArgs": ["--network=flowtime-dev"],
  "containerEnv": {
    "FLOWTIME_API_BASEURL": "http://flowtime-api:8080"
  },
  "remoteEnv": {
    "NODE_OPTIONS": "--dns-result-order=ipv4first"
  }
}
```

### Integration with FlowTime API

When both FlowTime and FlowTime-Sim containers are running on the `flowtime-dev` network:

```bash
# Test connectivity to main FlowTime API
curl -s http://flowtime-api:8080/healthz

# FlowTime-Sim can integrate with FlowTime for end-to-end testing
export FLOWTIME_URL=http://flowtime-api:8080
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml --flowtime $FLOWTIME_URL
```

### Host Access

FlowTime-Sim Service runs on port 8081:
```bash
# Health check (from host)
curl -s http://localhost:8081/healthz

# From other containers on flowtime-dev network
curl -s http://flowtime-sim:8081/healthz
```

## Port Configuration

**For detailed port configuration, see [development-setup.md](development-setup.md)**.

Default ports:
- **FlowTime API**: 8080 (flowtime-vnext repository)
- **FlowTime.Sim API**: 8081 (this repository)
- **FlowTime UI**: 5219 (flowtime-vnext repository)

## Multi-Root Workspace Support

FlowTime-Sim includes a multi-root workspace configuration (`flowtime-sim.code-workspace`) for comprehensive development:

```bash
# Open the complete workspace
code flowtime-sim.code-workspace
```

This workspace includes:
- FlowTime-Sim source code
- Example configurations
- Testing scripts
- Documentation

## Development Workflow

### 1. Container Initialization

The dev container runs initialization automatically:
```powershell
# Automatic on container start
pwsh .devcontainer/init.ps1 --PostCreate
```

This script:
- Restores the .NET solution
- Validates the development environment
- Provides helpful next-step commands

### 2. Building and Testing

```bash
# Build the solution
dotnet build

# Run all tests (fast execution ~2s in devcontainer)
dotnet test

# Run specific test projects
dotnet test tests/FlowTime.Sim.Tests
```

### 3. Running Services

```bash
# Start FlowTime.Sim Service
dotnet run --project src/FlowTime.Sim.Service

# Run CLI simulations
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml --out data/runs
```

## Troubleshooting

### Network Issues

```bash
# Check network membership
docker network inspect flowtime-dev | jq '.[0].Containers | keys'

# Test FlowTime API connectivity (if running)
curl -s http://flowtime-api:8080/healthz || echo "FlowTime API not running"

# Check FlowTime-Sim Service listening
ss -lntp | grep 8081
```

### Multi-Repository Development

1. **Start FlowTime API** (in flowtime-vnext):
   ```bash
   # In flowtime-vnext container
   dotnet run --project src/FlowTime.API --urls http://0.0.0.0:8080
   ```

2. **Start FlowTime-Sim Service** (in this container):
   ```bash
   # In flowtime-sim-vnext container  
   dotnet run --project src/FlowTime.Sim.Service --urls http://0.0.0.0:8081
   ```

3. **Test integration**:
   ```bash
   # Test FlowTime-Sim CLI with FlowTime API
   dotnet run --project src/FlowTime.Sim.Cli -- \
     --model examples/m0.const.yaml \
     --flowtime http://flowtime-api:8080 \
     --out data/integration-test
   ```

### Launch Configuration

Example VS Code launch configuration for FlowTime.Sim Service:
```json
{
  "name": "FlowTime.Sim.Service",
  "type": "coreclr",
  "request": "launch",
  "program": "dotnet",
  "args": ["run", "--project", "src/FlowTime.Sim.Service", "--urls", "http://0.0.0.0:8081"],
  "env": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "FLOWTIME_SIM_DATA_DIR": "/workspaces/flowtime-sim-vnext/data"
  }
}
```

## Environment Variables

Key environment variables for development:

| Variable | Default | Purpose |
|----------|---------|---------|
| `FLOWTIME_API_BASEURL` | `http://flowtime-api:8080` | FlowTime API endpoint for integration |
| `FLOWTIME_SIM_DATA_DIR` | `./data` | Data directory for runs and catalogs |
| `ASPNETCORE_ENVIRONMENT` | `Development` | .NET environment configuration |

## Use

1. **Open in VS Code** and choose "Reopen in Container" (or start a Codespace)
2. **Post-create runs** `.devcontainer/init.ps1` to restore the solution
3. **Optional**: Open `flowtime-sim.code-workspace` for full context

## Performance Notes

- Container initialization: ~30-60 seconds (including .NET restore)
- Test suite execution: ~2 seconds typical
- Service startup: ~3-5 seconds
- Cross-container network latency: <1ms (local Docker)

## Next Milestones

Future enhancements planned:
- **Integration testing automation** between FlowTime and FlowTime-Sim
- **Performance profiling tools** for synthetic data generation
- **Debug visualization** for simulation events
- **Docker Compose** orchestration for full-stack development

The devcontainer configuration evolves with milestone requirements while maintaining fast startup and clean diffs.
