#!/usr/bin/env bash
# statusline.sh — Claude Code status line for AI Framework v2 projects.
#
# Reads a JSON payload on stdin (provided by Claude Code per invocation)
# and prints a single line to stdout. Shows:
#   <threshold-icon>  <model>  <cwd-basename>  [<epic> <milestone>]  <tokens>
#
# Thresholds:
#   🟢 green   tokens <  250k
#   🟡 yellow  250k ≤ tokens < 500k   ("consider new session")
#   🔴 red     tokens ≥ 500k          ("START NEW SESSION")
#
# Epic/milestone detection:
#   - Epic ID: first `E-<slug>` match in `work/roadmap.md`
#   - Milestone ID: first `M-<TRACK>-<NN>` match in `CLAUDE.md` "Current Work"
# Falls back silently when the repo doesn't match the framework's layout.
#
# Token counting: parses the last `"usage"` record in the transcript JSONL
# and sums input_tokens + cache_read + cache_creation. Zero if unavailable.
#
# Requires: jq, bash, coreutils (tac, basename, grep, cat).

set -u

input=$(cat)

# ── Parse Claude Code stdin payload ─────────────
cwd=$(jq -r '.cwd // .workspace.current_dir // empty' <<<"$input" 2>/dev/null)
model_name=$(jq -r '.model.display_name // .model.id // "Claude"' <<<"$input" 2>/dev/null)
transcript_path=$(jq -r '.transcript_path // empty' <<<"$input" 2>/dev/null)
cwd=${cwd:-$PWD}

# ── Working directory (basename shows worktree names as-is) ──
dir=$(basename "$cwd")

# ── Active epic / milestone from the framework's artefact layout ──
epic=""
milestone=""
if [ -f "$cwd/work/roadmap.md" ]; then
  epic=$(grep -m1 -oE 'E-[A-Z][A-Za-z0-9-]+' "$cwd/work/roadmap.md" 2>/dev/null | head -1)
fi
if [ -f "$cwd/CLAUDE.md" ]; then
  # Scope to the Current Work section so we don't match M-IDs in routing tables.
  milestone=$(awk '/^## Current Work/{found=1} found' "$cwd/CLAUDE.md" 2>/dev/null \
    | grep -m1 -oE 'M-[A-Z0-9]+-[0-9]+[A-Za-z0-9-]*' | head -1)
fi
tag=""
if [ -n "$epic" ] && [ -n "$milestone" ]; then
  tag="$epic $milestone"
elif [ -n "$epic" ]; then
  tag="$epic"
elif [ -n "$milestone" ]; then
  tag="$milestone"
fi

# ── Context tokens (last usage record in the transcript) ──
tokens=0
if [ -n "$transcript_path" ] && [ -f "$transcript_path" ]; then
  usage_line=$(tac "$transcript_path" 2>/dev/null | grep -m1 '"usage"' || true)
  if [ -n "$usage_line" ]; then
    tokens=$(jq -r '
      (.message.usage // .usage // {}) |
      ((.input_tokens // 0)
        + (.cache_read_input_tokens // 0)
        + (.cache_creation_input_tokens // 0))
    ' <<<"$usage_line" 2>/dev/null || echo 0)
  fi
fi
tokens=${tokens:-0}

# ── Threshold icon / colour / warning ──
if   [ "$tokens" -ge 500000 ]; then icon="🔴"; color=31; warn=" · START NEW SESSION"
elif [ "$tokens" -ge 250000 ]; then icon="🟡"; color=33; warn=" · consider new session"
else                                icon="🟢"; color=32; warn=""
fi
tokens_k=$((tokens / 1000))

# ── Render: icon hugs the first text part; the rest join with " · " ──
parts=("$model_name" "$dir")
[ -n "$tag" ] && parts+=("$tag")
parts+=("$(printf '\033[%sm%dk tokens\033[0m%s' "$color" "$tokens_k" "$warn")")

body=""
for p in "${parts[@]}"; do
  if [ -z "$body" ]; then body="$p"; else body="$body · $p"; fi
done
printf '%s %s' "$icon" "$body"
