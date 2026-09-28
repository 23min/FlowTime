#!/usr/bin/env bash
set -euo pipefail

post_create=false
[[ "${1:-}" == "--post-create" ]] && post_create=true

echo "=> FlowTime consolidated init"
if ! dotnet --info >/dev/null 2>&1; then
  echo "dotnet SDK not found"; exit 1
fi

# Install tmux (persistent dev sessions, e.g. `dev left` / `dev right`)
if ! command -v tmux >/dev/null 2>&1; then
  echo "Installing tmux..."
  # The base image's yarn apt source is signed with a key the image doesn't have
  # (NO_PUBKEY 62D54FD4003F6525), which fails apt-get update; nothing here uses yarn.
  for list in /etc/apt/sources.list.d/*; do
    if grep -q 'dl.yarnpkg.com' "$list" 2>/dev/null; then sudo rm -f "$list"; fi
  done
  sudo apt-get update -qq
  sudo apt-get install -y -qq --no-install-recommends tmux
  sudo rm -rf /var/lib/apt/lists/*
fi

# Install Claude Code if not already installed
if ! command -v claude >/dev/null 2>&1; then
  echo "Installing Claude Code..."
  curl -fsSL https://claude.ai/install.sh | bash
  export PATH="$HOME/.local/bin:$PATH"
fi

# Install Codex CLI (pinned standalone release; see install-codex.sh)
bash "$(dirname "$0")/install-codex.sh"
export PATH="$HOME/.codex/bin:$PATH"

# Install uv if not already installed
if ! command -v uv >/dev/null 2>&1; then
  echo "Installing uv..."
  curl -LsSf https://astral.sh/uv/install.sh | sh
  export PATH="$HOME/.local/bin:$PATH"
fi

# Install Rust toolchain with clippy (needed by /wf-dead-code-audit rust recipe)
if ! command -v cargo >/dev/null 2>&1; then
  echo "Installing Rust toolchain (stable + clippy)..."
  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --default-toolchain stable --profile minimal --component clippy
  export PATH="$HOME/.cargo/bin:$PATH"
fi

# Install Roslynator CLI (needed by /wf-dead-code-audit dotnet recipe)
if ! command -v roslynator >/dev/null 2>&1; then
  echo "Installing Roslynator CLI..."
  dotnet tool install -g Roslynator.DotNet.Cli
  export PATH="$HOME/.dotnet/tools:$PATH"
fi

# Install Go (avoids the devcontainer Go feature, which fails on the .NET base
# image's stale yarn apt source — NO_PUBKEY 62D54FD4003F6525)
if ! command -v go >/dev/null 2>&1; then
  echo "Installing Go..."
  GO_VERSION=1.22.10
  curl -fsSL "https://go.dev/dl/go${GO_VERSION}.linux-amd64.tar.gz" \
    | sudo tar -C /usr/local -xz
  export PATH="/usr/local/go/bin:$PATH"
fi

# Install aiwf (AI workflow framework v3, branch-tip pin during PoC).
# `go install` rejects branch names containing slashes, so resolve the branch
# tip to a commit SHA via git ls-remote and install that.
if ! command -v aiwf >/dev/null 2>&1; then
  echo "Installing aiwf..."
  export PATH="$HOME/go/bin:/usr/local/go/bin:$PATH"
  aiwf_sha=$(git ls-remote https://github.com/23min/ai-workflow-v2.git refs/heads/poc/aiwf-v3 | awk '{print $1}')
  if [ -z "$aiwf_sha" ]; then
    echo "Failed to resolve aiwf branch tip" >&2
    exit 1
  fi
  go install "github.com/23min/ai-workflow-v2/tools/cmd/aiwf@${aiwf_sha}"
fi

echo "Restoring solution..."
dotnet restore >/dev/null

# Install Razor/Blazor workloads if needed for UI project
echo "Checking Razor workloads..."
dotnet workload restore

# Install Svelte UI dependencies if ui/ exists
if [ -d "ui" ] && [ -f "ui/package.json" ]; then
  echo "Installing Svelte UI dependencies..."
  (cd ui && pnpm install --frozen-lockfile)
fi

if $post_create; then
  echo "Ready. Try:"
  echo "  dotnet build FlowTime.sln"
  echo "  dotnet test FlowTime.sln"
  echo "  dotnet run --project src/FlowTime.Sim.Service --urls http://0.0.0.0:8090"
  echo "  cd ui && pnpm dev  # Svelte UI at http://localhost:5173"
fi
