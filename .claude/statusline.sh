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
# Epic detection (priority order):
#   1. Current git branch — parse `E-NN` out of `epic/E-NN-*`,
#      `milestone/m-ENN-*`, `feature/*-ENN-*`, etc. Most reliable signal of
#      what's actively being worked on.
#   2. CLAUDE.md "## Current Work" → `E-NN` on lines containing
#      `in-progress` (scoped to that H2, stops at the next H2).
#   3. ROADMAP.md first `E-NN` match. Last resort; narrative documents
#      list completed epics textually before the active one, so this is
#      unreliable as a primary source.
#
# Mismatch highlighting: when the branch epic is not among the
# in-progress epics in CLAUDE.md, the tag is rendered in yellow as
# `E-BRANCH ≠ E-CLAUDE`. Catches two drift cases:
#   - branch advanced past CLAUDE.md (spec/tracking update forgotten)
#   - CLAUDE.md advanced past the branch (working on a stale branch)
#
# Milestone ID: first `m-E\d+-\d+[-A-Za-z0-9]*` match in the Current
# Work section (framework's `milestoneIdPattern` default; loosened so
# abbreviated `m-ENN-MM` forms in prose also surface).
#
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

# ── Resolve active epic: branch → CLAUDE.md in-progress → ROADMAP.md ──
epic=""
milestone=""
epic_mismatch=""

# 1. Epic from current git branch (epic/E-NN-*, milestone/m-ENN-*, feature/*-ENN-*, …).
branch_epic=""
if git_branch=$(git -C "$cwd" rev-parse --abbrev-ref HEAD 2>/dev/null); then
  branch_epic=$(echo "$git_branch" | grep -oE 'E-?[0-9]{2,}' | head -1)
  case "$branch_epic" in
    E-*) ;;
    E*)  branch_epic="E-${branch_epic#E}" ;;
  esac
fi

# 2. In-progress epics from CLAUDE.md "## Current Work" (stop at next H2).
claude_in_progress=""
current_work=""
if [ -f "$cwd/CLAUDE.md" ]; then
  current_work=$(awk '/^## Current Work/{found=1; next} found && /^## /{exit} found' "$cwd/CLAUDE.md" 2>/dev/null)
  claude_in_progress=$(echo "$current_work" | grep -i 'in-progress' | grep -oE 'E-[0-9]{2,}' | sort -u)
fi

# Resolve active epic in fallback order and detect branch/CLAUDE.md drift.
if [ -n "$branch_epic" ]; then
  epic="$branch_epic"
  if [ -n "$claude_in_progress" ] && ! echo "$claude_in_progress" | grep -qx "$branch_epic"; then
    epic_mismatch=$(echo "$claude_in_progress" | head -1)
  fi
elif [ -n "$claude_in_progress" ]; then
  epic=$(echo "$claude_in_progress" | head -1)
elif [ -f "$cwd/ROADMAP.md" ]; then
  epic=$(grep -m1 -oE 'E-[0-9]{2,}' "$cwd/ROADMAP.md" 2>/dev/null | head -1)
fi

# Milestone from the Current Work section (loosened to tolerate abbreviated m-ENN-MM prose form).
if [ -n "$current_work" ]; then
  milestone=$(echo "$current_work" | grep -m1 -oE 'm-E[0-9]+-[0-9]+[-A-Za-z0-9]*' | head -1)
fi

tag=""
if [ -n "$epic" ] && [ -n "$milestone" ]; then
  tag="$epic $milestone"
elif [ -n "$epic" ]; then
  tag="$epic"
elif [ -n "$milestone" ]; then
  tag="$milestone"
fi

# Yellow-highlight the tag when branch and CLAUDE.md disagree on the active epic.
if [ -n "$tag" ] && [ -n "$epic_mismatch" ]; then
  tag=$(printf '\033[33m%s ≠ %s\033[0m' "$tag" "$epic_mismatch")
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
