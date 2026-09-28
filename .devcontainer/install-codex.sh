#!/usr/bin/env bash
set -euo pipefail

# Pin the official standalone release so rebuilding the same revision produces
# the same CLI. Bump deliberately and let a manual smoke test verify the change.
readonly CODEX_VERSION="0.155.1"
readonly codex_home="${CODEX_HOME:-$HOME/.codex}"
readonly codex_install_dir="${CODEX_INSTALL_DIR:-$codex_home/bin}"
readonly codex_binary="$codex_install_dir/codex"

mkdir -p "$codex_home" "$codex_install_dir"
chmod 700 "$codex_home"

installed_version="$($codex_binary --version 2>/dev/null || true)"
if [[ "$installed_version" == "codex-cli $CODEX_VERSION" ]]; then
  exit 0
fi

echo "Installing Codex CLI $CODEX_VERSION into $codex_install_dir..."
curl -fsSL https://chatgpt.com/codex/install.sh \
  | CODEX_HOME="$codex_home" \
    CODEX_INSTALL_DIR="$codex_install_dir" \
    CODEX_NON_INTERACTIVE=1 \
    sh -s -- --release "$CODEX_VERSION"
