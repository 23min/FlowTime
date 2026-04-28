import { test, expect, type Page, type Route } from '@playwright/test';

// End-to-end tests for the Svelte validation surface (m-E21-07 AC14).
//
// Hybrid spec strategy per tracking-doc confirmation 4 (Q4=D):
//   - Spec #2 ("real bytes / AC1 round-trip"): drives a deliberately-broken model
//     through POST /v1/run end-to-end. Asserts the AC1 wire-format round-trip —
//     warnings flow analyser → run artifact → state_window → wire DTO →
//     Svelte deserialization → panel render correctly.
//   - All other specs (#1, #3-#9): use page.route(...) mocks returning hand-rolled
//     state_window payloads. Deterministic, decouples assertions from analyser-
//     heuristic stability, exercises edge cases (multi-severity, severity-max
//     collapse, view-switch persistence) that real fixtures may not produce.
//
// The deliberately-broken YAML in spec #2 is a minimal two-node topology with a
// single edge carrying lag: 2. The analyser flags this as
// `edge_behavior_violation_lag` (severity "warning") — see InvariantAnalyzer.cs
// "Edge defines lag" branch (`tests/FlowTime.Core.Tests/Analysis/
// InvariantAnalyzerTests.cs:Analyze_WarnsWhenEdgeDefinesLag`). If the analyser
// stops flagging this YAML in the future, the spec should fail loudly — fix the
// YAML, do not mute the test (per chunk handoff).
//
// Graceful skip: if the API or Svelte dev server is not running, every test
// skips. Run locally:
//   Terminal A: dotnet run --project src/FlowTime.API
//   Terminal B: cd ui && pnpm dev
//   Terminal C: FLOWTIME_UI_BASE_URL=http://localhost:5173 \
//     npx playwright test --config tests/ui/playwright.config.ts \
//     tests/ui/specs/svelte-validation.spec.ts

const SVELTE_URL = 'http://localhost:5173';
const API_URL = 'http://localhost:8081';

// ---- infra probe (mirrors svelte-heatmap.spec.ts) ----------------------------

async function infraUp(): Promise<boolean> {
	const probe = async (url: string) => {
		try {
			const res = await fetch(url, { signal: AbortSignal.timeout(1500) });
			return res.ok;
		} catch {
			return false;
		}
	};
	const [api, svelte] = await Promise.all([
		probe(`${API_URL}/v1/healthz`),
		probe(`${SVELTE_URL}/`),
	]);
	return api && svelte;
}

async function loadTopology(page: Page) {
	await page.goto(`${SVELTE_URL}/time-travel/topology`);
	await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });
}

// ---- mock payload helpers ---------------------------------------------------
//
// The state_window response shape mirrors src/FlowTime.API responses. Only fields
// the topology page actually consumes are populated. The empty-but-required
// `metadata` block + window/timestamps/nodes/edges keep the StateWindowResponse
// type satisfied; warnings/edgeWarnings carry the specific test payload.
//
// We MUST include the same node ids in `nodes[]` (from the route's `loadBin`
// path) as the warnings reference — otherwise the topology indicators have
// nothing to attach to. Each test that needs topology DOM assertions chains a
// mock onto the existing run's topology rather than constructing one from
// scratch.

interface MockStateWindow {
	warnings: {
		code: string;
		message: string;
		severity: string;
		nodeId?: string;
		startBin?: number;
		endBin?: number;
		signal?: string;
	}[];
	edgeWarnings: Record<
		string,
		{
			code: string;
			message: string;
			severity: string;
			nodeId?: string;
		}[]
	>;
}

/**
 * Install a state_window mock that wraps the live response's metadata/window/
 * nodes payload but replaces the warnings + edgeWarnings fields. This lets
 * mock specs reuse the real run's topology while injecting deterministic
 * warning payloads.
 *
 * The route uses `**` glob match so query-string variations (mode=, startBin=)
 * still hit. Each install is per-spec — Playwright's `page.route` is scoped to
 * the page lifetime, so fresh installs in beforeEach don't leak.
 */
async function installStateWindowMock(page: Page, mock: MockStateWindow) {
	await page.route('**/v1/runs/*/state_window**', async (route: Route) => {
		// Fetch the live response from the API, then splice in the mock fields.
		const response = await route.fetch();
		const body = await response.json();
		body.warnings = mock.warnings;
		body.edgeWarnings = mock.edgeWarnings;
		await route.fulfill({
			response,
			json: body,
			contentType: 'application/json',
		});
	});
}

// ---- spec #2 helpers (real-bytes path) --------------------------------------

const LAG_TRIGGER_YAML = `schemaVersion: 1

grid:
  bins: 4
  binSize: 15
  binUnit: minutes
  start: 2025-01-01T00:00:00Z

topology:
  nodes:
    - id: SourceNode
      kind: service
      semantics:
        arrivals: source_arrivals
        served: source_served
    - id: TargetNode
      kind: service
      semantics:
        arrivals: target_arrivals
        served: target_served
  edges:
    - id: source_to_target
      from: SourceNode:out
      to: TargetNode:in
      measure: served
      lag: 2

nodes:
  - id: source_arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: source_served
    kind: const
    values: [10, 10, 10, 10]
  - id: target_arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: target_served
    kind: const
    values: [10, 10, 10, 10]
`;

/**
 * Create a run by POSTing YAML directly to /v1/run. Returns the runId.
 * Skips the test if the run can't be created (invariant: the chosen YAML must
 * succeed against the live engine; fail-loud on regression).
 */
async function createLagWarningRun(): Promise<string> {
	const res = await fetch(`${API_URL}/v1/run`, {
		method: 'POST',
		headers: { 'Content-Type': 'text/plain' },
		body: LAG_TRIGGER_YAML,
		signal: AbortSignal.timeout(15000),
	});
	if (!res.ok) {
		const text = await res.text().catch(() => '');
		throw new Error(`POST /v1/run failed (${res.status}): ${text}`);
	}
	const body = await res.json();
	if (typeof body.runId !== 'string') {
		throw new Error(`POST /v1/run response missing runId: ${JSON.stringify(body)}`);
	}
	return body.runId;
}

// =============================================================================
// Test suite
// =============================================================================

test.describe('Validation surface — m-E21-07', () => {
	test.beforeEach(async ({}, testInfo) => {
		if (!(await infraUp())) testInfo.skip();
	});

	// AC14 spec #1 — mocked
	// Trigger T1 zero-warnings: column collapses → pinned cards fill panel → no
	// topology warning indicators.
	test('#1 zero-warnings run collapses panel and shows no topology indicators', async ({
		page,
	}) => {
		await installStateWindowMock(page, { warnings: [], edgeWarnings: {} });
		await loadTopology(page);

		// Validation panel should NOT mount when state === 'empty' and not loading.
		// The route's wrapper guards on `validation.state === 'issues' || (loading
		// && selectedRunId !== undefined)`. Once load resolves with zero warnings,
		// the wrapper unmounts the panel entirely.
		await expect(page.getByTestId('validation-panel')).toHaveCount(0);

		// No topology warning indicators.
		await expect(page.locator('[data-warning-indicator]')).toHaveCount(0);

		// At least one workbench card should still pin (auto-pin highest-utilization
		// node). The pinned-card region reclaims the full panel width.
		await expect(page.locator('button[aria-label^="Unpin"]').first()).toBeVisible({
			timeout: 5000,
		});
	});

	// AC14 spec #2 — real bytes (AC1 round-trip regression)
	// Drives a deliberately-broken model through POST /v1/run and asserts the
	// wire-format round-trip lands in the panel + topology indicator.
	test('#2 real-bytes lag-warning model populates panel + topology indicator', async ({
		page,
	}) => {
		// Test-isolation guard — spec #2 is the only spec that hits POST /v1/run
		// for real (every other spec uses page.route mocks). Real runs land in the
		// API's data dir; running this without isolation pollutes data/runs/ with
		// lag-trigger artifacts. The guard requires the API to be started with
		// FLOWTIME_DATA_DIR pointing at data/test-runs/ and FLOWTIME_E2E_TEST_RUNS
		// flagged so artifacts go to a sandbox the human can wipe freely.
		// Restart recipe is captured verbatim in the skip message + tracking doc.
		if (!process.env.FLOWTIME_E2E_TEST_RUNS) {
			test.skip(
				true,
				'Spec #2 requires the API to be started in E2E-test-runs mode so real runs do not pollute data/runs/. Restart the API with: FLOWTIME_DATA_DIR=/workspaces/flowtime-vnext/data/test-runs FLOWTIME_E2E_TEST_RUNS=1 dotnet run --project src/FlowTime.API then re-run this spec.',
			);
			return;
		}

		let runId: string;
		try {
			runId = await createLagWarningRun();
		} catch (err) {
			// Surface-clearly: the chosen YAML failed to run. Per chunk handoff: fix
			// the YAML, do not mute. We fail rather than skip so heuristic drift is
			// visible.
			throw new Error(
				`Spec #2 prerequisite failed — POST /v1/run rejected the lag-trigger YAML. ` +
					`This means either (a) the analyser/runtime no longer accepts this minimal model, ` +
					`or (b) the trigger condition (edge with lag: 2 → edge_behavior_violation_lag) was ` +
					`removed/renamed. Update LAG_TRIGGER_YAML or the assertion below. Cause: ${(err as Error).message}`,
			);
		}

		// Visit the topology route. The route's onMount lists runs (sort: createdUtc
		// desc) and selects runs[0] — our just-created run. We explicitly select it
		// in case other test runs interleave or auto-pin fails to settle in time.
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		await page.waitForLoadState('networkidle');

		// Force-select our specific run via the dropdown so the assertion is robust
		// against listing-order surprises.
		const select = page.locator('select').first();
		await select.waitFor({ state: 'visible', timeout: 15000 });
		await select.selectOption(runId);
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// The validation panel mounts.
		await expect(page.getByTestId('validation-panel')).toBeVisible({ timeout: 5000 });

		// At least one row exists and carries the expected warning fields.
		const rows = page.locator('[data-testid="validation-row"]');
		await expect(rows.first()).toBeVisible();
		const rowCount = await rows.count();
		expect(rowCount).toBeGreaterThan(0);

		// The lag warning carries nodeId="SourceNode" (per InvariantAnalyzer's lag
		// branch — `nodeId` is the source-node of the edge). Either the row carries
		// kind="node" with the SourceNode key, or the analyser persisted edgeIds
		// (yielding an edge row instead). Both are valid AC1 round-trip evidence;
		// assert the row carries the expected message text either way.
		const firstRowKind = await rows.first().getAttribute('data-row-kind');
		expect(firstRowKind === 'node' || firstRowKind === 'edge').toBe(true);

		// Message should reference "lag" — analyser output is
		// `Edge '<id>' applies a lag of N bin(s)...`.
		const messageContent = await rows.first().textContent();
		expect(messageContent ?? '').toMatch(/lag/i);

		// Severity chip carries "warning" literal (the lag warning defaults to
		// "warning" per InvariantAnalyzer.cs:99).
		await expect(rows.first()).toContainText(/warning/i);

		// Edge indicator regression (smoke-test fix 2026-04-27). The lag YAML
		// produces both a node-attributed warning AND an entry in
		// `edgeWarnings` keyed by the analyser's edge id (`source_to_target`).
		// Pre-fix, the topology effect skipped these keys silently because they
		// don't carry `→`. Post-fix, the validation store translates raw keys
		// to the workbench `${from}→${to}` convention via `graph.edges`, so the
		// indicator must render. Tighten the spec: at least one edge indicator
		// must be present, and its severity must be `warning` (per the lag
		// branch's default severity in InvariantAnalyzer.cs).
		const edgeIndicators = page.locator('[data-warning-indicator="edge"]');
		await expect(edgeIndicators.first()).toBeVisible({ timeout: 5000 });
		const edgeIndicatorCount = await edgeIndicators.count();
		expect(edgeIndicatorCount).toBeGreaterThanOrEqual(1);
		await expect(edgeIndicators.first()).toHaveAttribute(
			'data-warning-severity',
			'warning',
		);
	});

	// AC14 spec #3 — mocked
	// Trigger T2: synthetic state_window payload for a selected run → panel
	// populates from that response.
	test('#3 selecting a run with persisted warnings populates the panel', async ({ page }) => {
		await installStateWindowMock(page, {
			warnings: [
				{
					code: 'queue_depth_mismatch',
					message: 'Queue depth series does not match derived inflow/outflow accumulation',
					severity: 'warning',
					nodeId: 'HubQueue',
					startBin: 0,
					endBin: 2,
				},
			],
			edgeWarnings: {},
		});
		await loadTopology(page);

		// Panel mounts with the mocked warning visible.
		await expect(page.getByTestId('validation-panel')).toBeVisible({ timeout: 5000 });
		const row = page.locator('[data-testid="validation-row"]').first();
		await expect(row).toBeVisible();
		await expect(row).toHaveAttribute('data-row-kind', 'node');
		await expect(row).toContainText(/Queue depth/);
		await expect(row).toContainText(/HubQueue/);
	});

	// AC14 spec #4 — mocked
	// Click a node-attributed warning row → workbench card pins → workbench-card
	// title cross-highlights via data-selected="true" (m-E21-06 convention).
	test('#4 node-attributed row click pins workbench card and cross-highlights title', async ({
		page,
	}) => {
		// Use a nodeId that the topology actually exposes — the auto-pinned highest
		// utilization node is queryable; we read its id and inject a warning for it.
		await loadTopology(page);
		const firstNodeId = await page.locator('[data-node-id]').first().getAttribute('data-node-id');
		expect(firstNodeId).not.toBeNull();
		const targetId = firstNodeId!;

		await installStateWindowMock(page, {
			warnings: [
				{
					code: 'missing_capacity_series',
					message: 'Capacity series was not available; utilization cannot be computed.',
					severity: 'info',
					nodeId: targetId,
				},
			],
			edgeWarnings: {},
		});
		// Reload so the route's loadWindow() picks up the mocked response.
		await page.reload();
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Unpin any auto-pinned card so the cross-link assertion is clean.
		const unpinBtns = page.locator('button[aria-label^="Unpin"]');
		while ((await unpinBtns.count()) > 0) {
			await unpinBtns.first().click();
		}

		// Click the warning row.
		const row = page
			.locator(`[data-testid="validation-row"][data-row-key="${targetId}"]`)
			.first();
		await expect(row).toBeVisible({ timeout: 5000 });
		await row.click();

		// Workbench card pins.
		await expect(
			page.locator(`button[aria-label="Unpin ${targetId}"]`).first(),
		).toBeVisible({ timeout: 5000 });

		// Workbench-card title carries data-selected="true".
		const selectedTitle = page.locator(
			`span[data-selected="true"]`,
			{ hasText: new RegExp(`^${targetId.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}$`) },
		);
		await expect(selectedTitle).toBeVisible();
	});

	// AC14 spec #5 — mocked
	// Click an edge-attributed warning row → workbench edge card pins → topology
	// edge styled per AC8 (sibling glyph at the edge midpoint with
	// data-warning-indicator="edge").
	test('#5 edge-attributed row click pins edge card and topology edge gets indicator', async ({
		page,
	}) => {
		// We need a real edge from the loaded run to inject an edge-warning for.
		await loadTopology(page);
		const edgeFrom = await page.locator('[data-edge-from]').first().getAttribute('data-edge-from');
		const edgeTo = await page.locator('[data-edge-from]').first().getAttribute('data-edge-to');
		if (!edgeFrom || !edgeTo) {
			test.skip(true, 'Current run has no edges to attach a warning to');
			return;
		}
		const edgeId = `${edgeFrom}→${edgeTo}`;

		await installStateWindowMock(page, {
			warnings: [],
			edgeWarnings: {
				[edgeId]: [
					{
						code: 'edge_flow_mismatch_outgoing',
						message: 'Served does not match sum of outgoing edge flows.',
						severity: 'warning',
					},
				],
			},
		});
		await page.reload();
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Edge row visible.
		const row = page
			.locator(`[data-testid="validation-row"][data-row-key="${edgeId}"]`)
			.first();
		await expect(row).toBeVisible({ timeout: 5000 });
		await expect(row).toHaveAttribute('data-row-kind', 'edge');

		// Topology edge has an indicator.
		const indicator = page.locator(
			`[data-warning-indicator="edge"][data-warning-edge-id="${edgeId}"]`,
		);
		await expect(indicator).toHaveCount(1);
		await expect(indicator).toHaveAttribute('data-warning-severity', 'warning');

		// Click the edge row → edge card pins.
		await row.click();
		await expect(
			page.locator(`button[aria-label="Unpin ${edgeFrom} to ${edgeTo}"]`).first(),
		).toBeVisible({ timeout: 5000 });
	});

	// AC14 spec #6 — mocked
	// Click a topology node that has warnings → warnings panel highlights its rows
	// (AC10 — highlight-and-de-emphasize-others). The selected row carries
	// `data-row-match="true"`.
	test('#6 topology node click highlights matching warning rows', async ({ page }) => {
		await loadTopology(page);
		const targetId = await page.locator('[data-node-id]').first().getAttribute('data-node-id');
		expect(targetId).not.toBeNull();

		await installStateWindowMock(page, {
			warnings: [
				{
					code: 'queue_depth_mismatch',
					message: 'Queue depth series does not match derived inflow/outflow accumulation',
					severity: 'warning',
					nodeId: targetId!,
				},
				{
					code: 'unrelated_code',
					message: 'A different node has this issue.',
					severity: 'warning',
					nodeId: '__different_node_id_unlikely_to_exist__',
				},
			],
			edgeWarnings: {},
		});
		await page.reload();
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Unpin auto-pin if any so click-to-pin produces a clean selection.
		const unpinBtns = page.locator('button[aria-label^="Unpin"]');
		while ((await unpinBtns.count()) > 0) {
			await unpinBtns.first().click();
		}

		// Click the topology node with warnings.
		await page.locator(`[data-node-id="${targetId}"]`).first().click();

		// Wait for the cross-link to land. Matching row carries
		// data-row-match="true"; non-matching row does NOT.
		const matchedRow = page.locator(
			`[data-testid="validation-row"][data-row-key="${targetId}"][data-row-match="true"]`,
		);
		await expect(matchedRow).toBeVisible({ timeout: 3000 });

		// At least one row exists without the match attribute (the unrelated_code one).
		const totalRows = await page.locator('[data-testid="validation-row"]').count();
		const matchedRows = await page
			.locator('[data-testid="validation-row"][data-row-match="true"]')
			.count();
		expect(matchedRows).toBeGreaterThan(0);
		expect(matchedRows).toBeLessThan(totalRows);
	});

	// AC14 spec #7 — mocked
	// Click a topology edge that has warnings → warnings panel highlights its rows.
	test('#7 topology edge click highlights matching warning rows', async ({ page }) => {
		await loadTopology(page);
		const edgeFrom = await page.locator('[data-edge-from]').first().getAttribute('data-edge-from');
		const edgeTo = await page.locator('[data-edge-from]').first().getAttribute('data-edge-to');
		if (!edgeFrom || !edgeTo) {
			test.skip(true, 'Current run has no edges to attach a warning to');
			return;
		}
		const edgeId = `${edgeFrom}→${edgeTo}`;

		await installStateWindowMock(page, {
			warnings: [
				{
					code: 'an_unrelated_node_warning',
					message: 'A different node has this issue.',
					severity: 'warning',
					nodeId: '__different_node_id_unlikely_to_exist__',
				},
			],
			edgeWarnings: {
				[edgeId]: [
					{
						code: 'edge_flow_mismatch_outgoing',
						message: 'Served does not match sum of outgoing edge flows.',
						severity: 'warning',
					},
				],
			},
		});
		await page.reload();
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Click the edge hit area to pin it (workbench convention from
		// svelte-workbench.spec.ts spec '#5 clicking an edge pins an edge card').
		// The hit-path has stroke="transparent" so Playwright's visibility check
		// fails — `force: true` bypasses the stability/visibility wait. This is
		// the same workaround the m-E21-06 heatmap suite would need if it clicked
		// transparent edges; today's svelte-workbench.spec.ts gets away without
		// it on the auto-loaded fixture purely by chance of element bounding box.
		const edgeHit = page
			.locator(`[data-edge-from="${edgeFrom}"][data-edge-to="${edgeTo}"][data-edge-hit="true"]`)
			.first();
		await edgeHit.click({ force: true });

		// Matching edge row carries data-row-match="true".
		const matchedRow = page.locator(
			`[data-testid="validation-row"][data-row-key="${edgeId}"][data-row-match="true"]`,
		);
		await expect(matchedRow).toBeVisible({ timeout: 3000 });
	});

	// AC14 spec #8 — mocked
	// Validation panel persists across view switch (Topology → Heatmap → Topology);
	// node + edge indicators re-render correctly when switching back to Topology.
	test('#8 validation panel persists across view switches', async ({ page }) => {
		await loadTopology(page);
		const targetNodeId = await page.locator('[data-node-id]').first().getAttribute('data-node-id');
		expect(targetNodeId).not.toBeNull();

		await installStateWindowMock(page, {
			warnings: [
				{
					code: 'missing_served_series',
					message: 'Served/output series was not available; utilization cannot be computed.',
					severity: 'info',
					nodeId: targetNodeId!,
				},
			],
			edgeWarnings: {},
		});
		await page.reload();
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Topology view: panel + indicator visible.
		await expect(page.getByTestId('validation-panel')).toBeVisible();
		await expect(
			page.locator(`[data-warning-indicator="node"][data-warning-node-id="${targetNodeId}"]`),
		).toHaveCount(1);

		// Switch to Heatmap.
		await page.getByRole('tab', { name: /Heatmap/ }).click();
		await expect(page.getByTestId('heatmap-grid')).toBeVisible({ timeout: 5000 });

		// Panel persists — same warning rows visible (panel is in the workbench
		// region which is shared across views).
		await expect(page.getByTestId('validation-panel')).toBeVisible();
		await expect(
			page.locator(`[data-testid="validation-row"][data-row-key="${targetNodeId}"]`),
		).toBeVisible();

		// Switch back to Topology.
		await page.getByRole('tab', { name: /Topology/ }).click();
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 5000 });

		// Indicator re-renders.
		await expect(
			page.locator(`[data-warning-indicator="node"][data-warning-node-id="${targetNodeId}"]`),
		).toHaveCount(1);

		// Panel still visible.
		await expect(page.getByTestId('validation-panel')).toBeVisible();
	});

	// AC14 spec #9 — mocked
	// At least two distinct severities render with distinct chrome tokens. Asserts
	// each row's severity dot inline-style references the correct CSS variable.
	test('#9 distinct severities render distinct chrome tokens', async ({ page }) => {
		await loadTopology(page);
		const firstId = await page.locator('[data-node-id]').nth(0).getAttribute('data-node-id');
		const secondId = await page.locator('[data-node-id]').nth(1).getAttribute('data-node-id');
		// If the run has fewer than 2 nodes the test still runs — both warnings just
		// land on the same node visually. Assert the per-row severity attribute (the
		// chrome-token wiring is what matters, not which node carries which row).
		await installStateWindowMock(page, {
			warnings: [
				{
					code: 'something_critical',
					message: 'A critical issue occurred.',
					severity: 'error',
					nodeId: firstId ?? undefined,
				},
				{
					code: 'something_warned',
					message: 'A warning was raised.',
					severity: 'warning',
					nodeId: secondId ?? firstId ?? undefined,
				},
			],
			edgeWarnings: {},
		});
		await page.reload();
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Both rows visible; chrome tokens are distinct in the rendered DOM. We
		// assert via the inline `style` attribute on each row's severity-dot span —
		// the format is `background: var(--ft-err)` or `background: var(--ft-warn)`.
		// Querying by data-row-kind alone is not enough — both rows are 'node' kind;
		// we discriminate by visible text.
		const errorRow = page
			.locator(`[data-testid="validation-row"]`, { hasText: /critical issue/i })
			.first();
		const warningRow = page
			.locator(`[data-testid="validation-row"]`, { hasText: /warning was raised/i })
			.first();

		await expect(errorRow).toBeVisible();
		await expect(warningRow).toBeVisible();

		// The chrome token is referenced via inline `style` on the dot span. Locate
		// the dot inside each row.
		const errorDotStyle = await errorRow.locator('span[aria-hidden="true"]').first().getAttribute('style');
		const warningDotStyle = await warningRow.locator('span[aria-hidden="true"]').first().getAttribute('style');
		expect(errorDotStyle ?? '').toContain('--ft-err');
		expect(warningDotStyle ?? '').toContain('--ft-warn');
		// Distinct tokens — sanity-check the two strings are not identical.
		expect(errorDotStyle).not.toBe(warningDotStyle);
	});
});
