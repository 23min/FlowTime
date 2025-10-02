# Future Tests

This directory contains test files for features that are **planned but not yet implemented**.

## Why These Tests Exist

Following TDD principles, we sometimes write tests ahead of implementation. When a feature is:
- **Deferred** (decided not to implement now)
- **Future Milestone** (planned for later)
- **Experimental** (exploring design)

...we move the tests here to preserve the work without breaking the build.

## Files in This Directory

## Current Deferred Features

(None - directory kept for future use)

## How to Use

When ready to implement a deferred feature:
1. Rename `.future` file back to `.cs`
2. Move back to appropriate directory
3. Run tests (they should still be RED - that's TDD!)
4. Implement feature to make tests GREEN
5. Update milestone documentation

## Guidelines

**Add tests here when**:
- Feature explicitly deferred (with documented rationale)
- Tests written but implementation postponed
- Want to preserve test design for future reference

**Don't add tests here if**:
- Tests are just broken (fix them instead)
- Feature canceled permanently (delete tests)
- Tests are outdated (update or delete)
