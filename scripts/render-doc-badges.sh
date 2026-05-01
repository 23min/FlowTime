#!/usr/bin/env bash
# Render shields-style SVG badges for doc-lint + doc-garden metrics.
#
# Reads  metrics.json         at repo root.
# Writes docs/badges/doc-health.svg       — doc_health       (doc-lint, structural)
#        docs/badges/doc-correctness.svg  — content_currency (doc-garden, content)
#
# Thresholds: ≥80 green, ≥60 yellow, else red.
# A missing / non-numeric score renders as "—" on a gray badge (expected
# for content_currency before the first doc-garden verify pass).
#
# Usage:  bash scripts/render-doc-badges.sh
# Invoke at the end of doc-lint full / doc-garden sessions and commit
# the regenerated SVGs alongside metrics.json.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
METRICS="$REPO_ROOT/metrics.json"
BADGES_DIR="$REPO_ROOT/docs/badges"

if [[ ! -f "$METRICS" ]]; then
  echo "error: $METRICS not found" >&2
  exit 1
fi

mkdir -p "$BADGES_DIR"

# ── color thresholds ──────────────────────────────
color_for_score() {
  local score="$1"
  if [[ -z "$score" || "$score" == "null" ]]; then
    echo "#9f9f9f"
    return
  fi
  if (( score >= 80 )); then echo "#4c1"
  elif (( score >= 60 )); then echo "#dfb317"
  else echo "#e05d44"
  fi
}

format_score() {
  local score="$1"
  if [[ -z "$score" || "$score" == "null" ]]; then
    echo "—"
  else
    echo "$score"
  fi
}

# ── SVG renderer ──────────────────────────────────
# Approximates text width at 7px/char + 10px padding per side — close
# enough for the shields flat template (within ~1 char on either side).
render_svg() {
  local label="$1" value="$2" color="$3"
  local per_char=7 pad=10
  local left_w=$(( ${#label} * per_char + pad ))
  local right_w=$(( ${#value} * per_char + pad ))
  local total_w=$(( left_w + right_w ))
  local label_x=$(( left_w / 2 ))
  local value_x=$(( left_w + right_w / 2 ))

  cat <<EOF
<svg xmlns="http://www.w3.org/2000/svg" width="$total_w" height="20" role="img" aria-label="$label: $value">
  <linearGradient id="s" x2="0" y2="100%">
    <stop offset="0" stop-color="#bbb" stop-opacity=".1"/>
    <stop offset="1" stop-opacity=".1"/>
  </linearGradient>
  <clipPath id="r"><rect width="$total_w" height="20" rx="3" fill="#fff"/></clipPath>
  <g clip-path="url(#r)">
    <rect width="$left_w" height="20" fill="#555"/>
    <rect x="$left_w" width="$right_w" height="20" fill="$color"/>
    <rect width="$total_w" height="20" fill="url(#s)"/>
  </g>
  <g fill="#fff" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
    <text x="$label_x" y="14">$label</text>
    <text x="$value_x" y="14">$value</text>
  </g>
</svg>
EOF
}

# ── render both badges ────────────────────────────
dh_score=$(jq -r '.metrics.doc_health.score // empty' "$METRICS")
cc_score=$(jq -r '.metrics.content_currency.score // empty' "$METRICS")

dh_color=$(color_for_score "$dh_score")
cc_color=$(color_for_score "$cc_score")

render_svg "doc-health"      "$(format_score "$dh_score")" "$dh_color" > "$BADGES_DIR/doc-health.svg"
render_svg "doc-correctness" "$(format_score "$cc_score")" "$cc_color" > "$BADGES_DIR/doc-correctness.svg"

echo "wrote docs/badges/doc-health.svg        ($(format_score "$dh_score"), $dh_color)"
echo "wrote docs/badges/doc-correctness.svg   ($(format_score "$cc_score"), $cc_color)"
