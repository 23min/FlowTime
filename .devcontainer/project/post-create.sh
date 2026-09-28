#!/usr/bin/env bash
# Project hook — runs INSIDE the container after the kit's post-create.sh (once per container).
# Owned by this repo: `copier update` never changes this file.
#
# Use for one-time project setup: git hooks, extra dependency installs, seed data. Keep it
# idempotent — it runs again after every rebuild.
set -euo pipefail

# The projects target net9.0. The kit installs the current LTS SDK, which builds net9.0 but
# runs and tests it only on the .NET 9 runtimes. dotnet-install skips what is already present.
curl -fsSLo /tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
bash /tmp/dotnet-install.sh --runtime dotnet --channel 9.0 --install-dir "$HOME/.dotnet"
bash /tmp/dotnet-install.sh --runtime aspnetcore --channel 9.0 --install-dir "$HOME/.dotnet"
rm -f /tmp/dotnet-install.sh

# The dead-code-audit skill's .NET recipe runs Roslynator.
command -v roslynator >/dev/null 2>&1 || dotnet tool install -g Roslynator.DotNet.Cli

dotnet restore
dotnet workload restore
(cd ui && pnpm install --frozen-lockfile)
