#!/usr/bin/env bash
# Runs on the HOST (not in the container) via devcontainer.json `initializeCommand`.
# Prepares stable mount sources under /tmp so devcontainer.json `mounts` entries
# don't have to reference $HOME (which devcontainer.json can't expand portably).
#
# /tmp is shared across every devcontainer on the host, so these symlink names
# are repo-prefixed (flowtime-vnext-*) — a generic name like /tmp/.claude-mount
# gets overwritten by whichever repo's initializeCommand last ran, which is a
# real, observed collision when multiple devcontainers are opened around the
# same time (each repo's own mount still resolves correctly once ITS
# initializeCommand runs, but a not-yet-refreshed container can transiently
# point at another repo's target in between).
#
# Option C — plugin shadow-mount:
#   ~/.claude            → /tmp/.flowtime-vnext-claude-mount         (full state shared with host)
#   ~/.claude-linux/     → /tmp/.flowtime-vnext-claude-plugins-mount (container-only plugin index,
#                                                                      shared across Linux devcontainers
#                                                                      by design — not repo-specific)
#                                                                      shadows /home/vscode/.claude/plugins
# Workaround for anthropics/claude-code#31388 (open as of 2026-05-02, subscribed):
# the plugin index stores absolute host paths, so a macOS-pathed index breaks
# inside a Linux container and vice versa. Remove this shadow-mount once #31388
# ships a fix that resolves plugin paths relative to $HOME.
# See: https://github.com/anthropics/claude-code/issues/31388
#
# Codex CLI's auth/config directory is kept container-only (a repo-suffixed dir,
# not the bare host `~/.codex`) so a Linux-container config never mixes with a
# native host install, mirroring the .claude-linux indirection above. chmod 700
# because it holds an OpenAI auth token.

set -euo pipefail

mkdir -p "$HOME/.claude"
mkdir -p "$HOME/.claude-linux/plugins"
mkdir -p "$HOME/.config/gh"
mkdir -p "$HOME/.codex-flowtime-vnext"
chmod 700 "$HOME/.codex-flowtime-vnext"

ln -sfn "$HOME/.claude"                /tmp/.flowtime-vnext-claude-mount
ln -sfn "$HOME/.claude-linux/plugins"  /tmp/.flowtime-vnext-claude-plugins-mount
ln -sfn "$HOME/.config/gh"             /tmp/.flowtime-vnext-gh-mount
ln -sfn "$HOME/.codex-flowtime-vnext"  /tmp/.flowtime-vnext-codex-mount
