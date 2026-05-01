# /// script
# requires-python = ">=3.10"
# dependencies = ["ruamel.yaml>=0.18"]
# ///
"""Project active-set E-NN epics into a combined aiwf import manifest.

Pass B scope (no milestones): E-13, E-14, E-15, plus E-22 carried from Pass A.
E-11 and E-18 are multi-milestone — handled in later passes (C/E).

Reads:  work/epics/E-NN-*/spec.md
Emits:  work/migration/manifests/epics-active.yaml
        work/migration/manifests/skip-log.md (accumulated findings, only when non-empty)

Validate downstream:  aiwf import --dry-run work/migration/manifests/epics-active.yaml
"""

import re
import sys
from pathlib import Path

from ruamel.yaml import YAML
from ruamel.yaml.scalarstring import LiteralScalarString


REPO_ROOT = Path(__file__).resolve().parents[3]
EPICS_DIR = REPO_ROOT / "work/epics"
OUT_PATH = REPO_ROOT / "work/migration/manifests/epics-active.yaml"
SKIP_LOG_PATH = REPO_ROOT / "work/migration/manifests/skip-log.md"

PASS_B_EPICS = ["E-13", "E-14", "E-15", "E-22"]

V1_TO_V3_EPIC_STATUS = {
    "planning": "proposed",
    "proposed": "proposed",
    "paused": "active",
    "in-progress": "active",
    "in_progress": "active",
    "active": "active",
    "superseded": "cancelled",
    "absorbed": "cancelled",
    "cancelled": "cancelled",
    "complete": "done",
    "completed": "done",
    "done": "done",
}


def find_epic_dir(epic_id: str) -> Path:
    matches = sorted(EPICS_DIR.glob(f"{epic_id}-*"))
    matches = [m for m in matches if m.is_dir()]
    if not matches:
        raise FileNotFoundError(f"no dir matching {epic_id}-* under {EPICS_DIR}")
    if len(matches) > 1:
        raise ValueError(f"multiple dirs match {epic_id}-*: {matches}")
    return matches[0]


def parse_epic_spec(epic_id: str, spec_path: Path, findings: list[str]) -> dict:
    text = spec_path.read_text(encoding="utf-8")
    lines = text.splitlines()
    if not lines or not lines[0].startswith("# "):
        raise ValueError(f"{epic_id}: spec missing H1 title")
    raw_title = lines[0][2:].strip()
    title = re.sub(r"^Epic:\s*", "", raw_title)
    title = re.sub(rf"^{epic_id}\s+", "", title)

    id_match = re.search(r"^\*\*ID:\*\*\s*(\S+)\s*$", text, re.MULTILINE)
    if not id_match:
        raise ValueError(f"{epic_id}: spec missing **ID:** line")
    if id_match.group(1) != epic_id:
        raise ValueError(
            f"{epic_id}: **ID:** says {id_match.group(1)!r}; expected {epic_id!r}"
        )

    status_match = re.search(
        r"^\*\*Status:\*\*\s*(\S+)(?:\s*\((.+?)\))?",
        text,
        re.MULTILINE,
    )
    qualifier: str | None = None
    if status_match:
        v1_status = status_match.group(1).lower()
        qualifier = status_match.group(2)
        if v1_status not in V1_TO_V3_EPIC_STATUS:
            raise ValueError(
                f"{epic_id}: unmapped epic status {v1_status!r} — add to V1_TO_V3_EPIC_STATUS"
            )
        v3_status = V1_TO_V3_EPIC_STATUS[v1_status]
    else:
        v3_status = "proposed"
        findings.append(
            f"- **{epic_id}**: source spec has no `**Status:**` line; "
            f"defaulted to `proposed`. Source: `{spec_path.relative_to(REPO_ROOT)}`."
        )

    body = strip_frontmatter_prose(text)
    if qualifier:
        body = f"> **Status note:** {qualifier}\n\n{body}"

    return {"id": epic_id, "title": title, "status": v3_status, "body": body}


def strip_frontmatter_prose(text: str) -> str:
    out: list[str] = []
    skip_blank = False
    for line in text.splitlines():
        if line.startswith("# "):
            skip_blank = True
            continue
        if re.match(r"^\*\*(ID|Status):\*\*", line):
            skip_blank = True
            continue
        if skip_blank and line.strip() == "":
            skip_blank = False
            continue
        skip_blank = False
        out.append(line)
    while out and out[0].strip() == "":
        out.pop(0)
    while out and out[-1].strip() == "":
        out.pop()
    return "\n".join(out) + "\n"


def build_manifest(epics: list[dict]) -> dict:
    return {
        "version": 1,
        "commit": {
            "mode": "single",
            "message": f"import(spike): Pass B — {len(epics)} active-set epics",
        },
        "entities": [
            {
                "kind": "epic",
                "id": e["id"],
                "frontmatter": {
                    "title": e["title"],
                    "status": e["status"],
                },
                "body": LiteralScalarString(e["body"]),
            }
            for e in epics
        ],
    }


def main() -> int:
    findings: list[str] = []
    epics: list[dict] = []
    for epic_id in PASS_B_EPICS:
        spec_dir = find_epic_dir(epic_id)
        spec_path = spec_dir / "spec.md"
        if not spec_path.exists():
            findings.append(
                f"- **{epic_id}**: no `spec.md` in `{spec_dir.relative_to(REPO_ROOT)}`; skipped."
            )
            continue
        epics.append(parse_epic_spec(epic_id, spec_path, findings))

    manifest = build_manifest(epics)
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    yaml = YAML()
    yaml.indent(mapping=2, sequence=4, offset=2)
    yaml.width = 120
    with OUT_PATH.open("w", encoding="utf-8") as f:
        yaml.dump(manifest, f)

    if findings:
        header = (
            "# Migration skip-log\n\n"
            "Findings accumulated by the projector. Triage in Phase 4 dry-run loop.\n\n"
        )
        SKIP_LOG_PATH.write_text(header + "\n".join(findings) + "\n", encoding="utf-8")
    elif SKIP_LOG_PATH.exists():
        SKIP_LOG_PATH.unlink()

    print(f"wrote {OUT_PATH.relative_to(REPO_ROOT)} ({len(epics)} epics)")
    for e in epics:
        print(f"  {e['id']:6} {e['status']:10} — {e['title']}")
    if findings:
        print(f"\n{len(findings)} finding(s) in {SKIP_LOG_PATH.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
