# Skill: ui-debug

Diagnose UI issues quickly and reproducibly.

**Use when:** UI behavior is incorrect, flaky, or unclear.

## Checklist

- [ ] Reproduce with the smallest possible scenario
- [ ] Check for an existing Playwright test — run or update it before manual fixes
- [ ] Prefer Playwright tests for deterministic reproduction
- [ ] If updating snapshots, note why and keep diffs minimal
- [ ] Avoid external network calls; mock or stub where needed
