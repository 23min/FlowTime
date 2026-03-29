#!/usr/bin/env bash
set -euo pipefail

post_create=false
[[ "${1:-}" == "--post-create" ]] && post_create=true

echo "=> FlowTime consolidated init"
if ! dotnet --info >/dev/null 2>&1; then
  echo "dotnet SDK not found"; exit 1
fi

# Install Claude Code if not already installed
if ! command -v claude >/dev/null 2>&1; then
  echo "Installing Claude Code..."
  curl -fsSL https://claude.ai/install.sh | bash
  export PATH="$HOME/.local/bin:$PATH"
fi

# Install uv if not already installed
if ! command -v uv >/dev/null 2>&1; then
  echo "Installing uv..."
  curl -LsSf https://astral.sh/uv/install.sh | sh
  export PATH="$HOME/.local/bin:$PATH"
fi

echo "Restoring solution..."
dotnet restore >/dev/null

# Install Razor/Blazor workloads if needed for UI project
echo "Checking Razor workloads..."
dotnet workload restore

if $post_create; then
  echo "Ready. Try:"
  echo "  dotnet build FlowTime.sln"
  echo "  dotnet test FlowTime.sln"
  echo "  dotnet run --project src/FlowTime.Sim.Service --urls http://0.0.0.0:8090"
fi
