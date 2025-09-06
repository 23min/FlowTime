# Development Setup: FlowTime Services

## **Service Port Assignments**

To avoid port conflicts and ensure stable development, each service has a dedicated port:

| Service | Port | Purpose |
|---------|------|---------|
| **FlowTime API** | `8080` | Main FlowTime engine API |
| **FlowTime UI** | `5219` | Blazor WebAssembly UI |
| **FlowTime.Sim API** | `8081` | Synthetic data simulation API (separate repo) |

## **Starting Services**

### FlowTime API (Main Engine)
```bash
cd /workspaces/flowtime-vnext
dotnet run --project src/FlowTime.API --urls http://localhost:8080
```

### FlowTime UI (Blazor WebAssembly)
```bash
cd /workspaces/flowtime-vnext
dotnet run --project ui/FlowTime.UI --urls http://localhost:5219
```

### FlowTime CLI (Example)
```bash
cd /workspaces/flowtime-vnext
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --verbose
```

## **Development Configuration**

### Configuration Precedence
1. **Command-line arguments** (highest priority)
2. **Environment variables**
3. **Configuration files** (`appsettings.json`, `appsettings.Development.json`)
4. **Defaults**

### FlowTime API Configuration

**Environment Variables**:
```bash
export ASPNETCORE_URLS="http://localhost:8080"
export ASPNETCORE_ENVIRONMENT="Development"
```

**Configuration File** (`src/FlowTime.API/appsettings.Development.json`):
```json
{
  "Urls": "http://localhost:8080",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### FlowTime UI Configuration

**Port Configuration** (`ui/FlowTime.UI/Properties/launchSettings.json`):
```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:5219",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**API Clients** (`ui/FlowTime.UI/Program.cs`):
- **FlowTime API**: `http://localhost:8080` (for engine operations)
- **FlowTime.Sim API**: `http://localhost:8081` (for simulation operations)

### Development File Locations

- **FlowTime data**: `/workspaces/flowtime-vnext/data/`
  - **Runs**: `/workspaces/flowtime-vnext/data/run_*/`
  - **Artifacts**: JSON files and series data
- **Examples**: `/workspaces/flowtime-vnext/examples/`
- **Output**: `/workspaces/flowtime-vnext/out/` (configurable via `--out` parameter)

### CLI Output Configuration

**Environment Variable**:
```bash
export FLOWTIME_OUTPUT_DIR="/custom/output/path"
```

**Command Line**:
```bash
dotnet run --project src/FlowTime.Cli -- run model.yaml --out /custom/output/path
```

## **VS Code Integration**

### Launch Configurations

The repository includes VS Code launch configurations in `.vscode/launch.json`:

- **".NET Launch FlowTime.API"** - Starts API on http://localhost:8080
- **".NET Launch FlowTime.UI"** - Starts UI on http://localhost:5219  
- **".NET Launch FlowTime.Cli (hello)"** - Runs CLI with hello example

### Tasks

Available tasks in `.vscode/tasks.json`:
- **build** - `dotnet build`
- **test** - `dotnet test --nologo`
- **run: hello** - Runs CLI with hello example

## **Development Workflow**

### 1. Start the API
```bash
dotnet run --project src/FlowTime.API --urls http://localhost:8080
```

### 2. Start the UI (Optional)
```bash
dotnet run --project ui/FlowTime.UI --urls http://localhost:5219
```

### 3. Run CLI Commands
```bash
# Run a simulation
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --verbose

# View results
ls out/hello/
```

### 4. Access Services
- **API**: http://localhost:8080
- **UI**: http://localhost:5219
- **API Health Check**: http://localhost:8080/healthz

## **Troubleshooting**

### Port Conflicts
- Ensure only one service runs per port
- Use `lsof -i :8080` and `lsof -i :5219` to check port usage
- Kill conflicting processes: `lsof -ti:8080 | xargs kill -9`

### Build Issues
```bash
# Clean and rebuild
dotnet clean
dotnet build

# Run tests
dotnet test --nologo
```

### Service Health Checks
```bash
# FlowTime API
curl http://localhost:8080/healthz

# FlowTime UI (check if responding)
curl -I http://localhost:5219
```

### Data Directory Issues
- Check CLI logs for "Created artifacts at: {path}" messages
- Verify output directory permissions
- Use `--verbose` flag for detailed CLI output

### Configuration Debugging
```bash
# Check environment variables
env | grep ASPNETCORE
env | grep FLOWTIME

# Verify configuration files
cat src/FlowTime.API/appsettings.Development.json
cat ui/FlowTime.UI/Properties/launchSettings.json
```

## **Integration with FlowTime.Sim**

If you're also running FlowTime.Sim (separate repository), the UI can connect to both services:

### FlowTime.Sim Setup
```bash
# In separate terminal/repo
cd /workspaces/flowtime-sim-vnext
dotnet run --project src/FlowTime.Sim.Service --urls http://localhost:8081
```

### UI Feature Flag
The UI uses a feature flag to switch between FlowTime API and FlowTime.Sim API. Check `ui/FlowTime.UI/Services/FeatureFlags.cs` for current configuration.
