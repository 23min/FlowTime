# Deployment Guide: FlowTime Services

## **Production Port Recommendations**

For production deployments, use these port assignments to ensure compatibility with standard practices:

| Service | Development Port | Production Port | Notes |
|---------|------------------|-----------------|-------|
| **FlowTime Engine API** | `8080` | `8080` | Standard for APIs |
| **FlowTime UI** | `5219` | `3000` | Standard for web apps |
| **FlowTime Sim API** | `8090` | `8090` | Avoid conflicts with main API |

## **Container Deployment**

### Docker Compose (Recommended)

**`docker-compose.yml`**:
```yaml
version: '3.8'
services:
  flowtime-api:
    build:
      context: .
      dockerfile: src/FlowTime.API/Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://*:8080
    volumes:
      - flowtime-data:/app/data
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - flowtime-net

  flowtime-sim:
    build:
      context: .
      dockerfile: src/FlowTime.Sim.Service/Dockerfile
    ports:
      - "8090:8090"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://*:8090
    volumes:
      - flowtime-sim-data:/app/data
    depends_on:
      - flowtime-api
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8090/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - flowtime-net

  flowtime-ui:
    build:
      context: .
      dockerfile: src/FlowTime.UI/Dockerfile
    ports:
      - "3000:3000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://*:3000
      - FLOWTIME_API_BASE_URL=http://flowtime-api:8080/
      - FLOWTIME_SIM_API_BASE_URL=http://flowtime-sim:8090/
    depends_on:
      - flowtime-api
      - flowtime-sim
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - flowtime-net

volumes:
  flowtime-data:
  flowtime-sim-data:

networks:
  flowtime-net:
```

### Individual Container Deployment

**FlowTime Engine API**:
```bash
docker network create flowtime-net
docker run -d \
  --name flowtime-api \
  --network flowtime-net \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://*:8080 \
  -v flowtime-data:/app/data \
  flowtime/api:latest
```

**FlowTime Sim API**:
```bash
docker run -d \
  --name flowtime-sim \
  --network flowtime-net \
  -p 8090:8090 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://*:8090 \
  -v flowtime-sim-data:/app/data \
  flowtime/sim:latest
```

**FlowTime UI**:
```bash
docker run -d \
  --name flowtime-ui \
  --network flowtime-net \
  -p 3000:3000 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://*:3000 \
  -e FLOWTIME_API_BASE_URL=http://flowtime-api:8080/ \
  -e FLOWTIME_SIM_API_BASE_URL=http://flowtime-sim:8090/ \
  flowtime/ui:latest
```

If you run the containers separately, create a shared network so they can resolve each other by name:
```bash
docker network create flowtime-net
docker network connect flowtime-net flowtime-api
docker network connect flowtime-net flowtime-sim
docker network connect flowtime-net flowtime-ui
```

## **Environment Variables**

### FlowTime Engine API Production Configuration

```bash
# Required
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://*:8080

# Optional
FLOWTIME_DATA_DIR=/app/data
FLOWTIME_LOG_LEVEL=Information
```

### FlowTime UI Production Configuration

```bash
# Required
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://*:3000

# API Endpoints
FLOWTIME_API_BASE_URL=http://flowtime-api:8080
FLOWTIME_SIM_API_BASE_URL=http://flowtime-sim:8090
```

## **Production Monitoring**

### Health Check Endpoints

- **FlowTime API**: `GET /healthz`
- **FlowTime Sim API**: `GET /healthz`
- **FlowTime UI**: `GET /` (returns 200 for healthy UI)

### Logging Configuration

**`appsettings.Production.json`**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "FlowTime": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "Console": {
      "IncludeScopes": true,
      "TimestampFormat": "yyyy-MM-dd HH:mm:ss "
    }
  }
}
```

## **Security Considerations**

### Production Security Checklist

- [ ] Use HTTPS in production
- [ ] Configure proper CORS policies
- [ ] Set secure authentication/authorization
- [ ] Use secrets management (Azure Key Vault, Kubernetes secrets)
- [ ] Enable request logging for auditing
- [ ] Configure rate limiting
- [ ] Use least-privilege container users

## **Backup and Data Management**

### Data Persistence

Ensure these directories are backed up:
- **FlowTime data**: `/app/data/` (simulation runs, artifacts)
- **Configuration**: Application configuration files
- **Logs**: Application logs for troubleshooting

### Backup Strategy

```bash
# Example backup script
#!/bin/bash
BACKUP_DIR="/backups/flowtime/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

# Backup data
docker cp flowtime-api:/app/data "$BACKUP_DIR/data"

# Backup configuration
docker cp flowtime-api:/app/appsettings.Production.json "$BACKUP_DIR/"

# Create archive
tar -czf "${BACKUP_DIR}.tar.gz" -C /backups/flowtime "$(basename $BACKUP_DIR)"
rm -rf "$BACKUP_DIR"
```

## **Troubleshooting Production Issues**

### Common Issues

1. **Port conflicts**: Check if ports are already in use
2. **Memory issues**: Monitor container memory usage
3. **Disk space**: Ensure adequate space for data persistence
4. **Network connectivity**: Verify service-to-service communication

### Debugging Commands

```bash
# Check container logs
docker logs flowtime-api
docker logs flowtime-ui

# Check resource usage
docker stats

# Test health endpoints
curl -f http://localhost:8080/healthz
curl -f http://localhost:3000/

# Check network connectivity
docker exec flowtime-ui curl -f http://flowtime-api:8080/healthz
```
