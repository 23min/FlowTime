# /// script
# requires-python = ">=3.10"
# dependencies = []
# ///
"""Per-gap archeology report.

For each work/gaps/G-NNN-*.md:
1. git-blame the gap's H2 line in the deleted work/gaps.md (last-pre-delete
   revision 98deaa2) → creation date + author.
2. Parse body for discovery signals:
     "Surfaced during <X>" / "Discovered during <X>" / "Discovered <date>"
     first M-NNN reference, first m-EXX-NN reference, first E-NN reference.
3. Map any v1 m-EXX-NN reference through id-map.csv to its v3 M-NNN.
4. Print a tab-separated report.

Output is printed; no files are mutated. Review the report; for high-
confidence rows, add `discovered_in: <id>` to the gap's frontmatter.
"""

from __future__ import annotations

import csv
import re
import subprocess
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
GAPS = REPO / "work/gaps"
ID_MAP_PATH = REPO / "work/migration/manifests/id-map.csv"
GAPS_MD_PRE_DELETE = "98deaa2"  # commit before Phase 5 Group B step 1 deleted gaps.md


def load_id_map() -> dict[str, str]:
    out: dict[str, str] = {}
    with ID_MAP_PATH.open(newline="") as f:
        for row in csv.DictReader(f):
            out[row["old_id"]] = row["new_id"]
    return out


def blame_h2_line(rev: str, gaps_md: str = "work/gaps.md") -> dict[str, tuple[str, str]]:
    """Return {h2_text: (date, author)} for every '## ' line at `rev`."""
    out = subprocess.run(
        ["git", "blame", "--date=short", rev, "--", gaps_md],
        cwd=REPO, capture_output=True, text=True, check=False,
    ).stdout
    blame_re = re.compile(
        r"^\^?[a-f0-9]+\s+(?:\S+\s+)?\(\s*(?P<author>.+?)\s+(?P<date>\d{4}-\d{2}-\d{2})\s+\d+\)\s+(?P<line>.*)$"
    )
    out_map: dict[str, tuple[str, str]] = {}
    for raw in out.splitlines():
        m = blame_re.match(raw)
        if not m:
            continue
        line = m.group("line")
        if not line.startswith("## "):
            continue
        out_map[line[3:].strip()] = (m.group("date"), m.group("author").strip())
    return out_map


def load_gap(path: Path) -> tuple[dict, str]:
    text = path.read_text(encoding="utf-8")
    end = re.search(r"^---\s*$", text[4:], re.MULTILINE)
    if not end:
        return {}, text
    fm_text = text[4:4 + end.start()]
    body = text[4 + end.end():]
    fm = {}
    for line in fm_text.splitlines():
        if ":" in line and not line.startswith(" "):
            k, _, v = line.partition(":")
            fm[k.strip()] = v.strip().strip("'\"")
    return fm, body


def find_discovery_signal(body: str, id_map: dict[str, str]) -> tuple[str, str]:
    """Return (signal_text, proposed_discovered_in_id) — id may be empty."""
    # 1. Explicit "Surfaced/Discovered during X"
    pat = re.compile(
        r"\b(?:[Ss]urfaced|[Dd]iscovered)\b[^\n]{0,200}?\b(M-\d{3}|E-\d{2}|m-E\d+-\d+|m-svui-\d+|m-ec-p\d+[a-z]?\d?|M-10\.\d+)",
        re.MULTILINE,
    )
    m = pat.search(body)
    if m:
        old = m.group(1)
        new = id_map.get(old, old) if old.startswith(("m-", "M-10.")) else old
        return (m.group(0).strip(), new)

    # 2. First "during M-NNN" / "during m-EXX-NN" / "during E-NN"
    pat2 = re.compile(
        r"\bduring\b[^\n]{0,80}?\b(M-\d{3}|E-\d{2}|m-E\d+-\d+|m-svui-\d+|m-ec-p\d+[a-z]?\d?|M-10\.\d+)",
    )
    m = pat2.search(body)
    if m:
        old = m.group(1)
        new = id_map.get(old, old) if old.startswith(("m-", "M-10.")) else old
        return (m.group(0).strip(), new)

    # 3. First milestone or epic reference anywhere in body
    pat3 = re.compile(
        r"\b(M-\d{3}|m-E\d+-\d+|m-svui-\d+|m-ec-p\d+[a-z]?\d?|M-10\.\d+|E-\d{2})\b",
    )
    m = pat3.search(body)
    if m:
        old = m.group(1)
        new = id_map.get(old, old) if old.startswith(("m-", "M-10.")) else old
        return (f"first-mention: {m.group(0)}", new)

    return ("", "")


def main() -> int:
    id_map = load_id_map()
    blame = blame_h2_line(GAPS_MD_PRE_DELETE)

    print(f"{'gap':<6} {'created':<12} {'author':<20} {'proposed':<10} signal")
    print("-" * 110)

    for path in sorted(GAPS.glob("G-*.md")):
        fm, body = load_gap(path)
        gap_id = fm.get("id", "?")
        title = fm.get("title", "")

        # Strip backticks for blame match (v1 had backticks in titles)
        clean_title = title.strip("'\"")
        # The v1 H2 may have had "(resolved YYYY-MM-DD)" suffix our lift stripped.
        # Try exact match first; if no hit, try prefix match.
        date, author = "?", "?"
        if clean_title in blame:
            date, author = blame[clean_title]
        else:
            for h2_text, (d, a) in blame.items():
                # strip resolved suffix
                stripped = re.sub(r"\s*\(resolved \d{4}-\d{2}-\d{2}\)\s*$", "", h2_text)
                if stripped == clean_title:
                    date, author = d, a
                    break

        signal, proposed = find_discovery_signal(body, id_map)
        # collapse whitespace in signal for display
        signal_disp = re.sub(r"\s+", " ", signal)[:60]
        print(f"{gap_id:<6} {date:<12} {author:<20} {proposed:<10} {signal_disp}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
