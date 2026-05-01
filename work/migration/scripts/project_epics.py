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
DECISIONS_PATH = REPO_ROOT / "work/decisions.md"
GAPS_PATH = REPO_ROOT / "work/gaps.md"
OUT_PATH = REPO_ROOT / "work/migration/manifests/epics-active.yaml"
SKIP_LOG_PATH = REPO_ROOT / "work/migration/manifests/skip-log.md"
ID_MAP_PATH = REPO_ROOT / "work/migration/manifests/id-map.csv"

# Epics to project, in manifest order. Milestones are auto-discovered per epic.
# Active-dir set (Pass A–C):  E-13, E-14, E-15, E-18, E-22
# Completed-id'd generic (Pass D): E-16, E-17, E-19, E-20, E-21, E-23, E-24
# Outliers (Pass E): E-10 (m-ec-pN), E-11 (m-svui-NN), E-12 (M-10.NN)
SCOPE_EPICS = [
    "E-13", "E-14", "E-15", "E-18", "E-22",
    "E-16", "E-17", "E-19", "E-20", "E-21", "E-23", "E-24",
    "E-10", "E-11", "E-12",
]

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

V1_TO_V3_DECISION_STATUS = {
    "active": "accepted",
    "accepted": "accepted",
    "superseded": "superseded",
    "withdrawn": "rejected",
    "rejected": "rejected",
    "proposed": "proposed",
}

# Headings inside gaps.md that are NOT individual gap entries (file-level sections)
GAP_NON_ENTITY_HEADINGS = {"open questions"}


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


@dataclass
class DecisionEntity:
    old_id: str            # D-2026-03-30-001
    new_id: str            # D-001
    date: str              # 2026-03-30
    seq: int               # 1
    title: str
    status: str
    body: str


@dataclass
class GapEntity:
    new_id: str            # G-001
    title: str
    status: str
    creation_date: str     # 2026-03-24
    body: str


# ----- shared -----------------------------------------------------------------

def find_epic_dir(epic_id: str) -> Path:
    """Locate epic dir under work/epics/ or work/epics/completed/."""
    candidates: list[Path] = []
    for root in (EPICS_DIR, EPICS_DIR / "completed"):
        if not root.exists():
            continue
        candidates.extend(p for p in root.glob(f"{epic_id}-*") if p.is_dir())
    if not candidates:
        raise FileNotFoundError(f"no dir matching {epic_id}-* under {EPICS_DIR}")
    if len(candidates) > 1:
        raise ValueError(f"multiple dirs match {epic_id}-*: {candidates}")
    return candidates[0]


def is_completed_dir(epic_dir: Path) -> bool:
    """True if the epic lives under work/epics/completed/."""
    return (EPICS_DIR / "completed") in epic_dir.parents


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

def strip_yaml_frontmatter(text: str) -> tuple[str, dict[str, str]]:
    """If text starts with a `---` YAML-frontmatter block, return (rest, kv_dict).

    Otherwise return (text, {}). Only handles flat scalar fields — adequate for
    our migration sources.
    """
    if not (text.startswith("---\n") or text.startswith("---\r\n")):
        return text, {}
    # Find closing fence
    body_start = 4 if text.startswith("---\n") else 5
    end = re.search(r"^---\s*$", text[body_start:], re.MULTILINE)
    if not end:
        return text, {}
    block = text[body_start : body_start + end.start()]
    rest = text[body_start + end.end():].lstrip("\n")
    fm: dict[str, str] = {}
    for line in block.splitlines():
        if ":" in line and not line.startswith(" "):
            k, _, v = line.partition(":")
            fm[k.strip()] = v.strip()
    return rest, fm


def parse_epic_spec(epic_id: str, spec_path: Path, findings: list[str]) -> EpicEntity:
    text = spec_path.read_text(encoding="utf-8")
    text, yaml_fm = strip_yaml_frontmatter(text)

    lines = text.splitlines()
    if not lines or not lines[0].startswith("# "):
        raise ValueError(f"{epic_id}: spec missing H1 title")
    raw_title = lines[0][2:].strip()
    title = re.sub(r"^Epic:\s*", "", raw_title)
    title = re.sub(rf"^{epic_id}:?\s+", "", title)

    id_match = re.search(r"^\*\*ID:\*\*\s*(\S+)\s*$", text, re.MULTILINE)
    if id_match:
        if id_match.group(1) != epic_id:
            raise ValueError(
                f"{epic_id}: **ID:** says {id_match.group(1)!r}; expected {epic_id!r}"
            )
    elif "id" in yaml_fm:
        # YAML id may be E-NN or E-NN-slug; both acceptable as long as prefix matches
        if not yaml_fm["id"].startswith(epic_id):
            raise ValueError(
                f"{epic_id}: YAML `id: {yaml_fm['id']}` doesn't match expected prefix {epic_id!r}"
            )
    else:
        raise ValueError(
            f"{epic_id}: spec has no **ID:** line and no YAML frontmatter `id` field"
        )

    completed = is_completed_dir(spec_path.parent)
    status_match = re.search(
        r"^\*\*Status:\*\*\s*(\S+)(?:\s*\((.+?)\))?", text, re.MULTILINE
    )
    qualifier: str | None = None

    if completed:
        # Dir-location wins for terminal state. Force `done` regardless of source.
        v3_status = "done"
        if status_match:
            qualifier = status_match.group(2)  # preserve parenthesized note if any
    elif status_match:
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

# Old-id patterns across all migration sources. Order: longest/most-specific first
# so alternation in MILESTONE_OLD_ID_RE matches greedily-correctly.
_MILESTONE_ID_PATTERNS = [
    r"M-10\.\d+",            # E-12 outlier (capitalized + dotted)
    r"m-ec-p\d+[a-z]?\d?",   # E-10 outlier (phase-numbered)
    r"m-svui-\d+",           # E-11 outlier (svelte-ui)
    r"m-E\d+-\d+",           # generic m-EXX-NN
]
MILESTONE_OLD_ID_RE = re.compile("|".join(f"(?:{p})" for p in _MILESTONE_ID_PATTERNS))
MILESTONE_SPEC_RE = re.compile(
    r"^(?:" + "|".join(_MILESTONE_ID_PATTERNS) + r")-[a-zA-Z0-9.-]+\.md$"
)


def discover_milestone_specs(epic_dir: Path) -> list[Path]:
    """Return milestone spec files (excluding tracking/log/review siblings)."""
    excluded_suffixes = ("-tracking", "-log", "-review")
    return sorted(
        p
        for p in epic_dir.iterdir()
        if p.is_file()
        and MILESTONE_SPEC_RE.match(p.name)
        and not any(suffix in p.name for suffix in excluded_suffixes)
    )


def parse_decisions(path: Path, findings: list[str]) -> list[DecisionEntity]:
    """Split work/decisions.md into individual decision entities, sorted chronologically."""
    text = path.read_text(encoding="utf-8")
    heading_re = re.compile(
        r"^## D-(\d{4}-\d{2}-\d{2})-(\d+):\s*(.+)$", re.MULTILINE
    )
    matches = list(heading_re.finditer(text))
    if not matches:
        return []

    items: list[DecisionEntity] = []
    for i, m in enumerate(matches):
        date_str, seq_str, raw_title = m.group(1), m.group(2), m.group(3).strip()
        # body: from first newline after heading to next heading (or EOF)
        body_start = text.find("\n", m.end()) + 1 if text.find("\n", m.end()) >= 0 else m.end()
        body_end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        body = text[body_start:body_end].strip("\n")
        if body:
            body += "\n"
        # Status from prose Status: line
        status_match = re.search(r"^\*\*Status:\*\*\s*(\S+)", body, re.MULTILINE)
        if status_match:
            v1 = status_match.group(1).lower().rstrip(",")
            v3 = V1_TO_V3_DECISION_STATUS.get(v1)
            if v3 is None:
                findings.append(
                    f"- **D-{date_str}-{seq_str}**: unmapped decision status {v1!r}; defaulted to `accepted`."
                )
                v3 = "accepted"
        else:
            v3 = "accepted"
        items.append(
            DecisionEntity(
                old_id=f"D-{date_str}-{seq_str}",
                new_id="",  # filled after sort
                date=date_str,
                seq=int(seq_str),
                title=raw_title,
                status=v3,
                body=body,
            )
        )
    # chronological sort, then assign D-NNN
    items.sort(key=lambda d: (d.date, d.seq))
    for i, d in enumerate(items, start=1):
        d.new_id = f"D-{i:03d}"
    return items


def git_blame_h2_dates(path: Path) -> dict[int, str]:
    """Return {line_no: YYYY-MM-DD} for every `## ` line in path via `git blame --date=short`."""
    import subprocess
    out = subprocess.run(
        ["git", "blame", "--date=short", str(path.relative_to(REPO_ROOT))],
        capture_output=True, text=True, check=True, cwd=REPO_ROOT,
    ).stdout
    line_dates: dict[int, str] = {}
    blame_re = re.compile(
        r"^[\^a-f0-9]+\s+(?:\S+\s+)?\([^)]*?(\d{4}-\d{2}-\d{2})\s+(\d+)\)\s+(.*)$"
    )
    for raw in out.splitlines():
        m = blame_re.match(raw)
        if not m:
            continue
        date, line_no, content = m.group(1), int(m.group(2)), m.group(3)
        if content.startswith("## "):
            line_dates[line_no] = date
    return line_dates


def parse_gaps(path: Path, findings: list[str]) -> list[GapEntity]:
    """Split work/gaps.md into individual gap entities, sorted by git-blame creation date."""
    text = path.read_text(encoding="utf-8")
    h2_re = re.compile(r"^## (.+)$", re.MULTILINE)
    matches = list(h2_re.finditer(text))
    if not matches:
        return []

    line_dates = git_blame_h2_dates(path)

    items: list[tuple[str, GapEntity]] = []  # (sort key, entity)
    for i, m in enumerate(matches):
        raw_title = m.group(1).strip()
        if raw_title.lower() in GAP_NON_ENTITY_HEADINGS:
            continue
        line_no = text.count("\n", 0, m.start()) + 1
        creation_date = line_dates.get(line_no)
        if not creation_date:
            findings.append(
                f"- **gap @ line {line_no}**: no git-blame date found; entry skipped."
            )
            continue
        body_start = text.find("\n", m.end()) + 1 if text.find("\n", m.end()) >= 0 else m.end()
        body_end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        body = text[body_start:body_end].strip("\n")
        if body:
            body += "\n"

        # Detect resolved suffix → addressed
        resolved_match = re.search(r"\s*\(resolved (\d{4}-\d{2}-\d{2})\)\s*$", raw_title)
        if resolved_match:
            title = raw_title[: resolved_match.start()].strip()
            status = "addressed"
        else:
            title = raw_title
            status = "open"

        items.append(
            (
                creation_date + f"-L{line_no:05d}",  # stable sort key
                GapEntity(
                    new_id="",  # filled after sort
                    title=title,
                    status=status,
                    creation_date=creation_date,
                    body=body,
                ),
            )
        )

    items.sort(key=lambda kv: kv[0])
    out_items = [g for _, g in items]
    for i, g in enumerate(out_items, start=1):
        g.new_id = f"G-{i:03d}"
    return out_items


def rewrite_body_text(text: str, id_map: dict[str, str]) -> str:
    """Substitute v1 ids → v3 ids in body prose. Skip fenced code blocks and
    inline code spans (their content is historical / executable and shouldn't be
    rewritten). Match both bare-id and full-slug forms; collapse both to bare new id.

    Order substitutions by len(old) DESC so prefix collisions can't cause partial
    over-matches even though word-boundary lookarounds also guard against them.
    """
    if not id_map:
        return text

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


def derive_title_from_h1(h1: str) -> str:
    """Strip a leading `Milestone:` and any embedded milestone-id prefix from an H1
    line, returning the title."""
    h = re.sub(r"^Milestone:\s*", "", h1)
    sep_re = re.compile(rf"^(?:{'|'.join(_MILESTONE_ID_PATTERNS)})\S*\s*[:—-]\s+(.+)$")
    m = sep_re.match(h)
    return m.group(1).strip() if m else h


def parse_milestone_spec(
    spec_path: Path,
    expected_parent: str,
    findings: list[str],
) -> tuple[str, str, str, list[str], str]:
    """Return (old_id, title, v3_status, depends_on_old_ids, body)."""
    text = spec_path.read_text(encoding="utf-8")
    text, yaml_fm = strip_yaml_frontmatter(text)
    lines = text.splitlines()
    if not lines or not lines[0].startswith("# "):
        raise ValueError(f"{spec_path.name}: spec missing H1 title")
    h1 = lines[0][2:].strip()

    # Variant A: prose **ID:** line. Variant B: id embedded in H1 (`m-EXX-NN — Title`).
    # Some sources put the full slug after **ID:** (e.g. `m-E16-01-compiled-semantic-references`);
    # normalize to canonical `m-EXX-NN` via MILESTONE_OLD_ID_RE.
    old_id: str | None = None
    title: str | None = None
    id_match = re.search(r"^\*\*ID:\*\*\s*(\S+)\s*$", text, re.MULTILINE)
    if id_match:
        captured = id_match.group(1)
        m = MILESTONE_OLD_ID_RE.match(captured)
        if not m:
            raise ValueError(
                f"{spec_path.name}: **ID:** value {captured!r} doesn't begin with `m-EXX-NN`"
            )
        old_id = m.group(0)
        title = re.sub(r"^Milestone:\s*", "", h1)
    elif "id" in yaml_fm:
        # YAML frontmatter carries the id; trust that and use H1 as title.
        m = MILESTONE_OLD_ID_RE.match(yaml_fm["id"])
        if not m:
            raise ValueError(
                f"{spec_path.name}: YAML `id: {yaml_fm['id']}` doesn't begin with a known milestone-id pattern"
            )
        old_id = m.group(0)
        title = derive_title_from_h1(h1)
    else:
        h1_stripped = re.sub(r"^Milestone:\s*", "", h1)
        h1_full_re = re.compile(
            rf"^({'|'.join(_MILESTONE_ID_PATTERNS)})\S*\s*[:—-]\s+(.+)$"
        )
        m = h1_full_re.match(h1_stripped)
        if not m:
            raise ValueError(
                f"{spec_path.name}: cannot derive id — no **ID:** line, no YAML `id` field, "
                f"and H1 doesn't match `<milestone-id>[-slug] [:—-] Title`"
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

    # Dir-location override: milestones inside a completed/ epic dir → done.
    # Otherwise pull status from prose **Status:** line, then YAML frontmatter, else default.
    if is_completed_dir(spec_path.parent):
        v3_status = "done"
    else:
        status_match = re.search(r"^\*\*Status:\*\*\s*(\S+)", text, re.MULTILINE)
        v1: str | None = None
        if status_match:
            v1 = status_match.group(1).lower()
        elif "status" in yaml_fm:
            v1 = yaml_fm["status"].split()[0].lower() if yaml_fm["status"] else None
        if v1:
            if v1 not in V1_TO_V3_MILESTONE_STATUS:
                raise ValueError(
                    f"{old_id}: unmapped milestone status {v1!r} — add to V1_TO_V3_MILESTONE_STATUS"
                )
            v3_status = V1_TO_V3_MILESTONE_STATUS[v1]
        else:
            v3_status = "draft"
            findings.append(
                f"- **{old_id}**: no `**Status:**` line and no YAML `status:`; defaulted to `draft`."
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
    filename_id_re = re.compile(rf"^({'|'.join(_MILESTONE_ID_PATTERNS)})-")
    for ep, path in sorted_pairs:
        m = filename_id_re.match(path.name)
        if not m:
            continue
        old_id = m.group(1)
        id_map[old_id] = f"M-{counter:03d}"
        counter += 1
    return id_map


# ----- manifest ---------------------------------------------------------------

def build_manifest(
    epics: list[EpicEntity],
    milestones: list[MilestoneEntity],
    decisions: list[DecisionEntity],
    gaps: list[GapEntity],
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
            fm["depends_on"] = list(m.depends_on_old_ids)
        milestone_entries.append(
            {
                "kind": "milestone",
                "id": m.new_id,
                "frontmatter": fm,
                "body": LiteralScalarString(m.body),
            }
        )
    decision_entries = [
        {
            "kind": "decision",
            "id": d.new_id,
            "frontmatter": {"title": d.title, "status": d.status},
            "body": LiteralScalarString(d.body),
        }
        for d in decisions
    ]
    gap_entries = [
        {
            "kind": "gap",
            "id": g.new_id,
            "frontmatter": {"title": g.title, "status": g.status},
            "body": LiteralScalarString(g.body),
        }
        for g in gaps
    ]
    return {
        "version": 1,
        "commit": {
            "mode": "single",
            "message": (
                f"import(spike): {len(epics)} epics + {len(milestones)} milestones "
                f"+ {len(decisions)} decisions + {len(gaps)} gaps"
            ),
        },
        "entities": epic_entries + milestone_entries + decision_entries + gap_entries,
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

    # Pass 4: decisions
    decisions = parse_decisions(DECISIONS_PATH, findings) if DECISIONS_PATH.exists() else []

    # Pass 5: gaps
    gaps = parse_gaps(GAPS_PATH, findings) if GAPS_PATH.exists() else []

    # Pass G: body-text rewriting. Build full id-map (milestones + decisions),
    # then substitute old ids → new ids in every entity body (skipping fenced
    # code + inline code). Epics keep their original ids; gaps have no v1 id.
    full_id_map = dict(id_map)
    for d in decisions:
        full_id_map[d.old_id] = d.new_id
    for e in epics:
        e.body = rewrite_body_text(e.body, full_id_map)
    for m in milestones:
        m.body = rewrite_body_text(m.body, full_id_map)
    for d in decisions:
        d.body = rewrite_body_text(d.body, full_id_map)
    for g in gaps:
        g.body = rewrite_body_text(g.body, full_id_map)

    manifest = build_manifest(epics, milestones, decisions, gaps)
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    yaml = YAML()
    yaml.indent(mapping=2, sequence=4, offset=2)
    yaml.width = 120
    with OUT_PATH.open("w", encoding="utf-8") as f:
        yaml.dump(manifest, f)

    # id-map.csv (milestones + decisions; gaps have no old id)
    if id_map or decisions:
        with ID_MAP_PATH.open("w", encoding="utf-8", newline="") as f:
            w = csv.writer(f)
            w.writerow(["old_id", "new_id", "kind"])
            for old, new in sorted(id_map.items()):
                w.writerow([old, new, "milestone"])
            for d in decisions:
                w.writerow([d.old_id, d.new_id, "decision"])
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
    print(f"  decisions:  {len(decisions)} (D-001..D-{len(decisions):03d})")
    print(f"  gaps:       {len(gaps)} (G-001..G-{len(gaps):03d})")
    total = len(epics) + len(milestones) + len(decisions) + len(gaps)
    print(f"  total entities: {total}")
    if id_map or decisions:
        n = len(id_map) + len(decisions)
        print(f"\nid-map: {ID_MAP_PATH.relative_to(REPO_ROOT)} ({n} entries)")
    if findings:
        print(f"findings: {len(findings)} in {SKIP_LOG_PATH.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
