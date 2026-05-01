# /// script
# requires-python = ">=3.10"
# dependencies = ["ruamel.yaml>=0.18"]
# ///
"""Lift v1 AC checkbox lists in milestone bodies into I2 structured form.

For each work/epics/E-NN-<slug>/M-NNN-<slug>.md file:

1. Read YAML frontmatter + body.
2. Locate the body section whose H2 heading is "Acceptance Criteria" or
   "Acceptance criteria" (case-insensitive).
3. Parse list items (each `- [ ]` or `- [x]` line + indented continuation).
4. Build `acs:` frontmatter list:
     - id: AC-N (sequential)
     - title: first line of list item (joined if multi-line, normalized
              whitespace)
     - status: for milestones with `status: done` → all ACs `met` (avoids
               milestone-done-incomplete-acs finding); else `[x]` → met,
               `[ ]` → open
5. Rewrite body section: replace the checkbox list with `### AC-N — Title`
   headings followed by the original item content (preserves details).
6. Write file back.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

from ruamel.yaml import YAML
from ruamel.yaml.scalarstring import LiteralScalarString


REPO_ROOT = Path(__file__).resolve().parents[3]
EPICS_DIR = REPO_ROOT / "work/epics"

AC_HEADING_RE = re.compile(r"^##\s+Acceptance\s+[Cc]riteria\s*$", re.MULTILINE)
NEXT_H2_RE = re.compile(r"^##\s+", re.MULTILINE)
# Three list-item shapes seen in v1 specs:
#   - [ ] / - [x] checkbox bullet  → status from box
#   - 1./2./… numbered list         → status inferred from milestone status
#   - plain `- <content>` bullet    → status inferred from milestone status
CHECKBOX_ITEM_RE = re.compile(r"^-\s+\[(?P<box>[ xX])\]\s+(?P<rest>.+)$")
NUMBERED_ITEM_RE = re.compile(r"^\d+\.\s+(?P<rest>.+)$")
PLAIN_BULLET_RE = re.compile(r"^-\s+(?P<rest>.+)$")
H3_SUBSECTION_RE = re.compile(r"^###\s+")
# Fourth shape: H3 subsection IS the AC. Format: `### AC1 — Title` or `### AC-1: Title`
H3_AC_RE = re.compile(r"^###\s+AC-?(?P<num>\d+)\s*[—:-]?\s*(?P<rest>.*)$", re.MULTILINE)


def load_milestone_file(path: Path) -> tuple[dict, str]:
    """Return (frontmatter_dict, body_text)."""
    text = path.read_text(encoding="utf-8")
    if not (text.startswith("---\n") or text.startswith("---\r\n")):
        raise ValueError(f"{path}: no YAML frontmatter")
    body_start = 4 if text.startswith("---\n") else 5
    end = re.search(r"^---\s*$", text[body_start:], re.MULTILINE)
    if not end:
        raise ValueError(f"{path}: unterminated YAML frontmatter")
    fm_text = text[body_start : body_start + end.start()]
    body = text[body_start + end.end() :].lstrip("\n")
    yaml = YAML()
    fm = yaml.load(fm_text) or {}
    return fm, body


def write_milestone_file(path: Path, fm: dict, body: str) -> None:
    yaml = YAML()
    yaml.indent(mapping=2, sequence=4, offset=2)
    yaml.width = 120
    import io
    buf = io.StringIO()
    yaml.dump(fm, buf)
    out = "---\n" + buf.getvalue() + "---\n\n" + body.lstrip("\n")
    path.write_text(out, encoding="utf-8")


def parse_ac_list_items(section_body: str) -> list[tuple[str | None, list[str]]]:
    """Return [(checkbox_or_None, item_lines), ...] — list-style parsing.

    Used when the AC section is a list (checkbox / numbered / plain bullet).
    Stops at the first non-list, non-indented, non-blank line that isn't an
    H3 subsection separator. NOT used for H3-AC-style sections (see
    `parse_ac_h3_items`).
    """
    items: list[tuple[str | None, list[str]]] = []
    current_box: str | None = None
    current_lines: list[str] = []
    in_item = False

    def _flush() -> None:
        nonlocal in_item
        if in_item:
            box = current_box if current_box != "" else None
            items.append((box, current_lines.copy()))
        in_item = False

    for line in section_body.splitlines():
        m_cb = CHECKBOX_ITEM_RE.match(line)
        m_num = NUMBERED_ITEM_RE.match(line)
        m_plain = PLAIN_BULLET_RE.match(line)
        if m_cb:
            _flush()
            current_box = m_cb.group("box")
            current_lines = [m_cb.group("rest")]
            in_item = True
        elif m_num:
            _flush()
            current_box = ""
            current_lines = [m_num.group("rest")]
            in_item = True
        elif m_plain and not m_cb:
            _flush()
            current_box = ""
            current_lines = [m_plain.group("rest")]
            in_item = True
        elif in_item and (line.startswith("  ") or line.startswith("\t")):
            current_lines.append(line.lstrip())
        elif line.strip() == "":
            continue
        elif H3_SUBSECTION_RE.match(line):
            # group heading inside section: flush, keep parsing
            _flush()
            current_box = None
            current_lines = []
        else:
            _flush()
            current_box = None
            current_lines = []
            break
    _flush()
    return items


def parse_ac_h3_items(section_body: str) -> list[tuple[None, list[str]]]:
    """H3-AC-style: each `### AC-N — Title` heading IS an AC. Returns one
    entry per H3-AC heading; status is inferred from milestone status."""
    items: list[tuple[None, list[str]]] = []
    for m in H3_AC_RE.finditer(section_body):
        title = m.group("rest").strip() or f"AC-{m.group('num')}"
        items.append((None, [title]))
    return items


def normalize_title(item_lines: list[str]) -> str:
    """Build a single-line title from a multi-line list item."""
    text = " ".join(line.strip() for line in item_lines if line.strip())
    text = re.sub(r"\s+", " ", text).strip()
    return text


def build_ac_entries(items: list[tuple[str | None, list[str]]], milestone_status: str) -> list[dict]:
    acs: list[dict] = []
    for i, (box, lines) in enumerate(items, start=1):
        title = normalize_title(lines)
        # Numbered-list items have no checkbox signal; defer to milestone status.
        if milestone_status == "done":
            status = "met"
        elif box is None:
            # numbered list, no per-item signal: default open for non-done milestones
            status = "open"
        elif box.lower() == "x":
            status = "met"
        else:
            status = "open"
        acs.append({"id": f"AC-{i}", "title": title, "status": status})
    return acs


def render_ac_body_section_list_mode(acs: list[dict], items: list[tuple[str | None, list[str]]]) -> str:
    """Render the new body section for list-style milestones: replace the v1
    list with `### AC-N — Title` blocks. Each block carries the original
    multi-line item content as body prose."""
    out = ["## Acceptance criteria", ""]
    for (ac, (_box, lines)) in zip(acs, items):
        out.append(f"### {ac['id']} — {ac['title']}")
        out.append("")
        if len(lines) > 1:
            for line in lines:
                out.append(line)
            out.append("")
    return "\n".join(out).rstrip() + "\n"


H3_AC_NORMALIZE_RE = re.compile(
    r"^###\s+AC-?(?P<num>\d+)(?P<sep>[\s—:-]+)?(?P<rest>.*)$",
    re.MULTILINE,
)


def normalize_h3_ac_headings(section_body: str) -> str:
    """For H3-AC-style milestones: rewrite `### AC1 — Title` / `### AC-1: Title`
    to canonical `### AC-1 — Title` so the kernel's body-coherence check pairs
    by id correctly. Body prose under each heading is left untouched."""
    def _sub(m: re.Match) -> str:
        rest = (m.group("rest") or "").strip()
        if rest:
            return f"### AC-{m.group('num')} — {rest}"
        return f"### AC-{m.group('num')}"
    return H3_AC_NORMALIZE_RE.sub(_sub, section_body)


def lift_one_file(path: Path, dry_run: bool = False) -> dict:
    """Returns a result dict: {path, ac_count, action, error}."""
    try:
        fm, body = load_milestone_file(path)
    except Exception as e:
        return {"path": str(path.relative_to(REPO_ROOT)), "action": "error", "error": str(e)}

    if "acs" in fm:
        return {"path": str(path.relative_to(REPO_ROOT)), "action": "skip-already-lifted"}

    m = AC_HEADING_RE.search(body)
    if not m:
        return {"path": str(path.relative_to(REPO_ROOT)), "action": "skip-no-ac-section"}

    section_start = m.end()
    next_h2 = NEXT_H2_RE.search(body, section_start)
    section_end = next_h2.start() if next_h2 else len(body)

    section_body = body[section_start:section_end]

    # Detect H3-AC-style: any `### AC<N>` heading in the AC section
    h3_ac_present = bool(re.search(r"^###\s+AC-?\d+", section_body, re.MULTILINE))

    if h3_ac_present:
        items = parse_ac_h3_items(section_body)
    else:
        items = parse_ac_list_items(section_body)
    if not items:
        return {"path": str(path.relative_to(REPO_ROOT)), "action": "skip-no-list-items"}

    milestone_status = fm.get("status", "draft")
    acs = build_ac_entries(items, milestone_status)
    fm["acs"] = acs

    if h3_ac_present:
        # Mode B: preserve rich body prose; just normalize H3 AC headings.
        new_section = "## Acceptance criteria\n" + normalize_h3_ac_headings(section_body)
    else:
        # Mode A: replace v1 list with ### AC-N — Title blocks.
        new_section = render_ac_body_section_list_mode(acs, items)

    new_body = body[:m.start()] + new_section + body[section_end:]
    if not new_body.endswith("\n"):
        new_body += "\n"

    if not dry_run:
        write_milestone_file(path, fm, new_body)

    return {
        "path": str(path.relative_to(REPO_ROOT)),
        "action": "lifted",
        "ac_count": len(acs),
        "status_dist": {s: sum(1 for a in acs if a["status"] == s) for s in {"met", "open", "deferred", "cancelled"} if any(a["status"] == s for a in acs)},
    }


def main() -> int:
    dry_run = "--dry-run" in sys.argv
    results = []
    for milestone_path in sorted(EPICS_DIR.glob("E-*/M-*.md")):
        results.append(lift_one_file(milestone_path, dry_run=dry_run))

    actions: dict[str, int] = {}
    total_acs = 0
    for r in results:
        actions[r["action"]] = actions.get(r["action"], 0) + 1
        total_acs += r.get("ac_count", 0)

    print(f"{'DRY-RUN: ' if dry_run else ''}processed {len(results)} milestone files")
    for action, count in sorted(actions.items()):
        print(f"  {action}: {count}")
    print(f"  total ACs lifted: {total_acs}")

    errors = [r for r in results if r["action"] == "error"]
    if errors:
        print(f"\nErrors:")
        for r in errors:
            print(f"  {r['path']}: {r['error']}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
