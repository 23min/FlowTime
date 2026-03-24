# Skill: ui-debug

Purpose: diagnose UI issues quickly and reproducibly.

Use when:
- UI behavior is incorrect, flaky, or unclear.

Process:
1) Reproduce with the smallest possible scenario.
2) Prefer Playwright tests for deterministic reproduction.
3) If updating snapshots, note why and keep diffs minimal.
4) Avoid external network calls; mock or stub where needed.

Notes:
- If a Playwright test exists, run or update it before manual fixes.
