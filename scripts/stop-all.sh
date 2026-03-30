#!/usr/bin/env bash
# ================================================================
# stop-all.sh — Kill FlowTime dev processes safely
# ================================================================
# Stops the Engine API, Sim API, Blazor UI, and Svelte UI dev server.
# Targets processes by name — never kills the devcontainer
# port-forwarder or VS Code remote server.
#
# Usage: ./scripts/stop-all.sh [--force]
#   --force: use SIGKILL after timeout (default: SIGTERM only)

set -euo pipefail

FORCE=false
if [[ "${1:-}" == "--force" ]]; then
  FORCE=true
fi

TARGETS=(
  "FlowTime.API"
  "FlowTime.Sim.Service"
  "FlowTime.UI"
  "node.*vite"
)

killed=0

for pattern in "${TARGETS[@]}"; do
  pids=$(pgrep -f "$pattern" 2>/dev/null || true)
  if [[ -z "$pids" ]]; then
    continue
  fi

  # Filter out this script and any VS Code / port-forwarder processes
  for pid in $pids; do
    cmdline=$(ps -p "$pid" -o args= 2>/dev/null || true)

    # Skip VS Code, port-forwarder, and this script
    if echo "$cmdline" | grep -qiE 'vscode|code-server|port.forward|stop-all'; then
      continue
    fi

    echo "  SIGTERM  $pid  $cmdline"
    kill -TERM "$pid" 2>/dev/null || true
    killed=$((killed + 1))

    if $FORCE; then
      # Wait up to 3 seconds, then SIGKILL
      for i in 1 2 3; do
        if ! kill -0 "$pid" 2>/dev/null; then
          break
        fi
        sleep 1
      done
      if kill -0 "$pid" 2>/dev/null; then
        echo "  SIGKILL  $pid  (still alive after 3s)"
        kill -KILL "$pid" 2>/dev/null || true
      fi
    fi
  done
done

if [[ $killed -eq 0 ]]; then
  echo "  No FlowTime processes running."
else
  echo "  Stopped $killed process(es)."
fi
