# Development Setup: FlowTime Services Port Configuration

## **Service Port Assignments**

To avoid port conflicts and ensure stable development, each service has a dedicated port:

| Service | Port | Purpose |
|---------|------|---------|
| **FlowTime API** | `8080` | Main FlowTime engine API (flowtime-vnext) |
| **FlowTime.Sim API** | `8081` | Synthetic data simulation API (flowtime-sim-vnext) |
| **FlowTime UI** | `5219` | Blazor UI that connects to both APIs |

## **Starting Services**

### FlowTime API (Main Engine)
```bash
cd /workspaces/flowtime/flowtime-vnext
dotnet run --project apis/FlowTime.API
# Runs on http://localhost:8080
```

### FlowTime.Sim API (Simulation)
```bash
cd /workspaces/flowtime/flowtime-sim-vnext  
dotnet run --project src/FlowTime.Sim.Service
# Runs on http://localhost:8081
```

### FlowTime UI
```bash
cd /workspaces/flowtime/flowtime-vnext/ui/FlowTime.UI
dotnet run --project ui/FlowTime.UI
# Runs on http://localhost:5219
```

## **FlowTime.Sim File Configuration**

### Configuration Pattern (Single Data Directory)
FlowTime.Sim uses a single data directory approach for simplified configuration:

**Environment Variable** (Highest Priority):
```bash
export FLOWTIME_SIM_DATA_DIR="/var/lib/flowtime-sim"
# Creates:
# - /var/lib/flowtime-sim/runs/ (simulation runs)
# - /var/lib/flowtime-sim/catalogs/ (system catalogs)
```

**Configuration File**:
```json
{
  "FlowTimeSim": {
    "DataDir": "/workspaces/flowtime/flowtime-sim-vnext/data"
  }
}
```

### Configuration Precedence
1. **Environment Variable**: `FLOWTIME_SIM_DATA_DIR` (highest priority)
2. **Configuration**: `FlowTimeSim:DataDir` 
3. **Defaults**: `./data/runs` and `./data/catalogs`

### Development File Locations
- **FlowTime data**: `/workspaces/flowtime/flowtime-vnext/data/`
- **FlowTime.Sim data**: `/workspaces/flowtime/flowtime-sim-vnext/data/`
  - **Runs**: `/workspaces/flowtime/flowtime-sim-vnext/data/runs/`
  - **Catalogs**: `/workspaces/flowtime/flowtime-sim-vnext/data/catalogs/` (copied from source during startup)

### Catalog Management
FlowTime.Sim uses a two-tier catalog system:
- **Source catalogs**: `/catalogs/` (version-controlled demo content)
- **Runtime catalogs**: `/data/catalogs/` (copied from source during startup)

**Startup Behavior**:
- Demo catalogs are automatically copied from `/catalogs/` to `/data/catalogs/` if the runtime directory is empty
- Users can customize catalogs by editing files in `/data/catalogs/`
- User customizations are preserved across service restarts

### Production Configuration Examples

#### Environment Variable (Recommended)
```bash
export FLOWTIME_SIM_DATA_DIR="/var/lib/flowtime-sim"
```

#### Docker Compose
```yaml
services:
  flowtime-sim:
    environment:
      - FLOWTIME_SIM_DATA_DIR=/app/data
    volumes:
      - flowtime-sim-data:/app/data
    ports:
      - "8081:8081"
volumes:
  flowtime-sim-data:
```

## **UI API Configuration**

The FlowTime UI should be configured to connect to:
- **FlowTime API**: `http://localhost:8080` (for engine operations)
- **FlowTime.Sim API**: `http://localhost:8081` (for simulation operations)

## **Troubleshooting**

### Port Conflicts
- Ensure only one service runs per port
- Use `lsof -i :8080` and `lsof -i :8081` to check port usage
- Kill conflicting processes: `lsof -ti:8080 | xargs kill -9`

### File Location Issues
- Check service logs for "Creating simulation run in: {path}" messages
- Verify configuration with environment variables
- Ensure directories have write permissions

### Service Health Checks
```bash
# FlowTime API
curl http://localhost:8080/healthz

# FlowTime.Sim API  
curl http://localhost:8081/healthz
```
