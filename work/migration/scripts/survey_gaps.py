# /// script
# requires-python = ">=3.10"
# dependencies = []
# ///
"""Survey work/gaps/ for stale-status, dead-reference, and duplicate signals.

For each gap, report:

1. **Frontmatter-vs-body status mismatch.** If frontmatter status is `open`
   but the body's `### Status` subsection (or any prominent status line)
   contains words like "resolved", "fixed", "addressed", "done" — flag.

2. **Dead file references.** Body mentions `src/...` or `tests/...` paths;
   check whether each path still exists in the current tree. Dead paths
   suggest the code described is gone, so the gap may be moot.

3. **Topic clusters.** Group gaps by shared specific tokens (file paths,
   class/method names, key topic words like "dag-map", "FFI", "heatmap").
   Same cluster ≥ 2 gaps suggests potential duplicates or sub-issues.

Read-only: prints a report; no file mutations.
"""

from __future__ import annotations

import re
from collections import defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
GAPS = REPO / "work/gaps"

# Words that, in the body's ### Status subsection (or near it), suggest
# "actually resolved" even though frontmatter says open.
RESOLVED_HINTS = re.compile(
    r"\b(resolved|fixed|addressed|completed|done|landed|merged|closed|shipped|delivered)\b",
    re.IGNORECASE,
)

# Shared-token candidates for clustering (specific symbols, class names,
# topic words). These are pre-curated based on what we've seen in the
# 33 gaps; the regex is generous and the report shows the matches so
# false positives are visible.
TOPIC_TOKENS = [
    r"dag-map",
    r"heatmap",
    r"MCP",
    r"FFI",
    r"telemetry",
    r"path[ -]analysis",
    r"path[ -]filter",
    r"crystal[ -]ball",
    r"streaming",
    r"chunked",
    r"router",
    r"parallelism",
    r"dependency[ -]constraint",
    r"validation[ -]surface",
    r"keyboard[ -]nav",
    r"color[ -]blind",
    r"scrubber",
    r"FlowTime\.Generator",
    r"ProvenanceEmbedder",
    r"GridDefinition",
    r"FlowTime\.Pipeline",
    r"transportation-basic",
]


def parse_gap(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    end = re.search(r"^---\s*$", text[4:], re.MULTILINE)
    fm_text = text[4 : 4 + end.start()] if end else ""
    body = text[4 + end.end():] if end else text
    fm = {}
    for line in fm_text.splitlines():
        if ":" in line and not line.startswith(" "):
            k, _, v = line.partition(":")
            fm[k.strip()] = v.strip().strip("'\"")
    return {"id": fm.get("id", path.stem), "title": fm.get("title", ""), "status": fm.get("status", "?"), "body": body, "path": path}


def find_status_mismatch(gap: dict) -> str | None:
    """Return a one-line note if frontmatter says open but body suggests
    resolved. None if no mismatch detected."""
    if gap["status"] != "open":
        return None
    # Look for an `### Status` subsection
    section = re.search(r"^###\s+Status[^\n]*\n([\s\S]*?)(?=^##|\Z)", gap["body"], re.MULTILINE)
    if section:
        st = section.group(1)
        m = RESOLVED_HINTS.search(st)
        if m:
            # Get the surrounding sentence
            idx = st.lower().find(m.group(0).lower())
            window = st[max(0, idx - 30): idx + 80].replace("\n", " ").strip()
            return f"### Status mentions \"{m.group(0)}\" — context: …{window}…"
    # Also check first 500 chars of body for a "Resolved YYYY-MM-DD" stamp
    m = re.search(r"\bResolved\s+\d{4}-\d{2}-\d{2}", gap["body"][:1000])
    if m:
        return f"body header has: {m.group(0)}"
    return None


def find_file_refs(body: str) -> list[str]:
    """Extract src/... or tests/... path references from body."""
    pat = re.compile(r"\b(?:src|tests|examples|engine|fixtures)/[\w./-]+\.(?:cs|py|ts|svelte|md|json|yaml|yml|rs|toml|sh)\b")
    seen = set()
    out = []
    for m in pat.findall(body):
        if m not in seen:
            seen.add(m)
            out.append(m)
    return out


def check_files_exist(refs: list[str]) -> list[tuple[str, bool]]:
    return [(r, (REPO / r).exists()) for r in refs]


def cluster_by_topic(gaps: list[dict]) -> dict[str, list[str]]:
    clusters: dict[str, list[str]] = defaultdict(list)
    for g in gaps:
        for token in TOPIC_TOKENS:
            if re.search(token, g["title"] + " " + g["body"], re.IGNORECASE):
                clusters[token].append(g["id"])
    return {k: v for k, v in clusters.items() if len(v) >= 2}


def main() -> int:
    gaps = [parse_gap(p) for p in sorted(GAPS.glob("G-*.md"))]
    print(f"surveyed {len(gaps)} gaps from {GAPS}")
    print()

    print("=" * 70)
    print("1. STATUS MISMATCH (frontmatter says open, body suggests resolved)")
    print("=" * 70)
    flagged = 0
    for g in gaps:
        note = find_status_mismatch(g)
        if note:
            flagged += 1
            print(f"\n  {g['id']}  status: {g['status']}")
            print(f"    title: {g['title']}")
            print(f"    note: {note}")
    if flagged == 0:
        print("\n  (none)")
    print()

    print("=" * 70)
    print("2. DEAD FILE REFERENCES (body mentions paths that don't exist)")
    print("=" * 70)
    any_dead = False
    for g in gaps:
        if g["status"] != "open":
            continue
        refs = find_file_refs(g["body"])
        if not refs:
            continue
        existence = check_files_exist(refs)
        dead = [r for r, ex in existence if not ex]
        if dead:
            any_dead = True
            print(f"\n  {g['id']}  status: {g['status']}  dead: {len(dead)}/{len(refs)}")
            print(f"    title: {g['title']}")
            for r in dead[:5]:
                print(f"      ✗ {r}")
            if len(dead) > 5:
                print(f"      ... +{len(dead) - 5} more")
    if not any_dead:
        print("\n  (none)")
    print()

    print("=" * 70)
    print("3. TOPIC CLUSTERS (≥2 gaps sharing a token)")
    print("=" * 70)
    clusters = cluster_by_topic(gaps)
    if not clusters:
        print("\n  (none)")
    else:
        for token, ids in sorted(clusters.items(), key=lambda kv: -len(kv[1])):
            print(f"\n  '{token}': {', '.join(ids)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
