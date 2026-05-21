---
id: G-0039
title: aiwf pre-commit hook fails on git-add of gitignored STATUS.md
status: open
---

## What's missing

The aiwf-installed `.git/hooks/pre-commit` hook regenerates `STATUS.md` and stages it via `git add "$repo_root/STATUS.md"` (line 37 of the installed hook). In this repo, `STATUS.md` is in `.gitignore` (since commit `7e9cc97` "stop tracking generated STATUS.md") because the file is regenerated on every commit and was producing churn. Adding a gitignored file with `git add` (without `--force`) exits non-zero. Combined with `set -e` at the top of the hook, this fails the entire hook and aborts the commit — even though the hook's own xmldoc declares STATUS regeneration "tolerant by design — never blocks commits".

The breakage cascades into anything that triggers the pre-commit hook: every `aiwf promote` / `aiwf add` / `aiwf cancel` invocation, every developer `git commit`, and every `git stash push` (stash internally invokes the pre-commit hook path). When the hook aborts mid-flight, it commonly leaves a zero-byte `.git/index.lock` behind, which then blocks subsequent git operations until the lock is manually removed. **This is the root cause of the recurring stale `.git/index.lock` issue we have been seeing in this repo.**

Reproduction: `touch some-file && git add some-file && git commit -m "test"` → hook runs, `git add STATUS.md` fails because STATUS.md is gitignored, commit aborts, lock file may persist.

## Why it matters

- **Productivity drag.** Every aiwf state transition (and every developer commit) is at risk of this failure. We have been hitting and clearing the lock manually for days.
- **Hidden state corruption.** A stale lock blocks `aiwf add` / `aiwf promote`, surfacing as confusing error messages that look like aiwf bugs rather than hook bugs. Time wasted in misdiagnosis.
- **Hook contract violation.** The hook's own comment promises "tolerant by design — never blocks commits". The actual behavior contradicts that contract.
- **The fix is one character.** Append ` 2>/dev/null || true` to the `git add` line, matching the tolerant-by-design semantics already documented in the hook header.

## Resolution path

1. **Local workaround applied** (2026-05-05): the `.git/hooks/pre-commit` line at this repo has been edited to `git add "$repo_root/STATUS.md" 2>/dev/null || true`. This is a local-only fix; `aiwf init`/`aiwf update` will overwrite it next time the hook is regenerated.
2. **Upstream fix needed in aiwf.** The fix lives in the aiwf source where the pre-commit hook template is generated. Either: (a) suppress the failure with `2>/dev/null || true` matching the hook's documented tolerance, or (b) detect ignored-state with `git check-ignore --quiet "$STATUS_PATH"` before attempting `git add`, or (c) probe `aiwf.yaml`'s `status_md.auto_update` config and skip the staging step when STATUS.md is gitignored.
3. **File upstream issue.** Once the project owner confirms the diagnosis, file an issue/PR against the aiwf repo.

## References

- The hook file in this repo (post-fix): `/workspaces/flowtime-vnext/.git/hooks/pre-commit:37`
- The commit that gitignored STATUS.md: `7e9cc97` ("stop tracking generated STATUS.md")
- Symptom thread: recurring zero-byte `.git/index.lock` blocking `aiwf add` / `aiwf promote` operations; observed during M-0066 wrap (2026-05-05) and E-0026 epic creation (2026-05-05).

