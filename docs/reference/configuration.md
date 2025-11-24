# Configuration Reference

FlowTime services share a small set of configuration knobs with a consistent precedence model.

## Configuration Precedence

1. **Command-line arguments** (highest priority)
2. **Environment variables**
3. **Configuration files** (`appsettings.json`, `appsettings.Development.json`)
4. **Default values** (lowest priority)

## Service Port Configuration

| Service      | Default Port | Development | Production | Configuration Method |
|--------------|--------------|-------------|------------|---------------------|
| FlowTime API | 8080 | 8080 | 8080 | `--urls` flag or `ASPNETCORE_URLS` env var |
| FlowTime UI  | 5219 | 5219 | 3000 (recommended) | `--urls` flag or `ASPNETCORE_URLS` env var |
| FlowTime-Sim | 8081 | 8081 | 8081 | `--urls` flag or `ASPNETCORE_URLS` env var |

**See [development-setup.md](../development/development-setup.md) for local development and [deployment.md](../guides/deployment.md) for production deployment.**

## CLI Output Directory

### **1. Configuration Precedence**
```
1. FLOWTIME_DATA_DIR environment variable       [Highest Priority]
2. ArtifactsDirectory in appsettings*.json      [API only]
3. Solution-root /data or CWD/data              [Default]
```

### **2. Environment Variable**
- **Variable Name**: `FLOWTIME_DATA_DIR`
- **Scope**: Engine CLI, Sim CLI (via orchestrated runs), API, tests
- **Usage**: Absolute or relative path; directories are created if missing.

### **3. Configuration Files (API)**
- `ArtifactsDirectory` in `appsettings.json` or `appsettings.Development.json`
- Relative paths are resolved against the solution root; absolute paths are used as-is.

### **4. CLI Flags**
- Engine CLI: `--out <dir>` overrides all other sources for that invocation.
- Sim CLI orchestration inherits `FLOWTIME_DATA_DIR` for run output unless `--out` is provided by the engine CLI that performs the run.

### **5. Directory Structure**
```
FLOWTIME_DATA_DIR/
└── run_TIMESTAMP_ID/
    ├── manifest.json
    ├── run.json
    ├── spec.yaml
    ├── model/
    │   ├── model.yaml
    │   ├── metadata.json
    │   └── telemetry/
    │       ├── manifest.json
    │       └── *.csv
    ├── aggregates/             # optional derived analytics outputs
    └── series/
        ├── index.json
        └── *.csv
```

## **✅ Usage Examples**

### **Environment Variable (Recommended)**
```bash
# Set globally
export FLOWTIME_DATA_DIR="/var/lib/flowtime"

# CLI usage
dotnet run --project src/FlowTime.Cli -- run model.yaml

# API usage  
dotnet run --project src/FlowTime.API
```

### **CLI --out Parameter Override**
```bash
# Override for specific run (highest precedence)
dotnet run --project src/FlowTime.Cli -- run model.yaml --out /custom/output
```

### **Docker Compose**
```yaml
services:
  flowtime-api:
    environment:
      - FLOWTIME_DATA_DIR=/app/data
    volumes:
      - flowtime-data:/app/data
```

### **Development Environment (API)**
`appsettings.Development.json` ships with `ArtifactsDirectory` pointing to `/workspaces/flowtime-vnext/data` inside the dev container. You can override with `FLOWTIME_DATA_DIR` locally.

## **✅ Configuration Methods**

### **Method 1: Environment Variable (Production)**
```bash
export FLOWTIME_DATA_DIR="/var/lib/flowtime"
```

### **Method 2: Configuration File (API only)**
```json
{
  "ArtifactsDirectory": "/path/to/data"
}
```

### **Method 3: CLI Parameter (Engine CLI only)**
```bash
dotnet run --project src/FlowTime.Cli -- run model.yaml --out /custom/path
```

## **✅ Benefits**

1. **Consistent Behavior**: CLI and API use identical precedence logic
2. **Flexible Deployment**: Environment variable works in any environment
3. **Development Friendly**: Defaults to `./data` for local development
4. **Container Ready**: Easy to configure with single environment variable
5. **Override Capability**: CLI supports per-run output directory override

## **✅ Testing Coverage**

Automated tests cover configuration precedence and directory handling:
- `tests/FlowTime.Api.Tests/ConfigurationTests.cs` exercises env var vs config vs default paths and whitespace handling.
- `tests/FlowTime.Cli.Tests/OutputDirectoryConfigurationTests.cs` and `TelemetryRunCommandTests.cs` cover env var override, relative/absolute paths, and CLI `--out` priority.
- Integration-style tests set `FLOWTIME_DATA_DIR` per-test to isolate artifacts (see `EnvVarScope` usage in CLI tests).

## **✅ Platform Integration**

### **Local Development**
```bash
# Use default ./data directory
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml
```

### **Production Environment**
```bash
# Configure data directory
export FLOWTIME_DATA_DIR="/var/lib/flowtime"
systemctl start flowtime-api
```

### **Development Container**
`FLOWTIME_DATA_DIR` is set to `/workspaces/flowtime-vnext/data` via `appsettings.Development.json`; override as needed with an environment variable.

## **✅ Configuration Validation**

FlowTime automatically:
- Creates data directories if they don't exist
- Validates write permissions
- Falls back gracefully through precedence chain
- Handles both relative and absolute paths
- Normalizes path separators across platforms

## **✅ Best Practices**

1. **Use environment variables** for production deployments
2. **Set FLOWTIME_DATA_DIR** in container orchestration
3. **Use --out parameter** for one-off CLI runs with custom output
4. **Configure appsettings** for development environments
5. **Ensure write permissions** on target directories
6. **Use absolute paths** in production for clarity

## Templates Directory (orchestrated runs)
- Environment variable: `FLOWTIME_TEMPLATES_DIR`
- Used by engine CLI orchestrated runs (`flowtime run --template-id ...`) and Sim CLI defaults.
- If unset, defaults to `<repo>/templates` or `AppContext.BaseDirectory/templates` in packaged builds.
