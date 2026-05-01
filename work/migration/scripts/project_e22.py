# /// script
# requires-python = ">=3.10"
# dependencies = ["ruamel.yaml>=0.18"]
# ///
"""Pass A spike — project E-22 epic into a single-entity aiwf import manifest.

Reads:  work/epics/E-22-model-fit-chunked-evaluation/spec.md
Emits:  work/migration/manifests/e22-spike.yaml

Validate downstream with:  aiwf import --dry-run work/migration/manifests/e22-spike.yaml
"""

import re
import sys
from pathlib import Path

from ruamel.yaml import YAML
from ruamel.yaml.scalarstring import LiteralScalarString


REPO_ROOT = Path(__file__).resolve().parents[3]
SPEC_PATH = REPO_ROOT / "work/epics/E-22-model-fit-chunked-evaluation/spec.md"
OUT_PATH = REPO_ROOT / "work/migration/manifests/e22-spike.yaml"

V1_TO_V3_EPIC_STATUS = {
    "planning": "proposed",
}


def parse_epic_spec(text: str) -> dict:
    lines = text.splitlines()
    if not lines or not lines[0].startswith("# "):
        raise ValueError("spec missing H1 title")
    raw_title = lines[0][2:].strip()
    title = re.sub(r"^Epic:\s*", "", raw_title)

    id_match = re.search(r"^\*\*ID:\*\*\s*(\S+)\s*$", text, re.MULTILINE)
    status_match = re.search(r"^\*\*Status:\*\*\s*(\S+)\s*$", text, re.MULTILINE)
    if not id_match:
        raise ValueError("spec missing **ID:** line")
    if not status_match:
        raise ValueError("spec missing **Status:** line")

    epic_id = id_match.group(1)
    v1_status = status_match.group(1)
    if v1_status not in V1_TO_V3_EPIC_STATUS:
        raise ValueError(
            f"unmapped epic status {v1_status!r} — add to V1_TO_V3_EPIC_STATUS"
        )
    status = V1_TO_V3_EPIC_STATUS[v1_status]

    body = strip_frontmatter_prose(text)

    return {"id": epic_id, "title": title, "status": status, "body": body}


def strip_frontmatter_prose(text: str) -> str:
    """Remove the H1 title, **ID:**, and **Status:** lines plus surrounding blanks.

    Everything else (## Goal, ## Context, etc.) is body content for aiwf.
    """
    out_lines: list[str] = []
    skip_next_blank = False
    for line in text.splitlines():
        if line.startswith("# "):
            skip_next_blank = True
            continue
        if re.match(r"^\*\*(ID|Status):\*\*", line):
            skip_next_blank = True
            continue
        if skip_next_blank and line.strip() == "":
            skip_next_blank = False
            continue
        skip_next_blank = False
        out_lines.append(line)
    while out_lines and out_lines[0].strip() == "":
        out_lines.pop(0)
    while out_lines and out_lines[-1].strip() == "":
        out_lines.pop()
    return "\n".join(out_lines) + "\n"


def build_manifest(epic: dict) -> dict:
    return {
        "version": 1,
        "commit": {
            "mode": "single",
            "message": f"import(spike): {epic['id']} — Pass A projector dry-run",
        },
        "entities": [
            {
                "kind": "epic",
                "id": epic["id"],
                "frontmatter": {
                    "title": epic["title"],
                    "status": epic["status"],
                },
                "body": LiteralScalarString(epic["body"]),
            }
        ],
    }


def main() -> int:
    spec_text = SPEC_PATH.read_text(encoding="utf-8")
    epic = parse_epic_spec(spec_text)
    manifest = build_manifest(epic)

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    yaml = YAML()
    yaml.indent(mapping=2, sequence=4, offset=2)
    yaml.width = 120
    with OUT_PATH.open("w", encoding="utf-8") as f:
        yaml.dump(manifest, f)

    print(f"wrote {OUT_PATH.relative_to(REPO_ROOT)}")
    print(f"  kind:   epic")
    print(f"  id:     {epic['id']}")
    print(f"  title:  {epic['title']}")
    print(f"  status: {epic['status']} (v1: {V1_TO_V3_EPIC_STATUS_REVERSED().get(epic['status'], '?')})")
    print(f"  body:   {len(epic['body'].splitlines())} lines")
    return 0


def V1_TO_V3_EPIC_STATUS_REVERSED() -> dict:
    return {v: k for k, v in V1_TO_V3_EPIC_STATUS.items()}


if __name__ == "__main__":
    sys.exit(main())
