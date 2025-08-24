# Devcontainer

This repo ships a minimal devcontainer for M0–M1 to keep startup fast and diffs clean. Optional tooling (Node, Azure CLI, Azurite) will be added per milestone.

## What’s included (base)
- .NET 9 SDK (via devcontainers/dotnet image)
- PowerShell 7 feature
- VS Code extensions: C# Dev Kit, C#, EditorConfig, GitLens, GitHub Actions, Git Graph, YAML, REST Client

## API-first dev with a shared Docker network

We support an "API-first" handoff so other repos (e.g., flowtime-sim) can call FlowTime over a Docker network.

### One-time: create the shared network on your host

```bash
docker network create flowtime-dev
```

### FlowTime devcontainer behavior

- Joins the `flowtime-dev` network and gets a stable name `flowtime-api`.
- Forwards port 8080 to the host (optional but convenient).
- Starts the API bound to `http://0.0.0.0:8080` inside the container.

This is wired via `.devcontainer/devcontainer.json` using:
- `runArgs: ["--network=flowtime-dev", "--name", "flowtime-api"]`
- `forwardPorts: [8080]`
- `postStartCommand: dotnet run --project apis/FlowTime.API --urls http://0.0.0.0:8080`

### How to call the API from another container (e.g., flowtime-sim)

From inside the other container joined to the same network, use the service name:

```bash
curl -s http://flowtime-api:8080/healthz

cat > /tmp/model.yaml << 'YAML'
grid: { bins: 4, binMinutes: 60 }
nodes:
	- id: demand
		kind: const
		values: [10,20,30,40]
	- id: served
		kind: expr
		expr: "demand * 0.8"
outputs:
	- series: served
		as: served.csv
YAML

curl -s -X POST http://flowtime-api:8080/run \
	-H "Content-Type: application/yaml" \
	--data-binary @/tmp/model.yaml
```

Environment variable pattern (recommended):

```bash
export FLOWTIME_URL=http://flowtime-api:8080
curl -s "$FLOWTIME_URL/healthz"
curl -s -X POST "$FLOWTIME_URL/run" -H "Content-Type: application/yaml" --data-binary @/tmp/model.yaml
```

### How to call the API from the host

If port forwarding is enabled, you can call:

```bash
curl -s http://localhost:8080/healthz
```

### Troubleshooting

- Network membership: `docker network inspect flowtime-dev | jq '.[0].Containers | keys'`
- Name resolution (from a sibling container): `getent hosts flowtime-api || ping -c1 flowtime-api`
- API listening (inside FlowTime container): `ss -lntp | grep 8080`
- Content type: POST `/run` accepts YAML; use `-H "Content-Type: application/yaml"` (or `text/plain` per M0 tests).

### Debugging across two devcontainers

Run two VS Code windows, one for FlowTime (API), one for your sim. In FlowTime, use the launch config that runs:

```json
{
	"name": "FlowTime.API",
	"type": "coreclr",
	"request": "launch",
	"program": "dotnet",
	"args": ["run", "--project", "apis/FlowTime.API", "--urls", "http://0.0.0.0:8080"],
	"cwd": "${workspaceFolder}",
	"launchBrowser": false,
	"justMyCode": true
}
```

Set breakpoints in the `/run` handler and step into requests triggered from the sim container.

## Use
- Open in VS Code and choose "Reopen in Container" (or start a Codespace).
- Post-create runs `.devcontainer/init.ps1` to restore the solution.

## Next milestones
When a milestone needs extra tooling, we’ll add flags and a Dockerfile to enable:
- Node (elkjs, Monaco)
- Azure CLI + Bicep + azd
- Azurite (Blob/Queue/Table emulator)

We’ll mirror additions in CI so devcontainer and Actions stay in sync.
