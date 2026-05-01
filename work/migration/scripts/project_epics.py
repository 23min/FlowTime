# /// script
# requires-python = ">=3.10"
# dependencies = ["ruamel.yaml>=0.18"]
# ///
"""Project E-NN epics (and their milestones, where applicable) into a combined
aiwf import manifest.

Pass A: E-22 only (epic spike).
Pass B: E-13, E-14, E-15 added (no milestones).
Pass C: E-18 added with 11 milestones (first multi-milestone epic).

Reads:  work/epics/E-NN-*/spec.md
        work/epics/E-NN-*/m-EXX-NN-*.md  (milestone files; tracking/log siblings excluded)
Emits:  work/migration/manifests/epics-active.yaml
        work/migration/manifests/skip-log.md  (only if findings)
        work/migration/manifests/id-map.csv   (only if any milestones projected)

Validate downstream:  aiwf import --dry-run work/migration/manifests/epics-active.yaml
"""

from __future__ import annotations

import csv
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

from ruamel.yaml import YAML
from ruamel.yaml.scalarstring import LiteralScalarString


REPO_ROOT = Path(__file__).resolve().parents[3]
EPICS_DIR = REPO_ROOT / "work/epics"
OUT_PATH = REPO_ROOT / "work/migration/manifests/epics-active.yaml"
SKIP_LOG_PATH = REPO_ROOT / "work/migration/manifests/skip-log.md"
ID_MAP_PATH = REPO_ROOT / "work/migration/manifests/id-map.csv"

# Epics to project, in manifest order. Milestones are auto-discovered per epic.
SCOPE_EPICS = ["E-13", "E-14", "E-15", "E-18", "E-22"]

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

V1_TO_V3_MILESTONE_STATUS = {
    "draft": "draft",
    "pending": "draft",
    "proposed": "draft",
    "in-progress": "in_progress",
    "in_progress": "in_progress",
    "active": "in_progress",
    "complete": "done",
    "completed": "done",
    "done": "done",
    "cancelled": "cancelled",
}


@dataclass
class EpicEntity:
    epic_id: str
    title: str
    status: str
    body: str


@dataclass
class MilestoneEntity:
    old_id: str            # m-E18-01
    new_id: str            # M-001
    parent_epic: str       # E-18
    title: str
    status: str
    depends_on_old_ids: list[str] = field(default_factory=list)
    body: str = ""


# ----- shared -----------------------------------------------------------------

def find_epic_dir(epic_id: str) -> Path:
    matches = sorted(p for p in EPICS_DIR.glob(f"{epic_id}-*") if p.is_dir())
    if not matches:
        raise FileNotFoundError(f"no dir matching {epic_id}-* under {EPICS_DIR}")
    if len(matches) > 1:
        raise ValueError(f"multiple dirs match {epic_id}-*: {matches}")
    return matches[0]


def strip_frontmatter_prose(text: str, prose_keys: tuple[str, ...]) -> str:
    """Drop the H1 line and any **<key>:** prose lines (and their trailing blank)."""
    pattern = re.compile(rf"^\*\*({'|'.join(prose_keys)}):\*\*", re.IGNORECASE)
    out: list[str] = []
    skip_blank = False
    for line in text.splitlines():
        if line.startswith("# "):
            skip_blank = True
            continue
        if pattern.match(line):
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


# ----- epics ------------------------------------------------------------------

def parse_epic_spec(epic_id: str, spec_path: Path, findings: list[str]) -> EpicEntity:
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
        raise ValueError(f"{epic_id}: **ID:** says {id_match.group(1)!r}; expected {epic_id!r}")

    status_match = re.search(
        r"^\*\*Status:\*\*\s*(\S+)(?:\s*\((.+?)\))?", text, re.MULTILINE
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

    body = strip_frontmatter_prose(text, ("ID", "Status"))
    if qualifier:
        body = f"> **Status note:** {qualifier}\n\n{body}"
    return EpicEntity(epic_id=epic_id, title=title, status=v3_status, body=body)


# ----- milestones -------------------------------------------------------------

MILESTONE_SPEC_RE = re.compile(r"^m-E\d+-\d+-[a-z0-9-]+\.md$")


def discover_milestone_specs(epic_dir: Path) -> list[Path]:
    """Return milestone spec files (excluding tracking/log siblings)."""
    return sorted(
        p
        for p in epic_dir.iterdir()
        if p.is_file()
        and MILESTONE_SPEC_RE.match(p.name)
        and "-tracking" not in p.name
        and "-log" not in p.name
    )


MILESTONE_OLD_ID_RE = re.compile(r"m-E\d+-\d+")


def parse_milestone_spec(
    spec_path: Path,
    expected_parent: str,
    findings: list[str],
) -> tuple[str, str, str, list[str], str]:
    """Return (old_id, title, v3_status, depends_on_old_ids, body)."""
    text = spec_path.read_text(encoding="utf-8")
    lines = text.splitlines()
    if not lines or not lines[0].startswith("# "):
        raise ValueError(f"{spec_path.name}: spec missing H1 title")
    h1 = lines[0][2:].strip()

    # Variant A: prose **ID:** line. Variant B: id embedded in H1 (`m-EXX-NN — Title`).
    old_id: str | None = None
    title: str | None = None
    id_match = re.search(r"^\*\*ID:\*\*\s*(\S+)\s*$", text, re.MULTILINE)
    if id_match:
        old_id = id_match.group(1)
        title = re.sub(r"^Milestone:\s*", "", h1)
    else:
        m = re.match(r"^(m-E\d+-\d+)\s*[—-]\s*(.+)$", h1)
        if not m:
            raise ValueError(
                f"{spec_path.name}: cannot derive id — no **ID:** line and H1 doesn't match `m-EXX-NN — Title`"
            )
        old_id = m.group(1)
        title = m.group(2).strip()

    # Epic field (informational only; we trust dir-derived parent)
    epic_match = re.search(r"^\*\*Epic:\*\*\s*(\S+)", text, re.MULTILINE)
    if epic_match and epic_match.group(1) != expected_parent:
        findings.append(
            f"- **{old_id}**: `**Epic:**` says `{epic_match.group(1)}` but file lives "
            f"under `{expected_parent}`'s dir; trusting dir-derived parent."
        )

    # Status — first whitespace token, lowercase, mapped
    status_match = re.search(r"^\*\*Status:\*\*\s*(\S+)", text, re.MULTILINE)
    if status_match:
        v1 = status_match.group(1).lower()
        if v1 not in V1_TO_V3_MILESTONE_STATUS:
            raise ValueError(
                f"{old_id}: unmapped milestone status {v1!r} — add to V1_TO_V3_MILESTONE_STATUS"
            )
        v3_status = V1_TO_V3_MILESTONE_STATUS[v1]
    else:
        v3_status = "draft"
        findings.append(
            f"- **{old_id}**: no `**Status:**` line; defaulted to `draft`."
        )

    # Depends on — only milestone targets are projectable; epic targets dropped
    depends_on_old: list[str] = []
    deps_match = re.search(r"^\*\*Depends on:\*\*\s*(.+)$", text, re.MULTILINE)
    if deps_match:
        deps_raw = deps_match.group(1)
        for tok in MILESTONE_OLD_ID_RE.findall(deps_raw):
            depends_on_old.append(tok)
        # Detect epic targets in the same field (E-NN tokens that aren't m-EXX-NN)
        for ep in re.findall(r"\bE-\d+\b", deps_raw):
            if not re.search(rf"m-{ep[:0]}{ep}", deps_raw):  # always true; epic match
                findings.append(
                    f"- **{old_id}**: `**Depends on:**` references epic `{ep}` — "
                    f"aiwf milestone.depends_on requires milestone targets only; dropped from frontmatter (body retains the prose)."
                )
                break  # one finding per milestone is enough

    body = strip_frontmatter_prose(
        text, ("ID", "Epic", "Status", "Branch", "Depends on")
    )
    return old_id, title, v3_status, depends_on_old, body


def allocate_milestone_ids(
    discovered: list[tuple[str, Path]],  # [(epic_id, milestone_path)]
) -> dict[str, str]:
    """Compute deterministic old_id → new_id mapping.

    Order: epic-id ascending (per SCOPE_EPICS order), then milestone old-id ascending.
    """
    sorted_pairs: list[tuple[str, Path]] = []
    for epic_id in SCOPE_EPICS:
        for ep, path in discovered:
            if ep == epic_id:
                sorted_pairs.append((ep, path))
    # Within each epic, sort by old-id (which is embedded in filename or extractable).
    # Filename pattern: m-EXX-NN-slug.md — sorts naturally by NN since EXX is constant per epic.
    sorted_pairs.sort(key=lambda p: (SCOPE_EPICS.index(p[0]), p[1].name))

    id_map: dict[str, str] = {}
    counter = 1
    for ep, path in sorted_pairs:
        # extract old_id from filename
        m = re.match(r"^(m-E\d+-\d+)-", path.name)
        if not m:
            continue
        old_id = m.group(1)
        id_map[old_id] = f"M-{counter:03d}"
        counter += 1
    return id_map


# ----- manifest ---------------------------------------------------------------

def build_manifest(
    epics: list[EpicEntity], milestones: list[MilestoneEntity]
) -> dict:
    epic_entries = [
        {
            "kind": "epic",
            "id": e.epic_id,
            "frontmatter": {"title": e.title, "status": e.status},
            "body": LiteralScalarString(e.body),
        }
        for e in epics
    ]
    milestone_entries = []
    for m in milestones:
        fm: dict[str, object] = {
            "title": m.title,
            "status": m.status,
            "parent": m.parent_epic,
        }
        if m.depends_on_old_ids:
            fm["depends_on"] = list(m.depends_on_old_ids)  # placeholder; replaced after id-map
        milestone_entries.append(
            {
                "kind": "milestone",
                "id": m.new_id,
                "frontmatter": fm,
                "body": LiteralScalarString(m.body),
            }
        )
    return {
        "version": 1,
        "commit": {
            "mode": "single",
            "message": f"import(spike): {len(epics)} epics + {len(milestones)} milestones",
        },
        "entities": epic_entries + milestone_entries,
    }


def main() -> int:
    findings: list[str] = []
    epics: list[EpicEntity] = []

    # Pass 1: epics
    for epic_id in SCOPE_EPICS:
        epic_dir = find_epic_dir(epic_id)
        spec_path = epic_dir / "spec.md"
        if not spec_path.exists():
            findings.append(
                f"- **{epic_id}**: no `spec.md` in `{epic_dir.relative_to(REPO_ROOT)}`; epic skipped."
            )
            continue
        epics.append(parse_epic_spec(epic_id, spec_path, findings))

    # Pass 2: discover all milestone files across in-scope epics
    discovered: list[tuple[str, Path]] = []  # [(epic_id, milestone_path)]
    for epic_id in SCOPE_EPICS:
        try:
            epic_dir = find_epic_dir(epic_id)
        except FileNotFoundError:
            continue
        for ms_path in discover_milestone_specs(epic_dir):
            discovered.append((epic_id, ms_path))

    id_map = allocate_milestone_ids(discovered)

    # Pass 3: parse milestones; resolve depends_on via id_map
    milestones: list[MilestoneEntity] = []
    for epic_id, ms_path in discovered:
        old_id, title, v3_status, depends_on_old, body = parse_milestone_spec(
            ms_path, epic_id, findings
        )
        new_id = id_map[old_id]
        # Resolve depends_on: keep only targets in id_map (in-scope milestones)
        deps_resolved = []
        for old_dep in depends_on_old:
            if old_dep in id_map:
                deps_resolved.append(id_map[old_dep])
            else:
                findings.append(
                    f"- **{old_id}**: `depends_on: {old_dep}` is out of migration scope; dropped from frontmatter."
                )
        milestones.append(
            MilestoneEntity(
                old_id=old_id,
                new_id=new_id,
                parent_epic=epic_id,
                title=title,
                status=v3_status,
                depends_on_old_ids=deps_resolved,  # already mapped to new ids
                body=body,
            )
        )

    manifest = build_manifest(epics, milestones)
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    yaml = YAML()
    yaml.indent(mapping=2, sequence=4, offset=2)
    yaml.width = 120
    with OUT_PATH.open("w", encoding="utf-8") as f:
        yaml.dump(manifest, f)

    # id-map.csv
    if id_map:
        with ID_MAP_PATH.open("w", encoding="utf-8", newline="") as f:
            w = csv.writer(f)
            w.writerow(["old_id", "new_id", "kind"])
            for old, new in sorted(id_map.items()):
                w.writerow([old, new, "milestone"])
    elif ID_MAP_PATH.exists():
        ID_MAP_PATH.unlink()

    # skip-log
    if findings:
        header = (
            "# Migration skip-log\n\n"
            "Findings accumulated by the projector. Triage in Phase 4 dry-run loop.\n\n"
        )
        SKIP_LOG_PATH.write_text(header + "\n".join(findings) + "\n", encoding="utf-8")
    elif SKIP_LOG_PATH.exists():
        SKIP_LOG_PATH.unlink()

    print(f"wrote {OUT_PATH.relative_to(REPO_ROOT)}")
    print(f"  epics:      {len(epics)}")
    for e in epics:
        ms_count = sum(1 for m in milestones if m.parent_epic == e.epic_id)
        print(f"    {e.epic_id:6} {e.status:10} — {e.title}  [{ms_count} milestones]")
    print(f"  milestones: {len(milestones)}")
    for m in milestones:
        deps = f" ← {','.join(m.depends_on_old_ids)}" if m.depends_on_old_ids else ""
        print(f"    {m.new_id} ({m.old_id}) {m.status:12} parent={m.parent_epic} — {m.title}{deps}")
    if id_map:
        print(f"\nid-map: {ID_MAP_PATH.relative_to(REPO_ROOT)} ({len(id_map)} entries)")
    if findings:
        print(f"findings: {len(findings)} in {SKIP_LOG_PATH.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
