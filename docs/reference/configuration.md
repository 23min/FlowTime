# Configuration Reference

FlowTime services support configuration through multiple methods with clear precedence rules.

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
2. ArtifactsDirectory in appsettings.json      [Configuration File]
3. ./data                                       [Default]
```

### **2. Environment Variable**
- **Variable Name**: `FLOWTIME_DATA_DIR`
- **Scope**: Both CLI and API respect this variable
- **Usage**: Set to any absolute or relative path

### **3. Configuration Files**
- **API**: Uses `ArtifactsDirectory` field in `appsettings.json`
- **CLI**: Uses environment variable or defaults to `./data`

### **4. Directory Structure**
```
FLOWTIME_DATA_DIR/
└── run_TIMESTAMP_ID/
    ├── manifest.json
    ├── run.json
    ├── spec.yaml
    ├── gold/
    │   └── *.csv
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

### **Development Environment**
```json
// appsettings.Development.json
{
  "ArtifactsDirectory": "/workspaces/flowtime-vnext/data"
}
```

## **✅ Configuration Methods**

### **Method 1: Environment Variable (Production)**
```bash
export FLOWTIME_DATA_DIR="/var/lib/flowtime"
```

### **Method 2: Configuration File (API Only)**
```json
{
  "ArtifactsDirectory": "/path/to/data"
}
```

### **Method 3: CLI Parameter (CLI Only)**
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

- ✅ Environment variable precedence
- ✅ Configuration file fallback
- ✅ Default directory behavior
- ✅ Empty/whitespace environment variable handling
- ✅ Relative and absolute path support
- ✅ CLI parameter override functionality

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
```bash
# Configured in appsettings.Development.json
FLOWTIME_DATA_DIR=/workspaces/flowtime-vnext/data
```

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
