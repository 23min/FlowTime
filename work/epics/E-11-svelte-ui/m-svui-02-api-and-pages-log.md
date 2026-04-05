# Tracking: m-svui-02-api-and-pages

**Started:** 2026-03-30
**Completed:** 2026-03-30
**Commit:** `5c69fca`

## Progress

| AC | Status | Notes |
|----|--------|-------|
| Health page shows live API status | done | Both APIs with green/red indicators, version/commit info, refresh |
| Artifacts list loads runs | done | Cards with title, date, type, size, tags; loading skeletons |
| Selecting run navigates to detail | done | Detail page with metadata display + inline file viewer |
| API error states show messages | done | Error cards with AlertCircle icon, graceful fallback |

## Decisions

- Vite dev proxy for CORS: `/v1/*` → :8080, `/api/v1/*` → :8090 (relative URLs in API clients)
- Fixed theme store `$effect` orphan: moved to `init()` called from root layout (not constructor)
- Home page shows API status dots and run count badge
