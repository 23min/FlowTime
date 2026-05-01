# /// script
# requires-python = ">=3.10"
# dependencies = []
# ///
"""One-off helper: apply id-map.csv substitutions to the Current Work section
of CLAUDE.md. Reads CLAUDE.md, finds the `## Current Work` heading, applies
the same body-rewrite logic from project_epics.py to that section only, and
writes back.
"""

import csv
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
CLAUDE_MD = REPO_ROOT / "CLAUDE.md"
ID_MAP = REPO_ROOT / "work/migration/manifests/id-map.csv"


def load_id_map() -> dict[str, str]:
    out: dict[str, str] = {}
    with ID_MAP.open(newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            out[row["old_id"]] = row["new_id"]
    return out


def rewrite_text(text: str, id_map: dict[str, str]) -> str:
    fenced: list[str] = []
    def _save_fenced(m: re.Match) -> str:
        fenced.append(m.group(0))
        return f"\x00FENCED{len(fenced) - 1}\x00"
    work = re.sub(r"```[\s\S]*?```", _save_fenced, text)
    inline: list[str] = []
    def _save_inline(m: re.Match) -> str:
        inline.append(m.group(0))
        return f"\x00INLINE{len(inline) - 1}\x00"
    work = re.sub(r"`[^`\n]+`", _save_inline, work)

    for old in sorted(id_map.keys(), key=len, reverse=True):
        new = id_map[old]
        escaped = re.escape(old)
        pattern = re.compile(rf"(?<![\w-]){escaped}(?:-[a-z0-9-]+)?(?![\w-])")
        work = pattern.sub(new, work)

    for i, code in enumerate(inline):
        work = work.replace(f"\x00INLINE{i}\x00", code)
    for i, code in enumerate(fenced):
        work = work.replace(f"\x00FENCED{i}\x00", code)
    return work


def main() -> int:
    id_map = load_id_map()
    text = CLAUDE_MD.read_text(encoding="utf-8")

    # Split at the Current Work heading
    marker = "\n## Current Work\n"
    idx = text.find(marker)
    if idx < 0:
        print("CLAUDE.md: no `## Current Work` heading found", file=sys.stderr)
        return 1

    head = text[: idx + len(marker)]
    body = text[idx + len(marker) :]
    rewritten = rewrite_text(body, id_map)

    CLAUDE_MD.write_text(head + rewritten, encoding="utf-8")
    print(f"rewrote Current Work section of {CLAUDE_MD.name} (head={len(head)}b, body={len(body)}→{len(rewritten)}b)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
