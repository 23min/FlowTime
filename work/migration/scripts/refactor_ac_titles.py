# /// script
# requires-python = ">=3.10"
# dependencies = ["ruamel.yaml>=0.18"]
# ///
"""Refactor prose-y AC titles → short labels; push full content into body prose.

Aiwf G20 (commit 4b13a0f) added an `acs-title-prose` warning that flags AC
titles that are >80 chars, contain markdown formatting (`**`, `__`, backticks),
contain link brackets, contain newlines, or look multi-sentence. Our initial
lift used the full AC content as the title, which violates G20 across 533 of
588 ACs.

This refactor:

1. For each AC in each milestone frontmatter, generate a short label:
   - Strip leading `AC-N:` / `AC-N.` prefix.
   - Strip markdown formatting (`**foo**` → `foo`, `` `bar` `` → `bar`,
     `[link](url)` → `link`).
   - Prefer "label.full-detail" or "label: full-detail" patterns: take
     the head phrase before the first period or colon if it makes a
     reasonable label.
   - Otherwise truncate at 70 chars at word boundary.
   - Strip trailing punctuation; ensure single line, no markdown.
2. Update frontmatter `acs[].title` to the new short label.
3. Update the body `### AC-N — <old long title>` heading to `### AC-N — <label>`.
4. Append the ORIGINAL full content as body prose immediately under the heading
   (so detail isn't lost).

Idempotent: if frontmatter title is already short (passes IsProseyTitle), no
changes to that AC.
"""

from __future__ import annotations

import io
import re
import sys
from pathlib import Path

from ruamel.yaml import YAML
from ruamel.yaml.scalarstring import LiteralScalarString


REPO_ROOT = Path(__file__).resolve().parents[3]
EPICS_DIR = REPO_ROOT / "work/epics"

# G20 prose detector (mirrors aiwf's IsProseyTitle):
#   length > 80 / newline / **/__/`/](/ multi-sentence
PROSE_LEN_THRESHOLD = 80
MARKDOWN_RE = re.compile(r"\*\*|__|`|\]\(")
MULTI_SENTENCE_RE = re.compile(r"[.!?]\s+[A-Z]")


def is_prose_y(title: str) -> bool:
    # aiwf's detector uses Go `len()` which counts BYTES, not runes. Match that
    # so multi-byte chars (em-dashes, smart quotes) trip the threshold.
    if len(title.encode("utf-8")) > PROSE_LEN_THRESHOLD:
        return True
    if "\n" in title:
        return True
    if MARKDOWN_RE.search(title):
        return True
    if MULTI_SENTENCE_RE.search(title):
        return True
    return False


def strip_markdown(s: str) -> str:
    """Remove inline markdown formatting from a string."""
    s = re.sub(r"\*\*([^*]+)\*\*", r"\1", s)         # bold
    s = re.sub(r"__([^_]+)__", r"\1", s)              # bold/under
    s = re.sub(r"\*([^*]+)\*", r"\1", s)              # italic
    s = re.sub(r"`([^`]+)`", r"\1", s)                # inline code
    s = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", s)   # markdown link
    return s


def truncate_at_word(s: str, limit: int) -> str:
    """Truncate to byte-length `limit` at word boundary."""
    if len(s.encode("utf-8")) <= limit:
        return s
    # Walk back from the rune boundary closest to `limit` bytes
    encoded = s.encode("utf-8")[:limit]
    # Decode dropping any partial multi-byte trailer
    cut = encoded.decode("utf-8", errors="ignore")
    cut = cut.rsplit(" ", 1)[0]
    return cut.rstrip(",;:.- ")


def derive_label(original_title: str) -> str:
    """Derive a short, kernel-safe label from a prose-y AC title."""
    s = original_title

    # Strip leading `AC-N:` / `AC-N.` / `AC-N -- ` prefix
    s = re.sub(r"^AC-?\d+\s*[:.\-—]+\s*", "", s)

    s = strip_markdown(s)
    # collapse whitespace
    s = re.sub(r"\s+", " ", s).strip()

    # Try the "label. detail" pattern: if there's a period before char 70,
    # the head before the period is often the label.
    period = s.find(". ")
    if 0 < period <= 70:
        candidate = s[:period].rstrip(",;:- ")
        if candidate and not is_prose_y(candidate):
            return candidate

    # Try "label: detail"
    colon = s.find(": ")
    if 0 < colon <= 70:
        candidate = s[:colon].rstrip(",;-—. ")
        if candidate and not is_prose_y(candidate):
            return candidate

    # Try "label — detail"
    em_dash = s.find(" — ")
    if 0 < em_dash <= 70:
        candidate = s[:em_dash].rstrip(",;:-. ")
        if candidate and not is_prose_y(candidate):
            return candidate

    # Otherwise truncate at 70 chars at word boundary
    candidate = truncate_at_word(s, 70)
    if not is_prose_y(candidate):
        return candidate

    # Final fallback: hard-truncate at 70 chars + strip multi-sentence
    s = re.sub(r"([.!?]).*", r"\1", s)  # keep only first sentence
    s = strip_markdown(s)
    return truncate_at_word(s, 70).rstrip(".") or "<unlabeled>"


# ----- file IO (shared with lift_acs.py shape) ---------------------------

def load_milestone_file(path: Path) -> tuple[dict, str]:
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
    buf = io.StringIO()
    yaml.dump(fm, buf)
    out = "---\n" + buf.getvalue() + "---\n\n" + body.lstrip("\n")
    path.write_text(out, encoding="utf-8")


# ----- body editing ------------------------------------------------------

AC_HEADING_LINE_RE_TPL = r"^(### AC-{n})( —[^\n]*)?$"


def update_body_for_ac(body: str, ac_num: int, label: str, original_title: str) -> str:
    """Replace `### AC-N — <anything>` with `### AC-N — <label>` and append the
    original_title as body prose immediately after, if not already present."""
    pattern = re.compile(AC_HEADING_LINE_RE_TPL.format(n=ac_num), re.MULTILINE)
    new_heading = f"### AC-{ac_num} — {label}"

    m = pattern.search(body)
    if not m:
        return body  # heading not found; leave body untouched

    # Locate the section under this heading (until next H3/H2).
    head_end = m.end()
    after = body[head_end:]
    rest = after.lstrip("\n")
    pre_lstrip = len(after) - len(rest)
    next_h_match = re.search(r"^### |^## ", rest, re.MULTILINE)
    next_h_pos = next_h_match.start() if next_h_match else len(rest)
    section_after = rest[:next_h_pos].rstrip("\n")

    # Decide whether we need to inject the original title as prose.
    # If the section already has any non-trivial prose, the detail is
    # already in the body — just rewrite the heading. Otherwise inject the
    # original_title as a prose line so the AC's content isn't lost.
    if section_after.strip():
        prose_block = f"\n\n{section_after}\n"
    else:
        prose_block = f"\n\n{original_title.strip()}\n"

    new_body = body[: m.start()] + new_heading + prose_block + body[head_end + pre_lstrip + next_h_pos:]
    return new_body


def refactor_one_file(path: Path, dry_run: bool = False) -> dict:
    try:
        fm, body = load_milestone_file(path)
    except Exception as e:
        return {"path": str(path.relative_to(REPO_ROOT)), "action": "error", "error": str(e)}

    acs = fm.get("acs")
    if not acs:
        return {"path": str(path.relative_to(REPO_ROOT)), "action": "skip-no-acs"}

    refactored = 0
    for ac in acs:
        title = ac.get("title", "")
        if not is_prose_y(title):
            continue
        # Derive new label
        label = derive_label(title)
        # Body update: replace heading + ensure original_title is in body prose
        ac_num_match = re.match(r"AC-(\d+)$", ac["id"])
        if not ac_num_match:
            continue
        n = int(ac_num_match.group(1))
        body = update_body_for_ac(body, n, label, title)
        ac["title"] = label
        refactored += 1

    if refactored == 0:
        return {"path": str(path.relative_to(REPO_ROOT)), "action": "skip-all-clean"}

    if not dry_run:
        write_milestone_file(path, fm, body)

    return {
        "path": str(path.relative_to(REPO_ROOT)),
        "action": "refactored",
        "ac_count": refactored,
    }


def main() -> int:
    dry_run = "--dry-run" in sys.argv
    results = []
    for milestone_path in sorted(EPICS_DIR.glob("E-*/M-*.md")):
        results.append(refactor_one_file(milestone_path, dry_run=dry_run))

    actions: dict[str, int] = {}
    total = 0
    for r in results:
        actions[r["action"]] = actions.get(r["action"], 0) + 1
        total += r.get("ac_count", 0)

    print(f"{'DRY-RUN: ' if dry_run else ''}processed {len(results)} milestone files")
    for action, count in sorted(actions.items()):
        print(f"  {action}: {count}")
    print(f"  total ACs refactored: {total}")

    errors = [r for r in results if r["action"] == "error"]
    if errors:
        print(f"\nErrors:")
        for r in errors:
            print(f"  {r['path']}: {r['error']}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
