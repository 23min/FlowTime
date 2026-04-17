# Skill: TDD Cycle

Test-driven development workflow for implementing acceptance criteria.

## When to Use

During milestone implementation. For each acceptance criterion or feature unit.

## Checklist

### RED — Write failing test

- [ ] Write test(s) that describe the expected behavior
- [ ] Test names follow convention: `MethodName_Scenario_ExpectedResult`
- [ ] Use project's test framework (xUnit, Jest, pytest, etc.)
- [ ] Use mocks/stubs for external dependencies
- [ ] Run tests → confirm they **FAIL** for the right reason

### GREEN — Make it pass

- [ ] Write the **minimum** code to make the test pass
- [ ] Don't add features the test doesn't require
- [ ] Run tests → confirm they **PASS**
- [ ] Check no other tests broke

### REFACTOR — Clean up

- [ ] Remove duplication
- [ ] Improve naming
- [ ] Extract methods/classes if needed
- [ ] Run tests → confirm still **GREEN**

### Update Tracking

- [ ] Check off the acceptance criterion in tracking doc
- [ ] Note any decisions or deviations

## Anti-patterns

- Writing code before tests
- Writing tests that can't fail
- Skipping the refactor step
- Testing implementation details instead of behavior
- Tests that depend on execution order

## Test Quality Checks

- [ ] Tests are deterministic (no randomness, no clock, no network)
- [ ] Tests are independent (no shared mutable state)
- [ ] Tests cover edge cases (null, empty, boundary values)
- [ ] Test names explain what is being tested

## Branch Coverage Audit (before declaring done)

Every TDD cycle ends with a final pass that confirms every reachable branch in the implementation has at least one test. This is a **hard rule** — see `.ai/agents/builder.md` Constraints.

- [ ] Open each new or changed source file and walk it line-by-line
- [ ] For every `if`/`else`/`switch`/`catch`/`?:`/early-return, identify which test exercises each side
- [ ] If a branch has no test, write one. Defensive paths (guards, exception catches, malformed-input handlers) count as reachable — if it ships, it gets a test
- [ ] If a helper is private and the branch is hard to reach via the public API, expose it to tests using the language's friend-assembly / package-private mechanism (e.g., `internal` + `InternalsVisibleTo` in C#, `pub(crate)` in Rust, `_internal` convention or `__all__` in Python, package-private in Java/Go) and write a direct test
- [ ] Genuinely unreachable branches (e.g., a defensive `null` check on a value the type system guarantees non-null) must be documented in the milestone spec under a "Coverage notes" section with the reason
- [ ] **Do not declare "every branch covered" without performing the audit.** Saying it is not the same as proving it. The audit happens before the commit-approval prompt, not after the human asks.
