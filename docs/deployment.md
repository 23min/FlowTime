# Deployment Guide: FlowTime Services

## **Production Port Recommendations**

For production deployments, use these port assignments to ensure compatibility with standard practices:

| Service | Development Port | Production Port | Notes |
|---------|------------------|-----------------|-------|
| **FlowTime API** | `8080` | `8080` | Standard for APIs |
| **FlowTime UI** | `5219` | `3000` | Standard for web apps |
| **FlowTime.Sim API** | `8081` | `8081` | Avoid conflicts with main API |

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

  flowtime-ui:
    build:
      context: .
      dockerfile: ui/FlowTime.UI/Dockerfile
    ports:
      - "3000:3000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://*:3000
    depends_on:
      - flowtime-api
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  flowtime-data:
```

### Individual Container Deployment

**FlowTime API**:
```bash
docker run -d \
  --name flowtime-api \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://*:8080 \
  -v flowtime-data:/app/data \
  flowtime/api:latest
```

**FlowTime UI**:
```bash
docker run -d \
  --name flowtime-ui \
  -p 3000:3000 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://*:3000 \
  --link flowtime-api \
  flowtime/ui:latest
```

## **Environment Variables**

### FlowTime API Production Configuration

```bash
# Required
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://*:8080

# Optional
FLOWTIME_DATA_DIR=/app/data
FLOWTIME_LOG_LEVEL=Information

# Database (if applicable)
ConnectionStrings__DefaultConnection="Server=..."
```

### FlowTime UI Production Configuration

```bash
# Required
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://*:3000

# API Endpoints
FLOWTIME_API_BASE_URL=http://flowtime-api:8080
FLOWTIME_SIM_API_BASE_URL=http://flowtime-sim:8081
```

## **Kubernetes Deployment**

### FlowTime API Deployment

**`k8s/flowtime-api-deployment.yaml`**:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: flowtime-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: flowtime-api
  template:
    metadata:
      labels:
        app: flowtime-api
    spec:
      containers:
      - name: flowtime-api
        image: flowtime/api:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://*:8080"
        volumeMounts:
        - name: data-volume
          mountPath: /app/data
        livenessProbe:
          httpGet:
            path: /healthz
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /healthz
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
      volumes:
      - name: data-volume
        persistentVolumeClaim:
          claimName: flowtime-data-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: flowtime-api-service
spec:
  selector:
    app: flowtime-api
  ports:
  - protocol: TCP
    port: 8080
    targetPort: 8080
  type: LoadBalancer
```

### FlowTime UI Deployment

**`k8s/flowtime-ui-deployment.yaml`**:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: flowtime-ui
spec:
  replicas: 2
  selector:
    matchLabels:
      app: flowtime-ui
  template:
    metadata:
      labels:
        app: flowtime-ui
    spec:
      containers:
      - name: flowtime-ui
        image: flowtime/ui:latest
        ports:
        - containerPort: 3000
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://*:3000"
        - name: FLOWTIME_API_BASE_URL
          value: "http://flowtime-api-service:8080"
        livenessProbe:
          httpGet:
            path: /
            port: 3000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /
            port: 3000
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: flowtime-ui-service
spec:
  selector:
    app: flowtime-ui
  ports:
  - protocol: TCP
    port: 3000
    targetPort: 3000
  type: LoadBalancer
```

## **Reverse Proxy Configuration**

### Nginx Configuration

**`/etc/nginx/sites-available/flowtime`**:
```nginx
upstream flowtime_api {
    server localhost:8080;
}

upstream flowtime_ui {
    server localhost:3000;
}

server {
    listen 80;
    server_name flowtime.example.com;

    # UI routes
    location / {
        proxy_pass http://flowtime_ui;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # API routes
    location /api/ {
        proxy_pass http://flowtime_api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Traefik Configuration

**`docker-compose.yml` with Traefik**:
```yaml
version: '3.8'
services:
  traefik:
    image: traefik:v2.10
    command:
      - "--api.insecure=true"
      - "--providers.docker=true"
      - "--entrypoints.web.address=:80"
    ports:
      - "80:80"
      - "8080:8080"  # Traefik dashboard
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock

  flowtime-api:
    build: .
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.api.rule=Host(`flowtime.example.com`) && PathPrefix(`/api`)"
      - "traefik.http.services.api.loadbalancer.server.port=8080"

  flowtime-ui:
    build: ./ui
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.ui.rule=Host(`flowtime.example.com`)"
      - "traefik.http.services.ui.loadbalancer.server.port=3000"
```

## **Production Monitoring**

### Health Check Endpoints

- **FlowTime API**: `GET /healthz`
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

### Metrics and Observability

Consider integrating:
- **Prometheus** for metrics collection
- **Grafana** for dashboards
- **Serilog** for structured logging
- **Application Insights** for .NET monitoring

## **Security Considerations**

### Production Security Checklist

- [ ] Use HTTPS in production
- [ ] Configure proper CORS policies
- [ ] Set secure authentication/authorization
- [ ] Use secrets management (Azure Key Vault, Kubernetes secrets)
- [ ] Enable request logging for auditing
- [ ] Configure rate limiting
- [ ] Use least-privilege container users

### Example HTTPS Configuration

```bash
# Environment variables for HTTPS
ASPNETCORE_URLS="https://*:443;http://*:80"
ASPNETCORE_Kestrel__Certificates__Default__Path="/app/certificates/cert.pfx"
ASPNETCORE_Kestrel__Certificates__Default__Password="certificate_password"
```

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
