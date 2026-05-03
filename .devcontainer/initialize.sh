#!/usr/bin/env bash
# Runs on the HOST (not in the container) via devcontainer.json `initializeCommand`.
# Prepares stable mount sources under /tmp so devcontainer.json `mounts` entries
# don't have to reference $HOME (which devcontainer.json can't expand portably).
#
# Option C — plugin shadow-mount:
#   ~/.claude            → /tmp/.claude-mount         (full state shared with host)
#   ~/.claude-linux/     → /tmp/.claude-plugins-mount (container-only plugin index)
#                                                      shadows /home/vscode/.claude/plugins
# Workaround for anthropics/claude-code#31388 (open as of 2026-05-02, subscribed):
# the plugin index stores absolute host paths, so a macOS-pathed index breaks
# inside a Linux container and vice versa. Remove this shadow-mount once #31388
# ships a fix that resolves plugin paths relative to $HOME.
# See: https://github.com/anthropics/claude-code/issues/31388

set -euo pipefail

mkdir -p "$HOME/.claude"
mkdir -p "$HOME/.claude-linux/plugins"
mkdir -p "$HOME/.config/gh"

ln -sfn "$HOME/.claude"                /tmp/.claude-mount
ln -sfn "$HOME/.claude-linux/plugins"  /tmp/.claude-plugins-mount
ln -sfn "$HOME/.config/gh"             /tmp/.gh-mount
