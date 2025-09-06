# FlowTime.Sim Configuration Guide

## ✅ **Data Storage Configuration**

FlowTime.Sim uses a single data directory approach for simplified and production-ready file management. Both CLI and Service components share the same configuration system.

### **1. Configuration Precedence**
```
1. FLOWTIME_SIM_DATA_DIR environment variable   [Highest Priority]
2. FlowTimeSim:DataDir in appsettings.json     [Configuration File]
3. ./data                                       [Default]
```

### **2. Environment Variable**
- **Variable Name**: `FLOWTIME_SIM_DATA_DIR`
- **Scope**: Both CLI and Service respect this variable
- **Behavior**: Automatically creates `/runs` and `/catalogs` subdirectories
- **Usage**: Set to any absolute or relative path

### **3. Configuration Files**
- **Service**: Uses `FlowTimeSim:DataDir` field in `appsettings.json`
- **CLI**: Uses environment variable or defaults to `./data`

### **4. Directory Structure**
```
FLOWTIME_SIM_DATA_DIR/
├── runs/           # Simulation run artifacts
│   └── sim_TIMESTAMP_ID/
│       ├── manifest.json
│       ├── run.json
│       ├── spec.yaml
│       └── series/
│           ├── index.json
│           └── *.csv
└── catalogs/       # Runtime catalogs (copied from source during startup)
    ├── demo-system.yaml
    └── tiny-demo.yaml
```

### **5. Catalog Management**
FlowTime.Sim uses a two-tier catalog system for maximum flexibility:

- **Source catalogs**: `/catalogs/` (version-controlled, shipped with application)
- **Runtime catalogs**: `/data/catalogs/` (working copy, user-customizable)

**Startup Behavior**:
- Demo catalogs are automatically copied from `/catalogs/` to `/data/catalogs/` if runtime directory is empty
- Users can safely customize catalogs by editing files in `/data/catalogs/`
- User customizations are preserved across service restarts and upgrades

## ✅ **Port Configuration**

FlowTime.Sim Service uses port `8081` to avoid conflicts with the main FlowTime API service.

| Service | Port | Purpose |
|---------|------|---------|
| **FlowTime API** | `8080` | Main FlowTime engine API |
| **FlowTime.Sim Service** | `8081` | Synthetic data simulation API |

## ✅ **Usage Examples**

### **Environment Variable (Recommended)**
```bash
# Set globally
export FLOWTIME_SIM_DATA_DIR="/var/lib/flowtime-sim"

# CLI usage
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml

# Service usage  
dotnet run --project src/FlowTime.Sim.Service
```

### **CLI --out Parameter Override**
```bash
# Override for specific run (highest precedence)
dotnet run --project src/FlowTime.Sim.Cli -- --model model.yaml --out /custom/output
```

### **Docker Compose**
```yaml
services:
  flowtime-sim:
    environment:
      - FLOWTIME_SIM_DATA_DIR=/app/data
    ports:
      - "8081:8081"
    volumes:
      - flowtime-sim-data:/app/data
```

### **Development Environment**
```json
// appsettings.Development.json
{
  "FlowTimeSim": {
    "DataDir": "/workspaces/flowtime/flowtime-sim-vnext/data"
  }
}
```

## ✅ **Configuration Methods**

### **Method 1: Environment Variable (Production)**
```bash
export FLOWTIME_SIM_DATA_DIR="/var/lib/flowtime-sim"
```
- **Auto-creates**: `/var/lib/flowtime-sim/runs/` and `/var/lib/flowtime-sim/catalogs/`
- **Used by**: Both CLI and Service
- **Best for**: Production deployments, containers

### **Method 2: Configuration File (Service Only)**
```json
{
  "FlowTimeSim": {
    "DataDir": "/path/to/data"
  }
}
```
- **Auto-creates**: `/path/to/data/runs/` and `/path/to/data/catalogs/`
- **Used by**: Service only
- **Best for**: Service-specific configuration

### **Method 3: CLI --out Parameter (CLI Only)**
```bash
dotnet run --project src/FlowTime.Sim.Cli -- --model model.yaml --out /custom/path
```
- **Direct output**: Files written directly to specified path
- **Used by**: CLI only
- **Best for**: Ad-hoc runs with custom output locations

## ✅ **Service Endpoints**

The FlowTime.Sim Service provides the following endpoints:

### **Simulation Endpoints**
- `POST /sim/run` - Execute simulation with YAML spec
- `GET /runs` - List available simulation runs
- `GET /runs/{id}` - Get specific run details
- `GET /runs/{id}/series/{seriesId}` - Get series data

### **Catalog Endpoints**
- `GET /catalogs` - List available system catalogs
- `GET /catalogs/{id}` - Get specific catalog details

### **Health Endpoint**
- `GET /healthz` - Service health check

## ✅ **Benefits**

1. **Simplified Configuration**: Single environment variable controls all data storage
2. **Logical Organization**: Related data grouped under one directory structure
3. **Production Ready**: Easy to configure with containers and orchestration
4. **Development Friendly**: Sensible defaults for local development
5. **Port Stability**: Dedicated port prevents conflicts with other FlowTime services
6. **Override Capability**: CLI supports per-run output directory override
