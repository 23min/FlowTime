(function () {
    const registry = new WeakMap();
    const activeStates = new Set();
    let cachedThemeIsDark = null;
    const EdgeTypeTopology = 'topology';
    const EdgeTypeDependency = 'dependency';
    const EdgeTypeThroughput = 'throughput';
    const EdgeTypeEffort = 'effort';
    const EdgeTypeTerminal = 'terminal';
    const EDGE_OVERLAY_BASE_COLORS = {
        success: '#009E73',
        warning: '#E69F00',
        error: '#D55E00',
        neutral: '#7C8BA1'
    };
    const EDGE_OVERLAY_STROKE_ALPHA = 0.85;
    const InspectorIconSize = 18;
    const InspectorIconGap = 4;
    const InspectorTooltipGap = 4;
    const InspectorTooltipInset = 6;
    const MIN_ZOOM_PERCENT = 25;
    const MAX_ZOOM_PERCENT = 200;
    const MIN_SCALE = MIN_ZOOM_PERCENT / 100;
    const MAX_SCALE = MAX_ZOOM_PERCENT / 100;
    const LEAF_FILL_LIGHT = '#E2E8F0';
    const LEAF_FILL_DARK = '#1E293B';
    const LEAF_STROKE_LIGHT = '#64748B';
    const LEAF_STROKE_DARK = '#475569';
    const NEUTRAL_FILL_LIGHT = '#E2E8F0';
    const NEUTRAL_FILL_DARK = '#B8C2D3';
    const QUEUE_PILL_FILL = '#60A5FA';
    const QUEUE_PILL_STROKE = '#2563EB';
    const NODE_LABEL_COLOR_LIGHT = '#0F172A';
    const NODE_LABEL_COLOR_DARK = '#F8FAFC';
    const QUEUE_LABEL_COLOR = NODE_LABEL_COLOR_LIGHT;
    const RETRY_BADGE_FILL = '#EF4444';
    const RETRY_BADGE_STROKE = '#B91C1C';
    const RETRY_BADGE_TEXT = '#FFFFFF';
    const DLQ_LIGHT_PALETTE = { fill: '#FDE68A', stroke: '#B45309', text: '#7C2D12' };
    const DLQ_DARK_PALETTE = { fill: RETRY_BADGE_FILL, stroke: RETRY_BADGE_STROKE, text: RETRY_BADGE_TEXT };
    const CHIP_BASE_FILL_LIGHT = '#F3F4F6';
    const CHIP_BASE_FILL_DARK = '#0F172A';
    const CHIP_TEXT_LIGHT = NODE_LABEL_COLOR_LIGHT;
    const CHIP_TEXT_DARK = '#F8FAFC';
    const EXPRESSION_DARK_FILL = '#1F2735';
    const POINTER_CLICK_DISTANCE = 4;
    const GRID_ROW_SPACING = 140;
    const GRID_COLUMN_SPACING = 240;
    const DIAGNOSTICS_UPDATE_INTERVAL_MS = 750;
    const DIAGNOSTICS_MIN_UPLOAD_INTERVAL_MS = 1000;
    const HOVER_CACHE_WORLD_EPSILON = 0.05;
    const NODE_HALO_PADDING = 12;
    const HOVER_INTENT_SPEED_THRESHOLD = 900; // pixels per second
    const HOVER_INTENT_MAX_SUPPRESSION_MS = 150;
    const HOVER_INTENT_RESUME_GRACE_MS = 150;
    const DIAGNOSTICS_COLLAPSE_STORAGE_KEY = 'ft.topology.diag.collapsed';
    const DEBUG_LOG_STORAGE_KEY = 'ft.topology.debuglog';
    const EDGE_SPATIAL_INDEX_CELL_SIZE = 256;
    const EDGE_SPATIAL_MIN_CELL_SIZE = 64;
    const EDGE_SPATIAL_MAX_CELL_SIZE = 512;
    const EDGE_SPATIAL_TARGET_EDGES_PER_CELL = 4;
    const CANVAS_RECT_CACHE_MAX_AGE_MS = 32;
    const VIEWPORT_EMIT_DEBOUNCE_MS = 120;

    function parseDebugFlagFromQuery() {
        if (typeof window === 'undefined' || !window.location) {
            return null;
        }
        try {
            const params = new URLSearchParams(window.location.search || '');
            const flag = params.get('topologyDebug');
            if (flag === null) {
                return null;
            }
            if (flag === '1') {
                return true;
            }
            if (flag === '0') {
                return false;
            }
            const normalized = flag.trim().toLowerCase();
            if (normalized === 'true') {
                return true;
            }
            if (normalized === 'false') {
                return false;
            }
        } catch {
            // Ignore malformed query strings
        }
        return null;
    }

    function loadPersistedDebugFlag() {
        if (typeof window === 'undefined') {
            return false;
        }
        try {
            const stored = window.localStorage?.getItem(DEBUG_LOG_STORAGE_KEY);
            if (stored === '1') {
                return true;
            }
            if (stored === '0') {
                return false;
            }
        } catch {
            // Ignore storage failures
        }
        return false;
    }

    let globalDebugLoggingEnabled = (() => {
        const queryFlag = parseDebugFlagFromQuery();
        if (queryFlag !== null) {
            return queryFlag;
        }
        return loadPersistedDebugFlag();
    })();

    function persistDebugFlag(enabled) {
        try {
            window.localStorage?.setItem(DEBUG_LOG_STORAGE_KEY, enabled ? '1' : '0');
        } catch {
            // Ignore storage failures
        }
    }

    function setGlobalDebugLogging(enabled, persist = true) {
        const normalized = Boolean(enabled);
        globalDebugLoggingEnabled = normalized;
        if (persist) {
            persistDebugFlag(normalized);
        }
        for (const state of activeStates) {
            if (state) {
                state.debugEnabled = normalized;
            }
        }
    }

    function debugLog(state, ...args) {
        if (!state?.debugEnabled) {
            return;
        }
        if (typeof console === 'undefined' || typeof console.log !== 'function') {
            return;
        }
        console.log(...args);
    }

    function createHoverCache() {
        return {
            worldX: null,
            worldY: null,
            scale: null,
            chipHit: null,
            edgeHit: null,
            chipVersion: -1,
            edgeVersion: -1
        };
    }

    function resetHoverCache(state) {
        if (!state) {
            return;
        }

        state.hoverCache = createHoverCache();
    }

    function logHoverInteropDispatch(state, details) {
        if (!state?.debugEnabled) {
            return;
        }
        const stats = state.hoverStats ?? createHoverStats();
        const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
            ? performance.now()
            : Date.now();
        const windowStart = Number.isFinite(stats.windowStartTimestamp) ? stats.windowStartTimestamp : now;
        const elapsedMs = Math.max(0, now - windowStart);
        const elapsedSeconds = elapsedMs > 0 ? elapsedMs / 1000 : 0;
        const windowDispatches = stats.windowDispatches ?? stats.interopDispatches ?? 0;
        const rate = elapsedSeconds > 0 ? windowDispatches / elapsedSeconds : 0;
        debugLog(state, '[Topology] hover interop dispatch', {
            kind: details?.kind ?? 'unknown',
            id: details?.id ?? null,
            inspectorVisible: Boolean(state.inspectorVisible),
            totalDispatches: stats.interopDispatches ?? 0,
            windowDispatches,
            ratePerSecond: Number(rate.toFixed(2))
        });
    }

    function getCanvasClientRect(canvas, state) {
        if (!canvas) {
            return new DOMRect(0, 0, 0, 0);
        }
        if (!state) {
            return canvas.getBoundingClientRect();
        }
        const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
            ? performance.now()
            : Date.now();
        const cache = state.canvasRectCache;
        if (cache &&
            cache.rect &&
            Number.isFinite(cache.timestamp) &&
            (now - cache.timestamp) <= CANVAS_RECT_CACHE_MAX_AGE_MS) {
            return cache.rect;
        }
        const rect = canvas.getBoundingClientRect();
        incrementLayoutReads(state);
        state.canvasRectCache = { rect, timestamp: now };
        return rect;
    }

    function invalidateCanvasRectCache(state) {
        if (!state) {
            return;
        }
        state.canvasRectCache = null;
    }

    function createHoverStats() {
        const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
            ? performance.now()
            : Date.now();
        return {
            interopDispatches: 0,
            lastDispatchTimestamp: 0,
            startTimestamp: now,
            windowStartTimestamp: now,
            windowDispatches: 0
        };
    }

    function createDrawStats() {
        return {
            frames: 0,
            totalDurationMs: 0,
            maxDurationMs: 0,
            lastDurationMs: 0
        };
    }

    function createPanStats() {
        return {
            distance: 0
        };
    }

    function createZoomStats() {
        return {
            events: 0
        };
    }

    function createPointerStats() {
        return {
            received: 0,
            processed: 0,
            queueDrops: 0,
            intentSkips: 0
        };
    }

    function createDragStats() {
        return {
            frames: 0,
            totalDurationMs: 0,
            maxDurationMs: 0
        };
    }

    function createSceneStats() {
        return {
            rebuilds: 0,
            overlayUpdates: 0
        };
    }

    function createLayoutStats() {
        return {
            reads: 0
        };
    }

    function createPointerInpStats() {
        return {
            samples: 0,
            totalMs: 0,
            maxMs: 0,
            lastMs: 0
        };
    }

    function createEdgeSpatialStats(cellSize = EDGE_SPATIAL_INDEX_CELL_SIZE) {
        return {
            cellSize,
            samples: 0,
            candidateTotal: 0,
            lastCandidates: 0,
            fallbackSamples: 0,
            cacheHits: 0,
            cacheMisses: 0
        };
    }

    function capturePointerSnapshot(event, rect) {
        if (!event || !rect) {
            return null;
        }
        return {
            clientX: event.clientX,
            clientY: event.clientY,
            rectLeft: rect.left,
            rectTop: rect.top,
            rectRight: rect.right,
            rectBottom: rect.bottom
        };
    }

    function resetPointerStatsSinceUpload(state) {
        if (!state) {
            return;
        }

        state.pointerStatsSinceUpload = createPointerStats();
    }

    function resetPointerStats(state) {
        if (!state) {
            return;
        }

        state.pointerStats = createPointerStats();
        resetPointerStatsSinceUpload(state);
    }

    function resetDragStatsSinceUpload(state) {
        if (!state) {
            return;
        }

        state.dragStatsSinceUpload = createDragStats();
    }

    function resetDragStats(state) {
        if (!state) {
            return;
        }

        state.dragStats = createDragStats();
        resetDragStatsSinceUpload(state);
    }

    function resetSceneStatsSinceUpload(state) {
        if (!state) {
            return;
        }

        state.sceneStatsSinceUpload = createSceneStats();
    }

    function resetSceneStats(state) {
        if (!state) {
            return;
        }

        state.sceneStats = createSceneStats();
        resetSceneStatsSinceUpload(state);
    }

    function incrementSceneStat(state, key, amount = 1) {
        if (!state || !key) {
            return;
        }

        state.sceneStats ??= createSceneStats();
        state.sceneStats[key] = (state.sceneStats[key] ?? 0) + amount;
        state.sceneStatsSinceUpload ??= createSceneStats();
        state.sceneStatsSinceUpload[key] = (state.sceneStatsSinceUpload[key] ?? 0) + amount;
    }

    function resetLayoutStatsSinceUpload(state) {
        if (!state) {
            return;
        }
        state.layoutStatsSinceUpload = createLayoutStats();
    }

    function resetLayoutStats(state) {
        if (!state) {
            return;
        }
        state.layoutStats = createLayoutStats();
        resetLayoutStatsSinceUpload(state);
    }

    function incrementLayoutReads(state, amount = 1) {
        if (!state) {
            return;
        }
        state.layoutStats ??= createLayoutStats();
        state.layoutStats.reads = (state.layoutStats.reads ?? 0) + amount;
        state.layoutStatsSinceUpload ??= createLayoutStats();
        state.layoutStatsSinceUpload.reads = (state.layoutStatsSinceUpload.reads ?? 0) + amount;
    }

    function resetPointerInpStatsSinceUpload(state) {
        if (!state) {
            return;
        }
        state.pointerInpStatsSinceUpload = createPointerInpStats();
    }

    function resetPointerInpStats(state) {
        if (!state) {
            return;
        }
        state.pointerInpStats = createPointerInpStats();
        resetPointerInpStatsSinceUpload(state);
    }

    function markPointerInteraction(state) {
        if (!state || typeof performance === 'undefined' || typeof performance.now !== 'function') {
            return;
        }

        state.pendingPointerInpStart = performance.now();
    }

    function recordPointerInteraction(state, frameEnd) {
        if (!state || !state.pendingPointerInpStart || typeof performance === 'undefined') {
            return;
        }

        const latency = Math.max(0, frameEnd - state.pendingPointerInpStart);
        state.pointerInpStats ??= createPointerInpStats();
        state.pointerInpStats.samples += 1;
        state.pointerInpStats.totalMs += latency;
        state.pointerInpStats.maxMs = Math.max(state.pointerInpStats.maxMs ?? 0, latency);
        state.pointerInpStats.lastMs = latency;

        state.pointerInpStatsSinceUpload ??= createPointerInpStats();
        state.pointerInpStatsSinceUpload.samples += 1;
        state.pointerInpStatsSinceUpload.totalMs += latency;
        state.pointerInpStatsSinceUpload.maxMs = Math.max(state.pointerInpStatsSinceUpload.maxMs ?? 0, latency);
        state.pointerInpStatsSinceUpload.lastMs = latency;

        state.pendingPointerInpStart = null;
    }

    function incrementPointerStat(state, key, amount = 1) {
        if (!state || !key) {
            return;
        }

        state.pointerStats ??= createPointerStats();
        state.pointerStats[key] = (state.pointerStats[key] ?? 0) + amount;
        state.pointerStatsSinceUpload ??= createPointerStats();
        state.pointerStatsSinceUpload[key] = (state.pointerStatsSinceUpload[key] ?? 0) + amount;
    }

    function clearHoverStats(state) {
        if (!state) {
            return;
        }

        state.hoverStats = createHoverStats();
        resetPointerStats(state);
        state.lastNodeHoverDispatchTime = 0;
        state.lastDrawnHoverNodeId = null;
        state.lastDrawnHoverChipId = null;
        state.lastDrawnHoverEdgeId = null;
        updateDiagnosticsHud(state);
        resetSceneStatsSinceUpload(state);
        resetLayoutStatsSinceUpload(state);
        resetPointerInpStatsSinceUpload(state);
    }

    function resetCanvasStatsSinceUpload(state) {
        if (!state) {
            return;
        }

        state.drawStatsSinceUpload = createDrawStats();
        state.panStatsSinceUpload = createPanStats();
        state.zoomStatsSinceUpload = createZoomStats();
        state.pointerThrottleSkipsSinceUpload = 0;
        resetDragStatsSinceUpload(state);
    }

    function resetCanvasStats(state) {
        if (!state) {
            return;
        }

        state.drawStats = createDrawStats();
        state.panStats = createPanStats();
        state.zoomStats = createZoomStats();
        state.pointerThrottleSkips = 0;
        resetCanvasStatsSinceUpload(state);
        resetDragStats(state);
        state.dragFrameCount = 0;
        state.dragAccumulatedDuration = 0;
        state.dragStartTime = null;
    }

    function registerActiveState(state) {
        if (!state) {
            return;
        }
        state.debugEnabled = globalDebugLoggingEnabled;
        activeStates.add(state);
    }

    function unregisterActiveState(state) {
        activeStates.delete(state);
    }

    function invalidateThemeCache() {
        cachedThemeIsDark = null;
    }

    function redrawActiveStates(options) {
        const markSceneDirty = Boolean(options?.markSceneDirty);
        for (const state of activeStates) {
            if (!state?.canvas) {
                continue;
            }
            if (markSceneDirty) {
                state.sceneDirty = true;
            }
            draw(state.canvas, state);
        }
    }

    (function setupThemeObservers() {
        const handleThemeChange = () => {
            invalidateThemeCache();
            redrawActiveStates({ markSceneDirty: true });
        };

        const tryObserve = () => {
            if (!document.body) {
                return false;
            }
            const observer = new MutationObserver(handleThemeChange);
            observer.observe(document.body, { attributes: true, attributeFilter: ['class'] });
            return true;
        };

        if (!tryObserve()) {
            document.addEventListener('DOMContentLoaded', () => tryObserve(), { once: true });
        }

        const media = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
        media?.addEventListener?.('change', handleThemeChange);

        window.addEventListener?.('ft-theme-changed', handleThemeChange);
    })();

    function isDarkTheme() {
        if (cachedThemeIsDark === null) {
            cachedThemeIsDark = evaluateDarkTheme();
        }
        return cachedThemeIsDark;
    }

    function evaluateDarkTheme() {
        if (document.body?.classList.contains('dark-mode')) {
            return true;
        }
        const paletteBackground = getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-background');
        const rgb = colorStringToRgb(paletteBackground);
        if (rgb) {
            const luminance = calculateLuminance(rgb);
            return luminance < 0.45;
        }
        return false;
    }

    function getLeafFill() {
        return isDarkTheme() ? LEAF_FILL_DARK : LEAF_FILL_LIGHT;
    }

    function normalizeNeutralFill(fill) {
        if (!fill || typeof fill !== 'string') {
            return fill;
        }
        if (!isDarkTheme()) {
            return fill;
        }
        return fill.toLowerCase() === NEUTRAL_FILL_LIGHT.toLowerCase()
            ? NEUTRAL_FILL_DARK
            : fill;
    }

    function getLeafStroke() {
        return isDarkTheme() ? LEAF_STROKE_DARK : LEAF_STROKE_LIGHT;
    }

    function getDefaultChipFill() {
        return isDarkTheme() ? CHIP_BASE_FILL_DARK : CHIP_BASE_FILL_LIGHT;
    }

    function getDefaultChipTextColor() {
        return isDarkTheme() ? CHIP_TEXT_DARK : CHIP_TEXT_LIGHT;
    }

    function getQueueLabelColor() {
        return isDarkTheme() ? NODE_LABEL_COLOR_DARK : QUEUE_LABEL_COLOR;
    }

    function getDlqPalette() {
        return isDarkTheme() ? DLQ_DARK_PALETTE : DLQ_LIGHT_PALETTE;
    }

    function getDlqValueColor() {
        return getDlqPalette().text;
    }

    function getDlqTagColors() {
        const palette = getDlqPalette();
        return { fill: palette.fill, text: palette.text, stroke: palette.stroke };
    }

    function getState(canvas) {
        if (!canvas) {
            return null;
        }

        let state = registry.get(canvas);
        if (!state) {
            const ctx = canvas.getContext?.('2d');
            if (!ctx) {
                return null;
            }
            state = {
                ctx,
                canvas,
                payload: null,
                offsetX: 0,
                offsetY: 0,
                scale: 1,
                dragging: false,
                dragStart: { x: 0, y: 0 },
                dragStartTime: null,
                dragAccumulatedDuration: 0,
                dragFrameCount: 0,
                dragStats: createDragStats(),
                dragStatsSinceUpload: createDragStats(),
                sceneStats: createSceneStats(),
                sceneStatsSinceUpload: createSceneStats(),
                layoutStats: createLayoutStats(),
                layoutStatsSinceUpload: createLayoutStats(),
                pointerInpStats: createPointerInpStats(),
                pointerInpStatsSinceUpload: createPointerInpStats(),
                lastPointerSnapshot: null,
                dragActive: false,
                dragEnding: false,
                pendingDelayedResume: null,
                panStart: { x: 0, y: 0 },
                deviceRatio: window.devicePixelRatio || 1,
                overlayScale: 1,
                lastOverlayZoom: null,
                overlaySettings: null,
                scenePayload: null,
                overlayPayload: null,
                userAdjusted: false,
                viewportSignature: null,
                preserveViewportRequest: false,
                resizeObserver: null,
                baseScale: null,
                worldCenterX: 0,
                worldCenterY: 0,
                viewportApplied: false,
                canvasWidth: null,
                canvasHeight: null,
                dotNetRef: null,
                lastViewportSignature: null,
                lastViewportPayload: null,
                eventSurface: null,
                chipHitboxes: [],
                nodeHitboxes: [],
                hoveredChipId: null,
                hoveredChip: null,
                hoveredNodeId: null,
                edgeHitboxes: [],
                edgeSpatialIndex: createEdgeSpatialIndex(),
                edgeSpatialCache: null,
                edgeSpatialStats: createEdgeSpatialStats(),
                sceneBounds: null,
                hoveredEdgeId: null,
                inspectorEdgeHoverId: null,
                focusedEdgeId: null,
                lastEdgeHoverId: null,
                pointerDownPoint: null,
                pointerMoved: false,
                pointerClickCandidate: false,
                title: '',
                settingsHitbox: null,
                settingsPointerActive: false,
                settingsPointerId: null,
                settingsPointerDown: null,
                tooltipMetrics: null,
                edgeOverlayLegend: null,
                edgeOverlayContext: null,
                globalWarningsByNode: new Map(),
                edgeSeries: new Map(),
                edgeSeriesStartIndex: 0,
                pendingHover: null,
                hoverDropReported: false,
                sceneDirty: true,
                sceneRawNodes: [],
                sceneRawEdges: [],
                sceneNodeMap: new Map(),
                sceneLegacyTooltip: null,
                collectingHitboxes: false,
                collectingChipHitboxes: false,
                refreshChipHitboxes: false,
                hoverFrameRequestId: null,
                chipHitVersion: 0,
                edgeHitVersion: 0,
                hoverCache: createHoverCache(),
                hoverStats: createHoverStats(),
                pointerThrottleSkips: 0,
                pointerThrottleSkipsSinceUpload: 0,
                lastNodeHoverDispatchTime: 0,
                pendingInspectorHoverId: null,
                inspectorDispatchHandle: null,
                lastInspectorDispatchId: null,
                hoverIntentBypassUntil: 0,
                lastDrawnHoverNodeId: null,
                lastDrawnHoverChipId: null,
                lastDrawnHoverEdgeId: null,
                hoverSuspended: false,
                pointerStats: createPointerStats(),
                pointerStatsSinceUpload: createPointerStats(),
                drawStats: createDrawStats(),
                drawStatsSinceUpload: createDrawStats(),
                panStats: createPanStats(),
                panStatsSinceUpload: createPanStats(),
                zoomStats: createZoomStats(),
                zoomStatsSinceUpload: createZoomStats(),
                diagnosticsOptions: null,
                debugEnabled: globalDebugLoggingEnabled,
                diagnosticsHud: null,
                diagnosticsUploadHandle: null,
                diagnosticsHudCollapsed: false,
                runId: null,
                buildHash: null,
                disableHoverCache: false,
                operationalViewOnly: false,
                inspectorVisible: false,
                lastNodeHoverId: null,
                lastPointerSample: null,
                lastNodeCount: 0,
                lastEdgeCount: 0,
                focusedNodeId: null,
                lastBinDumpSelectionId: null,
                lastPanSample: null,
                dragPointerId: null,
                windowPointerUpHandler: null,
                windowPointerCancelHandler: null,
                dragEnding: false,
                pendingDelayedResume: null,
                lastHoverResumeTimestamp: null,
                panFrameRequestId: null,
                pendingPan: null,
                canvasRectCache: null,
                viewportDebounceHandle: null,
                pendingViewportPayload: null
            };
            setupCanvas(canvas, state);
            registry.set(canvas, state);
        }
        return state;
    }

    function setupCanvas(canvas, state) {
        const updateCanvasSize = (width, height) => {
            const ratio = state.deviceRatio;
            const safeWidth = Math.max(1, width);
            const safeHeight = Math.max(1, height);
            const prevWidth = state.canvasWidth ?? safeWidth;
            const prevHeight = state.canvasHeight ?? safeHeight;

            if (state.userAdjusted || state.viewportApplied) {
                state.offsetX += (safeWidth - prevWidth) / 2;
                state.offsetY += (safeHeight - prevHeight) / 2;
            }

            canvas.width = safeWidth * ratio;
            canvas.height = safeHeight * ratio;
            state.ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
            state.canvasWidth = safeWidth;
            state.canvasHeight = safeHeight;
            invalidateCanvasRectCache(state);

            updateWorldCenter(state);

            draw(canvas, state);

            emitViewportChanged(canvas, state, { immediate: true });
        };

        const resize = () => {
            const host = canvas.parentElement ?? canvas;
            const rect = host.getBoundingClientRect();
            if (rect.width > 0 && rect.height > 0) {
                updateCanvasSize(rect.width, rect.height);
            }
        };

        resize();
        window.addEventListener('resize', resize);

        if (typeof ResizeObserver !== 'undefined') {
            const observer = new ResizeObserver(entries => {
                for (const entry of entries) {
                    if (entry.target === (canvas.parentElement ?? canvas)) {
                        const { width, height } = entry.contentRect;
                        if (width > 0 && height > 0) {
                            updateCanvasSize(width, height);
                        }
                    }
                }
            });
            observer.observe(canvas.parentElement ?? canvas);
            state.resizeObserver = observer;
        }

        const eventSurface = canvas.parentElement ?? canvas;
        state.eventSurface = eventSurface;

        const setCursor = (cursor) => {
            if (canvas?.style) {
                canvas.style.cursor = cursor;
            }
            if (eventSurface && eventSurface !== canvas && eventSurface.style) {
                eventSurface.style.cursor = cursor;
            }
        };

        const isProxyPointerTarget = (event) => {
            const target = event?.target;
            if (!target?.closest) {
                return false;
            }
            return Boolean(target.closest('.topology-node-proxy, .topology-node-inspector-toggle'));
        };

        const isDiagnosticsControlTarget = (event) => {
            const target = event?.target;
            if (!target?.closest) {
                return false;
            }
            return Boolean(target.closest('.topology-diag-panel, .topology-diag-panel__collapsed-chip'));
        };

        const handleBackgroundClick = (event) => {
            if (!state || !state.dotNetRef) {
                return;
            }
            if (isProxyPointerTarget(event) || isPointerInSettingsButton(event)) {
                return;
            }
            if (isDiagnosticsControlTarget(event)) {
                return;
            }
            const target = event.target;
            const withinHost = eventSurface && (target === eventSurface || target === canvas || target?.closest?.('.topology-node-layer'));
            if (!withinHost) {
                return;
            }
            clearHover();
            state.dotNetRef.invokeMethodAsync('OnCanvasBackgroundClicked');
        };

        registerActiveState(state);

        const cancelScheduledHover = () => {
            if (state.hoverFrameRequestId !== null) {
                cancelAnimationFrame(state.hoverFrameRequestId);
                state.hoverFrameRequestId = null;
            }
            state.pendingHover = null;
            state.hoverDropReported = false;
        };

        const cancelDelayedResume = () => {
            if (state.pendingDelayedResume !== null) {
                cancelAnimationFrame(state.pendingDelayedResume);
                state.pendingDelayedResume = null;
            }
        };

        const scheduleDelayedResume = (snapshot) => {
            cancelDelayedResume();
            const dispatch = () => {
                if (state.dragging || state.dragActive || state.dragEnding) {
                    state.pendingDelayedResume = window.requestAnimationFrame(dispatch);
                    return;
                }
                state.pendingDelayedResume = null;
                const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
                    ? performance.now()
                    : Date.now();
                const delta = state.lastHoverResumeTimestamp
                    ? Number((now - state.lastHoverResumeTimestamp).toFixed(3))
                    : null;
                debugLog(state, '[Topology] hover follow-up after drag', { delayedResume: snapshot, resumeDeltaMs: delta });
                queueHoverUpdate(snapshot, false);
            };
            if (!state.pendingDelayedResume) {
                state.pendingDelayedResume = window.requestAnimationFrame(dispatch);
            }
        };

        const applyDragMovement = (point) => {
            if (!state.dragging || state.dragEnding || !point) {
                return;
            }
            const clientX = point.clientX;
            const clientY = point.clientY;
            const dx = clientX - state.dragStart.x;
            const dy = clientY - state.dragStart.y;
            state.offsetX = state.panStart.x + dx;
            state.offsetY = state.panStart.y + dy;
            if (state.lastPanSample) {
                const moveDx = clientX - state.lastPanSample.x;
                const moveDy = clientY - state.lastPanSample.y;
                const moveDistance = Math.hypot(moveDx, moveDy);
                if (moveDistance > 0) {
                    state.panStats ??= createPanStats();
                    state.panStats.distance += moveDistance;
                    state.panStatsSinceUpload ??= createPanStats();
                    state.panStatsSinceUpload.distance += moveDistance;
                }
            }
            state.lastPanSample = { x: clientX, y: clientY };
            if (canvas) {
                const rect = getCanvasClientRect(canvas, state);
                state.lastPointerSnapshot = capturePointerSnapshot({ clientX, clientY }, rect);
            }
            const timestamp = typeof point.timeStamp === 'number'
                ? point.timeStamp
                : (typeof performance !== 'undefined' && typeof performance.now === 'function'
                    ? performance.now()
                    : Date.now());
            state.lastPointerSample = { x: clientX, y: clientY, time: timestamp };
            draw(canvas, state);
            canvas.style.cursor = 'grabbing';
        };

        const cancelPanFrame = () => {
            if (state.panFrameRequestId !== null) {
                cancelAnimationFrame(state.panFrameRequestId);
                state.panFrameRequestId = null;
            }
            state.pendingPan = null;
        };

        const requestPanFrame = () => {
            if (state.panFrameRequestId !== null) {
                return;
            }
            state.panFrameRequestId = window.requestAnimationFrame(() => {
                state.panFrameRequestId = null;
                processPendingPan();
            });
        };

        const processPendingPan = () => {
            const pending = state.pendingPan;
            if (!pending || !state.dragging || state.dragEnding) {
                state.pendingPan = null;
                return;
            }
            state.pendingPan = null;
            applyDragMovement(pending);
            if (state.pendingPan) {
                requestPanFrame();
            }
        };

        const clearHover = () => {
            const previousNodeId = state.hoveredNodeId;
            let invalidated = false;
            let hoverChanged = false;
            if (state.hoveredChipId !== null || state.hoveredChip !== null) {
                state.hoveredChipId = null;
                state.hoveredChip = null;
                invalidated = true;
            }

            if (setHoveredEdge(state, null)) {
                invalidated = true;
            }

            resetHoverCache(state);

            if (state.hoveredNodeId !== null) {
                state.hoveredNodeId = null;
                hoverChanged = true;
            }

            let needsRedraw = invalidated || hoverChanged;
            if (needsRedraw && !hasHoverVisualDelta(state)) {
                needsRedraw = false;
            }
            if (needsRedraw) {
                draw(canvas, state);
            }

            if (hoverChanged || previousNodeId !== state.hoveredNodeId) {
                notifyNodeHoverChanged(state);
            }
            state.lastPointerSample = null;
        };

        const isPointerInSettingsButton = (event) => {
            if (!state || !state.settingsHitbox) {
                return false;
            }

            const rect = getCanvasClientRect(canvas, state);
            const x = event.clientX - rect.left;
            const y = event.clientY - rect.top;
            const { x: hx, y: hy, width, height } = state.settingsHitbox;
            return x >= hx && x <= hx + width && y >= hy && y <= hy + height;
        };

        const updateCursorForEvent = (event) => {
            if (!canvas) {
                return;
            }

            if (isPointerInSettingsButton(event) || state.settingsPointerActive) {
                setCursor('pointer');
            } else if (!state.dragging) {
                setCursor('default');
            }
        };

        const applyHover = (pointer) => {
            const previousNodeId = state.hoveredNodeId;
            incrementPointerStat(state, 'processed');
            if (!state.scenePayload || !state.overlayPayload) {
                const changed = setHoveredEdge(state, null);
                if ((state.hoveredChipId !== null || state.hoveredChip !== null) || changed) {
                    state.hoveredChipId = null;
                    state.hoveredChip = null;
                    draw(canvas, state);
                }
                resetHoverCache(state);
                if (state.hoveredNodeId !== null) {
                    state.hoveredNodeId = null;
                }
                if (previousNodeId !== state.hoveredNodeId) {
                    notifyNodeHoverChanged(state);
                }
                return;
            }

            const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
                ? performance.now()
                : Date.now();
            let pointerSpeed = 0;
            if (state.lastPointerSample) {
                const dt = Math.max(1, now - state.lastPointerSample.time);
                const dx = pointer.clientX - state.lastPointerSample.x;
                const dy = pointer.clientY - state.lastPointerSample.y;
                const distance = Math.sqrt(dx * dx + dy * dy);
                pointerSpeed = (distance / dt) * 1000;
            }
            state.lastPointerSample = { x: pointer.clientX, y: pointer.clientY, time: now };
            const clientX = pointer.clientX - pointer.rectLeft;
            const clientY = pointer.clientY - pointer.rectTop;
            const worldX = (clientX - state.offsetX) / state.scale;
            const worldY = (clientY - state.offsetY) / state.scale;
            let invalidated = false;

            state.hoverCache ??= createHoverCache();
            const cache = state.hoverCache;
            const lastWorldX = typeof cache.worldX === 'number' ? cache.worldX : null;
            const lastWorldY = typeof cache.worldY === 'number' ? cache.worldY : null;
            const withinEpsilon = lastWorldX !== null && lastWorldY !== null
                ? Math.abs(worldX - lastWorldX) <= HOVER_CACHE_WORLD_EPSILON && Math.abs(worldY - lastWorldY) <= HOVER_CACHE_WORLD_EPSILON
                : false;
            const sameScale = cache.scale === state.scale;
            const disableCache = state.disableHoverCache === true;
            const canReuseChip = !disableCache && withinEpsilon && sameScale && cache.chipVersion === state.chipHitVersion;
            const canReuseEdge = !disableCache && withinEpsilon && sameScale && cache.edgeVersion === state.edgeHitVersion;

            const chipHitboxesAvailable = Array.isArray(state.chipHitboxes) && state.chipHitboxes.length > 0;
            let hitChip = null;
            if (chipHitboxesAvailable) {
                if (canReuseChip) {
                    hitChip = cache.chipHit ?? null;
                } else {
                    hitChip = hitTestChip(state, worldX, worldY);
                    cache.chipHit = hitChip;
                    cache.chipVersion = state.chipHitVersion;
                }
            } else {
                cache.chipHit = null;
                cache.chipVersion = state.chipHitVersion;
            }

            const nextChipId = hitChip ? hitChip.id : null;
            if (!chipHitboxesAvailable) {
                if (state.hoveredChipId !== null || state.hoveredChip !== null) {
                    state.hoveredChipId = null;
                    state.hoveredChip = null;
                    invalidated = true;
                }
            } else if (nextChipId !== state.hoveredChipId) {
                state.hoveredChipId = nextChipId;
                invalidated = true;
            }

            const edgeHitboxesAvailable = Array.isArray(state.edgeHitboxes) && state.edgeHitboxes.length > 0;
            let edgeHit = null;
            if (edgeHitboxesAvailable) {
                if (canReuseEdge) {
                    edgeHit = cache.edgeHit ?? null;
                } else {
                    edgeHit = hitTestEdge(state, worldX, worldY);
                    cache.edgeHit = edgeHit;
                    cache.edgeVersion = state.edgeHitVersion;
                }
            } else {
                cache.edgeHit = null;
                cache.edgeVersion = state.edgeHitVersion;
            }

            cache.worldX = worldX;
            cache.worldY = worldY;
            cache.scale = state.scale;

            if (edgeHit) {
                setCursor('pointer');
            } else if (!state.dragging && !state.settingsPointerActive) {
                setCursor('default');
            }

            if (setHoveredEdge(state, edgeHit ? edgeHit.id : null)) {
                invalidated = true;
            }

            const intentBypassActive = !state.dragging &&
                typeof state.hoverIntentBypassUntil === 'number' &&
                state.hoverIntentBypassUntil > now;
            const shouldThrottleIntent = !intentBypassActive && pointerSpeed > HOVER_INTENT_SPEED_THRESHOLD;
            let allowHoverHit = true;
            if (shouldThrottleIntent) {
                const lastDispatch = Number(state.lastNodeHoverDispatchTime ?? 0);
                const elapsedSinceDispatch = now - lastDispatch;
                if (elapsedSinceDispatch < HOVER_INTENT_MAX_SUPPRESSION_MS) {
                    allowHoverHit = false;
                }
            }
            if (intentBypassActive && !state.dragging) {
                state.hoverIntentBypassUntil = 0;
            } else if (!intentBypassActive && !state.dragging && typeof state.hoverIntentBypassUntil === 'number' && state.hoverIntentBypassUntil > 0 && state.hoverIntentBypassUntil <= now) {
                state.hoverIntentBypassUntil = 0;
            }
            if (!allowHoverHit && shouldThrottleIntent) {
                state.pointerThrottleSkips = (state.pointerThrottleSkips ?? 0) + 1;
                state.pointerThrottleSkipsSinceUpload = (state.pointerThrottleSkipsSinceUpload ?? 0) + 1;
                incrementPointerStat(state, 'intentSkips');
            }
            const nodeHit = allowHoverHit
                ? hitTestNode(state, worldX, worldY)
                : null;
            const nextNodeId = nodeHit ? (nodeHit.id ?? null) : null;
            let hoverChanged = nextNodeId !== state.hoveredNodeId;
            if (hoverChanged) {
                state.hoveredNodeId = nextNodeId;
            }

            let needsRedraw = invalidated || hoverChanged;
            if (needsRedraw && !hasHoverVisualDelta(state)) {
                needsRedraw = false;
            }
            if (needsRedraw) {
                draw(canvas, state);
            }

            if (hoverChanged) {
                notifyNodeHoverChanged(state);
            }
        };

        const processQueuedHover = () => {
            const pending = state.pendingHover;
            state.pendingHover = null;
            state.hoverDropReported = false;
            if (!pending) {
                return;
            }
            applyHover(pending);
        };

        const queueHoverUpdate = (event, forceImmediate) => {
            if (!canvas) {
                return;
            }

            if (state.dragging && !forceImmediate) {
                debugLog(state, '[Topology] hover skipped (dragging)');
                return;
            }

            if (state.hoverSuspended && !forceImmediate) {
                debugLog(state, '[Topology] hover skipped while suspended', { forceImmediate });
                cancelScheduledHover();
                return;
            }

            const rect = typeof event?.rectLeft === 'number'
                ? {
                    left: event.rectLeft,
                    top: event.rectTop,
                    right: event.rectRight ?? event.rectLeft,
                    bottom: event.rectBottom ?? event.rectTop
                }
                : getCanvasClientRect(canvas, state);
            const pointerEvent = typeof event?.rectLeft === 'number'
                ? event
                : {
                    clientX: event.clientX,
                    clientY: event.clientY,
                    rectLeft: rect.left,
                    rectTop: rect.top,
                    rectRight: rect.right,
                    rectBottom: rect.bottom
                };
            const inside = pointerEvent.clientX >= rect.left &&
                pointerEvent.clientX <= rect.right &&
                pointerEvent.clientY >= rect.top &&
                pointerEvent.clientY <= rect.bottom;

            if (!inside) {
                cancelScheduledHover();
                clearHover();
                state.lastPointerSnapshot = null;
                return;
            }

            const snapshot = capturePointerSnapshot(pointerEvent, rect);
            const hadPending = Boolean(state.pendingHover);
            state.pendingHover = snapshot;
            state.lastPointerSnapshot = snapshot ? { ...snapshot } : null;
            incrementPointerStat(state, 'received');
            markPointerInteraction(state);

            if (forceImmediate) {
                if (state.hoverFrameRequestId !== null) {
                    cancelAnimationFrame(state.hoverFrameRequestId);
                    state.hoverFrameRequestId = null;
                }
                processQueuedHover();
                return;
            }

            if (hadPending) {
                incrementPointerStat(state, 'queueDrops');
            }

            if (state.hoverFrameRequestId === null) {
                state.hoverFrameRequestId = window.requestAnimationFrame(() => {
                    state.hoverFrameRequestId = null;
                    processQueuedHover();
                });
            }
        };

        const pointerDown = (event) => {
            if (event.button !== 0) {
                return;
            }
            state.pointerClickCandidate = false;
            if (isProxyPointerTarget(event)) {
                return;
            }
            if (isDiagnosticsControlTarget(event)) {
                setCursor('default');
                return;
            }
            if (isPointerInSettingsButton(event)) {
                eventSurface?.setPointerCapture?.(event.pointerId);
                state.settingsPointerActive = true;
                state.settingsPointerId = event.pointerId;
                state.settingsPointerDown = { x: event.clientX, y: event.clientY };
                state.pointerMoved = false;
                state.pointerClickCandidate = true;
                setCursor('pointer');
                return;
            }
            eventSurface?.setPointerCapture?.(event.pointerId);
            cancelPanFrame();
            state.dragging = true;
            state.dragActive = false;
            state.dragPointerId = event.pointerId;
            state.dragStart = { x: event.clientX, y: event.clientY };
            state.panStart = { x: state.offsetX, y: state.offsetY };
            state.dragStartTime = performance.now ? performance.now() : Date.now();
            state.userAdjusted = true;
            state.pointerDownPoint = { x: event.clientX, y: event.clientY };
            state.pointerMoved = true;
            state.pointerClickCandidate = true;
            state.settingsPointerActive = false;
            state.settingsPointerId = null;
            state.settingsPointerDown = null;
            state.hoverIntentBypassUntil = 0;
            if (canvas) {
                const rect = getCanvasClientRect(canvas, state);
                state.lastPointerSnapshot = capturePointerSnapshot(event, rect);
            }
            state.lastPanSample = { x: event.clientX, y: event.clientY };
            markPointerInteraction(state);
        };

        const activateDragMotion = () => {
            if (state.dragActive) {
                return;
            }
            state.dragActive = true;
            state.hoverSuspended = true;
            cancelDelayedResume();
            cancelScheduledHover();
            debugLog(state, '[Topology] hover suspended (drag start)');
            clearHover();
            setCursor('grabbing');
        };

        const pointerMove = (event) => {
            updateCursorForEvent(event);

            if (state.settingsPointerActive) {
                const wasCandidate = state.pointerClickCandidate;
                state.pointerClickCandidate = false;
                const snapshot = capturePointerSnapshot(event, getCanvasClientRect(canvas, state));
                state.lastPointerSnapshot = snapshot;
                state.lastPointerSample = {
                    x: snapshot.clientX,
                    y: snapshot.clientY,
                    time: typeof performance !== 'undefined' && typeof performance.now === 'function'
                        ? performance.now()
                        : Date.now()
                };
                queueHoverUpdate(snapshot, false);
                if (wasCandidate) {
                    state.pointerClickCandidate = true;
                }
                return;
            }

            if (isDiagnosticsControlTarget(event)) {
                return;
            }

            if (!state.dragging) {
                markPointerInteraction(state);
                queueHoverUpdate(event);
                return;
            }
            if (state.dragEnding) {
                return;
            }
            if (state.pointerClickCandidate) {
                state.pointerClickCandidate = false;
                state.pointerMoved = true;
                activateDragMotion();
                cancelScheduledHover();
                state.lastPointerSnapshot = null;
            }
            state.pendingPan = {
                clientX: event.clientX,
                clientY: event.clientY,
                timeStamp: typeof event.timeStamp === 'number'
                    ? event.timeStamp
                    : (typeof performance !== 'undefined' && typeof performance.now === 'function'
                        ? performance.now()
                        : Date.now())
            };
            requestPanFrame();
            markPointerInteraction(state);
        };

        const pointerUp = (event) => {
            const isPrimaryButton = typeof event.button !== 'number' || event.button === 0;
            if (!isPrimaryButton) {
                return;
            }
            if (isDiagnosticsControlTarget(event)) {
                return;
            }
            if (state.settingsPointerActive && state.settingsPointerId === event.pointerId) {
                if (eventSurface?.hasPointerCapture?.(event.pointerId)) {
                    eventSurface.releasePointerCapture(event.pointerId);
                }
                const inside = isPointerInSettingsButton(event);
                const moved = !state.pointerClickCandidate;
                state.settingsPointerActive = false;
                state.settingsPointerId = null;
                state.settingsPointerDown = null;
                state.pointerDownPoint = null;
                state.pointerClickCandidate = false;
                if (!moved && inside && state.dotNetRef) {
                    state.dotNetRef.invokeMethodAsync('OnSettingsRequestedFromCanvas');
                }
                const snapshot = {
                    clientX: clamp(event.clientX, canvasRect.left, canvasRect.right),
                    clientY: clamp(event.clientY, canvasRect.top, canvasRect.bottom),
                    rectLeft: canvasRect.left,
                    rectTop: canvasRect.top,
                    rectRight: canvasRect.right,
                    rectBottom: canvasRect.bottom
                };
                queueHoverUpdate(snapshot, true);
                setCursor(inside ? 'pointer' : 'default');
                return;
            }
            if (eventSurface?.hasPointerCapture?.(event.pointerId)) {
                eventSurface.releasePointerCapture(event.pointerId);
            }
            const wasDragging = state.dragging;
            state.dragPointerId = null;
            markPointerInteraction(state);
            const canvasRect = getCanvasClientRect(canvas, state);
            const clamp = (value, min, max) => Math.min(Math.max(value, min), max);
            const releaseSnapshot = {
                clientX: clamp(event.clientX, canvasRect.left, canvasRect.right),
                clientY: clamp(event.clientY, canvasRect.top, canvasRect.bottom),
                rectLeft: canvasRect.left,
                rectTop: canvasRect.top,
                rectRight: canvasRect.right,
                rectBottom: canvasRect.bottom
            };
            state.lastPointerSnapshot = { ...releaseSnapshot };
            const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
                ? performance.now()
                : Date.now();
            state.hoverIntentBypassUntil = now + HOVER_INTENT_RESUME_GRACE_MS;
            state.lastPointerSample = {
                x: releaseSnapshot.clientX,
                y: releaseSnapshot.clientY,
                time: now
            };
            const performedDrag = state.dragActive;
            const finalizeRelease = () => {
                state.hoverSuspended = false;
                state.dragEnding = true;
                state.dragActive = false;
                state.dragging = false;
                cancelPanFrame();
                const resumeSnapshot = state.lastPointerSnapshot ?? releaseSnapshot;
                const resumeTime = typeof performance !== 'undefined' && typeof performance.now === 'function'
                    ? performance.now()
                    : Date.now();
                state.lastHoverResumeTimestamp = resumeTime;
                debugLog(state, '[Topology] hover resumed after drag', { resumeSnapshot, resumeTime });
                queueHoverUpdate(resumeSnapshot, false);
                const delayedResume = state.lastPointerSnapshot ?? resumeSnapshot;
                scheduleDelayedResume(delayedResume);
                updateWorldCenter(state);
                emitViewportChanged(canvas, state, { immediate: true });
            };
            if (performedDrag) {
                finalizeRelease();
            } else {
                state.dragging = false;
                state.hoverSuspended = false;
                state.dragEnding = false;
                state.dragActive = false;
                cancelPanFrame();
                cancelDelayedResume();
                const resumeSnapshot = state.lastPointerSnapshot ?? releaseSnapshot;
                state.lastHoverResumeTimestamp = typeof performance !== 'undefined' && typeof performance.now === 'function'
                    ? performance.now()
                    : Date.now();
                queueHoverUpdate(resumeSnapshot, true);
            }
            if (!performedDrag && state.pointerClickCandidate && state.pointerDownPoint) {
                const rect = getCanvasClientRect(canvas, state);
                const clientX = event.clientX - rect.left;
                const clientY = event.clientY - rect.top;
                const worldX = (clientX - state.offsetX) / state.scale;
                const worldY = (clientY - state.offsetY) / state.scale;
                const edgeHit = hitTestEdge(state, worldX, worldY);
                if (edgeHit) {
                    state.pointerDownPoint = null;
                    state.pointerClickCandidate = false;
                    setCursor('default');
                    return;
                }
                const hit = hitTestChip(state, worldX, worldY);
                if (!hit && !isProxyPointerTarget(event) && !wasDragging) {
                    const hadHover = state.hoveredNodeId !== null || state.hoveredChipId !== null;
                    if (state.hoveredNodeId !== null) {
                        state.hoveredNodeId = null;
                    }
                    if (state.hoveredChipId !== null || state.hoveredChip !== null) {
                        state.hoveredChipId = null;
                        state.hoveredChip = null;
                    }
                    if (hadHover) {
                        draw(canvas, state);
                        notifyNodeHoverChanged(state);
                    }
                    if (state.dotNetRef) {
                        state.dotNetRef.invokeMethodAsync('OnCanvasBackgroundClicked');
                    }
                }
            }
            state.pointerDownPoint = null;
            state.pointerClickCandidate = false;
            state.lastPanSample = null;
            state.dragActive = false;
            state.dragging = false;
            state.dragEnding = false;
            setCursor('default');
        };

        const handleWindowPointerUp = (event) => {
            if (!state) {
                return;
            }
            const matchesDragPointer = state.dragPointerId === null || state.dragPointerId === event.pointerId;
            const canHandle = (state.dragging && matchesDragPointer) ||
                (state.settingsPointerActive && state.settingsPointerId === event.pointerId);
            if (!canHandle) {
                return;
            }
            pointerUp(event);
        };

        const pointerLeave = (event) => {
            const primaryDown = (event.buttons & 1) === 1;
            const matchesDragPointer = state.dragPointerId === null || state.dragPointerId === event.pointerId;
            if (state.dragging && primaryDown && matchesDragPointer) {
                // Still dragging; keep capture and skip clearing hover so release can resume immediately.
                return;
            }
            if (eventSurface?.hasPointerCapture?.(event.pointerId)) {
                eventSurface.releasePointerCapture(event.pointerId);
            }
            const wasDragging = state.dragging;
            state.dragging = false;
            state.hoverSuspended = false;
            state.dragEnding = false;
            cancelPanFrame();
            state.pointerDownPoint = null;
            state.pointerClickCandidate = false;
            state.settingsPointerActive = false;
            state.settingsPointerId = null;
            state.settingsPointerDown = null;
            state.lastPointerSnapshot = null;
            if (wasDragging) {
                updateWorldCenter(state);
                emitViewportChanged(canvas, state, { immediate: true });
            }
            cancelScheduledHover();
            clearHover();
            setCursor('default');
            state.lastPanSample = null;
        };

        const wheel = (event) => {
            event.preventDefault();
            markPointerInteraction(state);

            const rect = getCanvasClientRect(canvas, state);
            const offsetX = event.clientX - rect.left;
            const offsetY = event.clientY - rect.top;
            const deltaY = event.deltaY;
            const scaleFactor = deltaY < 0 ? 1.1 : 0.9;
            const focusX = (offsetX - state.offsetX) / state.scale;
            const focusY = (offsetY - state.offsetY) / state.scale;

            const newScale = clamp(state.scale * scaleFactor, MIN_SCALE, MAX_SCALE);
            const previousScale = state.scale;
            state.scale = newScale;
            state.offsetX = offsetX - focusX * state.scale;
            state.offsetY = offsetY - focusY * state.scale;
            state.userAdjusted = true;

            state.overlayScale = newScale;
            state.lastOverlayZoom = newScale;
            const zoomPercent = clamp(newScale * 100, MIN_ZOOM_PERCENT, MAX_ZOOM_PERCENT);
            if (state.dotNetRef) {
                state.dotNetRef.invokeMethodAsync('OnCanvasZoomChanged', zoomPercent);
            }
            state.zoomStats ??= createZoomStats();
            state.zoomStats.events += 1;
            state.zoomStatsSinceUpload ??= createZoomStats();
            state.zoomStatsSinceUpload.events += 1;

            updateWorldCenter(state);

            draw(canvas, state);
            queueHoverUpdate(event);
            emitViewportChanged(canvas, state);
        };

        const wheelListenerOptions = { passive: false };

        if (eventSurface) {
            eventSurface.addEventListener('pointerdown', pointerDown);
            eventSurface.addEventListener('pointermove', pointerMove);
            eventSurface.addEventListener('pointerup', pointerUp);
            eventSurface.addEventListener('pointerleave', pointerLeave);
            eventSurface.addEventListener('wheel', wheel, wheelListenerOptions);
            eventSurface.addEventListener('click', handleBackgroundClick);
        }

        state.windowPointerUpHandler = (event) => handleWindowPointerUp(event);
        state.windowPointerCancelHandler = (event) => handleWindowPointerUp(event);
        window.addEventListener('pointerup', state.windowPointerUpHandler, true);
        window.addEventListener('pointercancel', state.windowPointerCancelHandler, true);

        state.cleanup = () => {
            window.removeEventListener('resize', resize);
            const listenerTarget = state.eventSurface ?? canvas;
            if (listenerTarget) {
                listenerTarget.removeEventListener('pointerdown', pointerDown);
                listenerTarget.removeEventListener('pointermove', pointerMove);
                listenerTarget.removeEventListener('pointerup', pointerUp);
                listenerTarget.removeEventListener('pointerleave', pointerLeave);
                listenerTarget.removeEventListener('wheel', wheel, wheelListenerOptions);
                listenerTarget.removeEventListener('click', handleBackgroundClick);
            }
            if (state.windowPointerUpHandler) {
                window.removeEventListener('pointerup', state.windowPointerUpHandler, true);
                state.windowPointerUpHandler = null;
            }
            if (state.windowPointerCancelHandler) {
                window.removeEventListener('pointercancel', state.windowPointerCancelHandler, true);
                state.windowPointerCancelHandler = null;
            }
            state.resizeObserver?.disconnect?.();
            state.resizeObserver = null;
            cancelScheduledHover();
            cancelDelayedResume();
            cancelPanFrame();
            resetHoverCache(state);
            clearHoverStats(state);
            resetCanvasStats(state);
            destroyDiagnosticsHud(state);
            state.diagnosticsOptions = null;
            if (state.diagnosticsUploadHandle) {
                clearInterval(state.diagnosticsUploadHandle);
                state.diagnosticsUploadHandle = null;
            }
            cancelViewportDebounce(state);
            state.canvasRectCache = null;
            state.dotNetRef = null;
            unregisterActiveState(state);
            state.sceneBounds = null;
            state.edgeSpatialCache = null;
            state.edgeSpatialStats = createEdgeSpatialStats(state.edgeSpatialIndex?.cellSize ?? EDGE_SPATIAL_INDEX_CELL_SIZE);
        };
    }

    function draw(canvas, state) {
        if (!state.scenePayload || !state.overlayPayload) {
            clear(state);
            state.chipHitboxes = [];
            state.chipHitVersion = (state.chipHitVersion ?? 0) + 1;
            state.edgeHitboxes = [];
            state.edgeHitVersion = (state.edgeHitVersion ?? 0) + 1;
            state.nodeHitboxes = [];
            state.sceneRawNodes = [];
            state.sceneRawEdges = [];
            state.sceneBounds = null;
            state.sceneNodeMap = new Map();
        state.sceneLegacyTooltip = null;
        state.edgeOverlayContext = null;
        state.edgeOverlayLegend = null;
        resetEdgeSpatialIndex(state);
        state.sceneDirty = true;
            state.collectingChipHitboxes = false;
            state.refreshChipHitboxes = false;
            const previousChipId = state.hoveredChipId;
            const previousNodeId = state.hoveredNodeId;
            state.hoveredChipId = null;
            state.hoveredChip = null;
            state.hoveredNodeId = null;
            resetHoverCache(state);
            if (previousChipId !== state.hoveredChipId || previousNodeId !== state.hoveredNodeId) {
                notifyNodeHoverChanged(state);
            }
            return;
        }

        const ctx = state.ctx;
        const overlaySettings = state.overlaySettings ?? parseOverlaySettings(state.overlayPayload.overlays ?? state.overlayPayload.Overlays ?? {});
        const nodesPayload = state.scenePayload.nodes ?? state.scenePayload.Nodes ?? [];
        const edgesPayload = state.scenePayload.edges ?? state.scenePayload.Edges ?? [];
        const legacyTooltipValue = state.overlayPayload.tooltip ?? state.overlayPayload.Tooltip ?? null;
        const edgeSeries = state.edgeSeries ?? new Map();
        const edgeSeriesStartIndex = Number(state.edgeSeriesStartIndex ?? 0);
        const overlayNodesPayload = state.overlayPayload.nodes ?? state.overlayPayload.Nodes ?? [];
        const overlayNodeLookup = new Map();
        for (const entry of overlayNodesPayload) {
            if (!entry) {
                continue;
            }
            const identifier = entry.id ?? entry.Id;
            if (!identifier) {
                continue;
            }
            overlayNodeLookup.set(identifier, entry);
        }

        let rebuildStaticScene = state.sceneDirty === true;
        const refreshChipHitboxes = Boolean(state.refreshChipHitboxes);
        if (rebuildStaticScene) {
            incrementSceneStat(state, 'rebuilds');
        } else {
            incrementSceneStat(state, 'overlayUpdates');
        }
        if (rebuildStaticScene) {
            state.sceneRawNodes = nodesPayload;
            state.sceneRawEdges = edgesPayload;
            state.sceneBounds = computeSceneBoundsFromNodes(nodesPayload);
            state.sceneLegacyTooltip = legacyTooltipValue;
            state.nodeHitboxes = [];
            state.chipHitboxes = [];
            state.chipHitVersion = (state.chipHitVersion ?? 0) + 1;
            state.edgeHitboxes = [];
            state.edgeHitVersion = (state.edgeHitVersion ?? 0) + 1;
            state.tooltipMetrics = null;
            resetEdgeSpatialIndex(state);

            const nodeMap = new Map();
            for (const n of nodesPayload) {
                const identifier = n.id ?? n.Id;

                const meta = {
                    id: identifier,
                    x: n.x ?? n.X,
                    y: n.y ?? n.Y,
                    width: n.width ?? n.Width ?? 54,
                    height: n.height ?? n.Height ?? 24,
                    cornerRadius: n.cornerRadius ?? n.CornerRadius ?? 3,
                    sparkline: n.sparkline ?? n.Sparkline ?? null,
                    isFocused: !!(n.isFocused ?? n.IsFocused),
                    visible: !(n.isVisible === false || n.IsVisible === false),
                    kind: String(n.kind ?? n.Kind ?? 'service'),
                    fill: n.fill ?? n.Fill ?? getLeafFill(),
                    focusLabel: n.focusLabel ?? n.FocusLabel ?? '',
                    metrics: n.metrics ?? n.Metrics ?? null,
                    semantics: n.semantics ?? n.Semantics ?? null,
                    nodeRole: n.nodeRole ?? n.NodeRole ?? null,
                    dispatchSchedule: n.dispatchSchedule ?? n.DispatchSchedule ?? null,
                    distribution: n.distribution ?? n.Distribution ?? (n.semantics?.distribution ?? n.Semantics?.Distribution ?? null),
                    leaf: Boolean(n.isLeaf ?? n.IsLeaf),
                    lane: Number.isFinite(n.lane ?? n.Lane) ? Number(n.lane ?? n.Lane) : null,
                    tooltip: n.tooltip ?? n.Tooltip ?? null
                };

                const overlayNode = overlayNodeLookup.get(identifier);
                if (overlayNode) {
                    meta.fill = overlayNode.fill ?? overlayNode.Fill ?? meta.fill;
                    meta.focusLabel = overlayNode.focusLabel ?? overlayNode.FocusLabel ?? meta.focusLabel;
                    meta.metrics = overlayNode.metrics ?? overlayNode.Metrics ?? meta.metrics ?? null;
                    meta.tooltip = overlayNode.tooltip ?? overlayNode.Tooltip ?? meta.tooltip;
                    meta.stroke = overlayNode.stroke ?? overlayNode.Stroke ?? undefined;
                    if (typeof overlayNode.isFocused === 'boolean' || typeof overlayNode.IsFocused === 'boolean') {
                        meta.isFocused = Boolean(overlayNode.isFocused ?? overlayNode.IsFocused);
                    }
                    if (typeof overlayNode.isVisible === 'boolean' || typeof overlayNode.IsVisible === 'boolean') {
                        meta.visible = !(overlayNode.isVisible === false || overlayNode.IsVisible === false);
                    }
                }

                nodeMap.set(identifier, meta);
                registerNodeHitbox(state, meta);
            }

            for (const [, meta] of nodeMap.entries()) {
                const kind = String(meta.kind || '').toLowerCase();
                if ((kind === 'const' || kind === 'constant') && !meta.sparkline) {
                    const inline = meta.semantics?.inline;
                    if (inline && Array.isArray(inline) && inline.length > 0) {
                        const slice = { values: inline.slice(), startIndex: 0 };
                        meta.sparkline = {
                            values: inline.slice(),
                            utilization: [],
                            errorRate: [],
                            queueDepth: [],
                            startIndex: 0,
                            series: { values: slice }
                        };
                    }
                }
            }

            state.sceneNodeMap = nodeMap;
            const overlayContext = buildEdgeOverlayContext(edgesPayload, nodeMap, overlaySettings, edgeSeries, edgeSeriesStartIndex);
            state.edgeOverlayContext = overlayContext;
            state.edgeOverlayLegend = overlayContext?.legend ?? null;
            state.lastNodeCount = nodesPayload.length;
            state.lastEdgeCount = edgesPayload.length;
            state.sceneDirty = false;
        } else if (refreshChipHitboxes) {
            state.chipHitboxes = [];
            state.chipHitVersion = (state.chipHitVersion ?? 0) + 1;
        }

        const nodeMap = state.sceneNodeMap ?? new Map();
        const nodes = state.sceneRawNodes ?? nodesPayload;
        const edges = state.sceneRawEdges ?? edgesPayload;
        const legacyTooltip = state.sceneLegacyTooltip ?? legacyTooltipValue;
        const overlayContext = state.edgeOverlayContext;

        const frameStart = typeof performance !== 'undefined' && typeof performance.now === 'function'
            ? performance.now()
            : Date.now();
        clear(state);

        // Sync overlay DOM (proxies + tooltip) with canvas pan/zoom so hover/focus hitboxes and callouts align
        applyOverlayTransform(canvas, state);
        state.collectingHitboxes = rebuildStaticScene;
        state.collectingChipHitboxes = rebuildStaticScene || refreshChipHitboxes;

        ctx.save();
        ctx.translate(state.offsetX, state.offsetY);
        ctx.scale(state.scale, state.scale);

        const sparkColor = resolveSparklineColor(overlaySettings.colorBasis);
        const portPadding = 0;

        let focusedId = null;
        for (const [id, meta] of nodeMap.entries()) {
            if (meta.isFocused) {
                focusedId = id;
                break;
            }
        }
        state.focusedNodeId = focusedId ?? null;
        updateBinDumpChipState(state);

        const hoveredNodeId = state.hoveredNodeId ?? null;
        const emphasisSeedId = focusedId ?? hoveredNodeId ?? null;
        const emphasisEnabled = overlaySettings.neighborEmphasis && emphasisSeedId;
        const neighborNodes = emphasisEnabled ? computeNeighborNodes(edges, emphasisSeedId) : null;
        const neighborEdges = emphasisEnabled ? computeNeighborEdges(edges, emphasisSeedId) : null;

        const defaultEdgeAlpha = 0.85;

        for (const edge of edges) {
            let fromX = edge.fromX ?? edge.FromX;
            let fromY = edge.fromY ?? edge.FromY;
            let toX = edge.toX ?? edge.ToX;
            let toY = edge.toY ?? edge.ToY;

            const fromId = edge.from ?? edge.From;
            const toId = edge.to ?? edge.To;
            const fromNode = nodeMap.get(fromId);
            const toNode = nodeMap.get(toId);

            if (!fromNode || !toNode) {
                continue;
            }

            if (fromNode.visible === false) {
                // treat compute edges as badges only
                continue;
            }

            const resolvedEdgeType = resolveEdgeType(edge);
            const isEffortEdge = resolvedEdgeType === EdgeTypeEffort;
            const isThroughputEdge = resolvedEdgeType === EdgeTypeThroughput;
            const isDependency = resolvedEdgeType === EdgeTypeDependency;

            if (isDependency) {
                const field = String(edge.field ?? edge.Field ?? '').toLowerCase();
                if (!shouldRenderDependencyEdge(field, overlaySettings)) {
                    continue;
                }
            }

            const edgeId = edge.id ?? edge.Id ?? `${fromId}->${toId}`;
            const pointerHoveredEdge = state.hoveredEdgeId && state.hoveredEdgeId === edgeId;
            const inspectorHoveredEdge = state.inspectorEdgeHoverId && state.inspectorEdgeHoverId === edgeId;
            const focusedEdge = state.focusedEdgeId && state.focusedEdgeId === edgeId;
            const neighborHighlighted = neighborEdges?.has(edgeId) ?? false;
            const highlightEdge = pointerHoveredEdge || inspectorHoveredEdge || focusedEdge || !emphasisEnabled || neighborHighlighted;
            const emphasized = pointerHoveredEdge || inspectorHoveredEdge || focusedEdge || (emphasisEnabled && neighborHighlighted);
            const edgeAlpha = emphasized ? 1 : (highlightEdge ? defaultEdgeAlpha : defaultEdgeAlpha * 0.25);

            if (isEffortEdge) {
                const sourceHeight = fromNode.height ?? fromNode.Height ?? 24;
                const sourceX = fromNode.x ?? fromNode.X ?? fromX ?? 0;
                const sourceY = fromNode.y ?? fromNode.Y ?? fromY ?? 0;
                fromX = sourceX;
                fromY = sourceY + (sourceHeight / 2);
            }

            let skipFromPortOffset = false;
            if (isEffortEdge) {
                const sourceX = fromNode.x ?? fromNode.X ?? fromX ?? 0;
                const sourceY = fromNode.y ?? fromNode.Y ?? fromY ?? 0;
                const sourceHeight = fromNode.height ?? fromNode.Height ?? 24;
                fromX = sourceX;
                fromY = sourceY + (sourceHeight / 2);
                skipFromPortOffset = true;
            }

            const baseStrokeColor = isEffortEdge
                ? '#1D4ED8'
                : isThroughputEdge
                    ? '#2563EB'
                    : '#9AA1AC';

            let dashPattern = isDependency ? [4, 3] : (isEffortEdge ? [8, 4] : null);
            let lineWidth = isDependency ? 1.4 : (isEffortEdge ? 3.2 : 3);
            const overlaySample = overlayContext?.samples?.get(edgeId) ?? null;
            if (overlaySample && !isDependency) {
                lineWidth *= 2;
            }
            let strokeColor = baseStrokeColor;
            if (overlaySample?.color) {
                strokeColor = overlaySample.color;
            }

            if (emphasized) {
                strokeColor = focusedEdge ? '#1D4ED8' : '#0EA5E9';
                dashPattern = null;
                lineWidth += 0.6;
            }

            const offset = edgeLaneOffset(edge);
            const fromLane = Number.isFinite(fromNode.lane) ? fromNode.lane : null;
            const toLane = Number.isFinite(toNode.lane) ? toNode.lane : null;
            const fromCenterX = fromNode.x ?? fromNode.X ?? 0;
            const toCenterX = toNode.x ?? toNode.X ?? 0;
            const laneEqual = Number.isFinite(fromLane) && Number.isFinite(toLane) && fromLane === toLane;
            const xAligned = Math.abs(fromCenterX - toCenterX) < 0.1;
            const sameLaneConnection = laneEqual || xAligned;
            const verticallyAdjacent = areNodesVerticallyAdjacent(fromNode, toNode);
            const laneDescriptor = sameLaneConnection ? edgeLaneDescriptor(edge, fromCenterX, toCenterX) : null;
            let pathPoints;
            let startPoint;
            let endPoint;

            ctx.save();
            ctx.globalAlpha = edgeAlpha;
            ctx.lineWidth = lineWidth;
            ctx.strokeStyle = strokeColor;
            if (dashPattern && dashPattern.length > 0) {
                ctx.setLineDash(dashPattern);
            } else {
                ctx.setLineDash([]);
            }

            if (overlaySettings.edgeStyle === 'bezier') {
                if (verticallyAdjacent) {
                    const segment = buildVerticalAdjacentSegment(fromNode, toNode, portPadding);
                    ctx.beginPath();
                    ctx.moveTo(segment.start.x, segment.start.y);
                    ctx.lineTo(segment.end.x, segment.end.y);
                    ctx.stroke();
                    pathPoints = [segment.start, segment.end];
                    startPoint = segment.start;
                    endPoint = segment.end;
                } else if (sameLaneConnection && laneDescriptor) {
                    const segments = buildSameLaneBezierSegments(fromNode, toNode, laneDescriptor, portPadding);
                    ctx.beginPath();
                    ctx.moveTo(segments[0].start.x, segments[0].start.y);
                    for (const seg of segments) {
                        ctx.bezierCurveTo(seg.cp1.x, seg.cp1.y, seg.cp2.x, seg.cp2.y, seg.end.x, seg.end.y);
                    }
                    ctx.stroke();
                    pathPoints = sampleSegmentSeries(segments);
                    startPoint = segments[0].start;
                    endPoint = segments[segments.length - 1].end;
                } else {
                    const path = computeBezierPath(fromNode, toNode, offset, portPadding, laneDescriptor, sameLaneConnection, {
                        forceBottomStart: isEffortEdge
                    });
                    ctx.beginPath();
                    ctx.moveTo(path.start.x, path.start.y);
                    ctx.bezierCurveTo(path.cp1.x, path.cp1.y, path.cp2.x, path.cp2.y, path.end.x, path.end.y);
                    ctx.stroke();
                    pathPoints = path.samples;
                    startPoint = path.start;
                    endPoint = path.end;
                }
            } else {
                let path;
                if (sameLaneConnection && laneDescriptor) {
                    path = computeSiblingLaneElbowPath(fromNode, toNode, laneDescriptor, portPadding);
                } else {
                    const dx = toX - fromX;
                    const dy = toY - fromY;
                    const len = Math.hypot(dx, dy) || 1;
                    const ux = dx / len;
                    const uy = dy / len;

                    const startShrink = skipFromPortOffset ? 0 : computePortOffset(fromNode, ux, uy, portPadding);
                    const endShrink = computePortOffset(toNode, -ux, -uy, portPadding);
                    const sx = skipFromPortOffset ? fromX : fromX + ux * startShrink;
                    const sy = skipFromPortOffset ? fromY : fromY + uy * startShrink;
                    const ex = toX - ux * endShrink;
                    const ey = toY - uy * endShrink;

                    path = computeElbowPath(sx, sy, ex, ey, offset);
                }

                ctx.beginPath();
                drawRoundedPolyline(ctx, path.points, 6);
                ctx.stroke();

                pathPoints = path.points;
                startPoint = path.points[0];
                endPoint = path.points[path.points.length - 1];
            }

            if (overlaySettings.showEdgeArrows && pathPoints.length >= 2) {
                const prevPoint = pathPoints[pathPoints.length - 2];
                const endPathPoint = pathPoints[pathPoints.length - 1];
                drawArrowhead(ctx, prevPoint.x, prevPoint.y, endPathPoint.x, endPathPoint.y);
            }

            const share = edge.share ?? edge.Share;
            if (overlaySettings.showEdgeShares && share !== null && share !== undefined) {
                drawEdgeShare(ctx, pathPoints, share);
            }

            const showEdgeMultipliers = overlaySettings.showEdgeMultipliers !== false;
            if (isEffortEdge && showEdgeMultipliers) {
                const multiplierRaw = edge.multiplier ?? edge.Multiplier;
                const lagRaw = edge.lag ?? edge.Lag;
                const multiplier = Number(multiplierRaw);
                const lagValue = Number(lagRaw);
                if (Number.isFinite(multiplier) && multiplier > 0 && pathPoints.length > 0) {
                    const label = formatMultiplierLabel(multiplier, Number.isFinite(lagValue) ? lagValue : 0);
                    drawEdgeMultiplier(ctx, pathPoints, label, baseStrokeColor);
                }
            }

            if (overlaySample && overlaySettings.showEdgeOverlayLabels !== false) {
                const labelColor = overlaySample.baseColor ?? overlaySample.color ?? '#2563EB';
                drawEdgeOverlayLabel(ctx, pathPoints, overlaySample.text, labelColor);
            } else if (isThroughputEdge && overlaySettings.showEdgeOverlayLabels !== false) {
                const throughputValue = sampleThroughputValue(fromNode, overlaySettings);
                if (throughputValue !== null && throughputValue !== undefined) {
                    const text = formatMetricValue(throughputValue);
                    if (text) {
                        drawEdgeOverlayLabel(ctx, pathPoints, text, baseStrokeColor);
                    }
                }
            }


            if (state.collectingHitboxes) {
                const hitbox = {
                    id: edgeId,
                    points: pathPoints.slice(),
                    type: resolvedEdgeType,
                    bounds: computePolylineBounds(pathPoints)
                };
                state.edgeHitboxes.push(hitbox);
                registerEdgeSpatialEntry(state, hitbox);
            }

            if (dashPattern) {
                ctx.setLineDash([]);
            }

            ctx.restore();
        }

        ctx.globalAlpha = 1;

        const dimmedAlpha = 0.35;

        for (const node of nodes) {
            const id = node.id ?? node.Id;
            const meta = nodeMap.get(id);
            const overlayNode = overlayNodeLookup.get(id);
            const isVisible = overlayNode?.isVisible ?? overlayNode?.IsVisible ?? meta?.visible !== false;

            if (!isVisible) {
                continue;
            }

            const x = node.x ?? node.X;
            const y = node.y ?? node.Y;
            const width = node.width ?? node.Width ?? 54;
            const height = node.height ?? node.Height ?? 24;
            const cornerRadius = node.cornerRadius ?? node.CornerRadius ?? 3;
            const defaultLeafFill = getLeafFill();
            let fill = overlayNode?.fill ?? overlayNode?.Fill ?? meta?.fill ?? defaultLeafFill;
            fill = normalizeNeutralFill(fill);
            const stroke = overlayNode?.stroke ?? overlayNode?.Stroke ?? meta?.stroke ?? '#262626';

            const pointerHovered = hoveredNodeId && hoveredNodeId === id;
            const highlightNode = pointerHovered || !emphasisEnabled || (neighborNodes?.has(id) ?? false);
            const nodeAlpha = highlightNode ? 1 : dimmedAlpha;

            ctx.save();
            ctx.globalAlpha = nodeAlpha;

            const nodeMeta = nodeMap.get(id);
            const kind = String(meta?.kind ?? node.kind ?? node.Kind ?? 'service').toLowerCase();
            const logicalType = String(meta?.logicalType ?? node.logicalType ?? node.LogicalType ?? kind).toLowerCase();
            if (nodeMeta) {
                nodeMeta.logicalType = logicalType;
            }
            const queueLikeNode = isQueueLikeKind(kind, logicalType);

            if (overlayNode) {
                nodeMeta.fill = fill;
                nodeMeta.focusLabel = overlayNode.focusLabel ?? overlayNode.FocusLabel ?? nodeMeta.focusLabel ?? '';
                nodeMeta.metrics = overlayNode.metrics ?? overlayNode.Metrics ?? nodeMeta.metrics ?? null;
                nodeMeta.tooltip = overlayNode.tooltip ?? overlayNode.Tooltip ?? nodeMeta.tooltip ?? null;
                if (typeof overlayNode.isFocused === 'boolean' || typeof overlayNode.IsFocused === 'boolean') {
                    nodeMeta.isFocused = Boolean(overlayNode.isFocused ?? overlayNode.IsFocused);
                }
                if (typeof overlayNode.isVisible === 'boolean' || typeof overlayNode.IsVisible === 'boolean') {
                    nodeMeta.visible = !(overlayNode.isVisible === false || overlayNode.IsVisible === false);
                }
                if (overlayNode.warnings || overlayNode.Warnings) {
                    nodeMeta.warnings = overlayNode.warnings ?? overlayNode.Warnings;
                }
            }
            if (nodeMeta) {
                const warningEntries = normalizeWarningEntries(nodeMeta, state.globalWarningsByNode);
                nodeMeta.queueWarningText = extractQueueWarningText(warningEntries);
            }
            const dlqNode = isDlqKind(kind);
            const nodeRole = String(meta?.nodeRole ?? meta?.NodeRole ?? '').trim().toLowerCase();
            const isSinkNode = kind === 'sink' || nodeRole === 'sink';
            const isLeafComputed = !!nodeMeta?.leaf && isComputedKind(kind);
            const serviceLikeNode = kind === 'service' || logicalType === 'servicewithbuffer';
            const retryTax = serviceLikeNode ? resolveRetryTaxValue(nodeMeta) : null;
            const hasRetryLoop = overlaySettings.showRetryMetrics !== false &&
                serviceLikeNode &&
                Number.isFinite(retryTax) &&
                retryTax > 0;

            let focusLabelWidth = Math.max(width - 14, 18);
            const expressionLikeNode = isExpressionKind(kind) || isConstKind(kind);
            const expressionOverride = isDarkTheme() && expressionLikeNode ? EXPRESSION_DARK_FILL : null;
            const expressionStrokeOverride = isDarkTheme() && expressionLikeNode ? NODE_LABEL_COLOR_DARK : null;
            const computedOverride = isDarkTheme() && isComputedKind(kind) ? defaultLeafFill : null;
            let fillForText = expressionOverride ?? computedOverride ?? fill;

            if (isLeafComputed) {
                const pillMetrics = drawLeafNode(ctx, nodeMeta);
                focusLabelWidth = Math.max(pillMetrics.width - 14, 18);
                fillForText = pillMetrics.fill ?? defaultLeafFill;
            } else if (kind === 'queue' && logicalType !== 'servicewithbuffer') {
                drawQueueNode(ctx, nodeMeta, overlaySettings);
                fillForText = QUEUE_PILL_FILL;
            } else if (dlqNode) {
                drawDlqNode(ctx, nodeMeta, overlaySettings);
                fillForText = getDlqPalette().fill;
            } else {
                ctx.beginPath();
                if (kind === 'expr' || kind === 'expression') {
                    const hw = width / 2;
                    const hh = height / 2;
                    ctx.moveTo(x, y - hh);
                    ctx.lineTo(x + hw, y);
                    ctx.lineTo(x, y + hh);
                    ctx.lineTo(x - hw, y);
                    ctx.closePath();
                } else {
                    const r = Math.min(cornerRadius + 6, Math.min(width, height) / 2);
                    traceRoundedRect(ctx, x, y, width, height, r);
                }
                ctx.fillStyle = fillForText;
                ctx.strokeStyle = expressionStrokeOverride ?? stroke;
                ctx.lineWidth = 0.9;
                ctx.fill();
                ctx.stroke();

                if (nodeMeta) {
                    nodeMeta.fill = fillForText;
                }
            }

            if (logicalType === 'servicewithbuffer') {
                drawServiceWithBufferBadge(ctx, nodeMeta);
            }

            if (kind === 'service' || logicalType === 'servicewithbuffer' || kind === 'queue' || kind === 'router' || dlqNode || isSinkNode) {
                drawServiceDecorations(ctx, nodeMeta, overlaySettings, state);
            } else if (kind === 'pmf') {
                const hasSparkline = overlaySettings.showSparklines && nodeMeta?.sparkline;
                const sparklineLayout = hasSparkline
                    ? computeSparklineLayout(nodeMeta, overlaySettings, nodeMeta.sparkline)
                    : null;

                if (nodeMeta?.distribution) {
                    drawPmfDistribution(ctx, nodeMeta, nodeMeta.distribution, overlaySettings, sparklineLayout);
                }

                if (hasSparkline && sparklineLayout) {
                    drawInputSparkline(ctx, nodeMeta, overlaySettings, sparklineLayout);
                }
            } else if ((kind === 'const' || kind === 'constant') && overlaySettings.showSparklines && nodeMeta?.sparkline) {
                drawInputSparkline(ctx, nodeMeta, overlaySettings);
            }

            if (overlaySettings.showLabels) {
                ctx.save();
                const label = String(id);
                ctx.fillStyle = isDarkTheme() ? NODE_LABEL_COLOR_DARK : NODE_LABEL_COLOR_LIGHT;
                ctx.globalAlpha = highlightNode ? 1 : 0.75;
                ctx.font = '10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
                ctx.textAlign = 'right';
                ctx.textBaseline = 'middle';
                const labelX = x - (width / 2) - 8;
                const maxWidth = 140;
                drawFittedText(ctx, label, labelX, y, maxWidth);
                ctx.restore();
            }

            const focusLabel = String(overlayNode?.focusLabel ?? overlayNode?.FocusLabel ?? nodeMeta?.focusLabel ?? '').trim();
            if (focusLabel) {
                ctx.save();
                ctx.fillStyle = isDarkColor(fillForText) ? '#FFFFFF' : NODE_LABEL_COLOR_LIGHT;
                ctx.globalAlpha = highlightNode ? 1 : 0.85;
                ctx.font = '600 12px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                const focusYOffset = logicalType === 'servicewithbuffer' ? 3 : 1;
                if (focusLabel === '-') {
                    const dashWidth = 7;
                    const dashY = y + focusYOffset;
                    ctx.strokeStyle = ctx.fillStyle;
                    ctx.lineWidth = 1.6;
                    ctx.beginPath();
                    ctx.moveTo(x - (dashWidth / 2), dashY);
                    ctx.lineTo(x + (dashWidth / 2), dashY);
                    ctx.stroke();
                } else {
                    drawFittedText(ctx, focusLabel, x, y + focusYOffset, focusLabelWidth);
                }
                ctx.restore();
            }

            ctx.restore();
        }

        const previousChipIdForDraw = state.hoveredChipId;
        let hoveredChip = null;
        if (state.hoveredChipId) {
            hoveredChip = state.chipHitboxes.find(chip => chip.id === state.hoveredChipId) ?? null;
        }

        if (hoveredChip) {
            state.hoveredChip = hoveredChip;
        } else {
            state.hoveredChip = null;
            if (state.hoveredChipId !== null) {
                state.hoveredChipId = null;
            }
        }

        if (previousChipIdForDraw !== state.hoveredChipId) {
            notifyNodeHoverChanged(state);
        }

        ctx.restore();

        drawCanvasTitle(ctx, state);
        drawEdgeOverlayLegend(ctx, state);

        const tooltipMeta = hoveredNodeId ? nodeMap.get(hoveredNodeId) : null;
        const tooltipAnchorMeta = hoveredNodeId ? nodeMap.get(hoveredNodeId) : null;
        tryDrawTooltip(ctx, nodeMap, tooltipAnchorMeta, legacyTooltip, state);

        if (state.hoveredChip) {
            drawChipTooltip(ctx, state);
        }

        positionInspectorToggle(state, nodeMap);
        state.collectingHitboxes = false;
        state.collectingChipHitboxes = false;
        state.refreshChipHitboxes = false;

            const frameEnd = typeof performance !== 'undefined' && typeof performance.now === 'function'
                ? performance.now()
                : Date.now();
            const durationMs = Math.max(0, frameEnd - frameStart);
        const recordDrawStats = (stats) => {
            stats.frames += 1;
            stats.totalDurationMs += durationMs;
            stats.lastDurationMs = durationMs;
            stats.maxDurationMs = Math.max(stats.maxDurationMs, durationMs);
        };
        state.drawStats ??= createDrawStats();
        state.drawStatsSinceUpload ??= createDrawStats();
        recordDrawStats(state.drawStats);
        recordDrawStats(state.drawStatsSinceUpload);
        recordPointerInteraction(state, frameEnd);
        state.lastDrawnHoverNodeId = state.hoveredNodeId ?? null;
        state.lastDrawnHoverChipId = state.hoveredChipId ?? null;
        state.lastDrawnHoverEdgeId = state.hoveredEdgeId ?? null;
        if (state.dragging && state.dragStartTime) {
            state.dragFrameCount = (state.dragFrameCount ?? 0) + 1;
            state.dragAccumulatedDuration = (state.dragAccumulatedDuration ?? 0) + durationMs;
            state.dragStats ??= createDragStats();
            state.dragStats.frames += 1;
            state.dragStats.totalDurationMs += durationMs;
            state.dragStats.maxDurationMs = Math.max(state.dragStats.maxDurationMs ?? 0, durationMs);
            state.dragStatsSinceUpload ??= createDragStats();
            state.dragStatsSinceUpload.frames += 1;
            state.dragStatsSinceUpload.totalDurationMs += durationMs;
            state.dragStatsSinceUpload.maxDurationMs = Math.max(state.dragStatsSinceUpload.maxDurationMs ?? 0, durationMs);
        }
    }

    function drawCanvasTitle(ctx, state) {
        if (!state) {
            return;
        }

        state.settingsHitbox = null;

        const rawTitle = state.title;
        const title = typeof rawTitle === 'string' ? rawTitle.trim() : '';
        if (!title) {
            return;
        }

        const ratio = Number(state.deviceRatio ?? window.devicePixelRatio ?? 1) || 1;
        const canvasWidth = Number(state.canvasWidth ?? (ctx.canvas.width / ratio));
        const margin = 6;
        const fontSize = 12;
        const boxHeight = 18;
        const paddingY = Math.max(1.5, (boxHeight - fontSize) / 2);
        const paddingX = 8;
        const maxContainerWidth = Math.max(60, canvasWidth - (margin * 2) - 52);
        const maxTextWidth = Math.max(48, maxContainerWidth - paddingX * 2);

        ctx.save();
        ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
        ctx.font = `${fontSize}px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;

        const iconSpacing = 6;
        const iconBoxSize = boxHeight;
        const iconRectX = margin;
        const iconRectY = margin;

        const measured = ctx.measureText(title).width;
        const textWidth = Math.min(measured, maxTextWidth);
        const minBoxWidth = 80;
        const boxWidth = Math.max(minBoxWidth, textWidth + paddingX * 2);
        const rectX = iconRectX + iconBoxSize + iconSpacing;
        const rectY = margin;

        const isDark = isDarkTheme();
        const background = isDark ? '#0F172A' : '#F8FAFC';
        const border = isDark ? 'rgba(148, 163, 184, 0.6)' : 'rgba(148, 163, 184, 0.4)';
        const textColor = isDark ? '#E2E8F0' : '#111827';

        ctx.fillStyle = background;
        ctx.strokeStyle = border;
        ctx.lineWidth = 1;
        drawRoundedRectTopLeft(ctx, rectX, rectY, boxWidth, boxHeight, 6);
        ctx.fill();
        drawRoundedRectTopLeft(ctx, rectX, rectY, boxWidth, boxHeight, 6);
        ctx.stroke();

        ctx.fillStyle = textColor;
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        const textX = rectX + paddingX;
        const textY = rectY + (boxHeight / 2);
        drawFittedText(ctx, title, textX, textY, boxWidth - paddingX * 2);

        const iconBackground = background;
        const iconBorder = border;
        const iconColor = isDark ? '#CBD5F5' : '#4B5563';

        ctx.fillStyle = iconBackground;
        ctx.strokeStyle = iconBorder;
        ctx.lineWidth = 1;
        drawRoundedRectTopLeft(ctx, iconRectX, iconRectY, iconBoxSize, iconBoxSize, 5);
        ctx.fill();
        drawRoundedRectTopLeft(ctx, iconRectX, iconRectY, iconBoxSize, iconBoxSize, 5);
        ctx.stroke();

        const iconCenterX = iconRectX + iconBoxSize / 2;
        const iconCenterY = iconRectY + iconBoxSize / 2;
        drawSettingsGlyph(ctx, iconCenterX, iconCenterY, iconBoxSize - 3, iconColor);

        state.settingsHitbox = {
            x: iconRectX,
            y: iconRectY,
            width: iconBoxSize,
            height: iconBoxSize
        };

        ctx.restore();
    }

    function hitTestChip(state, worldX, worldY) {
        if (!state || !Array.isArray(state.chipHitboxes) || state.chipHitboxes.length === 0) {
            return null;
        }

        for (let i = state.chipHitboxes.length - 1; i >= 0; i--) {
            const chip = state.chipHitboxes[i];
            if (!chip) {
                continue;
            }

            const left = Number(chip.x);
            const top = Number(chip.y);
            const width = Number(chip.width);
            const height = Number(chip.height);

            if (!Number.isFinite(left) || !Number.isFinite(top) || !Number.isFinite(width) || !Number.isFinite(height)) {
                continue;
            }

            const right = left + width;
            const bottom = top + height;
            if (worldX >= left && worldX <= right && worldY >= top && worldY <= bottom) {
                return chip;
            }
        }

        return null;
    }

    function hitTestNode(state, worldX, worldY) {
        if (!state || !Array.isArray(state.nodeHitboxes) || state.nodeHitboxes.length === 0) {
            return null;
        }

        for (const node of state.nodeHitboxes) {
            if (!node) {
                continue;
            }

            const left = Number(node.x);
            const top = Number(node.y);
            const width = Number(node.width);
            const height = Number(node.height);

            if (!Number.isFinite(left) || !Number.isFinite(top) || !Number.isFinite(width) || !Number.isFinite(height)) {
                continue;
            }

            const right = left + width;
            const bottom = top + height;
            if (worldX >= left && worldX <= right && worldY >= top && worldY <= bottom) {
                return node;
            }
        }

        return null;
    }

    function hitTestEdge(state, worldX, worldY) {
        if (!state || !Array.isArray(state.edgeHitboxes) || state.edgeHitboxes.length === 0) {
            return null;
        }

        const tolerance = 8 / Math.max(state.scale || 1, 0.0001);
        const index = state?.edgeSpatialIndex;
        const cellSize = index?.cellSize;
        const cellKey = cellSize ? `${Math.floor(worldX / cellSize)}:${Math.floor(worldY / cellSize)}` : null;

        if (cellKey && state.edgeSpatialCache && state.edgeSpatialCache.key === cellKey) {
            const cachedBox = state.edgeSpatialCache.hitbox;
            if (cachedBox) {
                const distance = distanceToPolyline(cachedBox.points, worldX, worldY);
                if (distance <= tolerance) {
                    recordEdgeSpatialCacheHit(state);
                    updateEdgeSpatialStats(state, 1, false);
                    state.edgeSpatialCache.worldX = worldX;
                    state.edgeSpatialCache.worldY = worldY;
                    return cachedBox;
                }
            }
            recordEdgeSpatialCacheMiss(state);
        }

        const spatialCandidates = queryEdgeSpatialIndex(state, worldX, worldY, tolerance);
        const usedFallback = !Array.isArray(spatialCandidates) || spatialCandidates.length === 0;
        const candidates = usedFallback ? state.edgeHitboxes : spatialCandidates;
        const candidateCount = Array.isArray(candidates) ? candidates.length : 0;
        updateEdgeSpatialStats(state, candidateCount, usedFallback);
        let closest = null;
        let minDistance = tolerance;

        for (const hitbox of candidates) {
            const distance = distanceToPolyline(hitbox.points, worldX, worldY);
            if (distance < minDistance) {
                minDistance = distance;
                closest = hitbox;
            }
        }

        if (cellKey) {
            setEdgeSpatialCache(state, cellKey, closest, worldX, worldY);
        } else if (!closest) {
            clearEdgeSpatialCache(state);
        }

        return closest;
    }

    function distanceToPolyline(points, x, y) {
        if (!Array.isArray(points) || points.length < 2) {
            return Number.POSITIVE_INFINITY;
        }

        let best = Number.POSITIVE_INFINITY;
        for (let i = 0; i < points.length - 1; i++) {
            const start = points[i];
            const end = points[i + 1];
            const px = end.x - start.x;
            const py = end.y - start.y;
            const lenSq = px * px + py * py;
            let t = 0;
            if (lenSq > 0) {
                t = ((x - start.x) * px + (y - start.y) * py) / lenSq;
                t = Math.max(0, Math.min(1, t));
            }

            const projX = start.x + t * px;
            const projY = start.y + t * py;
            const dx = x - projX;
            const dy = y - projY;
            const dist = Math.hypot(dx, dy);
            if (dist < best) {
                best = dist;
            }
        }

        return best;
    }

    function applyOverlayTransform(canvas, state) {
        const host = canvas.parentElement ?? document;
        const transform = `translate(${state.offsetX}px, ${state.offsetY}px) scale(${state.scale})`;
        const origin = 'top left';

        const nodeLayer = host.querySelector('.topology-node-layer');
        if (nodeLayer && nodeLayer.style) {
            nodeLayer.style.transform = transform;
            nodeLayer.style.transformOrigin = origin;
        }

    }

    function positionInspectorToggle(state, nodeMap) {
        if (!state || !nodeMap) {
            return;
        }

        const host = state.canvas?.parentElement ?? state.canvas?.parentNode ?? document;
        if (!host) {
            return;
        }

        const toggle = host.querySelector('.topology-node-inspector-toggle');
        if (!toggle) {
            return;
        }

        const tooltipMetrics = state.tooltipMetrics;
        if (!tooltipMetrics?.active) {
            toggle.style.display = 'none';
            return;
        }

        const nodeId = toggle.getAttribute('data-node-id');
        if (!nodeId || !nodeMap.has(nodeId)) {
            toggle.style.display = 'none';
            return;
        }

        const meta = nodeMap.get(nodeId);
        const base = computeInspectorAnchor(meta);
        let targetX = base.x;
        let targetY = base.y;

        const rightEdge = tooltipMetrics.x + tooltipMetrics.width;
        const iconAnchorCssX = rightEdge - InspectorTooltipInset;
        const iconAnchorCssY = tooltipMetrics.y + InspectorTooltipInset;
        const worldPoint = cssToWorld(state, iconAnchorCssX, iconAnchorCssY);
        targetX = worldPoint.x;
        targetY = worldPoint.y;

        toggle.style.display = '';
        const scale = Number(state.scale ?? 1) || 1;
        const normalizedScale = scale > 0 ? (1 / scale) : 1;
        toggle.style.setProperty('--topology-inspector-toggle-scale', normalizedScale.toFixed(4));
        toggle.style.left = `${targetX}px`;
        toggle.style.top = `${targetY}px`;
    }

    function computeInspectorAnchor(meta) {
        const nodeWidth = Number(meta.width ?? meta.Width ?? 54);
        const nodeX = Number(meta.x ?? meta.X ?? 0);
        const nodeY = Number(meta.y ?? meta.Y ?? 0);
        const offset = (nodeWidth / 2) + InspectorIconGap + (InspectorIconSize / 2);
        return {
            x: nodeX + offset,
            y: nodeY
        };
    }

    function cssToWorld(state, cssX, cssY) {
        const scale = Number(state.scale ?? 1) || 1;
        const offsetX = Number(state.offsetX ?? 0);
        const offsetY = Number(state.offsetY ?? 0);
        return {
            x: (cssX - offsetX) / scale,
            y: (cssY - offsetY) / scale
        };
    }

    function findFocusedMeta(nodeMap) {
        if (!nodeMap) {
            return null;
        }
        for (const [, meta] of nodeMap.entries()) {
            if (meta?.isFocused) {
                return meta;
            }
        }
        return null;
    }

    function tryDrawTooltip(ctx, nodeMap, anchorCandidate, legacyTooltip, state) {
        if (!ctx || !nodeMap || nodeMap.size === 0) {
            return;
        }

        let anchorMeta = anchorCandidate ?? null;
        let tooltipPayload = anchorMeta?.tooltip ?? anchorMeta?.Tooltip ?? null;

        if (!tooltipPayload && legacyTooltip) {
            anchorMeta = anchorMeta ?? findFocusedMeta(nodeMap);
            if (legacyTooltip && (legacyTooltip.title || legacyTooltip.subtitle || (legacyTooltip.lines?.length ?? 0) > 0)) {
                tooltipPayload = legacyTooltip;
            }
        }

        if (!anchorMeta || !tooltipPayload) {
            state.tooltipMetrics = null;
            positionInspectorToggle(state, nodeMap);
            return;
        }

        const title = tooltipPayload.title ?? '';
        const subtitle = tooltipPayload.subtitle ?? '';
        const lines = (tooltipPayload.lines ?? []).map(line => String(line).trim()).filter(line => line.length > 0);
        const warningLine = (anchorMeta?.queueWarningText ?? '').toString();
        const warningLines = warningLine
            .split('\n')
            .map(line => line.trim())
            .filter(line => line.length > 0);
        const hasWarningLine = warningLines.length > 0;

        const ratio = Number(state.deviceRatio ?? window.devicePixelRatio ?? 1) || 1;
        const toDevice = (value) => Math.round(value * ratio * 1000) / 1000;
        const paddingX = toDevice(12);
        const paddingY = toDevice(6);
        const lineHeight = toDevice(16);
        const fontSizePx = 12 * ratio;
        const fontRegular = `${fontSizePx}px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;
        const fontStrong = `600 ${fontSizePx}px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;
        const dateOffsetY = 5 * ratio;
        const overlays = state.overlaySettings ?? {};
        const preparedSparkline = overlays.showSparklines
            ? prepareTooltipSparklineData(anchorMeta, overlays)
            : null;
        const hasSparkline = preparedSparkline !== null;
        const sparklineWidth = hasSparkline ? toDevice(90) : 0;
        const sparklineHeight = hasSparkline ? toDevice(26) : 0;
        const sparklineMarginTop = hasSparkline ? toDevice(6) : 0;
        const sparklineMarginBottom = hasSparkline ? toDevice(11) : 0;
        const sparklineBlockHeight = hasSparkline ? sparklineHeight + sparklineMarginTop + sparklineMarginBottom : 0;

        const canvasWidthDevice = Number.isFinite(state.canvasWidth) ? state.canvasWidth * ratio : ctx.canvas.width;
        const canvasHeightDevice = Number.isFinite(state.canvasHeight) ? state.canvasHeight * ratio : ctx.canvas.height;

        ctx.save();
        ctx.setTransform(1, 0, 0, 1, 0, 0);

        let width = 0;
        ctx.font = fontStrong;
        width = Math.max(width, ctx.measureText(title).width);
        ctx.font = fontRegular;
        if (subtitle) {
            width = Math.max(width, ctx.measureText(subtitle).width);
        }
        if (hasWarningLine) {
            for (const line of warningLines) {
                width = Math.max(width, ctx.measureText(line).width);
            }
        }
        for (const line of lines) {
            width = Math.max(width, ctx.measureText(line).width);
        }
        if (hasSparkline) {
            width = Math.max(width, sparklineWidth);
        }

        const textLineCount = 1 + (subtitle ? 1 : 0) + (hasWarningLine ? warningLines.length : 0) + lines.length;
        const boxWidth = Math.ceil(width + paddingX * 2);
        const boxHeight = Math.ceil(paddingY * 2 + textLineCount * lineHeight + sparklineBlockHeight);

        const scale = Number(state.scale ?? 1);
        const offsetX = Number(state.offsetX ?? 0);
        const offsetY = Number(state.offsetY ?? 0);

        const nodeWidth = Number(anchorMeta.width ?? 54);
        const nodeHeight = Number(anchorMeta.height ?? 24);
        const nodeX = Number(anchorMeta.x ?? 0);
        const nodeY = Number(anchorMeta.y ?? 0);
        const nodeCenterScreenX = toDevice(offsetX + scale * nodeX);
        const halfWidthScreen = toDevice((nodeWidth * scale) / 2);

        let badgeBottomWorld = nodeY - (nodeHeight / 2) - 6;
        const spark = anchorMeta.sparkline ?? null;
        if (spark && overlays.showSparklines) {
            const mode = overlays.sparklineMode === 'bar' ? 'bar' : 'line';
            const sparkHeight = mode === 'bar' ? 16 : 12;
            badgeBottomWorld -= (sparkHeight + 6);
        }

        // Position tooltip at constant screen distance to the left of the node, vertically centered
        const nodeCenterScreenY = toDevice(offsetY + scale * nodeY);
        const gap = toDevice(8); // constant px gap from node edge
        const minMargin = toDevice(12);
        let tooltipTop = Math.round(nodeCenterScreenY - (boxHeight / 2));
        tooltipTop = Math.max(minMargin, Math.min(canvasHeightDevice - boxHeight - minMargin, tooltipTop));

        const leftCandidate = Math.round((nodeCenterScreenX - halfWidthScreen - gap) - boxWidth);
        const rightCandidate = Math.round(nodeCenterScreenX + halfWidthScreen + gap);
        let tooltipX = leftCandidate;
        if (!Number.isFinite(tooltipX)) {
            tooltipX = 0;
        }
        let tooltipSide = 'left';
        if (tooltipX < minMargin) {
            tooltipX = rightCandidate;
            tooltipSide = 'right';
        }
        const maxX = canvasWidthDevice - boxWidth - minMargin;
        if (tooltipX > maxX) {
            tooltipX = maxX;
        }
        if (tooltipX < minMargin) {
            tooltipX = minMargin;
        }

        const darkMode = isDarkTheme();
        const bg = darkMode ? 'rgba(15, 23, 42, 0.96)' : 'rgba(255, 255, 255, 0.97)';
        const fg = darkMode ? '#F8FAFC' : '#0F172A';
        const subtitleColor = darkMode ? '#94A3B8' : '#4B5563';
        const warningColor = darkMode ? '#FCD34D' : '#78350F';
        const border = darkMode ? 'rgba(148, 163, 184, 0.35)' : 'rgba(15, 23, 42, 0.12)';

        ctx.lineWidth = Math.max(1, ratio);
        ctx.fillStyle = bg;
        ctx.strokeStyle = border;

        const radius = 6;
        ctx.beginPath();
        roundedRectPath(ctx, tooltipX, tooltipTop, boxWidth, boxHeight, radius);
        ctx.fill();
        ctx.stroke();

        ctx.textBaseline = 'top';
        let textY = tooltipTop + paddingY;
        ctx.fillStyle = fg;
        ctx.font = fontStrong;
        ctx.fillText(title, tooltipX + paddingX, textY);

        ctx.font = fontRegular;
        if (subtitle) {
            textY += lineHeight + dateOffsetY;
            ctx.fillStyle = subtitleColor;
            ctx.fillText(subtitle, tooltipX + paddingX, textY);
            ctx.fillStyle = fg;
        }
        if (hasWarningLine) {
            ctx.fillStyle = warningColor;
            for (const line of warningLines) {
                textY += lineHeight;
                ctx.fillText(line, tooltipX + paddingX, textY);
            }
            ctx.fillStyle = fg;
        }

        if (hasSparkline) {
            const baselineStart = textY + lineHeight;
            const sparkTop = baselineStart + sparklineMarginTop;
            const sparkDrawn = drawTooltipSparkline(ctx, preparedSparkline, overlays, tooltipX + paddingX, sparkTop, sparklineWidth, sparklineHeight);
            if (sparkDrawn) {
                textY = sparkTop + sparklineHeight + sparklineMarginBottom - lineHeight;
            }
        }

        for (const line of lines) {
            textY += lineHeight;
            ctx.fillText(line, tooltipX + paddingX, textY);
        }

        ctx.restore();

        const tooltipCssX = tooltipX / ratio;
        const tooltipCssY = tooltipTop / ratio;
        state.tooltipMetrics = {
            active: true,
            x: tooltipCssX,
            y: tooltipCssY,
            width: (boxWidth / ratio),
            height: (boxHeight / ratio),
            side: tooltipSide
        };
    }

    function prepareTooltipSparklineData(nodeMeta, overlays) {
        const sparkline = nodeMeta?.sparkline ?? nodeMeta?.Sparkline ?? null;
        if (!sparkline) {
            return null;
        }

        const kind = String(nodeMeta?.kind ?? nodeMeta?.Kind ?? '').trim().toLowerCase();
        const logicalType = String(nodeMeta?.logicalType ?? nodeMeta?.LogicalType ?? '').trim().toLowerCase();
        const overlayBasis = Number(overlays?.colorBasis ?? 0);
        const basis = isQueueLikeKind(kind, logicalType) ? 3 : overlayBasis;
        const series = selectSeriesForBasis(sparkline, basis);
        if (!Array.isArray(series) || series.length === 0) {
            return null;
        }

        const values = new Array(series.length);
        let min = Infinity;
        let max = -Infinity;
        let hasValue = false;

        for (let i = 0; i < series.length; i++) {
            const sample = series[i];
            if (sample === null || sample === undefined) {
                values[i] = null;
                continue;
            }

            const numeric = Number(sample);
            if (!Number.isFinite(numeric)) {
                values[i] = null;
                continue;
            }

            values[i] = numeric;
            hasValue = true;
            if (numeric < min) {
                min = numeric;
            }
            if (numeric > max) {
                max = numeric;
            }
        }

        if (!hasValue || !Number.isFinite(min) || !Number.isFinite(max)) {
            return null;
        }

        if (Math.abs(max - min) < 1e-9) {
            max = min + 0.001;
        }

        return {
            values,
            min,
            max,
            length: values.length,
            startIndex: Number(sparkline.startIndex ?? sparkline.StartIndex ?? 0),
            raw: sparkline,
            basis,
            mode: overlays?.sparklineMode === 'bar' ? 'bar' : 'line'
        };
    }

    function drawTooltipSparkline(ctx, sparklineData, overlays, x, y, width, height) {
        if (!sparklineData) {
            return false;
        }

        const { values, min, max, length, startIndex, raw, basis, mode } = sparklineData;
        if (!Array.isArray(values) || length === 0) {
            return false;
        }

        const resolvedBasis = Number.isFinite(basis) ? basis : Number(overlays?.colorBasis ?? 0);
        const defaultColor = resolveSparklineColor(resolvedBasis);
        const thresholds = overlays.thresholds ?? {};
        const lastIndex = length - 1;
        const range = max - min;
        const drawAsBars = mode === 'bar';

        ctx.save();
        ctx.translate(x, y);

        ctx.fillStyle = 'rgba(148, 163, 184, 0.08)';
        ctx.fillRect(0, 0, width, height);
        ctx.strokeStyle = 'rgba(148, 163, 184, 0.25)';
        ctx.lineWidth = 0.75;
        ctx.strokeRect(0, 0, width, height);

        ctx.beginPath();
        ctx.strokeStyle = defaultColor;
        ctx.lineWidth = 1.4;
        let segmentActive = false;
        let previousPoint = null;
        let previousColor = defaultColor;
        let highlightPoint = null;
        let highlightColor = defaultColor;
        let highlightBar = null;

        for (let i = 0; i < length; i++) {
            const sample = values[i];
            if (sample === null || sample === undefined) {
                segmentActive = false;
                previousPoint = null;
                continue;
            }

            const fraction = lastIndex <= 0 ? 0 : i / lastIndex;
            const xPos = fraction * width;
            const normalized = range <= 0 ? 0.5 : clamp((sample - min) / range, 0, 1);
            const yPos = height - (normalized * height);
            const sampleColor = resolveSampleColor(resolvedBasis, i, raw, thresholds, defaultColor);

            if (drawAsBars) {
                const barWidth = Math.max((width / Math.max(length - 1, 1)) * 0.6, 1.5);
                ctx.fillStyle = sampleColor;
                ctx.fillRect(xPos - (barWidth / 2), yPos, barWidth, height - yPos);
                if (i === (overlays.selectedBin ?? -1) - startIndex) {
                    highlightColor = sampleColor;
                    highlightBar = { x: xPos, width: Math.max(barWidth, 3) };
                }
            } else {
                if (previousPoint) {
                    ctx.beginPath();
                    ctx.strokeStyle = previousColor ?? sampleColor;
                    ctx.lineWidth = 1.4;
                    ctx.moveTo(previousPoint.x, previousPoint.y);
                    ctx.lineTo(xPos, yPos);
                    ctx.stroke();
                }
                previousPoint = { x: xPos, y: yPos };
                previousColor = sampleColor;
                segmentActive = true;
                if (i === (overlays.selectedBin ?? -1) - startIndex) {
                    highlightPoint = { x: xPos, y: yPos };
                    highlightColor = sampleColor;
                }
            }
        }

        if (!drawAsBars && !segmentActive && length === 1 && values[0] !== null && values[0] !== undefined) {
            const yPos = height / 2;
            ctx.beginPath();
            ctx.strokeStyle = defaultColor;
            ctx.moveTo(0, yPos);
            ctx.lineTo(width, yPos);
            ctx.stroke();
        }

        if (!drawAsBars && highlightPoint) {
            ctx.beginPath();
            ctx.fillStyle = highlightColor ?? defaultColor;
            ctx.strokeStyle = '#FFFFFF';
            ctx.lineWidth = 1;
            ctx.arc(highlightPoint.x, highlightPoint.y, 3, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();
        }

        if (drawAsBars && highlightBar) {
            const apexY = height + 1;
            const baseY = apexY + 5;
            const pointerWidth = Math.max(highlightBar.width + 4, 8);
            const halfWidth = pointerWidth / 2;
            ctx.beginPath();
            ctx.moveTo(highlightBar.x, apexY);
            ctx.lineTo(highlightBar.x - halfWidth, baseY);
            ctx.lineTo(highlightBar.x + halfWidth, baseY);
            ctx.closePath();
            ctx.fillStyle = highlightColor ?? defaultColor;
            ctx.fill();
        }

        ctx.restore();
        return true;
    }

    function drawChipTooltip(ctx, state) {
        const chip = state?.hoveredChip ?? null;
        if (!chip) {
            return;
        }

        const rawText = typeof chip.tooltip === 'string' ? chip.tooltip.trim() : '';
        if (!rawText) {
            return;
        }

        const ratio = Number(state.deviceRatio ?? window.devicePixelRatio ?? 1) || 1;
        const paddingX = 8 * ratio;
        const paddingY = 4 * ratio;
        const fontSize = 11 * ratio;
        const pointerSize = 6 * ratio;
        const pointerWidth = 12 * ratio;
        const margin = 8 * ratio;
        const radius = 6 * ratio;

        const scale = Number(state.scale ?? 1) || 1;
        const offsetX = Number(state.offsetX ?? 0);
        const offsetY = Number(state.offsetY ?? 0);

        const chipLeftCss = offsetX + scale * Number(chip.x ?? 0);
        const chipTopCss = offsetY + scale * Number(chip.y ?? 0);
        const chipWidthCss = scale * Number(chip.width ?? 0);
        const chipHeightCss = scale * Number(chip.height ?? 0);

        const canvasWidthDevice = Number.isFinite(state.canvasWidth) ? state.canvasWidth * ratio : ctx.canvas.width;
        const canvasHeightDevice = Number.isFinite(state.canvasHeight) ? state.canvasHeight * ratio : ctx.canvas.height;

        const darkMode = isDarkTheme();
        const background = darkMode ? 'rgba(15, 23, 42, 0.95)' : 'rgba(255, 255, 255, 0.97)';
        const foreground = darkMode ? '#F8FAFC' : '#0F172A';
        const border = darkMode ? 'rgba(148, 163, 184, 0.45)' : 'rgba(15, 23, 42, 0.18)';

        const lines = rawText.split(/\n/).map(line => line.trim()).filter(line => line.length > 0);
        if (lines.length === 0) {
            lines.push(rawText.trim());
        }

        ctx.save();
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.textBaseline = 'top';
        ctx.textAlign = 'left';

        const boldFont = `600 ${fontSize}px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;
        const regularFont = `400 ${fontSize}px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;
        const lineFonts = lines.map((_, idx) => idx === 0 ? boldFont : regularFont);

        let textWidth = 0;
        for (let i = 0; i < lines.length; i++) {
            ctx.font = lineFonts[i];
            const measured = ctx.measureText(lines[i]).width;
            if (measured > textWidth) {
                textWidth = measured;
            }
        }

        const lineGap = 4 * ratio;
        const textHeight = lines.length * fontSize + Math.max(0, lines.length - 1) * lineGap;
        const boxWidth = Math.ceil(textWidth + paddingX * 2);
        const boxHeight = Math.ceil(textHeight + paddingY * 2);

        const placement = String(chip.placement ?? 'top');
        let bubbleAbove = placement.startsWith('top');

        const anchorCssX = chipLeftCss + chipWidthCss / 2;
        const anchorCssY = bubbleAbove ? chipTopCss : chipTopCss + chipHeightCss;
        const anchorX = anchorCssX * ratio;
        const anchorY = anchorCssY * ratio;

        let bubbleX = anchorX - boxWidth / 2;
        const minX = margin;
        const maxX = canvasWidthDevice - boxWidth - margin;
        if (bubbleX < minX) {
            bubbleX = minX;
        }
        if (bubbleX > maxX) {
            bubbleX = Math.max(minX, maxX);
        }

        let bubbleY;
        if (bubbleAbove) {
            bubbleY = anchorY - pointerSize - boxHeight;
            if (bubbleY < margin) {
                bubbleAbove = false;
            }
        }

        if (!bubbleAbove) {
            bubbleY = anchorY + pointerSize;
            if (bubbleY + boxHeight + margin > canvasHeightDevice) {
                bubbleY = canvasHeightDevice - boxHeight - margin;
            }
        }

        ctx.lineWidth = Math.max(1, ratio);
        ctx.fillStyle = background;
        ctx.strokeStyle = border;

        ctx.beginPath();
        ctx.moveTo(bubbleX + radius, bubbleY);
        ctx.lineTo(bubbleX + boxWidth - radius, bubbleY);
        ctx.quadraticCurveTo(bubbleX + boxWidth, bubbleY, bubbleX + boxWidth, bubbleY + radius);
        ctx.lineTo(bubbleX + boxWidth, bubbleY + boxHeight - radius);
        ctx.quadraticCurveTo(bubbleX + boxWidth, bubbleY + boxHeight, bubbleX + boxWidth - radius, bubbleY + boxHeight);
        ctx.lineTo(bubbleX + radius, bubbleY + boxHeight);
        ctx.quadraticCurveTo(bubbleX, bubbleY + boxHeight, bubbleX, bubbleY + boxHeight - radius);
        ctx.lineTo(bubbleX, bubbleY + radius);
        ctx.quadraticCurveTo(bubbleX, bubbleY, bubbleX + radius, bubbleY);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();

        const pointerCenter = Math.max(bubbleX + radius, Math.min(bubbleX + boxWidth - radius, anchorX));
        if (bubbleAbove) {
            const baseY = bubbleY + boxHeight;
            ctx.beginPath();
            ctx.moveTo(pointerCenter - pointerWidth / 2, baseY);
            ctx.lineTo(pointerCenter + pointerWidth / 2, baseY);
            ctx.lineTo(anchorX, anchorY);
            ctx.closePath();
            ctx.fill();
            ctx.stroke();
        } else {
            const baseY = bubbleY;
            ctx.beginPath();
            ctx.moveTo(pointerCenter - pointerWidth / 2, baseY);
            ctx.lineTo(anchorX, anchorY);
            ctx.lineTo(pointerCenter + pointerWidth / 2, baseY);
            ctx.closePath();
            ctx.fill();
            ctx.stroke();
        }

        ctx.fillStyle = foreground;
        let textY = bubbleY + paddingY;
        for (let i = 0; i < lines.length; i++) {
            ctx.font = lineFonts[i];
            ctx.fillText(lines[i], bubbleX + paddingX, textY);
            textY += fontSize + lineGap;
        }

        ctx.restore();
    }

    function roundedRectPath(ctx, x, y, width, height, radius) {
        const r = Math.max(0, Math.min(radius, Math.min(width, height) / 2));
        const right = x + width;
        const bottom = y + height;

        ctx.moveTo(x + r, y);
        ctx.lineTo(right - r, y);
        ctx.quadraticCurveTo(right, y, right, y + r);
        ctx.lineTo(right, bottom - r);
        ctx.quadraticCurveTo(right, bottom, right - r, bottom);
        ctx.lineTo(x + r, bottom);
        ctx.quadraticCurveTo(x, bottom, x, bottom - r);
        ctx.lineTo(x, y + r);
        ctx.quadraticCurveTo(x, y, x + r, y);
    }

    function clear(state) {
        state.ctx.save();
        state.ctx.setTransform(state.deviceRatio, 0, 0, state.deviceRatio, 0, 0);
        state.ctx.clearRect(0, 0, state.ctx.canvas.width, state.ctx.canvas.height);
        state.ctx.restore();
        state.settingsHitbox = null;
    }

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function computeAdaptiveWidth(length, baseWidth, overrides) {
        const opts = overrides || {};
        const count = Math.max(Number(length) || 0, 1);
        const scale = Number.isFinite(opts.scale) ? opts.scale : 9;
        const base = Number.isFinite(baseWidth) ? baseWidth : 24;
        const min = Number.isFinite(opts.min) ? opts.min : Math.max(base, 20);
        const max = Number.isFinite(opts.max) ? opts.max : 140;
        const dynamic = Math.sqrt(count) * scale;
        return clamp(Math.max(base, dynamic), min, max);
    }

    function applyScaleAroundCenter(state) {
        if (!state) {
            return;
        }

        const scale = Number.isFinite(state.overlayScale) && state.overlayScale > 0
            ? state.overlayScale
            : state.scale ?? 1;

        state.scale = scale;

        const width = Number(state.canvasWidth ?? 0);
        const height = Number(state.canvasHeight ?? 0);
        const centerX = Number(state.worldCenterX ?? 0);
        const centerY = Number(state.worldCenterY ?? 0);

        if (Number.isFinite(width) && width > 0 && Number.isFinite(height) && height > 0 &&
            Number.isFinite(centerX) && Number.isFinite(centerY)) {
            state.offsetX = (width / 2) - centerX * state.scale;
            state.offsetY = (height / 2) - centerY * state.scale;
        }

        state.userAdjusted = true;
    }

    function applyViewportSnapshot(state, snapshot) {
        if (!state || !snapshot) {
            return;
        }

        const scale = Number(snapshot.scale ?? snapshot.Scale);
        const overlayScale = Number(snapshot.overlayScale ?? snapshot.OverlayScale);
        const baseScale = Number(snapshot.baseScale ?? snapshot.BaseScale);
        const offsetX = Number(snapshot.offsetX ?? snapshot.OffsetX);
        const offsetY = Number(snapshot.offsetY ?? snapshot.OffsetY);
        const worldCenterX = Number(snapshot.worldCenterX ?? snapshot.WorldCenterX);
        const worldCenterY = Number(snapshot.worldCenterY ?? snapshot.WorldCenterY);

        if (Number.isFinite(scale) && scale > 0) {
            state.scale = scale;
        }

        if (Number.isFinite(overlayScale) && overlayScale > 0) {
            state.overlayScale = overlayScale;
        } else if (Number.isFinite(state.scale) && state.scale > 0) {
            state.overlayScale = state.scale;
        }

        if (Number.isFinite(baseScale) && baseScale > 0) {
            state.baseScale = baseScale;
        }

        if (Number.isFinite(worldCenterX)) {
            state.worldCenterX = worldCenterX;
        }

        if (Number.isFinite(worldCenterY)) {
            state.worldCenterY = worldCenterY;
        }

        const width = Number(state.canvasWidth ?? 0);
        const height = Number(state.canvasHeight ?? 0);

        if (Number.isFinite(width) && width > 0 && Number.isFinite(state.worldCenterX)) {
            state.offsetX = (width / 2) - state.worldCenterX * state.scale;
        } else if (Number.isFinite(offsetX)) {
            state.offsetX = offsetX;
        }

        if (Number.isFinite(height) && height > 0 && Number.isFinite(state.worldCenterY)) {
            state.offsetY = (height / 2) - state.worldCenterY * state.scale;
        } else if (Number.isFinite(offsetY)) {
            state.offsetY = offsetY;
        }

        state.userAdjusted = true;
        state.viewportApplied = true;
    }

    function render(canvas, payload) {
        if (!payload) {
            return;
        }
        if (payload.scene || payload.overlay) {
            if (payload.scene) {
                applyScenePayload(canvas, payload.scene);
            }
            if (payload.overlay) {
                applyOverlayPayload(canvas, payload.overlay);
            }
            return;
        }

        console.warn('[Topology] Legacy render payload is no longer supported. Use renderScene/applyOverlayDelta.');
    }

    function renderScene(canvas, payload) {
        applyScenePayload(canvas, payload);
    }

    function applyOverlayDelta(canvas, payload) {
        applyOverlayPayload(canvas, payload);
    }

    function applyScenePayload(canvas, payload) {
        if (!canvas) {
            return;
        }

        const state = getState(canvas);
        if (!state) {
            return;
        }

        if (!payload) {
            state.scenePayload = null;
            state.sceneDirty = true;
            draw(canvas, state);
            return;
        }

        state.scenePayload = payload;
        state.sceneDirty = true;
        const rawTitle = payload?.title ?? payload?.Title ?? '';
        state.title = typeof rawTitle === 'string' ? rawTitle : '';

        const rawEdgeSeries = payload?.edgeSeries ?? payload?.EdgeSeries ?? null;
        state.edgeSeries = normalizeEdgeSeries(rawEdgeSeries);
        const edgeSeriesStartIndex = Number(payload?.edgeSeriesStartIndex ?? payload?.EdgeSeriesStartIndex ?? 0);
        state.edgeSeriesStartIndex = Number.isFinite(edgeSeriesStartIndex) ? edgeSeriesStartIndex : 0;

        const viewport = getViewport(payload);
        const signature = computeViewportSignature(viewport);
        const preserveViewport = Boolean(state.preserveViewportRequest);
        if (signature && signature !== state.viewportSignature)
        {
            state.viewportSignature = signature;
            if (!preserveViewport)
            {
                const needAutoFit = !state.viewportApplied || !state.userAdjusted;
                state.viewportApplied = false;
                state.baseScale = null;
                if (needAutoFit)
                {
                    state.userAdjusted = false;
                }
            }
        }

        if (viewport && (!state.userAdjusted || !state.viewportApplied))
        {
            applyViewport(canvas, state, viewport);
        }

        state.preserveViewportRequest = false;

        if (state.overlayPayload)
        {
            draw(canvas, state);
            emitViewportChanged(canvas, state, { immediate: true });
        }
    }

    function applyOverlayPayload(canvas, payload) {
        if (!canvas) {
            return;
        }

        const state = getState(canvas);
        if (!state) {
            return;
        }

        if (!payload) {
            state.overlayPayload = null;
            return;
        }

        state.overlayPayload = payload;
        const preserveViewport = Boolean(payload?.preserveViewport ?? payload?.PreserveViewport);
        state.preserveViewportRequest = preserveViewport;
        const overlaySettings = parseOverlaySettings(payload?.overlays ?? payload?.Overlays ?? {});
        state.overlaySettings = overlaySettings;
        state.refreshChipHitboxes = true;

        const desiredScale = overlaySettings.manualScale ?? state.overlayScale ?? 1;
        let manualScaleChanged = state.lastOverlayZoom === null || Math.abs(state.lastOverlayZoom - desiredScale) > 0.001;
        if (state.lastOverlayZoom === null) {
            state.overlayScale = clamp(desiredScale, MIN_SCALE, MAX_SCALE);
            state.lastOverlayZoom = state.overlayScale;
            manualScaleChanged = false;
        } else if (manualScaleChanged) {
            state.overlayScale = clamp(desiredScale, MIN_SCALE, MAX_SCALE);
            state.lastOverlayZoom = state.overlayScale;
            if (state.viewportApplied) {
                applyScaleAroundCenter(state);
            } else if (!preserveViewport) {
                state.userAdjusted = false;
            }
        }

        state.globalWarningsByNode = new Map();
        const globalWarnings = payload?.warnings ?? payload?.Warnings ?? [];
        if (Array.isArray(globalWarnings)) {
            for (const entry of globalWarnings) {
                if (!entry) continue;
                const nodeId = (entry.nodeId ?? entry.NodeId ?? '').toString().toLowerCase();
                if (!nodeId) continue;
                if (!state.globalWarningsByNode.has(nodeId)) {
                    state.globalWarningsByNode.set(nodeId, []);
                }
                state.globalWarningsByNode.get(nodeId).push(entry);
            }
        }

        if (preserveViewport) {
            const savedViewport = payload?.savedViewport ?? payload?.SavedViewport ?? null;
            if (savedViewport) {
                applyViewportSnapshot(state, savedViewport);
            }
        } else {
            state.preserveViewportRequest = false;
        }

        if (state.scenePayload) {
            draw(canvas, state);
            emitViewportChanged(canvas, state, { immediate: true });
        }
    }

    function fitToViewport(canvas) {
        const state = getState(canvas);
        if (!state || !state.scenePayload) {
            return NaN;
        }

        const viewport = getViewport(state.scenePayload);
        if (!viewport) {
            return NaN;
        }

        state.userAdjusted = false;
        state.viewportApplied = false;
        state.baseScale = null;
        state.overlayScale = null;
        state.lastOverlayZoom = null;

        applyViewport(canvas, state, viewport);
        updateWorldCenter(state);
        state.userAdjusted = true;
        state.viewportApplied = true;

        draw(canvas, state);
        emitViewportChanged(canvas, state, { immediate: true });

        const zoomPercent = clamp((state.scale ?? 1) * 100, MIN_ZOOM_PERCENT, MAX_ZOOM_PERCENT);
        return zoomPercent;
    }

    function resetViewportState(canvas) {
        const state = getState(canvas);
        if (!state) {
            return;
        }

        state.userAdjusted = false;
        state.viewportApplied = false;
        state.baseScale = null;
        state.overlayScale = null;
        state.lastOverlayZoom = null;
        state.viewportSignature = null;
        state.lastViewportSignature = null;
        state.lastViewportPayload = null;
        state.scale = 1;
        state.offsetX = 0;
        state.offsetY = 0;
        state.worldCenterX = 0;
        state.worldCenterY = 0;
    }

    function dispose(canvas) {
        const state = registry.get(canvas);
        if (!state) {
            return;
        }
        state.cleanup?.();
        registry.delete(canvas);
    }

    function updateWorldCenter(state) {
        if (!state.canvasWidth || !state.canvasHeight || !state.scale) {
            return;
        }

        const scale = state.scale;
        const width = state.canvasWidth;
        const height = state.canvasHeight;

        state.worldCenterX = (width / 2 - state.offsetX) / scale;
        state.worldCenterY = (height / 2 - state.offsetY) / scale;
    }

    function getViewport(payload) {
        return payload?.viewport ?? payload?.Viewport ?? null;
    }

    function computeViewportSignature(viewport) {
        if (!viewport) {
            return null;
        }
        const minX = viewport.minX ?? viewport.MinX ?? 0;
        const minY = viewport.minY ?? viewport.MinY ?? 0;
        const maxX = viewport.maxX ?? viewport.MaxX ?? 0;
        const maxY = viewport.maxY ?? viewport.MaxY ?? 0;
        return `${minX}|${minY}|${maxX}|${maxY}`;
    }

    function applyViewport(canvas, state, viewport) {
        if (!viewport) {
            return;
        }

        const width = canvas.width / state.deviceRatio;
        const height = canvas.height / state.deviceRatio;

        const minX = viewport.minX ?? viewport.MinX ?? 0;
        const minY = viewport.minY ?? viewport.MinY ?? 0;
        const maxX = viewport.maxX ?? viewport.MaxX ?? 0;
        const maxY = viewport.maxY ?? viewport.MaxY ?? 0;
        const padding = viewport.padding ?? viewport.Padding ?? 48;

        const contentWidth = Math.max(maxX - minX, 1);
        const contentHeight = Math.max(maxY - minY, 1);
        const availableWidth = Math.max(width - (padding * 2), 1);
        const availableHeight = Math.max(height - (padding * 2), 1);

        const desiredScale = Math.min(availableWidth / contentWidth, availableHeight / contentHeight);
        const safeScale = clamp(desiredScale, 0.2, 1.5);

        if (!state.viewportApplied) {
            state.baseScale = safeScale;
            state.worldCenterX = minX + (contentWidth / 2);
            state.worldCenterY = minY + (contentHeight / 2);
            state.viewportApplied = true;
        } else if (!state.userAdjusted) {
            // Ensure stored center remains valid
            if (!Number.isFinite(state.worldCenterX) || !Number.isFinite(state.worldCenterY)) {
                state.worldCenterX = minX + (contentWidth / 2);
                state.worldCenterY = minY + (contentHeight / 2);
            }
        }

        const baseScale = clamp(state.baseScale ?? safeScale, 0.05, MAX_SCALE);
        const targetScale = clamp(state.overlayScale ?? baseScale ?? safeScale, MIN_SCALE, MAX_SCALE);

        state.baseScale = baseScale;
        state.scale = targetScale;
        state.overlayScale = targetScale;
        state.lastOverlayZoom = targetScale;
        state.offsetX = (width / 2) - (state.worldCenterX * targetScale);
        state.offsetY = (height / 2) - (state.worldCenterY * targetScale);
        state.userAdjusted = false;

        updateWorldCenter(state);
    }

    function traceRoundedRect(ctx, centerX, centerY, width, height, radius) {
        const halfWidth = width / 2;
        const halfHeight = height / 2;
        const left = centerX - halfWidth;
        const right = centerX + halfWidth;
        const top = centerY - halfHeight;
        const bottom = centerY + halfHeight;
        const effectiveRadius = Math.min(radius, Math.min(width, height) / 2);

        ctx.moveTo(left + effectiveRadius, top);
        ctx.lineTo(right - effectiveRadius, top);
        ctx.quadraticCurveTo(right, top, right, top + effectiveRadius);
        ctx.lineTo(right, bottom - effectiveRadius);
        ctx.quadraticCurveTo(right, bottom, right - effectiveRadius, bottom);
        ctx.lineTo(left + effectiveRadius, bottom);
        ctx.quadraticCurveTo(left, bottom, left, bottom - effectiveRadius);
        ctx.lineTo(left, top + effectiveRadius);
        ctx.quadraticCurveTo(left, top, left + effectiveRadius, top);
        ctx.closePath();
    }

    function hexToRgb(hex) {
        if (!hex || typeof hex !== 'string') return null;
        let normalized = hex.trim();
        if (normalized.startsWith('#')) {
            normalized = normalized.slice(1);
        }
        if (normalized.length === 3) {
            normalized = normalized.split('').map(ch => `${ch}${ch}`).join('');
        }
        if (!/^[a-f0-9]{6}$/i.test(normalized)) {
            return null;
        }
        const intVal = parseInt(normalized, 16);
        return {
            r: (intVal >> 16) & 255,
            g: (intVal >> 8) & 255,
            b: intVal & 255
        };
    }

    function colorStringToRgb(value) {
        if (!value || typeof value !== 'string') {
            return null;
        }
        const trimmed = value.trim().toLowerCase();
        if (trimmed.startsWith('#')) {
            return hexToRgb(trimmed);
        }
        const rgbMatch = trimmed.match(/rgba?\(([^)]+)\)/);
        if (rgbMatch) {
            const parts = rgbMatch[1].split(',').map(p => parseFloat(p.trim())).filter(v => !Number.isNaN(v));
            if (parts.length >= 3) {
                return { r: parts[0], g: parts[1], b: parts[2] };
            }
        }
        return null;
    }

    function calculateLuminance(rgb) {
        const srgb = [rgb.r, rgb.g, rgb.b].map(v => {
            v /= 255;
            return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
        });
        return 0.2126 * srgb[0] + 0.7152 * srgb[1] + 0.0722 * srgb[2];
    }

    function isDarkColor(hex) {
        const rgb = colorStringToRgb(hex);
        if (!rgb) return false;
        const L = calculateLuminance(rgb);
        return L < 0.5;
    }

    function drawFittedText(ctx, text, x, y, maxWidth) {
        const full = String(text);
        if (ctx.measureText(full).width <= maxWidth) {
            ctx.fillText(full, x, y);
            return;
        }
        const ellipsis = '…';
        let lo = 0, hi = full.length;
        while (lo < hi) {
            const mid = (lo + hi + 1) >> 1;
            const candidate = full.slice(0, mid) + ellipsis;
            if (ctx.measureText(candidate).width <= maxWidth) {
                lo = mid;
            } else {
                hi = mid - 1;
            }
        }
        const finalText = full.slice(0, lo) + ellipsis;
        ctx.fillText(finalText, Math.round(x), Math.round(y));
    }

    function drawRoundedRectTopLeft(ctx, x, y, width, height, radius) {
        const r = Math.min(radius, Math.min(width, height) / 2);
        ctx.beginPath();
        ctx.moveTo(x + r, y);
        ctx.lineTo(x + width - r, y);
        ctx.quadraticCurveTo(x + width, y, x + width, y + r);
        ctx.lineTo(x + width, y + height - r);
        ctx.quadraticCurveTo(x + width, y + height, x + width - r, y + height);
        ctx.lineTo(x + r, y + height);
        ctx.quadraticCurveTo(x, y + height, x, y + height - r);
        ctx.lineTo(x, y + r);
        ctx.quadraticCurveTo(x, y, x + r, y);
        ctx.closePath();
    }

    function drawSettingsGlyph(ctx, centerX, centerY, size, color) {
        const halfLength = size / 2;
        const spacing = size / 3.2;
        ctx.save();
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.1;

        for (let i = -1; i <= 1; i++) {
            const y = centerY + i * spacing;
            ctx.beginPath();
            ctx.moveTo(centerX - halfLength, y);
            ctx.lineTo(centerX + halfLength, y);
            ctx.stroke();
        }

        ctx.fillStyle = color;
        const dotRadius = Math.max(1.3, size * 0.16);
        const offsets = [
            { x: -halfLength * 0.55, y: -spacing },
            { x: halfLength * 0.2, y: 0 },
            { x: -halfLength * 0.25, y: spacing }
        ];
        for (const offset of offsets) {
            ctx.beginPath();
            ctx.arc(centerX + offset.x, centerY + offset.y, dotRadius, 0, Math.PI * 2);
            ctx.fill();
        }

        ctx.restore();

        return {
            width: halfLength * 2,
            height: spacing * 2
        };
    }

    function parseOverlaySettings(raw) {
        const boolOr = (value, fallback) => {
            if (typeof value === 'boolean') {
                return value;
            }
            if (typeof value === 'number') {
                return value !== 0;
            }
            if (typeof value === 'string') {
                const lowered = value.trim().toLowerCase();
                if (lowered === 'true') return true;
                if (lowered === 'false') return false;
                const num = Number(value);
                return Number.isFinite(num) ? num !== 0 : fallback;
            }
            return fallback;
        };

        const sparkMode = Number(raw.sparklineMode ?? raw.SparklineMode ?? 0);
        const edgeStyleRaw = raw.edgeStyle ?? raw.EdgeStyle ?? 0;
        const edgeOverlayRaw = raw.edgeOverlay ?? raw.EdgeOverlay ?? null;
        const zoomPercentRaw = Number(raw.zoomPercent ?? raw.ZoomPercent ?? 100);
        const zoomPercent = Number.isFinite(zoomPercentRaw)
            ? clamp(zoomPercentRaw, MIN_ZOOM_PERCENT, MAX_ZOOM_PERCENT)
            : 100;
        const manualScale = clamp(zoomPercent / 100, MIN_SCALE, MAX_SCALE);
        const selectedBin = Number(raw.selectedBin ?? raw.SelectedBin ?? -1);

        const slaSuccess = clamp(Number(raw.slaSuccessThreshold ?? raw.SlaSuccessThreshold ?? 0.95), 0, 1);
        const slaWarning = clamp(Number(raw.slaWarningCutoff ?? raw.SlaWarningCutoff ?? 0.8), 0, 1);
        const utilWarn = clamp(Number(raw.utilizationWarningCutoff ?? raw.UtilizationWarningCutoff ?? 0.9), 0, 1);
        const utilCritical = clamp(Number(raw.utilizationCriticalCutoff ?? raw.UtilizationCriticalCutoff ?? 0.95), 0, 1);
        const errorWarn = clamp(Number(raw.errorWarningCutoff ?? raw.ErrorWarningCutoff ?? 0.02), 0, 1);
        const errorCritical = clamp(Number(raw.errorCriticalCutoff ?? raw.ErrorCriticalCutoff ?? 0.05), 0, 1);
        const serviceTimeWarnRaw = Number(raw.serviceTimeWarningThresholdMs ?? raw.ServiceTimeWarningThresholdMs ?? 400);
        const serviceTimeCritRaw = Number(raw.serviceTimeCriticalThresholdMs ?? raw.ServiceTimeCriticalThresholdMs ?? 700);
        const serviceTimeWarningMs = Number.isFinite(serviceTimeWarnRaw) && serviceTimeWarnRaw > 0 ? serviceTimeWarnRaw : 400;
        const serviceTimeCriticalCandidate = Number.isFinite(serviceTimeCritRaw) && serviceTimeCritRaw > 0 ? serviceTimeCritRaw : 700;
        const serviceTimeCriticalMs = Math.max(serviceTimeWarningMs, serviceTimeCriticalCandidate);
        const flowLatencyWarnRaw = Number(raw.flowLatencyWarningThresholdMs ?? raw.FlowLatencyWarningThresholdMs ?? 2000);
        const flowLatencyCritRaw = Number(raw.flowLatencyCriticalThresholdMs ?? raw.FlowLatencyCriticalThresholdMs ?? 10000);
        const flowLatencyWarningMs = Number.isFinite(flowLatencyWarnRaw) && flowLatencyWarnRaw > 0 ? flowLatencyWarnRaw : 2000;
        const flowLatencyCriticalCandidate = Number.isFinite(flowLatencyCritRaw) && flowLatencyCritRaw > 0 ? flowLatencyCritRaw : 10000;
        const flowLatencyCriticalMs = Math.max(flowLatencyWarningMs, flowLatencyCriticalCandidate);

        let edgeStyle = 'orthogonal';
        if (typeof edgeStyleRaw === 'string') {
            const normalized = edgeStyleRaw.trim().toLowerCase();
            if (normalized === 'bezier') {
                edgeStyle = 'bezier';
            }
        } else {
            edgeStyle = Number(edgeStyleRaw) === 1 ? 'bezier' : 'orthogonal';
        }

        let edgeOverlayMode = 'off';
        if (typeof edgeOverlayRaw === 'string') {
            const normalized = edgeOverlayRaw.trim().toLowerCase();
            if (normalized === 'retryrate' || normalized === 'retry_rate') {
                edgeOverlayMode = 'retryRate';
            } else if (normalized === 'attempts') {
                edgeOverlayMode = 'attempts';
            } else if (normalized === 'off' || normalized === 'none') {
                edgeOverlayMode = 'off';
            }
        } else if (edgeOverlayRaw !== undefined && edgeOverlayRaw !== null) {
            const numeric = Number(edgeOverlayRaw);
            if (numeric === 1) {
                edgeOverlayMode = 'retryRate';
            } else if (numeric === 0) {
                edgeOverlayMode = 'off';
            } else if (numeric === 2) {
                edgeOverlayMode = 'attempts';
            }
        }

        return {
            showLabels: boolOr(raw.showLabels ?? raw.ShowLabels, true),
            showEdgeArrows: boolOr(raw.showEdgeArrows ?? raw.ShowEdgeArrows, true),
            showEdgeShares: boolOr(raw.showEdgeShares ?? raw.ShowEdgeShares, false),
            showEdgeMultipliers: boolOr(raw.showEdgeMultipliers ?? raw.ShowEdgeMultipliers, true),
            showSparklines: boolOr(raw.showSparklines ?? raw.ShowSparklines, true),
            showRetryMetrics: boolOr(raw.showRetryMetrics ?? raw.ShowRetryMetrics, true),
            sparklineMode: sparkMode === 1 ? 'bar' : 'line',
            edgeStyle,
            edgeOverlayMode,
            showEdgeOverlayLabels: true,
            colorBasis: raw.colorBasis ?? raw.ColorBasis ?? 0,
            zoomPercent,
            manualScale,
            neighborEmphasis: boolOr(raw.neighborEmphasis ?? raw.NeighborEmphasis, true),
            enableFullDag: boolOr(raw.enableFullDag ?? raw.EnableFullDag, false),
            includeServiceNodes: boolOr(raw.includeServiceNodes ?? raw.IncludeServiceNodes, true),
            includeDlqNodes: boolOr(raw.includeDlqNodes ?? raw.IncludeDlqNodes, true),
            includeExpressionNodes: boolOr(raw.includeExpressionNodes ?? raw.IncludeExpressionNodes, false),
            includeConstNodes: boolOr(raw.includeConstNodes ?? raw.IncludeConstNodes, false),
            selectedBin,
            showArrivalsDependencies: boolOr(raw.showArrivalsDependencies ?? raw.ShowArrivalsDependencies, true),
            showServedDependencies: boolOr(raw.showServedDependencies ?? raw.ShowServedDependencies, true),
            showErrorsDependencies: boolOr(raw.showErrorsDependencies ?? raw.ShowErrorsDependencies, true),
            showQueueDependencies: boolOr(raw.showQueueDependencies ?? raw.ShowQueueDependencies, true),
            showCapacityDependencies: boolOr(raw.showCapacityDependencies ?? raw.ShowCapacityDependencies, true),
            showExpressionDependencies: boolOr(raw.showExpressionDependencies ?? raw.ShowExpressionDependencies, true),
            thresholds: {
                slaSuccess,
                slaWarning,
                utilizationWarning: utilWarn,
                utilizationCritical: utilCritical,
                errorWarning: errorWarn,
                errorCritical,
                serviceTimeWarningMs,
                serviceTimeCriticalMs,
                flowLatencyWarningMs,
                flowLatencyCriticalMs
            }
        };
    }

    function resolveSparklineColor(basis) {
        switch (basis) {
            case 1: // Utilization
                return '#5F3DC4';
            case 2: // Errors
                return '#D9480F';
            case 3: // Queue
                return '#2F9E44';
            case 4: // Service Time
                return '#1D4ED8';
            case 5: // Flow latency
                return '#2563EB';
            default: // SLA / fallback
                return '#0B7285';
        }
    }

    function isComputedKind(kind) {
        if (typeof kind !== 'string') {
            return false;
        }

        const normalized = kind.trim().toLowerCase();
        return normalized === 'expr' || normalized === 'expression' || normalized === 'const' || normalized === 'constant' || normalized === 'pmf';
    }

    function isQueueLikeKind(kind, logicalType) {
        if (typeof kind !== 'string') {
            return false;
        }

        if (typeof logicalType === 'string' && logicalType.trim().toLowerCase() === 'servicewithbuffer') {
            return false;
        }

        const normalized = kind.trim().toLowerCase();
        return normalized === 'queue' || normalized === 'dlq';
    }

    function isDlqKind(kind) {
        if (typeof kind !== 'string') {
            return false;
        }
        return kind.trim().toLowerCase() === 'dlq';
    }

    function isExpressionKind(kind) {
        if (typeof kind !== 'string') {
            return false;
        }
        const normalized = kind.trim().toLowerCase();
        return normalized === 'expr' || normalized === 'expression';
    }

    function isConstKind(kind) {
        if (typeof kind !== 'string') {
            return false;
        }
        const normalized = kind.trim().toLowerCase();
        return normalized === 'const' || normalized === 'constant';
    }

    function resolveSampleColor(basis, index, sparkline, thresholds, defaultColor) {
        const value = getBasisValue(basis, index, sparkline);
        if (value === null || value === undefined || !Number.isFinite(value)) {
            return 'rgba(203, 213, 225, 0.75)';
        }

        switch (basis) {
            case 1: // Utilization
                if (value <= thresholds.utilizationWarning) return '#009E73';
                if (value <= thresholds.utilizationCritical) return '#E69F00';
                return '#D55E00';
            case 2: // Errors
                if (value >= thresholds.errorCritical) return '#D55E00';
                if (value >= thresholds.errorWarning) return '#E69F00';
                return '#009E73';
            case 3: // Queue depth
                if (value >= 0.8) return '#D55E00';
                if (value >= 0.4) return '#E69F00';
                return '#009E73';
            case 4: // Service time (ms)
                if (value >= thresholds.serviceTimeCriticalMs) return '#D55E00';
                if (value >= thresholds.serviceTimeWarningMs) return '#E69F00';
                return '#009E73';
            case 5: // Flow latency (ms)
                if (value >= thresholds.flowLatencyCriticalMs) return '#D55E00';
                if (value >= thresholds.flowLatencyWarningMs) return '#E69F00';
                return '#009E73';
            default: // SLA success
                if (value >= thresholds.slaSuccess) return '#009E73';
                if (value >= thresholds.slaWarning) return '#E69F00';
                return '#D55E00';
        }
    }

    function getBasisValue(basis, index, sparkline) {
        const sliceValue = (key) => {
            const slice = getSeriesSlice(sparkline, key);
            const values = slice?.values ?? slice?.Values;
            if (Array.isArray(values)) {
                return valueAtArray(values, index);
            }
            return null;
        };

        switch (basis) {
            case 1:
                return sliceValue('utilization') ?? valueAtArray(sparkline.utilization ?? sparkline.Utilization, index);
            case 2:
                return sliceValue('errorRate') ?? valueAtArray(sparkline.errorRate ?? sparkline.ErrorRate, index);
            case 3:
                return sliceValue('queue') ?? sliceValue('queueDepth') ?? valueAtArray(sparkline.queueDepth ?? sparkline.QueueDepth, index);
            case 4:
                return sliceValue('serviceTimeMs') ?? sliceValue('serviceTime') ?? valueAtArray(sparkline.serviceTimeMs ?? sparkline.ServiceTimeMs, index);
            case 5:
                return sliceValue('flowLatencyMs') ?? sliceValue('flowLatency') ?? valueAtArray(sparkline.flowLatencyMs ?? sparkline.FlowLatencyMs, index);
            default:
                return sliceValue('successRate') ?? valueAtArray(sparkline.values ?? sparkline.Values, index);
        }
    }

    function valueAtArray(arr, index) {
        if (!arr || index < 0 || index >= arr.length) {
            return null;
        }
        const value = Number(arr[index]);
        return Number.isFinite(value) ? value : null;
    }

    function computePortOffset(node, ux, uy, padding) {
        if (!node) {
            return 16;
        }

        const width = Number(node.width ?? node.Width ?? 54);
        const height = Number(node.height ?? node.Height ?? 24);
        const absUx = Math.abs(ux);
        const absUy = Math.abs(uy);
        const epsilon = 1e-6;
        const kind = String(node.kind ?? node.Kind ?? 'service').toLowerCase();
        const isLeafComputed = !!(node.leaf ?? node.Leaf) && isComputedKind(kind);

        let boundary;
        if (kind === 'expr' || kind === 'expression') {
            const halfW = width / 2;
            const halfH = height / 2;
            const denom = (absUx / halfW) + (absUy / halfH);
            boundary = denom < epsilon ? Math.max(halfW, halfH) : 1 / denom;
        } else {
            const halfW = width / 2;
            const halfH = Math.max(height, isLeafComputed ? 20 : height) / 2;
            const candidates = [];

            if (absUx > epsilon) {
                candidates.push(halfW / absUx);
            }

            if (absUy > epsilon) {
                candidates.push(halfH / absUy);
            }

            if (candidates.length === 0) {
                boundary = Math.max(halfW, halfH);
            } else {
                boundary = Math.min(...candidates);
            }
        }

        const extra = Number.isFinite(padding) ? padding : 0;
        return boundary + extra;
    }

    function edgeLaneOffset(edge) {
        const raw = String(edge?.id ?? edge?.Id ?? '');
        let hash = 0;
        for (let i = 0; i < raw.length; i++) {
            hash = ((hash << 5) - hash) + raw.charCodeAt(i);
            hash |= 0;
        }
        const lane = (hash % 5) - 2; // -2..2
        return lane * 6;
    }

    function edgeLaneDescriptor(edge, fromX, toX) {
        const raw = String(edge?.id ?? edge?.Id ?? '');
        let hash = 0;
        for (let i = 0; i < raw.length; i++) {
            hash = ((hash << 5) - hash) + raw.charCodeAt(i);
            hash |= 0;
        }

        const band = Math.abs(hash % 3);
        let side = (hash & 1) === 0 ? -1 : 1;
        if (Number.isFinite(fromX) && Number.isFinite(toX)) {
            const meanX = (fromX + toX) / 2;
            if (Math.abs(meanX) > Math.abs(meanX + side * 0.01)) {
                side = meanX >= 0 ? -1 : 1;
            }
        }
        return { side, band };
    }

    function computeNeighborNodes(edges, focusedId) {
        const set = new Set();
        if (!focusedId) {
            return set;
        }

        set.add(focusedId);
        for (const edge of edges) {
            const fromId = edge?.from ?? edge?.From;
            const toId = edge?.to ?? edge?.To;

            if (fromId === focusedId || toId === focusedId) {
                if (fromId) {
                    set.add(fromId);
                }
                if (toId) {
                    set.add(toId);
                }
            }
        }

        return set;
    }

    function computeNeighborEdges(edges, focusedId) {
        const set = new Set();
        if (!focusedId) {
            return set;
        }

        for (const edge of edges) {
            const fromId = edge?.from ?? edge?.From;
            const toId = edge?.to ?? edge?.To;

            if (fromId === focusedId || toId === focusedId) {
                const edgeId = edge?.id ?? edge?.Id ?? `${fromId ?? ''}->${toId ?? ''}`;
                set.add(edgeId);
            }
        }

        return set;
    }

    function shouldRenderDependencyEdge(field, overlays) {
        const normalized = (field || '').toLowerCase();
        switch (normalized) {
            case 'arrivals':
                return overlays.showArrivalsDependencies !== false;
            case 'served':
                return overlays.showServedDependencies !== false;
            case 'errors':
                return overlays.showErrorsDependencies !== false;
            case 'queue':
                return overlays.showQueueDependencies !== false;
            case 'capacity':
                return overlays.showCapacityDependencies !== false;
            case 'expr':
                return overlays.showExpressionDependencies !== false;
            default:
                return true;
        }
    }

    function normalizeWarningEntries(nodeMeta, globalWarnings) {
        const rawFromNode = nodeMeta?.warnings ?? nodeMeta?.Warnings ?? null;
        const raw = (rawFromNode && Array.isArray(rawFromNode) && rawFromNode.length > 0)
            ? rawFromNode
            : (globalWarnings?.get?.((nodeMeta?.id ?? nodeMeta?.Id ?? '').toString().toLowerCase()) ?? null);
        if (!Array.isArray(raw) || raw.length === 0) {
            return [];
        }

        const normalized = [];
        for (const entry of raw) {
            if (!entry) continue;
            const codeCandidate = typeof entry.code === 'string' ? entry.code : entry.Code;
            const messageCandidate = typeof entry.message === 'string' ? entry.message : entry.Message;
            const severityCandidate = typeof entry.severity === 'string' ? entry.severity : entry.Severity;
            const code = (codeCandidate ?? '').toString().trim() || 'warning';
            if (code.toLowerCase() === 'queue_latency_gate_closed') {
                continue;
            }
            const message = (messageCandidate ?? '').toString().trim();
            const severity = (severityCandidate ?? '').toString().trim().toLowerCase() || 'warning';
            normalized.push({ code, message, severity });
        }
        return normalized;
    }

    function isQueueWarningEntry(entry) {
        if (!entry) {
            return false;
        }
        const code = (entry.code ?? '').toString().toLowerCase();
        const message = (entry.message ?? '').toString().toLowerCase();
        const combined = `${code} ${message}`.trim();
        if (combined.length === 0) {
            return false;
        }
        if (combined.includes('queue_depth') || combined.includes('queue depth')) {
            return true;
        }
        if (combined.includes('queue')) {
            return combined.includes('depth')
                || combined.includes('backlog')
                || combined.includes('accumulation')
                || combined.includes('inflow')
                || combined.includes('outflow')
                || combined.includes('mismatch')
                || combined.includes('building');
        }
        if (combined.includes('arrivals') && combined.includes('capacity')) {
            return true;
        }
        return false;
    }

    function extractQueueWarningText(entries) {
        if (!Array.isArray(entries) || entries.length === 0) {
            return null;
        }
        const text = entries
            .filter(entry => isQueueWarningEntry(entry))
            .map(entry => entry.message || entry.code)
            .filter(value => typeof value === 'string' && value.trim().length > 0)
            .join('\n');
        return text.length > 0 ? text : null;
    }

    function normalizeQueueLatencyStatus(status) {
        if (!status) return null;
        const codeCandidate = typeof status.code === 'string' ? status.code : status.Code;
        const messageCandidate = typeof status.message === 'string' ? status.message : status.Message;
        const code = (codeCandidate ?? '').toString().trim();
        if (!code) {
            return null;
        }

        const normalized = {
            code: code.toLowerCase(),
            originalCode: code,
            message: (messageCandidate ?? '').toString().trim()
        };
        return normalized;
    }

    function formatQueueLatencyStatusLabel(status) {
        if (!status) {
            return 'Latency paused';
        }

        switch (status.code) {
            case 'queue_latency_gate_closed':
                return 'Paused (gate closed)';
            case 'queue_latency_unreported':
                return 'Latency unavailable';
            default:
                if (status.message && status.message.length > 0) {
                    return status.message;
                }
                return toPascal(status.originalCode ?? 'Latency status');
        }
    }

    function drawServiceDecorations(ctx, nodeMeta, overlaySettings, state) {
        if (!nodeMeta) return;

        const overlays = overlaySettings ?? {};
        const semanticsRaw = nodeMeta.semantics ?? null;
        const spark = nodeMeta.sparkline ?? null;

        const semantics = normalizeSemantics(semanticsRaw);
        const metricSnapshot = nodeMeta.metrics ?? nodeMeta.Metrics ?? null;
        const rawMetrics = normalizeRawMetricMap(metricSnapshot?.raw ?? metricSnapshot?.Raw ?? null);
        const hasSemantics = Object.values(semantics).some(value => value);
        const hasSpark = spark !== null;
        const shouldDrawSparkline = overlays.showSparklines && spark;
        const hasRetrySemantics = Boolean(semantics.attempts || semantics.failures || semantics.retry);
        const showRetryMetrics = overlays.showRetryMetrics !== false && hasRetrySemantics;
        const nodeKind = String(nodeMeta.kind ?? nodeMeta.Kind ?? '').trim().toLowerCase();
        const nodeLogicalType = String(nodeMeta.logicalType ?? nodeMeta.LogicalType ?? '').trim().toLowerCase();
        const nodeRole = String(nodeMeta.nodeRole ?? nodeMeta.NodeRole ?? '').trim().toLowerCase();
        const isSinkNode = nodeKind === 'sink' || nodeLogicalType === 'sink' || nodeRole === 'sink';
        const isServiceNode = nodeKind === 'service' || nodeLogicalType === 'servicewithbuffer';
        const isServiceWithBuffer = nodeLogicalType === 'servicewithbuffer';
        const isDlqNode = isDlqKind(nodeKind);
        const schedule = nodeMeta.dispatchSchedule ?? nodeMeta.DispatchSchedule ?? null;
        const retryTax = isServiceNode ? resolveRetryTaxValue(nodeMeta) : null;
        const hasRetryLoop = showRetryMetrics && isServiceNode && Number.isFinite(retryTax) && retryTax > 0;
        let warningEntries = normalizeWarningEntries(nodeMeta, state.globalWarningsByNode);
        const warningCodes = new Set(warningEntries.map(entry => (entry.code ?? '').toString().toLowerCase()));
        const addSyntheticWarning = (code, message) => {
            const normalized = (code ?? '').toString().toLowerCase() || 'warning';
            if (warningCodes.has(normalized)) {
                return;
            }

            warningEntries.push({
                code: code ?? 'warning',
                message: message ?? ''
            });
            warningCodes.add(normalized);
        };

        const queueLatencyStatus = normalizeQueueLatencyStatus(metricSnapshot?.queueLatencyStatus ?? metricSnapshot?.QueueLatencyStatus ?? null);
        const queueWarningText = extractQueueWarningText(warningEntries);
        if (queueWarningText) {
            warningEntries = warningEntries.filter(entry => !isQueueWarningEntry(entry));
        }

        if (!hasSemantics && !hasSpark) {
            return;
        }

        const x = nodeMeta.x ?? 0;
        const y = nodeMeta.y ?? 0;
        const nodeWidth = nodeMeta.width ?? 54;
        const nodeHeight = nodeMeta.height ?? 24;
        const gap = 6;
        const chipH = 16;
        const paddingX = 8;

        const topRowTop = y - (nodeHeight / 2) - chipH - gap;
        const bottomRowTop = y + (nodeHeight / 2) + gap;

        const thresholds = overlays.thresholds || {
            slaSuccess: 0.95,
            slaWarning: 0.8,
            utilizationWarning: 0.9,
            utilizationCritical: 0.95,
            errorWarning: 0.02,
            errorCritical: 0.05
        };

        const selectedBin = Number(overlays.selectedBin ?? -1);
        const hasSelectedBin = Number.isFinite(selectedBin) && selectedBin >= 0;

        const sampleBin = (() => {
            if (hasSelectedBin || !spark) {
                return selectedBin;
            }

            const baseStart = Number(spark.startIndex ?? spark.StartIndex ?? 0);
            const baseValues = spark.values ?? spark.Values;
            if (Array.isArray(baseValues) && baseValues.length > 0) {
                return baseStart + baseValues.length - 1;
            }

            const map = spark.series ?? spark.Series;
            if (map) {
                for (const slice of Object.values(map)) {
                    if (!slice) {
                        continue;
                    }

                    const sliceValues = slice.values ?? slice.Values;
                    if (Array.isArray(sliceValues) && sliceValues.length > 0) {
                        const sliceStart = Number(slice.startIndex ?? slice.StartIndex ?? baseStart);
                        return sliceStart + sliceValues.length - 1;
                    }
                }
            }

            return baseStart;
        })();

        const sampleValueFor = (defaultKey, semanticEntry, extraKeys) => {
            const keys = [];
            const pushKey = (candidate) => {
                if (candidate === null || candidate === undefined) {
                    return;
                }

                const text = String(candidate).trim();
                if (text.length === 0) {
                    return;
                }

                if (!keys.includes(text)) {
                    keys.push(text);
                }
            };

            pushKey(defaultKey);

            if (semanticEntry) {
                pushKey(semanticEntry.key);
                pushKey(semanticEntry.canonical);
                pushKey(semanticEntry.reference);
                pushKey(semanticEntry.label);
            }

            if (Array.isArray(extraKeys)) {
                for (const candidate of extraKeys) {
                    pushKey(candidate);
                }
            }

            const lookupRawMetric = (candidate) => {
                if (!rawMetrics || !candidate) {
                    return null;
                }

                const text = String(candidate).trim();
                if (text.length === 0) {
                    return null;
                }

                const normalized = text.toLowerCase();
                if (!Object.prototype.hasOwnProperty.call(rawMetrics, normalized)) {
                    return null;
                }

                const rawValue = rawMetrics[normalized];
                if (rawValue === null || rawValue === undefined) {
                    return null;
                }

                const numeric = Number(rawValue);
                return Number.isFinite(numeric) ? numeric : null;
            };

            for (const candidate of keys) {
                let value = null;
                if (spark) {
                    value = sampleSeriesValueAt(spark, candidate, sampleBin);
                }

                if (value === null || value === undefined) {
                    value = lookupRawMetric(candidate);
                }

                if (value !== null && value !== undefined) {
                    return value;
                }
            }

            return lookupRawMetric(defaultKey);
        };

        if (shouldDrawSparkline) {
            const neutralSpark = '#94A3B8';
            const isComputedNode = isComputedKind(nodeMeta.kind);
            const defaultSpark = isComputedNode ? neutralSpark : (nodeMeta.fill ?? resolveSparklineColor(overlays.colorBasis));
            drawSparkline(ctx, nodeMeta, spark, overlays, defaultSpark, {
                top: topRowTop,
                height: chipH,
                right: (x - (nodeWidth / 2) - 8),
                neutralize: isComputedNode
            });

            // Tiny label positioned directly above the sparkline (left-aligned)
            try {
                const basis = overlays.colorBasis ?? 0;
                const queueLike = isQueueLikeKind(nodeKind, nodeLogicalType);
                const seriesBasis = queueLike ? 3 : Number(basis);
                const label = queueLike
                    ? (isDlqKind(nodeKind) ? 'DLQ' : 'Queue')
                    : (function () {
                        switch (basis) {
                            case 1: return 'Util';
                            case 2: return 'Errors';
                            case 3: return 'Queue';
                            case 4: return 'Svc time';
                            default: return 'SLA';
                        }
                    })();

                // Recompute sparkline geometry to determine its left edge
                const mode = overlays.sparklineMode === 'bar' ? 'bar' : 'line';
                const nodeWidth = nodeMeta.width ?? 54;
                const defaultSparkWidth = Math.max(nodeWidth - 6, 16);
                const baseWidth = defaultSparkWidth;
                const series = selectSeriesForBasis(spark, seriesBasis) ?? [];
                const sparkWidth = computeAdaptiveWidth(series.length, baseWidth, {
                    min: Math.max(baseWidth, 20),
                    max: 140,
                    scale: mode === 'bar' ? 11 : 9
                });
                const rightEdge = (x - (nodeWidth / 2) - 8);
                const leftEdge = rightEdge - sparkWidth;

                ctx.save();
                const sparkLabelColor = isDarkTheme() ? NODE_LABEL_COLOR_DARK : '#64748B';
                ctx.fillStyle = sparkLabelColor;
                ctx.globalAlpha = 0.9;
                ctx.font = '10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
                ctx.textAlign = 'left';
                ctx.textBaseline = 'alphabetic';
                // place just above the sparkline row, aligned to its left edge
                ctx.fillText(label, leftEdge, topRowTop - 2);
                ctx.restore();
            } catch { /* draw label is best-effort */ }
        }

        if (!hasSemantics) {
            return;
        }

        ctx.save();
        ctx.font = '11px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';

        let topLeft = x - (nodeWidth / 2) + gap;
        let topRight = x + (nodeWidth / 2) + gap;
        let bottomLeft = topLeft;
        const bottomLeftBaseline = bottomLeft;
        let bottomRight = topRight;
        const scheduleInfo = (() => {
            if (!schedule) {
                return null;
            }

            const period = Number(schedule.periodBins ?? schedule.PeriodBins);
            if (!Number.isFinite(period) || period <= 0) {
                return null;
            }

            const phaseRaw = Number(schedule.phaseOffset ?? schedule.PhaseOffset ?? 0);
            const phase = Number.isFinite(phaseRaw) ? Math.max(0, phaseRaw) : 0;
            const capacitySeries = schedule.capacitySeries ?? schedule.CapacitySeries ?? null;

            return { period, phase, capacitySeries };
        })();

        const arrivalsValue = sampleValueFor('arrivals', semantics.arrivals);
        const attemptsValue = isServiceNode ? sampleValueFor('attempts', semantics.attempts, ['attempt']) : null;
        const failuresValue = sampleValueFor('failures', semantics.failures, ['failure']);
        const retryValue = sampleValueFor('retryEcho', semantics.retry, ['retry', 'retry_echo']);
        const servedValue = sampleValueFor('served', semantics.served);
        const exhaustedValue = isServiceNode ? sampleValueFor('exhaustedFailures', semantics.exhausted, ['exhausted', 'exhaustedfailures']) : null;
        const budgetRemainingValue = isServiceNode ? sampleValueFor('retryBudgetRemaining', semantics.retryBudget, ['retrybudgetremaining', 'retrybudget']) : null;

        if (!isServiceWithBuffer && Number.isFinite(servedValue) && Number.isFinite(arrivalsValue) && (servedValue - arrivalsValue) > 1e-6) {
            addSyntheticWarning('served_exceeds_arrivals', 'Served volume exceeded arrivals');
        }

        if (overlays.showArrivalsDependencies !== false) {
            if (arrivalsValue !== null) {
                const arrivalLabel = formatMetricValue(arrivalsValue);
                if (arrivalLabel) {
                    const dims = drawChip(ctx, topLeft, topRowTop + chipH, arrivalLabel, '#1976D2', '#FFFFFF', paddingX, chipH);
                    registerChipHitbox(state, {
                        nodeId: nodeMeta.id ?? null,
                        metric: 'arrivals',
                        placement: 'top',
                        tooltip: semanticTooltip(semantics.arrivals, 'Arrivals'),
                        x: topLeft,
                        y: dims.top,
                        width: dims.width,
                        height: dims.height
                    });
                    topLeft += dims.width + gap;

                    if (isDlqNode) {
                        const tagColors = getDlqTagColors();
                        const dlqDims = drawChip(
                            ctx,
                            topLeft,
                            topRowTop + chipH,
                            'DLQ',
                            tagColors.stroke ?? getDlqPalette().stroke,
                            tagColors.text,
                            paddingX,
                            chipH,
                            'bottom',
                            tagColors.fill,
                            null,
                            '600');
                        topLeft += dlqDims.width + gap;
                    }
                }
            }
        }

        if (isSinkNode && scheduleInfo) {
            const cadence = scheduleInfo.period === 1 ? 'Every bin' : `Every ${scheduleInfo.period} bins`;
            const summary = `${cadence} (phase ${scheduleInfo.phase})`;
            const capacityLabel = scheduleInfo.capacitySeries
                ? `Capacity: ${scheduleInfo.capacitySeries}`
                : 'Capacity: unbounded';
            const tooltip = `Arrival schedule: ${summary}\n${capacityLabel}`;
            const scheduleTop = y - (chipH / 2);
            const scheduleX = x + (nodeWidth / 2) + 5;
            const dims = drawScheduleBadge(ctx, scheduleX, scheduleTop, chipH);
            registerChipHitbox(state, {
                nodeId: nodeMeta.id ?? null,
                metric: 'schedule',
                placement: 'right',
                tooltip,
                x: scheduleX,
                y: dims.top,
                width: dims.width,
                height: dims.height
            });
        }

        if (isServiceWithBuffer) {
            const queueValue = sampleValueFor('queue', semantics.queue, ['queueDepth']);
            if (queueValue !== null) {
                if (queueWarningText) {
                    warningEntries = warningEntries.filter(entry => !isQueueWarningEntry(entry));
                }
                const backlogLabel = formatMetricValue(queueValue);
                if (backlogLabel) {
                    const warningStyle = queueWarningText
                        ? {
                            fill: '#FFE04D',
                            stroke: '#F59E0B',
                            text: '#78350F'
                        }
                        : null;
                    const dims = drawQueueChip(ctx, topLeft, topRowTop + chipH, backlogLabel, paddingX, chipH, 'bottom', warningStyle);
                    registerChipHitbox(state, {
                        nodeId: nodeMeta.id ?? null,
                        metric: 'queue',
                        placement: 'top',
                        tooltip: queueWarningText
                            ? `${semanticTooltip(semantics.queue, 'Staged backlog') ?? 'Staged backlog'}\n${queueWarningText}`
                            : semanticTooltip(semantics.queue, 'Staged backlog'),
                        x: topLeft,
                        y: dims.top,
                        width: dims.width,
                        height: dims.height
                    });
                    topLeft += dims.width + gap;
                }
            }

            if (queueLatencyStatus && queueLatencyStatus.code === 'queue_latency_gate_closed') {
                const statusLabel = formatQueueLatencyStatusLabel(queueLatencyStatus);
                const statusFill = isDarkTheme() ? 'rgba(14, 165, 233, 0.3)' : 'rgba(14, 165, 233, 0.18)';
                const dims = drawChip(ctx, topLeft, topRowTop + chipH, statusLabel, '#0EA5E9', getQueueLabelColor(), paddingX, chipH, 'top', statusFill);
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric: 'queueLatencyStatus',
                    placement: 'top',
                    tooltip: queueLatencyStatus.message || 'Latency paused while gate closed',
                    x: topLeft,
                    y: dims.top,
                    width: dims.width,
                    height: dims.height
                });
                topLeft += dims.width + gap;
            }
        }

        if (showRetryMetrics && isServiceNode && attemptsValue !== null) {
            const attemptsBaseTooltip = semanticTooltip(semantics.attempts, 'Attempts') ?? 'Attempts';
            const attemptsTooltip = `${attemptsBaseTooltip}\nIncludes retries`;
            const attemptsLabel = formatMetricValue(attemptsValue);
            if (attemptsLabel) {
                const dims = drawChip(ctx, topLeft, topRowTop + chipH, attemptsLabel, '#0EA5E9', '#0F172A', paddingX, chipH);
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric: 'attempts',
                    placement: 'top',
                    tooltip: attemptsTooltip,
                    x: topLeft,
                    y: dims.top,
                    width: dims.width,
                    height: dims.height
                });
                topLeft += dims.width + gap;
            }
        }

        if (semantics.series) {
            const outValue = sampleValueFor('series', semantics.series);
            if (outValue !== null) {
                const outLabel = formatMetricValue(outValue);
                if (outLabel) {
                    const dims = drawChip(ctx, topLeft, topRowTop + chipH, outLabel, '#5C6BC0', '#FFFFFF', paddingX, chipH);
                    registerChipHitbox(state, {
                        nodeId: nodeMeta.id ?? null,
                        metric: 'series',
                        placement: 'top',
                        tooltip: semanticTooltip(semantics.series, 'Series'),
                        x: topLeft,
                        y: dims.top,
                        width: dims.width,
                        height: dims.height
                    });
                    topLeft += dims.width + gap;
                }
            }
        }

        let servedChipDims = null;
        if (!isSinkNode && overlays.showServedDependencies !== false) {
            const servedValue = sampleValueFor('served', semantics.served);
            if (servedValue !== null) {
                const servedLabel = formatMetricValue(servedValue);
                if (servedLabel) {
                    const dims = drawChip(ctx, bottomLeft, bottomRowTop, servedLabel, '#2E7D32', '#FFFFFF', paddingX, chipH, 'top');
                    servedChipDims = dims;
                    registerChipHitbox(state, {
                        nodeId: nodeMeta.id ?? null,
                        metric: 'served',
                        placement: 'bottom-left',
                        tooltip: semanticTooltip(semantics.served, 'Served'),
                        x: bottomLeft,
                        y: dims.top,
                        width: dims.width,
                        height: dims.height
                    });
                    bottomLeft += dims.width + gap;
                }
            }
        }

        const warningEntriesForChip = warningEntries.filter(entry => !isQueueWarningEntry(entry));
        if (warningEntriesForChip.length > 0) {
            const primary = warningEntriesForChip[0];
            const severity = (primary.severity ?? '').toLowerCase() || 'warning';
            const isInfo = severity === 'info';
            const labelBaseRaw = primary.code || (isInfo ? 'Info' : 'Warning');
            const warningLabel = isInfo ? 'Info' : 'Warning';
            const tooltipText = warningEntriesForChip
                .map(entry => entry.message || entry.code)
                .join('\n') || labelBaseRaw;
            const measurement = measureChipText(ctx, warningLabel, paddingX, chipH);
            const anchorLeft = servedChipDims?.left ?? bottomLeftBaseline;
            const warningLeft = anchorLeft - measurement.width - gap;
            const chipBg = isInfo ? '#BFDBFE' : '#FFE04D';
            const chipStroke = isInfo ? '#2563EB' : '#F59E0B';
            const chipText = isInfo ? '#0F2A7A' : '#78350F';
            const dims = drawChip(ctx, warningLeft, bottomRowTop, warningLabel, chipStroke, chipText, paddingX, chipH, 'top', chipBg, measurement, '600');
            registerChipHitbox(state, {
                nodeId: nodeMeta.id ?? null,
                metric: 'warning',
                placement: 'bottom-left',
                tooltip: tooltipText,
                x: warningLeft,
                y: dims.top,
                width: dims.width,
                height: dims.height
            });
        }

        if (showRetryMetrics && !isServiceNode) {
            const failuresTooltip = semanticTooltip(semantics.failures, 'Failed retries');
            if (failuresValue !== null) {
                const failureLabel = formatMetricValue(failuresValue);
                if (failureLabel) {
                    let failureBg = '#DC2626';
                    let failureFg = '#FFFFFF';
                    if (failuresValue <= 0) {
                        failureBg = '#E5E7EB';
                        failureFg = '#1F2937';
                    }

                    const dims = drawChip(ctx, bottomLeft, bottomRowTop, failureLabel, failureBg, failureFg, paddingX, chipH, 'top');
                    registerChipHitbox(state, {
                        nodeId: nodeMeta.id ?? null,
                        metric: 'failures',
                        placement: 'bottom-left',
                        tooltip: failuresTooltip,
                        x: bottomLeft,
                        y: dims.top,
                        width: dims.width,
                        height: dims.height
                    });
                    bottomLeft += dims.width + gap;
                }
            }

            const retryTooltip = semanticTooltip(semantics.retry, 'Retry echo');
            if (retryValue !== null) {
                const retryLabel = formatMetricValue(retryValue);
                if (retryLabel) {
                    let retryBg = '#7C3AED';
                    let retryFg = '#FFFFFF';
                    if (retryValue <= 0) {
                        retryBg = '#EDE9FE';
                        retryFg = '#4C1D95';
                    }

                    const dims = drawChip(ctx, bottomLeft, bottomRowTop, retryLabel, retryBg, retryFg, paddingX, chipH, 'top');
                    registerChipHitbox(state, {
                        nodeId: nodeMeta.id ?? null,
                        metric: 'retryEcho',
                        placement: 'bottom-left',
                        tooltip: retryTooltip,
                        x: bottomLeft,
                        y: dims.top,
                        width: dims.width,
                        height: dims.height
                    });
                    bottomLeft += dims.width + gap;
                }
            }
        }

        if (hasRetryLoop) {
            const chipsStart = x + (nodeWidth / 2) + gap;
            let stackX = chipsStart;
            const stackSpacing = 6;
            const chipY = y - (chipH / 2);
            let drewChip = false;

            const drawStackChip = (value, tooltip, metric, bg, fg, options) =>
            {
                const allowZero = options && options.showZero === true;
                if (value === null || (!allowZero && value <= 0)) {
                    return;
                }

                const label = formatMetricValue(value);
                if (!label) {
                    return;
                }

                const dims = drawChip(ctx, stackX, chipY, label, bg, fg, paddingX, chipH, 'top');
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric,
                    placement: 'right',
                    tooltip,
                    x: stackX,
                    y: dims.top,
                    width: dims.width,
                    height: dims.height
                });
                stackX += dims.width + stackSpacing;
                drewChip = true;
            };

            const retryAttempts = (attemptsValue !== null && arrivalsValue !== null)
                ? Math.max(attemptsValue - arrivalsValue, 0)
                : null;

            drawStackChip(retryAttempts, 'Retries', 'retries', '#7C3AED', '#FFFFFF');

            if (failuresValue !== null) {
                drawStackChip(failuresValue, semanticTooltip(semantics.failures, 'Failed retries'), 'failures', '#DC2626', '#FFFFFF');
            }

            drawStackChip(retryValue, semanticTooltip(semantics.retry, 'Retry echo'), 'retryEcho', '#EDE9FE', '#4C1D95');

            if (overlays.showRetryBudget !== false) {
                if (exhaustedValue !== null) {
                    drawStackChip(exhaustedValue, semanticTooltip(semantics.exhausted, 'Exhausted'), 'exhaustedFailures', '#7F1D1D', '#FEE2E2');
                }

                if (budgetRemainingValue !== null) {
                    drawStackChip(budgetRemainingValue, semanticTooltip(semantics.retryBudget, 'Budget remaining'), 'retryBudgetRemaining', '#0F766E', '#D1FAE5', { showZero: true });
                }
            }

            const finalStackX = drewChip ? stackX : chipsStart;
            const badgeGap = drewChip ? stackSpacing : Math.max(8, Math.min(16, nodeWidth * 0.2));
            const badgeLeft = finalStackX + badgeGap;
            drawRetryBadge(ctx, nodeMeta, retryTax, { left: badgeLeft });
        }

        if (overlays.showCapacityDependencies !== false) {
            const capacityValue = sampleValueFor('capacity', semantics.capacity, ['cap']);
            if (capacityValue !== null) {
                const capacityLabel = formatMetricValue(capacityValue);
                if (capacityLabel) {
                    const dims = drawChip(ctx, bottomLeft, bottomRowTop, capacityLabel, '#FFB300', '#1F2937', paddingX, chipH, 'top');
                    registerChipHitbox(state, {
                        nodeId: nodeMeta.id ?? null,
                        metric: 'capacity',
                        placement: 'bottom-left',
                        tooltip: semanticTooltip(semantics.capacity, 'Capacity'),
                        x: bottomLeft,
                        y: dims.top,
                        width: dims.width,
                        height: dims.height
                    });
                    bottomLeft += dims.width + gap;
                }
            }
        }

        if (overlays.showTerminalEdges !== false && semantics.exhausted) {
            const terminalLabel = semantics.exhausted.aliasLabel
                || semantics.exhausted.label
                || semantics.exhausted.canonicalLabel
                || 'DLQ';
            drawTerminalEdgeBadge(ctx, nodeMeta, terminalLabel);
        }

        let pendingQueueChip = null;
        let queueTooltip = null;

        if (overlays.showQueueDependencies !== false) {
            const nodeKind = String(nodeMeta.kind ?? nodeMeta.Kind ?? '').trim().toLowerCase();
            const nodeLogicalType = String(nodeMeta.logicalType ?? nodeMeta.LogicalType ?? '').trim().toLowerCase();
            const isQueueNode = isQueueLikeKind(nodeKind, nodeLogicalType);
            const isServiceWithBufferNode = nodeLogicalType === 'servicewithbuffer';
            const queueValue = sampleValueFor('queue', semantics.queue);
            queueTooltip = semanticTooltip(semantics.queue, 'Queue depth');

            if (queueValue !== null && !isQueueNode && !isServiceWithBufferNode) {
                const queueLabel = formatMetricValue(queueValue);
                if (queueLabel) {
                    pendingQueueChip = {
                        label: queueLabel,
                        tooltip: queueTooltip
                    };
                }
            }
        }

        if (!isSinkNode && overlays.showErrorsDependencies !== false) {
            const errorCount = sampleValueFor('errors', semantics.errors);
            const errorRateValue = sampleValueFor('errorRate', null, ['error_rate']);
            const errorsTooltip = semanticTooltip(semantics.errors, 'Errors') ?? 'Errors';

            const drawErrorChip = (label, bg, fg, metric, tooltipText) => {
                if (!label) {
                    return;
                }

                const dims = drawChip(ctx, bottomLeft, bottomRowTop, label, bg, fg, paddingX, chipH, 'top');
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric,
                    placement: 'bottom-left',
                    tooltip: tooltipText,
                    x: bottomLeft,
                    y: dims.top,
                    width: dims.width,
                    height: dims.height
                });
                bottomLeft += dims.width + gap;
            };

            if (errorCount !== null) {
                const zeroCount = errorCount <= 0;
                const countLabel = zeroCount ? '-' : formatMetricValue(errorCount);
                const countBg = zeroCount ? '#E5E7EB' : '#C62828';
                const countFg = zeroCount ? '#1F2937' : '#FFFFFF';
                drawErrorChip(countLabel, countBg, countFg, 'errors', errorsTooltip);
            }

            if (errorRateValue !== null) {
                let rateBg = '#C62828';
                let rateFg = '#FFFFFF';
                const zeroRate = errorRateValue <= 0;
                if (zeroRate) {
                    rateBg = '#E5E7EB';
                    rateFg = '#1F2937';
                } else if (errorRateValue >= thresholds.errorCritical) {
                    rateBg = '#B71C1C';
                } else if (errorRateValue >= thresholds.errorWarning) {
                    rateBg = '#FB8C00';
                    rateFg = '#1F2937';
                }

                const rateLabel = zeroRate ? '-' : formatPercent(errorRateValue);
                drawErrorChip(rateLabel, rateBg, rateFg, 'errorRate', `${errorsTooltip} (%)`);
            }
        }

        if (pendingQueueChip) {
            const dims = drawChip(ctx, bottomRight, bottomRowTop, pendingQueueChip.label, '#8E24AA', '#FFFFFF', paddingX, chipH, 'top');
            registerChipHitbox(state, {
                nodeId: nodeMeta.id ?? null,
                metric: 'queue',
                placement: 'bottom-right',
                tooltip: queueTooltip ?? 'Queue depth',
                x: bottomRight,
                y: dims.top,
                width: dims.width,
                height: dims.height
            });
            bottomRight += dims.width + gap;
        }

        ctx.restore();
    }

    function drawQueueNode(ctx, nodeMeta, overlays) {
        const x = Number(nodeMeta.x ?? nodeMeta.X ?? 0);
        const y = Number(nodeMeta.y ?? nodeMeta.Y ?? 0);
        const width = Number(nodeMeta.width ?? nodeMeta.Width ?? 54);
        const height = Number(nodeMeta.height ?? nodeMeta.Height ?? 24);
        const trapezoidHeight = Math.max(height * 0.85, 14);
        const halfHeight = trapezoidHeight / 2;
        const topWidth = width * 0.65;
        const bottomWidth = width;
        const topHalf = topWidth / 2;
        const bottomHalf = bottomWidth / 2;
        const warningStyle = nodeMeta?.queueWarningText
            ? {
                fill: '#FFE04D',
                stroke: '#F59E0B',
                text: '#78350F'
            }
            : null;

        ctx.beginPath();
        ctx.moveTo(x - topHalf, y - halfHeight);
        ctx.lineTo(x + topHalf, y - halfHeight);
        ctx.lineTo(x + bottomHalf, y + halfHeight);
        ctx.lineTo(x - bottomHalf, y + halfHeight);
        ctx.closePath();
        ctx.fillStyle = warningStyle?.fill ?? QUEUE_PILL_FILL;
        ctx.strokeStyle = warningStyle?.stroke ?? QUEUE_PILL_STROKE;
        ctx.lineWidth = 1.2;
        ctx.fill();
        ctx.stroke();

        if (nodeMeta) {
            nodeMeta.fill = warningStyle?.fill ?? QUEUE_PILL_FILL;
        }

        const queueValue = resolveQueueValue(nodeMeta, overlays);
        const displayValue = queueValue !== null ? formatMetricValue(queueValue) : '—';

        ctx.fillStyle = warningStyle?.text ?? getQueueLabelColor();
        ctx.font = '600 12px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(displayValue, x, y);
    }

    function drawServiceWithBufferBadge(ctx, nodeMeta) {
        if (!nodeMeta) {
            return;
        }

        const x = Number(nodeMeta.x ?? nodeMeta.X ?? 0);
        const y = Number(nodeMeta.y ?? nodeMeta.Y ?? 0);
        const width = Number(nodeMeta.width ?? nodeMeta.Width ?? 54);
        const height = Number(nodeMeta.height ?? nodeMeta.Height ?? 24);
        const barWidth = 3;
        const gap = 1;
        const barCount = 4;
        const barHeight = Math.min(10, Math.max(4, height * 0.18));
        const startRight = x + (width / 2) - 4;
        let cursor = startRight - barWidth;
        const fillColor = isDarkTheme() ? NODE_LABEL_COLOR_DARK : NODE_LABEL_COLOR_LIGHT;
        const topEdge = y - (height / 2) + 3;
        ctx.save();
        ctx.globalAlpha = 0.9;
        ctx.fillStyle = fillColor;

        for (let i = 0; i < barCount; i++) {
            ctx.fillRect(cursor, topEdge, barWidth, barHeight);
            cursor -= (barWidth + gap);
        }

        ctx.restore();
    }

    function drawDlqNode(ctx, nodeMeta, overlays) {
        const x = Number(nodeMeta.x ?? nodeMeta.X ?? 0);
        const y = Number(nodeMeta.y ?? nodeMeta.Y ?? 0);
        const width = Number(nodeMeta.width ?? nodeMeta.Width ?? 54);
        const height = Number(nodeMeta.height ?? nodeMeta.Height ?? 24);
        const trapezoidHeight = Math.max(height * 0.85, 16);
        const halfHeight = trapezoidHeight / 2;
        const topWidth = width * 0.65;
        const bottomWidth = width;
        const topHalf = topWidth / 2;
        const bottomHalf = bottomWidth / 2;
        const palette = getDlqPalette();
        const fillColor = palette.fill;
        const textColor = palette.text;

        ctx.beginPath();
        ctx.moveTo(x - topHalf, y - halfHeight);
        ctx.lineTo(x + topHalf, y - halfHeight);
        ctx.lineTo(x + bottomHalf, y + halfHeight);
        ctx.lineTo(x - bottomHalf, y + halfHeight);
        ctx.closePath();
        ctx.fillStyle = fillColor;
        ctx.strokeStyle = palette.stroke;
        ctx.lineWidth = 1.2;
        ctx.fill();
        ctx.stroke();

        if (nodeMeta) {
            nodeMeta.fill = fillColor;
        }

        const queueValue = resolveQueueValue(nodeMeta, overlays);
        const displayValue = queueValue !== null ? formatMetricValue(queueValue) : '—';
        ctx.fillStyle = textColor;
        ctx.font = '600 12px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(displayValue, x, y);
    }

    function drawLeafNode(ctx, nodeMeta) {
        const x = Number(nodeMeta.x ?? nodeMeta.X ?? 0);
        const y = Number(nodeMeta.y ?? nodeMeta.Y ?? 0);
        const width = Number(nodeMeta.width ?? nodeMeta.Width ?? 54);
        const height = Number(nodeMeta.height ?? nodeMeta.Height ?? 24);
        const pillHeight = Math.max(height, 20);
        const pillWidth = width;
        const radius = Math.min(pillHeight / 2, pillWidth / 2);
        const fill = getLeafFill();
        const stroke = getLeafStroke();

        ctx.save();
        ctx.beginPath();
        traceRoundedRect(ctx, x, y, pillWidth, pillHeight, radius);
        ctx.fillStyle = fill;
        ctx.strokeStyle = stroke;
        ctx.lineWidth = 1.2;
        ctx.fill();
        ctx.stroke();
        ctx.restore();

        if (nodeMeta) {
            nodeMeta.fill = fill;
        }

        return { width: pillWidth, height: pillHeight, fill };
    }

    function resolveQueueValue(nodeMeta, overlays) {
        const metrics = nodeMeta?.metrics ?? nodeMeta?.Metrics ?? null;
        if (metrics) {
            const queueDepth = metrics.queueDepth ?? metrics.QueueDepth;
            if (isRenderableNumber(queueDepth)) {
                return Number(queueDepth);
            }

            const raw = metrics.raw ?? metrics.Raw ?? null;
            if (raw) {
                const rawValue = raw.queue ?? raw.Queue ?? raw.queueDepth ?? raw.QueueDepth;
                if (isRenderableNumber(rawValue)) {
                    return Number(rawValue);
                }
            }
        }

        const spark = nodeMeta?.sparkline ?? nodeMeta?.Sparkline ?? null;
        if (spark) {
            const selectedBin = Number(overlays?.selectedBin ?? overlays?.SelectedBin ?? -1);
            const queueSeries = spark.queueDepth ?? spark.QueueDepth ?? null;
            const startIndex = Number.isFinite(spark.startIndex ?? spark.StartIndex)
                ? Number(spark.startIndex ?? spark.StartIndex)
                : 0;

            const sparkValue = sampleSparklineSeriesValue(queueSeries, startIndex, selectedBin);
            if (sparkValue !== null) {
                return sparkValue;
            }

            const additionalSeries = spark.series ?? spark.Series;
            if (additionalSeries) {
                const queueSlice = additionalSeries.queue ?? additionalSeries.Queue ?? additionalSeries.queueDepth ?? additionalSeries.QueueDepth;
                const sliceValue = sampleSparklineSliceValue(queueSlice, selectedBin);
                if (sliceValue !== null) {
                    return sliceValue;
                }
            }
        }

        return null;
    }

    function sampleSparklineSeriesValue(values, startIndex, selectedBin) {
        if (!Array.isArray(values) || values.length === 0) {
            return null;
        }

        const safeStart = Number.isFinite(startIndex) ? Number(startIndex) : 0;
        const defaultIndex = safeStart + values.length - 1;
        let targetIndex = Number.isFinite(selectedBin) && selectedBin >= safeStart
            ? selectedBin
            : defaultIndex;

        targetIndex = Math.min(values.length - 1, Math.max(0, targetIndex - safeStart));
        const candidate = values[targetIndex];
        return isRenderableNumber(candidate) ? Number(candidate) : null;
    }

    function sampleSparklineSliceValue(slice, selectedBin) {
        if (!slice) {
            return null;
        }

        const values = slice.values ?? slice.Values;
        const startIndex = Number.isFinite(slice.startIndex ?? slice.StartIndex)
            ? Number(slice.startIndex ?? slice.StartIndex)
            : 0;

        return sampleSparklineSeriesValue(values, startIndex, selectedBin);
    }

    function resolveRetryTaxValue(nodeMeta) {
        const metrics = nodeMeta.metrics ?? nodeMeta.Metrics ?? null;
        if (metrics) {
            const retryTax = metrics.retryTax ?? metrics.RetryTax;
            if (Number.isFinite(retryTax)) {
                return Number(retryTax);
            }
        }

        const raw = metrics?.raw ?? metrics?.Raw ?? null;
        if (raw) {
            const rawValue = raw.retryTax ?? raw.retrytax ?? raw.retry_tax;
            if (Number.isFinite(rawValue)) {
                return Number(rawValue);
            }
        }

        return null;
    }

function drawRetryBadge(ctx, nodeMeta, retryTax, options) {
    if (!Number.isFinite(retryTax) || retryTax <= 0) {
        return null;
    }

    const x = Number(nodeMeta.x ?? nodeMeta.X ?? 0);
    const y = Number(nodeMeta.y ?? nodeMeta.Y ?? 0);
    const width = Number(nodeMeta.width ?? nodeMeta.Width ?? 54);
    const height = Number(nodeMeta.height ?? nodeMeta.Height ?? 24);
    const badgeSize = Math.min(22, Math.max(16, height * 0.55));
    const explicitLeft = options && Number.isFinite(options.left) ? Number(options.left) : null;
    const offset = Math.max(8, Math.min(16, width * 0.2));
    const centerX = explicitLeft !== null
        ? explicitLeft + (badgeSize / 2)
        : x + (width / 2) + offset + (badgeSize / 2);

        ctx.save();
        ctx.beginPath();
        traceRoundedRect(ctx, centerX, y, badgeSize, badgeSize, Math.min(6, badgeSize / 2));
        ctx.fillStyle = RETRY_BADGE_FILL;
        ctx.strokeStyle = RETRY_BADGE_STROKE;
        ctx.lineWidth = 1.2;
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = RETRY_BADGE_TEXT;
        ctx.font = '600 11px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText('R', centerX, y + 0.5);
        ctx.restore();

        const left = centerX - (badgeSize / 2);
        const right = centerX + (badgeSize / 2);

        return {
            left,
            right,
            width: badgeSize,
            centerY: y,
            size: badgeSize
        };
    }

    function drawTerminalEdgeBadge(ctx, nodeMeta, label) {
        const text = (label ?? 'DLQ').toUpperCase();
        const font = '600 9px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        ctx.save();
        ctx.font = font;
        const textMetrics = ctx.measureText(text);
        const paddingX = 6;
        const paddingY = 3;
        const chipWidth = Math.max(textMetrics.width + paddingX * 2, 26);
        const chipHeight = 14;

        const nodeX = Number(nodeMeta.x ?? nodeMeta.X ?? 0);
        const nodeY = Number(nodeMeta.y ?? nodeMeta.Y ?? 0);
        const nodeWidth = Number(nodeMeta.width ?? nodeMeta.Width ?? 54);
        const nodeHeight = Number(nodeMeta.height ?? nodeMeta.Height ?? 24);
        const chipCenterX = nodeX + (nodeWidth / 2) + (chipWidth / 2) + 10;
        const chipCenterY = nodeY - (nodeHeight / 2) - (chipHeight / 2) - 6;
        const radius = chipHeight / 2;

        const bg = isDarkTheme() ? '#374151' : '#E5E7EB';
        const fg = isDarkTheme() ? '#F8FAFC' : '#111827';
        const border = isDarkTheme() ? '#1F2937' : '#CBD5F5';

        ctx.beginPath();
        traceRoundedRect(ctx, chipCenterX, chipCenterY, chipWidth, chipHeight, radius);
        ctx.fillStyle = bg;
        ctx.strokeStyle = border;
        ctx.lineWidth = 1;
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = fg;
        ctx.font = font;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(text, chipCenterX, chipCenterY + 0.5);
        ctx.restore();
    }

    function computeSparklineLayout(nodeMeta, overlaySettings, spark) {
        if (!spark) {
            return null;
        }

        const mode = overlaySettings.sparklineMode === 'bar' ? 'bar' : 'line';
        const chipH = mode === 'bar' ? 14 : 12;
        const gap = 6;
        const nodeHeight = nodeMeta.height ?? 24;
        const nodeWidth = nodeMeta.width ?? 54;
        const top = (nodeMeta.y ?? 0) - (nodeHeight / 2) - chipH - gap;
        const seriesLength = (spark.values ?? spark.Values ?? []).length;
        if (!seriesLength) {
            return null;
        }

        const desiredWidth = computeAdaptiveWidth(seriesLength, nodeWidth, {
            min: 32,
            max: 140,
            scale: mode === 'bar' ? 11 : 10
        });
        const center = nodeMeta.x ?? 0;

        return {
            top,
            height: chipH,
            left: center - desiredWidth / 2,
            minWidth: desiredWidth,
            maxWidth: desiredWidth,
            neutralize: true
        };
    }

    function drawInputSparkline(ctx, nodeMeta, overlaySettings, layoutOverride) {
        const spark = nodeMeta.sparkline ?? null;
        if (!spark) {
            return;
        }

        const layout = layoutOverride ?? computeSparklineLayout(nodeMeta, overlaySettings, spark);
        if (!layout) {
            return;
        }

        drawSparkline(ctx, nodeMeta, spark, overlaySettings, '#94A3B8', layout);
    }

    function drawPmfDistribution(ctx, nodeMeta, distribution, overlaySettings, sparklineLayout) {
        if (!distribution) {
            return;
        }

        const values = distribution.values ?? distribution.Values;
        const probabilities = distribution.probabilities ?? distribution.Probabilities;
        if (!Array.isArray(values) || !Array.isArray(probabilities)) {
            return;
        }

        const count = Math.min(values.length, probabilities.length);
        if (!count) {
            return;
        }

        const nodeHeight = nodeMeta.height ?? 24;
        const nodeWidth = nodeMeta.width ?? 54;
        const chartHeight = 14;
        const gap = 6;
        const stackGap = 4;
        let top = (nodeMeta.y ?? 0) - (nodeHeight / 2) - chartHeight - gap;
        if (sparklineLayout) {
            top = Math.min(sparklineLayout.top - stackGap - chartHeight, top);
        }
        const chartWidth = computeAdaptiveWidth(count, nodeWidth, {
            min: 36,
            max: 150,
            scale: 8
        });
        const left = (nodeMeta.x ?? 0) - chartWidth / 2;

        ctx.save();
        ctx.translate(left, top);

        ctx.fillStyle = 'rgba(148, 163, 184, 0.12)';
        ctx.fillRect(0, 0, chartWidth, chartHeight);
        ctx.strokeStyle = 'rgba(148, 163, 184, 0.35)';
        ctx.lineWidth = 0.6;
        ctx.strokeRect(0, 0, chartWidth, chartHeight);

        const maxProbability = probabilities.reduce((max, value) => {
            const numeric = Number(value);
            if (!Number.isFinite(numeric) || numeric < 0) {
                return max;
            }
            return Math.max(max, numeric);
        }, 0);

        const safeMax = maxProbability > 0 ? maxProbability : 1;
        const barWidth = chartWidth / count;

        for (let i = 0; i < count; i++) {
            const probability = Number(probabilities[i]);
            if (!Number.isFinite(probability) || probability < 0) {
                continue;
            }

            const normalized = probability / safeMax;
            const barHeight = Math.max(normalized * (chartHeight - 4), 1);
            const barLeft = i * barWidth + barWidth * 0.15;
            const barTop = chartHeight - barHeight - 2;

            ctx.fillStyle = '#2563EB';
            ctx.fillRect(barLeft, barTop, barWidth * 0.7, barHeight);
        }

        ctx.restore();
    }

    function normalizeSemantics(raw) {
        if (!raw) {
            return {
                arrivals: null,
                served: null,
                errors: null,
                attempts: null,
                failures: null,
                exhausted: null,
                retry: null,
                queue: null,
                capacity: null,
                series: null,
                distribution: null,
                inline: null,
                retryEcho: null,
                retryBudget: null,
                maxAttempts: null,
                backoffStrategy: null,
                exhaustedPolicy: null
            };
        }

        const aliasMap = normalizeAliasMap(raw.aliases ?? raw.Aliases);
        return {
            arrivals: normalizeSemanticValue(raw.arrivals ?? raw.Arrivals, 'Arrivals', aliasFor(aliasMap, 'arrivals'), 'arrivals'),
            served: normalizeSemanticValue(raw.served ?? raw.Served, 'Served', aliasFor(aliasMap, 'served'), 'served'),
            errors: normalizeSemanticValue(raw.errors ?? raw.Errors, 'Errors', aliasFor(aliasMap, 'errors'), 'errors'),
            attempts: normalizeSemanticValue(raw.attempts ?? raw.Attempts, 'Attempts', aliasFor(aliasMap, 'attempts'), 'attempts'),
            failures: normalizeSemanticValue(raw.failures ?? raw.Failures, 'Failed retries', aliasFor(aliasMap, 'failures'), 'failures'),
            exhausted: normalizeSemanticValue(raw.exhaustedFailures ?? raw.ExhaustedFailures, 'Exhausted', aliasFor(aliasMap, 'exhaustedFailures') ?? aliasFor(aliasMap, 'exhausted'), 'exhaustedFailures'),
            retry: normalizeSemanticValue(raw.retry ?? raw.Retry ?? raw.retryEcho ?? raw.RetryEcho, 'Retry', aliasFor(aliasMap, 'retry') ?? aliasFor(aliasMap, 'retryecho'), 'retryEcho'),
            queue: normalizeSemanticValue(raw.queue ?? raw.Queue, 'Queue depth', aliasFor(aliasMap, 'queue'), 'queue'),
            capacity: normalizeSemanticValue(raw.capacity ?? raw.Capacity, 'Capacity', aliasFor(aliasMap, 'capacity'), 'capacity'),
            series: normalizeSemanticValue(raw.series ?? raw.Series, 'Series', null, 'series'),
            distribution: normalizeDistribution(raw.distribution ?? raw.Distribution),
            inline: normalizeInlineSeries(raw.inlineValues ?? raw.InlineValues),
            retryEcho: normalizeSemanticValue(raw.retryEcho ?? raw.RetryEcho, 'Retry echo', aliasFor(aliasMap, 'retryecho'), 'retryEcho'),
            retryBudget: normalizeSemanticValue(raw.retryBudgetRemaining ?? raw.RetryBudgetRemaining, 'Retry budget', aliasFor(aliasMap, 'retryBudgetRemaining'), 'retryBudgetRemaining'),
            aliases: aliasMap,
            maxAttempts: normalizeNumericValue(raw.maxAttempts ?? raw.MaxAttempts),
            backoffStrategy: typeof raw.backoffStrategy === 'string' ? raw.backoffStrategy.trim() : (typeof raw.BackoffStrategy === 'string' ? raw.BackoffStrategy.trim() : null),
            exhaustedPolicy: typeof raw.exhaustedPolicy === 'string' ? raw.exhaustedPolicy.trim() : (typeof raw.ExhaustedPolicy === 'string' ? raw.ExhaustedPolicy.trim() : null)
        };
    }

    function normalizeSemanticValue(raw, canonicalLabel, aliasLabel, canonicalKey) {
        if (raw === null || raw === undefined) {
            return null;
        }

        const text = String(raw).trim();
        if (text.length === 0) {
            return null;
        }

        const seriesMatch = text.match(/^series:(.+)$/i);
        const fileMatch = text.match(/^file:(.+)$/i);
        const rawIdentifier = (seriesMatch ?? fileMatch)?.[1]?.trim() ?? text;
        const canonical = canonicalKey ?? canonicalizeSeriesKey(rawIdentifier);
        const key = extractSeriesKey(rawIdentifier);
        const override = typeof aliasLabel === 'string' ? aliasLabel.trim() : '';
        const resolvedCanonicalLabel = canonicalLabel ?? toPascal(canonicalKey ?? key ?? 'Metric');
        const label = override.length > 0 ? override : resolvedCanonicalLabel;

        return {
            key,
            label,
            reference: rawIdentifier,
            canonical,
            canonicalLabel: resolvedCanonicalLabel,
            aliasLabel: override.length > 0 ? override : null
        };
    }

    function normalizeAliasMap(raw) {
        if (!raw || typeof raw !== 'object') {
            return null;
        }

        const normalized = {};
        for (const [key, value] of Object.entries(raw)) {
            if (typeof value !== 'string') {
                continue;
            }

            const normalizedKey = String(key ?? '').trim().toLowerCase();
            if (!normalizedKey || value.trim().length === 0) {
                continue;
            }

            normalized[normalizedKey] = value.trim();
        }

        return Object.keys(normalized).length === 0 ? null : normalized;
    }

    function aliasFor(map, key) {
        if (!map || !key) {
            return null;
        }

        return map[String(key).trim().toLowerCase()] ?? null;
    }

    function canonicalizeSeriesKey(identifier) {
        if (identifier === null || identifier === undefined) {
            return null;
        }

        let text = String(identifier).trim();
        if (text.length === 0) {
            return null;
        }

        text = text.replace(/^(\.{1,2}\/)+/g, '');
        text = text.replace(/^file:/i, '');
        text = text.replace(/^series:/i, '');
        text = text.replace(/^[\\/]+/g, '');
        text = text.replace(/\\/g, '/');

        const parts = text.split('/');
        text = parts.length > 0 ? parts[parts.length - 1] : text;
        text = text.replace(/\.[^.]+$/, '');
        text = text.replace(/@.+$/, '');

        return text.trim();
    }

    function normalizeKeyForComparison(value) {
        if (value === null || value === undefined) {
            return '';
        }

        const canonical = canonicalizeSeriesKey(value);
        if (!canonical) {
            return '';
        }

        return canonical.replace(/[^a-zA-Z0-9]/g, '').toLowerCase();
    }

    function collectSeriesKeyCandidates(value) {
        const candidates = new Set();
        const push = (candidate) =>
        {
            if (typeof candidate !== 'string') {
                return;
            }

            const trimmed = candidate.trim();
            if (trimmed.length > 0) {
                candidates.add(trimmed);
            }
        };

        const base = typeof value === 'string' ? value : String(value ?? '');
        push(base);

        const canonical = canonicalizeSeriesKey(base);
        push(canonical);

        if (canonical) {
            push(canonical.toLowerCase());
            push(canonical.toUpperCase());
            push(canonical.replace(/[\s]+/g, '_'));
            push(canonical.replace(/[\s]+/g, '-').toLowerCase());

            const segments = canonical.replace(/[_-]+/g, ' ').split(' ').filter(part => part.length > 0);
            if (segments.length > 0) {
                const pascal = segments.map(capitalize).join('');
                if (pascal.length > 0) {
                    push(pascal);
                    push(pascal.charAt(0).toLowerCase() + pascal.slice(1));
                }
            }
        }

        return Array.from(candidates.values());
    }

    function capitalize(text) {
        if (typeof text !== 'string' || text.length === 0) {
            return text;
        }

        return text.charAt(0).toUpperCase() + text.slice(1);
    }

    function normalizeDistribution(raw) {
        if (!raw) {
            return null;
        }

        const values = raw.values ?? raw.Values;
        const probabilities = raw.probabilities ?? raw.Probabilities;
        if (!Array.isArray(values) || !Array.isArray(probabilities)) {
            return null;
        }

        const count = Math.min(values.length, probabilities.length);
        if (count <= 0) {
            return null;
        }

        const normalizedValues = new Array(count);
        const normalizedProbabilities = new Array(count);
        for (let i = 0; i < count; i++) {
            normalizedValues[i] = Number(values[i]);
            normalizedProbabilities[i] = Number(probabilities[i]);
        }

        return {
            values: normalizedValues,
            probabilities: normalizedProbabilities
        };
    }

    function normalizeRawMetricMap(raw) {
        if (!raw || typeof raw !== 'object') {
            return null;
        }

        const normalized = {};
        for (const [key, value] of Object.entries(raw)) {
            if (typeof key !== 'string' || key.trim().length === 0) {
                continue;
            }

            const normalizedKey = key.trim().toLowerCase();
            let normalizedValue = null;
            if (value !== null && value !== undefined) {
                const numeric = Number(value);
                normalizedValue = Number.isFinite(numeric) ? numeric : null;
            }

            normalized[normalizedKey] = normalizedValue;
        }

        return normalized;
    }

    function normalizeInlineSeries(raw) {
        if (!raw) {
            return null;
        }

        const values = Array.isArray(raw) ? raw : null;
        if (!values || values.length === 0) {
            return null;
        }

        const numericValues = new Array(values.length);
        for (let i = 0; i < values.length; i++) {
            const numeric = Number(values[i]);
            numericValues[i] = Number.isFinite(numeric) ? numeric : null;
        }

        return numericValues;
    }

    function normalizeNumericValue(raw) {
        if (raw === null || raw === undefined) {
            return null;
        }

        const numeric = Number(raw);
        return Number.isFinite(numeric) ? numeric : null;
    }

    function extractSeriesKey(identifier) {
        if (typeof identifier !== 'string' || identifier.length === 0) {
            return null;
        }

        const trimmed = identifier.trim();
        if (trimmed.length === 0) {
            return null;
        }

        const withoutDirs = trimmed.replace(/^(\.{1,2}\/)+/g, '').split(/[\\/]/).pop() ?? trimmed;
        const withoutSuffix = withoutDirs.replace(/\.[^.]+$/, '');
        const withoutVariant = withoutSuffix.replace(/@.+$/, '');
        return withoutVariant;
    }

    function simplifySemanticLabel(identifier) {
        if (typeof identifier !== 'string') {
            return null;
        }

        let label = identifier.trim();
        if (label.length === 0) {
            return null;
        }

        label = label.replace(/^file:/i, '')
            .replace(/^series:/i, '');

        label = label.replace(/^(\.{1,2}\/)+/g, '')
            .split(/[\\/]/).pop() ?? label;

        label = label.replace(/\.[^.]+$/, '');
        label = label.replace(/@.+$/, '');
        label = label.replace(/[_-]+/g, ' ');
        label = label.replace(/\s+/g, ' ').trim();

        return label.length > 0 ? label : null;
    }

    function sampleSeriesValueAt(sparkline, key, selectedBin) {
        const slice = getSeriesSlice(sparkline, key);
        const values = slice?.values ?? slice?.Values ?? null;
        const startIndex = Number(slice?.startIndex ?? slice?.StartIndex ?? sparkline.startIndex ?? sparkline.StartIndex ?? 0);

        if (!Array.isArray(values) || values.length === 0) {
            return null;
        }

        let index = selectedBin - startIndex;
        if (!Number.isFinite(index)) {
            index = values.length - 1;
        }

        if (index < 0) {
            index = 0;
        } else if (index >= values.length) {
            index = values.length - 1;
        }

        let sample = values[index];
        if (sample === null || sample === undefined) {
            for (let i = index - 1; i >= 0; i--) {
                if (values[i] !== null && values[i] !== undefined) {
                    sample = values[i];
                    break;
                }
            }
        }

        if (sample === null || sample === undefined) {
            for (let i = index + 1; i < values.length; i++) {
                if (values[i] !== null && values[i] !== undefined) {
                    sample = values[i];
                    break;
                }
            }
        }

        if (sample === null || sample === undefined) {
            return null;
        }

        const numeric = Number(sample);
        return Number.isFinite(numeric) ? numeric : null;
    }

    function getSeriesSlice(sparkline, key) {
        if (!sparkline || key === null || key === undefined) {
            return null;
        }

        const map = sparkline.series ?? sparkline.Series;
        if (!map) {
            return null;
        }

        const candidates = collectSeriesKeyCandidates(key);
        for (const candidate of candidates) {
            if (candidate && map[candidate]) {
                return map[candidate];
            }
        }

        for (const candidate of candidates) {
            if (!candidate) {
                continue;
            }

            if (typeof candidate === 'string') {
                const lowerKey = candidate.toLowerCase();
                if (lowerKey && map[lowerKey]) {
                    return map[lowerKey];
                }

                const upperKey = candidate.toUpperCase();
                if (upperKey && map[upperKey]) {
                    return map[upperKey];
                }
            }

            const pascal = map[toPascal(candidate)];
            if (pascal) {
                return pascal;
            }
        }

        const target = normalizeKeyForComparison(key);
        if (!target) {
            return null;
        }

        const entries = Object.keys(map);
        for (const name of entries) {
            if (normalizeKeyForComparison(name) === target) {
                return map[name];
            }
        }

        return null;
    }

    function sampleSparklineAt(sparkline, property, selectedBin) {
        if (!sparkline || selectedBin < 0 || !property) {
            return null;
        }

        const slice = getSeriesSlice(sparkline, property);
        let values = slice?.values ?? slice?.Values ?? sparkline[property] ?? sparkline[toPascal(property)];
        let startIndex = Number(slice?.startIndex ?? slice?.StartIndex ?? sparkline.startIndex ?? sparkline.StartIndex ?? 0);

        if (!Array.isArray(values) || values.length === 0) {
            return null;
        }

        const index = selectedBin - startIndex;
        if (index < 0 || index >= values.length) {
            return null;
        }

        const sample = values[index];
        if (sample === null || sample === undefined) {
            return null;
        }

        const numeric = Number(sample);
        return Number.isFinite(numeric) ? numeric : null;
    }

    function isRenderableNumber(value) {
        if (value === null || value === undefined) {
            return false;
        }

        return Number.isFinite(Number(value));
    }

    function formatMetricValue(value) {
        if (value === null || value === undefined) {
            return null;
        }

        if (!Number.isFinite(value)) {
            return null;
        }

        const abs = Math.abs(value);
        if (abs >= 1_000_000) {
            return `${(value / 1_000_000).toFixed(1).replace(/\.0$/, '')}M`;
        }

        if (abs >= 1_000) {
            return `${(value / 1_000).toFixed(1).replace(/\.0$/, '')}k`;
        }

        if (abs >= 10) {
            return value.toFixed(0);
        }

        if (abs >= 1) {
            return value.toFixed(1).replace(/\.0$/, '');
        }

        if (abs >= 0.1) {
            return value.toFixed(2).replace(/0$/, '').replace(/\.$/, '');
        }

        return value.toPrecision(2);
    }

    function formatPercent(value) {
        if (value === null || value === undefined) {
            return null;
        }

        if (!Number.isFinite(value)) {
            return null;
        }

        const pct = value * 100;
        const abs = Math.abs(pct);
        if (abs >= 100) {
            return `${pct.toFixed(0)}%`;
        }

        if (abs >= 10) {
            return `${pct.toFixed(1).replace(/\.0$/, '')}%`;
        }

        if (abs >= 1) {
            return `${pct.toFixed(1)}%`;
        }

        return `${pct.toFixed(2).replace(/0$/, '').replace(/\.$/, '')}%`;
    }

    function formatMultiplierLabel(multiplier, lag) {
        if (!Number.isFinite(multiplier) || multiplier <= 0) {
            return null;
        }

        let magnitude;
        if (multiplier >= 10) {
            magnitude = multiplier.toFixed(0);
        } else if (multiplier >= 3) {
            magnitude = multiplier.toFixed(1).replace(/\.0$/, '');
        } else {
            magnitude = multiplier.toFixed(2).replace(/0$/, '').replace(/\.$/, '');
        }

        let label = `×${magnitude}`;
        if (Number.isFinite(lag) && lag > 0) {
            const lagInt = Math.round(lag);
            label += ` • +${lagInt}`;
        }

        return label;
    }

    function toPascal(name) {
        if (typeof name !== 'string' || name.length === 0) {
            return name;
        }
        return name.charAt(0).toUpperCase() + name.slice(1);
    }

    function formatSemanticAlias(entry, canonicalFallback, options) {
        const settings = {
            newlineAlias: false,
            includeColon: true,
            ...(options ?? {})
        };

        const canonical = entry?.canonicalLabel ?? canonicalFallback ?? entry?.label ?? null;
        const aliasRaw = typeof entry?.aliasLabel === 'string' ? entry.aliasLabel.trim() : '';
        const alias = aliasRaw.length > 0 ? aliasRaw : null;

        if (alias && canonical && alias.localeCompare(canonical, undefined, { sensitivity: 'accent' }) !== 0) {
            if (settings.newlineAlias) {
                return settings.includeColon ? `${canonical}:\n${alias}` : `${canonical}\n${alias}`;
            }

            if (settings.includeColon) {
                return `${canonical}: ${alias}`;
            }

            return `${canonical} ${alias}`.trim();
        }

        return canonical ?? alias ?? canonicalFallback ?? null;
    }

    function semanticTooltip(entry, canonicalFallback) {
        if (entry?.aliasLabel) {
            return entry.aliasLabel;
        }
        return formatSemanticAlias(entry, canonicalFallback, { newlineAlias: true, includeColon: false });
    }

    function semanticChipLabel(entry, canonicalFallback) {
        if (entry?.aliasLabel) {
            return entry.aliasLabel;
        }
        return formatSemanticAlias(entry, canonicalFallback, { newlineAlias: true, includeColon: true });
    }

    function registerChipHitbox(state, chip) {
        if (!state || !(state.collectingHitboxes || state.collectingChipHitboxes)) {
            return;
        }

        if (!Array.isArray(state.chipHitboxes)) {
            state.chipHitboxes = [];
        }

        const width = Number(chip?.width);
        const height = Number(chip?.height);
        const left = Number(chip?.x);
        const top = Number(chip?.y);

        if (!Number.isFinite(width) || width <= 0 || !Number.isFinite(height) || height <= 0) {
            return;
        }

        if (!Number.isFinite(left) || !Number.isFinite(top)) {
            return;
        }

        const placement = typeof chip?.placement === 'string' && chip.placement.length > 0 ? chip.placement : 'top';
        const tooltip = typeof chip?.tooltip === 'string' && chip.tooltip.trim().length > 0
            ? chip.tooltip.trim()
            : toPascal(chip?.metric ?? 'metric');

        const nodeId = chip?.nodeId ?? null;
        const keyParts = [
            nodeId ?? 'node',
            chip?.metric ?? 'metric',
            placement,
            Math.round(left * 10),
            Math.round(top * 10),
            Math.round(width * 10)
        ];

        state.chipHitboxes.push({
            id: keyParts.join('|'),
            nodeId,
            metric: chip?.metric ?? null,
            placement,
            tooltip,
            x: left,
            y: top,
            width,
            height
        });
    }

    function createEdgeSpatialIndex(cellSize = EDGE_SPATIAL_INDEX_CELL_SIZE) {
        const normalized = Number.isFinite(cellSize) && cellSize > 0
            ? cellSize
            : EDGE_SPATIAL_INDEX_CELL_SIZE;
        return {
            cellSize: normalized,
            cells: new Map()
        };
    }

    function resetEdgeSpatialIndex(state) {
        if (!state) {
            return;
        }
        const nodes = state.sceneRawNodes ?? [];
        const edges = state.sceneRawEdges ?? [];
        const bounds = computeSceneBoundsFromNodes(nodes);
        state.sceneBounds = bounds;
        const nextCellSize = computeEdgeSpatialCellSize(bounds, Array.isArray(edges) ? edges.length : 0);
        state.edgeSpatialIndex = createEdgeSpatialIndex(nextCellSize);
        state.edgeSpatialStats = createEdgeSpatialStats(nextCellSize);
        state.edgeSpatialCache = null;
    }

    function registerEdgeSpatialEntry(state, hitbox) {
        if (!state || !hitbox) {
            return;
        }
        const index = state.edgeSpatialIndex ?? (state.edgeSpatialIndex = createEdgeSpatialIndex());
        const bounds = hitbox.bounds ?? computePolylineBounds(hitbox.points);
        if (!bounds) {
            return;
        }
        hitbox.bounds = bounds;
        const cellSize = index.cellSize;
        const minCol = Math.floor(bounds.minX / cellSize);
        const maxCol = Math.floor(bounds.maxX / cellSize);
        const minRow = Math.floor(bounds.minY / cellSize);
        const maxRow = Math.floor(bounds.maxY / cellSize);

        for (let col = minCol; col <= maxCol; col++) {
            for (let row = minRow; row <= maxRow; row++) {
                const key = `${col}:${row}`;
                let bucket = index.cells.get(key);
                if (!bucket) {
                    bucket = [];
                    index.cells.set(key, bucket);
                }
                bucket.push(hitbox);
            }
        }
    }

    function queryEdgeSpatialIndex(state, worldX, worldY, tolerance) {
        const index = state?.edgeSpatialIndex;
        if (!index || index.cells.size === 0) {
            return null;
        }
        const cellSize = index.cellSize;
        const minCol = Math.floor((worldX - tolerance) / cellSize);
        const maxCol = Math.floor((worldX + tolerance) / cellSize);
        const minRow = Math.floor((worldY - tolerance) / cellSize);
        const maxRow = Math.floor((worldY + tolerance) / cellSize);
        const visited = new Set();
        const candidates = [];

        for (let col = minCol; col <= maxCol; col++) {
            for (let row = minRow; row <= maxRow; row++) {
                const bucket = index.cells.get(`${col}:${row}`);
                if (!bucket) {
                    continue;
                }
                for (const hitbox of bucket) {
                    if (!hitbox || visited.has(hitbox)) {
                        continue;
                    }
                    const bounds = hitbox.bounds ?? computePolylineBounds(hitbox.points);
                    if (!bounds) {
                        continue;
                    }
                    if ((worldX + tolerance) < bounds.minX ||
                        (worldX - tolerance) > bounds.maxX ||
                        (worldY + tolerance) < bounds.minY ||
                        (worldY - tolerance) > bounds.maxY) {
                        continue;
                    }
                    visited.add(hitbox);
                    candidates.push(hitbox);
                }
            }
        }

        return candidates;
    }

    function computePolylineBounds(points) {
        if (!Array.isArray(points) || points.length === 0) {
            return null;
        }
        let minX = Infinity;
        let minY = Infinity;
        let maxX = -Infinity;
        let maxY = -Infinity;
        for (const point of points) {
            if (!point) {
                continue;
            }
            const px = Number(point.x);
            const py = Number(point.y);
            if (!Number.isFinite(px) || !Number.isFinite(py)) {
                continue;
            }
            if (px < minX) minX = px;
            if (px > maxX) maxX = px;
            if (py < minY) minY = py;
            if (py > maxY) maxY = py;
        }
        if (!Number.isFinite(minX) || !Number.isFinite(minY) || !Number.isFinite(maxX) || !Number.isFinite(maxY)) {
            return null;
        }
        return { minX, minY, maxX, maxY };
    }

    function computeSceneBoundsFromNodes(nodes) {
        if (!Array.isArray(nodes) || nodes.length === 0) {
            return null;
        }
        let minX = Infinity;
        let minY = Infinity;
        let maxX = -Infinity;
        let maxY = -Infinity;
        for (const node of nodes) {
            if (!node) {
                continue;
            }
            const width = Number(node.width ?? node.Width ?? 54);
            const height = Number(node.height ?? node.Height ?? 24);
            const cx = Number(node.x ?? node.X ?? 0);
            const cy = Number(node.y ?? node.Y ?? 0);
            if (!Number.isFinite(cx) || !Number.isFinite(cy)) {
                continue;
            }
            const halfWidth = Number.isFinite(width) ? width / 2 : 27;
            const halfHeight = Number.isFinite(height) ? height / 2 : 12;
            const left = cx - halfWidth;
            const right = cx + halfWidth;
            const top = cy - halfHeight;
            const bottom = cy + halfHeight;
            if (left < minX) minX = left;
            if (right > maxX) maxX = right;
            if (top < minY) minY = top;
            if (bottom > maxY) maxY = bottom;
        }

        if (!Number.isFinite(minX) || !Number.isFinite(minY) || !Number.isFinite(maxX) || !Number.isFinite(maxY)) {
            return null;
        }

        const width = Math.max(maxX - minX, EDGE_SPATIAL_MIN_CELL_SIZE);
        const height = Math.max(maxY - minY, EDGE_SPATIAL_MIN_CELL_SIZE);
        return {
            minX,
            minY,
            maxX,
            maxY,
            width,
            height
        };
    }

    function computeEdgeSpatialCellSize(bounds, edgeCount) {
        if (!bounds || !Number.isFinite(edgeCount) || edgeCount <= 0) {
            return EDGE_SPATIAL_INDEX_CELL_SIZE;
        }
        const area = Math.max(bounds.width * bounds.height, 1);
        const desiredCells = Math.max(1, edgeCount / EDGE_SPATIAL_TARGET_EDGES_PER_CELL);
        const estimated = Math.sqrt(area / desiredCells);
        const clamped = Math.max(EDGE_SPATIAL_MIN_CELL_SIZE, Math.min(EDGE_SPATIAL_MAX_CELL_SIZE, estimated));
        return clamped;
    }

    function updateEdgeSpatialStats(state, candidateCount, usedFallback) {
        if (!state) {
            return;
        }
        const stats = state.edgeSpatialStats ?? (state.edgeSpatialStats = createEdgeSpatialStats(state.edgeSpatialIndex?.cellSize ?? EDGE_SPATIAL_INDEX_CELL_SIZE));
        const normalizedCount = Number.isFinite(candidateCount) ? candidateCount : 0;
        stats.samples += 1;
        stats.candidateTotal += normalizedCount;
        stats.lastCandidates = normalizedCount;
        if (usedFallback) {
            stats.fallbackSamples += 1;
        }
    }

    function recordEdgeSpatialCacheHit(state) {
        if (!state) {
            return;
        }
        const stats = state.edgeSpatialStats ?? (state.edgeSpatialStats = createEdgeSpatialStats(state.edgeSpatialIndex?.cellSize ?? EDGE_SPATIAL_INDEX_CELL_SIZE));
        stats.cacheHits += 1;
        stats.samples += 1;
    }

    function recordEdgeSpatialCacheMiss(state) {
        if (!state) {
            return;
        }
        const stats = state.edgeSpatialStats ?? (state.edgeSpatialStats = createEdgeSpatialStats(state.edgeSpatialIndex?.cellSize ?? EDGE_SPATIAL_INDEX_CELL_SIZE));
        stats.cacheMisses += 1;
    }

    function setEdgeSpatialCache(state, key, hitbox, worldX, worldY) {
        if (!state) {
            return;
        }
        if (hitbox) {
            state.edgeSpatialCache = {
                key,
                hitbox,
                worldX,
                worldY
            };
        } else {
            state.edgeSpatialCache = null;
        }
    }

    function clearEdgeSpatialCache(state) {
        if (state) {
            state.edgeSpatialCache = null;
        }
    }

    function measureChipText(ctx, text, paddingX, lineHeight) {
        const normalized = typeof text === 'string' ? text : String(text ?? '');
        const rawLines = normalized.split(/\r?\n/);
        const lines = rawLines
            .map(line => line.trim())
            .filter(line => line.length > 0);
        if (lines.length === 0) {
            lines.push(normalized.trim().length > 0 ? normalized.trim() : '');
        }

        const metrics = lines.map(line => ctx.measureText(line));
        const maxWidth = metrics.reduce((max, metric) => Math.max(max, metric.width), 0);
        const width = Math.ceil(maxWidth) + paddingX * 2;
        const baseLineHeight = Math.max(12, lineHeight);
        const lineGap = Math.max(2, Math.round(baseLineHeight * 0.3));
        const totalHeight = lines.length > 0
            ? Math.ceil((lines.length * baseLineHeight) + ((lines.length - 1) * lineGap))
            : baseLineHeight;

        return {
            normalized,
            lines,
            metrics,
            width,
            totalHeight,
            baseLineHeight,
            lineGap
        };
    }

    function drawChip(ctx, x, anchorY, text, outlineColor, fg, paddingX, lineHeight, anchor = 'bottom', fillColor = null, measurement = null, fontWeight = 'normal') {
        const normalized = typeof text === 'string' ? text : String(text ?? '');
        const chipMetrics = measurement ?? measureChipText(ctx, normalized, paddingX, lineHeight);
        const lines = chipMetrics.lines;
        const width = chipMetrics.width;
        const totalHeight = chipMetrics.totalHeight;
        const baseLineHeight = chipMetrics.baseLineHeight;
        const lineGap = chipMetrics.lineGap;
        const top = anchor === 'top'
            ? Math.round(anchorY)
            : Math.round(anchorY - totalHeight);
        const bottom = top + totalHeight;

        ctx.save();
        const darkMode = isDarkTheme();
        const hasCustomFill = typeof fillColor === 'string' && fillColor.trim().length > 0;
        let chipFill = hasCustomFill ? fillColor : getDefaultChipFill();
        let textColor = getDefaultChipTextColor();
        if (!hasCustomFill) {
            if (darkMode) {
                chipFill = CHIP_BASE_FILL_DARK;
                textColor = CHIP_TEXT_DARK;
            }
        } else if (typeof fg === 'string' && fg.trim().length > 0) {
            textColor = fg;
        }
        ctx.fillStyle = chipFill;
        ctx.strokeStyle = outlineColor ?? '#94A3B8';
        ctx.lineWidth = 1;
        const r = Math.min(baseLineHeight / 1.5, totalHeight / 2);
        const bx = Math.round(x);
        ctx.beginPath();
        ctx.moveTo(bx + r, top);
        ctx.arcTo(bx + width, top, bx + width, top + totalHeight, r);
        ctx.arcTo(bx + width, top + totalHeight, bx, top + totalHeight, r);
        ctx.arcTo(bx, top + totalHeight, bx, top, r);
        ctx.arcTo(bx, top, bx + r, top, r);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
        ctx.fillStyle = textColor;
        ctx.font = `${fontWeight} 11px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        const firstLineCenter = top + (baseLineHeight / 2);
        const lineAdvance = baseLineHeight + lineGap;
        for (let i = 0; i < lines.length; i++) {
            const lineCenter = firstLineCenter + i * lineAdvance;
            ctx.fillText(lines[i], bx + paddingX, lineCenter);
        }
        ctx.restore();
        return { width, height: totalHeight, top, bottom, left: bx };
    }

    function drawScheduleBadge(ctx, x, anchorY, height) {
        const badgeHeight = Math.max(14, Math.round(height));
        const badgeWidth = Math.max(18, Math.round(badgeHeight * 1.2));
        const top = Math.round(anchorY);
        const left = Math.round(x);
        const fill = '#0EA5E9';
        const stroke = '#38BDF8';
        const icon = '#FFFFFF';

        ctx.save();
        ctx.fillStyle = fill;
        ctx.strokeStyle = stroke;
        ctx.lineWidth = 1;
        drawRoundedRectTopLeft(ctx, left, top, badgeWidth, badgeHeight, Math.min(7, badgeHeight / 2));
        ctx.fill();
        ctx.stroke();

        const pad = Math.max(3, Math.round(badgeHeight * 0.2));
        const centerX = left + (badgeWidth / 2);
        const centerY = top + (badgeHeight / 2);
        const radius = Math.max(4, Math.min(badgeWidth, badgeHeight) / 2 - pad);

        ctx.strokeStyle = icon;
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.arc(centerX, centerY, radius, 0, Math.PI * 2);
        ctx.stroke();

        ctx.strokeStyle = icon;
        ctx.lineCap = 'round';
        ctx.lineWidth = 1.2;
        ctx.beginPath();
        ctx.moveTo(centerX, centerY);
        ctx.lineTo(centerX, centerY - radius * 0.55);
        ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(centerX, centerY);
        ctx.lineTo(centerX + radius * 0.45, centerY + radius * 0.1);
        ctx.stroke();
        ctx.restore();

        return {
            width: badgeWidth,
            height: badgeHeight,
            top,
            bottom: top + badgeHeight,
            left
        };
    }

    function drawQueueChip(ctx, x, anchorY, text, paddingX, lineHeight, anchor = 'bottom', styleOverrides) {
        const normalized = typeof text === 'string' ? text : String(text ?? '');
        const chipMetrics = measureChipText(ctx, normalized, paddingX, lineHeight);
        const width = Math.max(chipMetrics.width + paddingX * 2, 28);
        const totalHeight = Math.max(chipMetrics.totalHeight, lineHeight);
        const top = anchor === 'top'
            ? Math.round(anchorY)
            : Math.round(anchorY - totalHeight);
        const bottom = top + totalHeight;
        const topWidth = Math.max(width * 0.65, width - 12);
        const offset = (width - topWidth) / 2;

        ctx.save();
        ctx.beginPath();
        ctx.moveTo(x + offset, top);
        ctx.lineTo(x + offset + topWidth, top);
        ctx.lineTo(x + width, bottom);
        ctx.lineTo(x, bottom);
        ctx.closePath();
        ctx.fillStyle = styleOverrides?.fill ?? QUEUE_PILL_FILL;
        ctx.strokeStyle = styleOverrides?.stroke ?? QUEUE_PILL_STROKE;
        ctx.lineWidth = 1;
        ctx.fill();
        ctx.stroke();
        ctx.fillStyle = styleOverrides?.text ?? getQueueLabelColor();
        ctx.font = '600 11px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(normalized, x + width / 2, top + totalHeight / 2);
        ctx.restore();
        return { width, height: totalHeight, top, bottom, left: x };
    }

    function dedupePoints(points) {
        const result = [];
        let last = null;
        for (const point of points) {
            if (!last || Math.abs(point.x - last.x) > 0.1 || Math.abs(point.y - last.y) > 0.1) {
                const clone = { x: point.x, y: point.y };
                result.push(clone);
                last = clone;
            }
        }
        return result;
    }

    function drawRoundedPolyline(ctx, points, radius) {
        if (!points || points.length < 2) {
            return;
        }

        ctx.moveTo(points[0].x, points[0].y);

        for (let i = 1; i < points.length - 1; i++) {
            const prev = points[i - 1];
            const curr = points[i];
            const next = points[i + 1];

            const v1x = curr.x - prev.x;
            const v1y = curr.y - prev.y;
            const v2x = next.x - curr.x;
            const v2y = next.y - curr.y;
            const len1 = Math.hypot(v1x, v1y) || 1;
            const len2 = Math.hypot(v2x, v2y) || 1;

            const r = Math.min(radius, len1 / 2, len2 / 2);

            const p1x = curr.x - (v1x / len1) * r;
            const p1y = curr.y - (v1y / len1) * r;
            const p2x = curr.x + (v2x / len2) * r;
            const p2y = curr.y + (v2y / len2) * r;

            ctx.lineTo(p1x, p1y);
            ctx.quadraticCurveTo(curr.x, curr.y, p2x, p2y);
        }

        const last = points[points.length - 1];
        ctx.lineTo(last.x, last.y);
    }

    function pointAlongSegments(segments, distance) {
        let remaining = distance;
        for (const segment of segments) {
            if (segment.length === 0) {
                continue;
            }

            if (remaining <= segment.length) {
                const t = remaining / segment.length;
                const x = segment.x1 + (segment.x2 - segment.x1) * t;
                const y = segment.y1 + (segment.y2 - segment.y1) * t;
                const dx = (segment.x2 - segment.x1) / segment.length;
                const dy = (segment.y2 - segment.y1) / segment.length;
                return {
                    point: { x, y },
                    tangent: { dx, dy }
                };
            }

            remaining -= segment.length;
        }

        return null;
    }

    function computeElbowPath(sx, sy, ex, ey, offset) {
        const dx = ex - sx;
        const dy = ey - sy;
        const absDx = Math.abs(dx);
        const absDy = Math.abs(dy);
        const eps = 1e-3;
        const points = [{ x: sx, y: sy }];

        if (absDy < eps) {
            points.push({ x: ex, y: ey });
            return { points };
        }

        if (absDx < eps) {
            points.push({ x: ex, y: ey });
            return { points };
        }

        const baseLead = 12;
        const laneShift = clamp(offset ?? 0, -32, 32);
        const horizontalFirst = absDx >= absDy;

        if (horizontalFirst) {
            const dirX = dx >= 0 ? 1 : -1;
            let stub = Math.min(baseLead, absDx / 2);
            if (stub > 0 && stub < 6) {
                stub = Math.min(6, absDx / 2);
            }

            const entryX = sx + dirX * stub;
            const exitX = ex - dirX * stub;

            if (Math.abs(entryX - sx) > eps) {
                points.push({ x: entryX, y: sy });
            }

            if (laneShift !== 0) {
                points.push({ x: entryX, y: sy + laneShift });
            }

            points.push({ x: exitX, y: laneShift !== 0 ? sy + laneShift : sy });
            points.push({ x: exitX, y: ey });
        } else {
            const dirY = dy >= 0 ? 1 : -1;
            let stub = Math.min(baseLead, absDy / 2);
            if (stub > 0 && stub < 6) {
                stub = Math.min(6, absDy / 2);
            }

            const entryY = sy + dirY * stub;
            const exitY = ey - dirY * stub;

            if (Math.abs(entryY - sy) > eps) {
                points.push({ x: sx, y: entryY });
            }

            if (laneShift !== 0) {
                points.push({ x: sx + laneShift, y: entryY });
            }

            points.push({ x: laneShift !== 0 ? sx + laneShift : sx, y: exitY });
            points.push({ x: ex, y: exitY });
        }

        points.push({ x: ex, y: ey });

        return { points: dedupePoints(points) };
    }

    function computeBezierPath(fromNode, toNode, laneOffset, padding, laneDescriptor, sameLaneConnection, options) {
        const anchorsFrom = createAnchors(fromNode);
        const anchorsTo = createAnchors(toNode);

        const fromCenterX = fromNode.x ?? fromNode.X ?? 0;
        const fromCenterY = fromNode.y ?? fromNode.Y ?? 0;
        const toCenterX = toNode.x ?? toNode.X ?? 0;
        const toCenterY = toNode.y ?? toNode.Y ?? 0;
        const dx = toCenterX - fromCenterX;
        const dy = toCenterY - fromCenterY;
        const forceBottomStart = Boolean(options?.forceBottomStart);

        const horizontal = {
            start: dx >= 0 ? anchorsFrom.right : anchorsFrom.left,
            end: dx >= 0 ? anchorsTo.left : anchorsTo.right,
            orientation: 'horizontal'
        };
        const vertical = {
            start: dy >= 0 ? anchorsFrom.bottom : anchorsFrom.top,
            end: dy >= 0 ? anchorsTo.top : anchorsTo.bottom,
            orientation: 'vertical'
        };

        let selected;
        if (forceBottomStart) {
            selected = {
                start: anchorsFrom.bottom,
                end: dy >= 0 ? anchorsTo.top : anchorsTo.bottom,
                orientation: 'vertical'
            };
        } else if (sameLaneConnection && laneDescriptor) {
            selected = {
                start: laneDescriptor.side >= 0 ? anchorsFrom.right : anchorsFrom.left,
                end: laneDescriptor.side >= 0 ? anchorsTo.right : anchorsTo.left,
                orientation: 'horizontal'
            };
        } else {
            const useVertical = Math.abs(dy) > Math.abs(dx);
            selected = useVertical ? vertical : horizontal;
        }

        const startDistance = computePortOffset(fromNode, selected.start.normalX, selected.start.normalY, padding);
        const endDistance = computePortOffset(toNode, selected.end.normalX, selected.end.normalY, padding);

        const start = {
            x: fromCenterX + selected.start.normalX * startDistance,
            y: fromCenterY + selected.start.normalY * startDistance,
            normalX: selected.start.normalX,
            normalY: selected.start.normalY
        };
        const end = {
            x: toCenterX + selected.end.normalX * endDistance,
            y: toCenterY + selected.end.normalY * endDistance,
            normalX: selected.end.normalX,
            normalY: selected.end.normalY
        };

        const span = selected.orientation === 'horizontal'
            ? Math.abs(end.x - start.x)
            : Math.abs(end.y - start.y);
        const baseTension = sameLaneConnection
            ? Math.max(48, Math.min(180, span * 0.5))
            : Math.max(24, Math.min(120, span * 0.45));
        const offset = clamp(laneOffset ?? 0, -24, 24);

        let cp1, cp2;
        if (selected.orientation === 'horizontal') {
            cp1 = {
                x: start.x + start.normalX * baseTension,
                y: start.y
            };
            cp2 = {
                x: end.x + end.normalX * baseTension,
                y: end.y
            };
        } else {
            cp1 = {
                x: start.x,
                y: start.y + start.normalY * baseTension
            };
            cp2 = {
                x: end.x,
                y: end.y + end.normalY * baseTension
            };
        }

        const samples = sampleCubicBezier(start, cp1, cp2, end, 32);
        return { start, end, cp1, cp2, samples };
    }

    function resolveRowIndex(node) {
        if (!node) {
            return NaN;
        }
        const y = Number(node.y ?? node.Y);
        if (!Number.isFinite(y)) {
            return NaN;
        }
        return Math.round(y / GRID_ROW_SPACING);
    }

    function areNodesVerticallyAdjacent(nodeA, nodeB) {
        if (!nodeA || !nodeB) {
            return false;
        }

        const laneA = Number.isFinite(nodeA.lane) ? nodeA.lane : null;
        const laneB = Number.isFinite(nodeB.lane) ? nodeB.lane : null;

        if (laneA !== null && laneB !== null) {
            if (laneA !== laneB) {
                return false;
            }

            const rowA = resolveRowIndex(nodeA);
            const rowB = resolveRowIndex(nodeB);
            if (Number.isFinite(rowA) && Number.isFinite(rowB)) {
                return Math.abs(rowA - rowB) === 1;
            }
        }

        const ax = Number(nodeA.x ?? nodeA.X ?? 0);
        const ay = Number(nodeA.y ?? nodeA.Y ?? 0);
        const bx = Number(nodeB.x ?? nodeB.X ?? 0);
        const by = Number(nodeB.y ?? nodeB.Y ?? 0);
        const aw = Math.abs(Number(nodeA.width ?? nodeA.Width ?? 54));
        const bw = Math.abs(Number(nodeB.width ?? nodeB.Width ?? 54));
        const ah = Math.abs(Number(nodeA.height ?? nodeA.Height ?? 24));
        const bh = Math.abs(Number(nodeB.height ?? nodeB.Height ?? 24));
        const sameLane = laneA !== null && laneA === laneB;

        const avgWidth = (aw + bw) / 2;
        const avgHeight = (ah + bh) / 2;
        const horizontalTolerance = sameLane
            ? Math.max(12, Math.min(96, avgWidth * 1.05))
            : Math.max(4, Math.min(24, avgWidth * 0.4));

        if (Math.abs(ax - bx) > horizontalTolerance) {
            return false;
        }

        const verticalDistance = Math.abs(ay - by);
        const minSeparation = Math.max(6, Math.min(24, avgHeight * 0.35));
        if (verticalDistance < minSeparation) {
            return false;
        }

        if (sameLane) {
            const maxSeparation = Math.max(180, avgHeight * 8);
            return verticalDistance <= maxSeparation;
        }

        const combinedHalfHeight = (ah + bh) / 2;
        const gap = verticalDistance - combinedHalfHeight;
        const overlapAllowance = Math.max(8, avgHeight * 0.25);
        const spacingAllowance = Math.max(40, avgHeight * 1.75);
        return gap >= -overlapAllowance && gap <= spacingAllowance;
    }

    function buildVerticalAdjacentSegment(fromNode, toNode, padding) {
        const fromCenterX = Number(fromNode.x ?? fromNode.X ?? 0);
        const fromCenterY = Number(fromNode.y ?? fromNode.Y ?? 0);
        const toCenterX = Number(toNode.x ?? toNode.X ?? 0);
        const toCenterY = Number(toNode.y ?? toNode.Y ?? 0);
        const pad = Number.isFinite(padding) ? padding : 0;
        const direction = toCenterY >= fromCenterY ? 1 : -1;
        const alignX = (fromCenterX + toCenterX) / 2;

        const fromNormalY = direction;
        const toNormalY = -direction;

        const startDistance = computePortOffset(fromNode, 0, fromNormalY, pad);
        const endDistance = computePortOffset(toNode, 0, toNormalY, pad);

        const start = {
            x: alignX,
            y: fromCenterY + fromNormalY * startDistance
        };

        const end = {
            x: alignX,
            y: toCenterY + toNormalY * endDistance
        };

        return { start, end };
    }

    function buildSameLaneBezierSegments(fromNode, toNode, descriptor, padding) {
        const side = descriptor?.side >= 0 ? 1 : -1;
        const band = Math.max(0, Math.min(2, descriptor?.band ?? 0));
        const fromCenterX = fromNode.x ?? fromNode.X ?? 0;
        const fromCenterY = fromNode.y ?? fromNode.Y ?? 0;
        const toCenterX = toNode.x ?? toNode.X ?? 0;
        const toCenterY = toNode.y ?? toNode.Y ?? 0;
        const pad = Number.isFinite(padding) ? padding : 0;

        const surfacePadding = 2 + band;
        const startOffset = computePortOffset(fromNode, side, 0, pad) + surfacePadding;
        const endOffset = computePortOffset(toNode, side, 0, pad) + surfacePadding;

        const start = {
            x: fromCenterX + side * startOffset,
            y: fromCenterY
        };

        const end = {
            x: toCenterX + side * endOffset,
            y: toCenterY
        };

        const spanY = Math.abs(end.y - start.y);
        let direction = Math.sign(end.y - start.y);
        if (direction === 0) {
            direction = fromCenterY <= toCenterY ? 1 : -1;
        }

        const stub = 18 + band * 4;
        const reach = 32 + Math.min(72, spanY * 0.2) + band * 8;
        const settle = Math.max(14, Math.min(48, spanY * 0.35 + 10));

        const exitCtrlX = start.x + side * stub;
        const entryCtrlX = end.x + side * stub;
        const baseOuter = side > 0 ? Math.max(exitCtrlX, entryCtrlX) : Math.min(exitCtrlX, entryCtrlX);
        const outerX = baseOuter + side * reach;

        const bendExtent = spanY > 0 ? Math.min(spanY / 2, Math.max(36, Math.min(110, spanY * 0.45))) : 48 + band * 6;
        const bendAmount = spanY > 0 ? bendExtent : Math.max(36, bendExtent);

        let startBendY = start.y + direction * bendAmount;
        let endBendY = end.y - direction * bendAmount;

        if ((direction > 0 && startBendY > endBendY) || (direction < 0 && startBendY < endBendY)) {
            const mid = (start.y + end.y) / 2;
            const delta = Math.max(18, Math.min(60, spanY * 0.5 || 36));
            startBendY = mid - direction * delta;
            endBendY = mid + direction * delta;
        }

        const seg1End = { x: outerX, y: startBendY };
        const seg2End = { x: outerX, y: endBendY };

        const segments = [
            {
                start,
                cp1: { x: exitCtrlX, y: start.y },
                cp2: { x: outerX, y: startBendY - direction * settle },
                end: seg1End
            },
            {
                start: seg1End,
                cp1: { x: outerX, y: startBendY + direction * settle },
                cp2: { x: outerX, y: endBendY - direction * settle },
                end: seg2End
            },
            {
                start: seg2End,
                cp1: { x: outerX, y: endBendY + direction * settle },
                cp2: { x: entryCtrlX, y: end.y },
                end
            }
        ];

        return segments;
    }

    function computeSiblingLaneElbowPath(fromNode, toNode, descriptor, padding) {
        const side = descriptor?.side >= 0 ? 1 : -1;
        const band = descriptor?.band ?? 0;

        const fromCenterX = fromNode.x ?? fromNode.X ?? 0;
        const fromCenterY = fromNode.y ?? fromNode.Y ?? 0;
        const toCenterX = toNode.x ?? toNode.X ?? 0;
        const toCenterY = toNode.y ?? toNode.Y ?? 0;
        const pad = Number.isFinite(padding) ? padding : 0;
        const lateralStub = 28 + band * 6;
        const verticalLift = 40 + Math.abs(fromCenterY - toCenterY) * 0.35 + band * 6;

        const startOffset = computePortOffset(fromNode, side, 0, pad) + 6 + band * 2;
        const endOffset = computePortOffset(toNode, side, 0, pad) + 6 + band * 2;

        const startX = fromCenterX + side * startOffset;
        const startY = fromCenterY;
        const endX = toCenterX + side * endOffset;
        const endY = toCenterY;

        const midX = startX + side * lateralStub;
        const landingX = endX - side * lateralStub;
        const verticalDir = fromCenterY <= toCenterY ? 1 : -1;
        const bendY = (startY + endY) / 2 - verticalDir * verticalLift;

        const points = dedupePoints([
            { x: startX, y: startY },
            { x: midX, y: startY },
            { x: midX, y: bendY },
            { x: landingX, y: bendY },
            { x: landingX, y: endY },
            { x: endX, y: endY }
        ]);

        return { points };
    }

    function createAnchors(node) {
        const cx = node.x ?? node.X ?? 0;
        const cy = node.y ?? node.Y ?? 0;
        const width = node.width ?? node.Width ?? 54;
        const height = node.height ?? node.Height ?? 24;
        const halfW = width / 2;
        const halfH = height / 2;

        return {
            left: { x: cx - halfW, y: cy, normalX: -1, normalY: 0 },
            right: { x: cx + halfW, y: cy, normalX: 1, normalY: 0 },
            top: { x: cx, y: cy - halfH, normalX: 0, normalY: -1 },
            bottom: { x: cx, y: cy + halfH, normalX: 0, normalY: 1 }
        };
    }

    function sampleCubicBezier(start, cp1, cp2, end, segments) {
        const points = [];
        const steps = Math.max(4, segments | 0);
        for (let i = 0; i <= steps; i++) {
            const t = i / steps;
            const omt = 1 - t;
            const omt2 = omt * omt;
            const t2 = t * t;
            const x = (omt2 * omt * start.x) + (3 * omt2 * t * cp1.x) + (3 * omt * t2 * cp2.x) + (t2 * t * end.x);
            const y = (omt2 * omt * start.y) + (3 * omt2 * t * cp1.y) + (3 * omt * t2 * cp2.y) + (t2 * t * end.y);
            points.push({ x, y });
        }
        return points;
    }

    function sampleSegmentSeries(segments, samplesPerSegment = 16) {
        if (!Array.isArray(segments) || segments.length === 0) {
            return [];
        }

        const collected = [];
        const sampleCount = Math.max(6, samplesPerSegment | 0);

        segments.forEach((segment, index) => {
            if (!segment?.start || !segment?.cp1 || !segment?.cp2 || !segment?.end) {
                return;
            }

            const points = sampleCubicBezier(segment.start, segment.cp1, segment.cp2, segment.end, sampleCount);
            if (index > 0 && points.length > 0) {
                points.shift();
            }
            collected.push(...points);
        });

        return collected;
    }

    function drawArrowhead(ctx, fromX, fromY, toX, toY) {
        let dx = toX - fromX;
        let dy = toY - fromY;
        const len = Math.hypot(dx, dy) || 1;
        dx /= len;
        dy /= len;

        const arrowLength = 9;
        const arrowWidth = 7;
        const baseX = toX - dx * arrowLength;
        const baseY = toY - dy * arrowLength;
        const perpX = -dy;
        const perpY = dx;

        ctx.beginPath();
        ctx.moveTo(toX, toY);
        ctx.lineTo(baseX + perpX * (arrowWidth / 2), baseY + perpY * (arrowWidth / 2));
        ctx.lineTo(baseX - perpX * (arrowWidth / 2), baseY - perpY * (arrowWidth / 2));
        ctx.closePath();
        ctx.fillStyle = ctx.strokeStyle;
        ctx.fill();
    }

    function drawEdgeShare(ctx, points, share) {
        const pct = Number(share);
        if (!Number.isFinite(pct) || !Array.isArray(points) || points.length < 2) {
            return;
        }

        const segments = buildPolylineSegments(points);
        const totalLength = segments.reduce((sum, seg) => sum + seg.length, 0);
        if (!Number.isFinite(totalLength) || totalLength === 0) {
            return;
        }

        const midpoint = pointAlongSegments(segments, totalLength / 2);
        if (!midpoint) {
            return;
        }

        const label = `${Math.round(pct * 100)}%`;
        const offset = 10;
        const perpX = -midpoint.tangent.dy;
        const perpY = midpoint.tangent.dx;
        const labelX = midpoint.point.x + perpX * offset;
        const labelY = midpoint.point.y + perpY * offset;

        ctx.save();
        ctx.fillStyle = 'rgba(33, 37, 41, 0.85)';
        ctx.font = '10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(label, labelX, labelY);
        ctx.restore();
    }

    function buildPolylineSegments(points) {
        if (!Array.isArray(points) || points.length < 2) {
            return [];
        }

        const segments = [];
        for (let i = 0; i < points.length - 1; i++) {
            const start = points[i];
            const end = points[i + 1];
            const dx = end.x - start.x;
            const dy = end.y - start.y;
            const length = Math.hypot(dx, dy);
            segments.push({ x1: start.x, y1: start.y, x2: end.x, y2: end.y, length });
        }

        return segments;
    }

    function drawEdgeMultiplier(ctx, points, label, strokeColor) {
        if (!Array.isArray(points) || points.length === 0 || typeof label !== 'string' || label.length === 0) {
            return;
        }

        const index = Math.floor(points.length / 2);
        const anchor = points[Math.max(0, Math.min(points.length - 1, index))];
        if (!anchor) {
            return;
        }

        ctx.save();
        ctx.font = '10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        const paddingX = 6;
        const chipHeight = 14;
        const textWidth = Math.ceil(ctx.measureText(label).width);
        const chipWidth = textWidth + paddingX * 2;
        const left = anchor.x - chipWidth / 2;
        const baseline = anchor.y - 8;
        drawChip(ctx, left, baseline, label, 'rgba(255, 255, 255, 0.92)', strokeColor, paddingX, chipHeight);
        ctx.restore();
    }

    function drawEdgeOverlayLabel(ctx, points, text, accentColor) {
        if (!Array.isArray(points) || points.length < 2 || typeof text !== 'string' || text.trim().length === 0) {
            return;
        }

        const segments = buildPolylineSegments(points);
        const totalLength = segments.reduce((sum, seg) => sum + seg.length, 0);
        if (!Number.isFinite(totalLength) || totalLength === 0) {
            return;
        }

        const midpoint = pointAlongSegments(segments, totalLength / 2);
        if (!midpoint || !midpoint.point) {
            return;
        }

        const anchor = midpoint.point;
        const border = accentColor ?? EDGE_OVERLAY_BASE_COLORS.neutral;
        const background = accentColor ?? EDGE_OVERLAY_BASE_COLORS.neutral;
        const textColor = '#FFFFFF';

        ctx.save();
        ctx.font = '600 10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        const paddingX = 6;
        const paddingY = 3;
        const measured = ctx.measureText(text);
        const width = Math.ceil(measured.width) + paddingX * 2;
        const height = 14;
        const x = anchor.x - width / 2;
        const y = anchor.y - height - 4;

        ctx.fillStyle = background;
        ctx.strokeStyle = border;
        ctx.lineWidth = 0.9;
        drawRoundedRectTopLeft(ctx, x, y, width, height, 6);
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = textColor;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(text, anchor.x, y + height / 2);
        ctx.restore();
    }

    function drawEdgeOverlayLegend(ctx, state) {
        const legend = state?.edgeOverlayLegend;
        if (!legend || !Array.isArray(legend.entries) || legend.entries.length === 0) {
            return;
        }

        const ratio = Number(state.deviceRatio ?? window.devicePixelRatio ?? 1) || 1;
        ctx.save();
        ctx.setTransform(ratio, 0, 0, ratio, 0, 0);

        const isDark = isDarkTheme();
        const background = isDark ? 'rgba(15, 23, 42, 0.92)' : 'rgba(248, 250, 252, 0.95)';
        const border = isDark ? 'rgba(148, 163, 184, 0.45)' : 'rgba(148, 163, 184, 0.35)';
        const textColor = isDark ? '#E2E8F0' : '#0F172A';
        const badgeBorder = isDark ? '#1E293B' : '#E2E8F0';

        const margin = 6;
        const badgeSize = 10;
        const paddingX = 12;
        const paddingY = 10;
        const rowGap = 4;
        const rowHeight = 16;

        ctx.font = '600 11px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        let width = ctx.measureText(legend.title ?? 'Edge overlays').width;
        ctx.font = '10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        for (const entry of legend.entries) {
            const text = buildLegendEntryText(entry);
            width = Math.max(width, ctx.measureText(text).width);
        }

        width += paddingX * 2 + badgeSize + 12;
        const height = paddingY * 2 + 4 + legend.entries.length * (rowHeight + rowGap);
        const canvasWidth = state.canvasWidth ?? (ctx.canvas.width / ratio);
        const x = canvasWidth - width - margin;
        const y = margin;

        ctx.fillStyle = background;
        ctx.strokeStyle = border;
        ctx.lineWidth = 1;
        drawRoundedRectTopLeft(ctx, x, y, width, height, 10);
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = textColor;
        ctx.font = '600 11px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        ctx.textBaseline = 'top';
        ctx.fillText(legend.title ?? 'Edge overlays', x + paddingX, y + paddingY);

        ctx.font = '10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        ctx.textBaseline = 'middle';
        let cursorY = y + paddingY + 18;
        for (const entry of legend.entries) {
            const text = buildLegendEntryText(entry);
            ctx.fillStyle = entry.color ?? EDGE_OVERLAY_BASE_COLORS.neutral;
            ctx.beginPath();
            ctx.arc(x + paddingX, cursorY + 6, badgeSize / 2, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = badgeBorder;
            ctx.lineWidth = 0.5;
            ctx.stroke();

            ctx.fillStyle = textColor;
            ctx.textAlign = 'left';
            ctx.fillText(text, x + paddingX + badgeSize + 6, cursorY + 6);
            cursorY += rowHeight + rowGap;
        }

        ctx.restore();
    }

    function buildLegendEntryText(entry) {
        const label = typeof entry?.label === 'string' ? entry.label : '';
        const value = typeof entry?.value === 'string' ? entry.value : '';
        return value ? `${label}: ${value}` : label;
    }

    function normalizeEdgeSeries(raw) {
        const map = new Map();
        if (!raw || !Array.isArray(raw)) {
            return map;
        }

        for (const entry of raw) {
            if (!entry) continue;
            const id = (entry.id ?? entry.Id ?? '').toString();
            if (!id) continue;

            map.set(id, {
                id,
                from: entry.from ?? entry.From ?? '',
                to: entry.to ?? entry.To ?? '',
                field: typeof entry?.field === 'string' ? entry.field.toLowerCase() : (typeof entry?.Field === 'string' ? entry.Field.toLowerCase() : ''),
                series: entry.series ?? entry.Series ?? {},
                multiplier: entry.multiplier ?? entry.Multiplier,
                lag: entry.lag ?? entry.Lag
            });
        }

        return map;
    }

    function sampleEdgeSeriesValue(series, key, selectedBin, startIndex) {
        if (!series || typeof key !== 'string') {
            return null;
        }

        const normalizedKey = key.toLowerCase();
        const pascalKey = key.length > 0 ? `${key[0].toUpperCase()}${key.slice(1)}` : key;
        const values = Array.isArray(series[key])
            ? series[key]
            : Array.isArray(series[normalizedKey])
                ? series[normalizedKey]
                : Array.isArray(series[pascalKey])
                    ? series[pascalKey]
                    : null;

        if (!values || values.length === 0) {
            return null;
        }

        const index = Math.round(selectedBin - startIndex);
        if (!Number.isFinite(index) || index < 0 || index >= values.length) {
            return null;
        }

        const value = values[index];
        return Number.isFinite(value) ? Number(value) : null;
    }

    function buildEdgeOverlayContext(edges, nodeMap, overlays, edgeSeries, edgeSeriesStartIndex) {
        if (!overlays) {
            return null;
        }

        const mode = normalizeEdgeOverlayMode(overlays.edgeOverlayMode);
        if (mode === 'off') {
            return null;
        }

        const selectedBin = Number(overlays.selectedBin ?? -1);
        if (!Number.isFinite(selectedBin) || selectedBin < 0) {
            return null;
        }

        const seriesMap = edgeSeries instanceof Map ? edgeSeries : normalizeEdgeSeries(edgeSeries);
        if (!seriesMap || seriesMap.size === 0) {
            return null;
        }

        const startIndex = Number.isFinite(edgeSeriesStartIndex) ? edgeSeriesStartIndex : 0;
        const samples = new Map();
        const values = [];

        for (const edge of edges) {
            const fromId = edge.from ?? edge.From;
            const toId = edge.to ?? edge.To;
            if (!fromId || !toId) {
                continue;
            }

            if (!nodeMap.has(fromId)) {
                continue;
            }

            const edgeId = edge.id ?? edge.Id ?? `${fromId}->${toId}`;
            const series = seriesMap.get(edgeId);
            if (!series) {
                continue;
            }

            if (mode === 'retryRate') {
                const value = sampleEdgeSeriesValue(series.series, 'retryRate', selectedBin, startIndex);
                if (!Number.isFinite(value)) {
                    continue;
                }
                const clamped = clamp(Number(value), 0, 1);
                samples.set(edgeId, {
                    mode,
                    value: clamped,
                    text: formatPercent(clamped)
                });
                values.push(clamped);
            } else if (mode === 'attempts') {
                let attempts = sampleEdgeSeriesValue(series.series, 'attemptsLoad', selectedBin, startIndex);
                if (!Number.isFinite(attempts)) {
                    continue;
                }
                if (attempts < 0) {
                    attempts = 0;
                }

                const value = attempts;
                samples.set(edgeId, {
                    mode,
                    value,
                    text: formatMetricValue(value)
                });
                values.push(value);
            }
        }

        if (samples.size === 0) {
            return null;
        }

        if (mode === 'retryRate') {
            const warning = Number(overlays.thresholds?.errorWarning ?? 0.02);
            const critical = Number(overlays.thresholds?.errorCritical ?? 0.05);
            for (const sample of samples.values()) {
                sample.bucket = classifyRetryRate(sample.value, warning, critical);
                const bucketColors = overlayBucketColors(sample.bucket);
                sample.color = bucketColors.stroke;
                sample.baseColor = bucketColors.base;
            }
            return {
                mode,
                samples,
                legend: buildRetryLegend(warning, critical, samples.size)
            };
        }

        const thresholds = buildAttemptsThresholds(values);
        for (const sample of samples.values()) {
            sample.bucket = classifyAttempts(sample.value, thresholds);
            const bucketColors = overlayBucketColors(sample.bucket);
            sample.color = bucketColors.stroke;
            sample.baseColor = bucketColors.base;
        }

        return {
            mode,
            samples,
            legend: buildAttemptsLegend(thresholds, samples.size)
        };
    }

    function normalizeEdgeOverlayMode(raw) {
        if (typeof raw === 'string') {
            const normalized = raw.trim().toLowerCase();
            if (normalized === 'retryrate' || normalized === 'retry_rate') {
                return 'retryRate';
            }
            if (normalized === 'attempts') {
                return 'attempts';
            }
            return 'off';
        }

        const numeric = Number(raw);
        if (numeric === 1) {
            return 'retryRate';
        }
        if (numeric === 2) {
            return 'attempts';
        }
        return 'off';
    }

    function classifyRetryRate(value, warning, critical) {
        if (!Number.isFinite(value)) {
            return 'neutral';
        }

        if (Number.isFinite(critical) && value >= critical) {
            return 'error';
        }

        if (Number.isFinite(warning) && value >= warning) {
            return 'warning';
        }

        return 'success';
    }

    function classifyAttempts(value, thresholds) {
        if (!Number.isFinite(value)) {
            return 'neutral';
        }

        if (value >= thresholds.critical) {
            return 'error';
        }

        if (value >= thresholds.warning) {
            return 'warning';
        }

        return 'success';
    }

    function overlayBucketColors(bucket) {
        const key = bucket ?? 'neutral';
        const base = EDGE_OVERLAY_BASE_COLORS[key] ?? EDGE_OVERLAY_BASE_COLORS.neutral;
        return {
            base,
            stroke: hexToRgba(base, EDGE_OVERLAY_STROKE_ALPHA)
        };
    }

    function buildRetryLegend(warning, critical, count) {
        const warningLabel = formatPercent(Math.max(warning, 0));
        const criticalLabel = formatPercent(Math.max(critical, 0));
        return {
            title: 'Retry rate',
            count,
            entries: [
                { label: 'Healthy', value: `< ${warningLabel}`, color: overlayBucketColors('success').base },
                { label: 'Elevated', value: `${warningLabel} – ${criticalLabel}`, color: overlayBucketColors('warning').base },
                { label: 'Critical', value: `> ${criticalLabel}`, color: overlayBucketColors('error').base }
            ]
        };
    }

    function buildAttemptsLegend(thresholds, count) {
        const warningLabel = formatMetricValue(thresholds.warning);
        const criticalLabel = formatMetricValue(thresholds.critical);
        return {
            title: 'Attempts',
            count,
            entries: [
                { label: 'Low', value: `< ${warningLabel}`, color: overlayBucketColors('success').base },
                { label: 'Busy', value: `${warningLabel} – ${criticalLabel}`, color: overlayBucketColors('warning').base },
                { label: 'Hot', value: `> ${criticalLabel}`, color: overlayBucketColors('error').base }
            ]
        };
    }

    function buildAttemptsThresholds(values) {
        const valid = values
            .map(value => Number(value))
            .filter(value => Number.isFinite(value))
            .sort((a, b) => a - b);

        if (valid.length === 0) {
            return {
                warning: 0,
                critical: 1,
                min: 0,
                max: 1
            };
        }

        const warning = percentile(valid, 0.6);
        const criticalRaw = percentile(valid, 0.85);
        const span = Math.max(valid[valid.length - 1] - valid[0], 1);
        const epsilon = span * 0.05;
        const critical = Math.max(criticalRaw, warning + epsilon);

        return {
            warning,
            critical,
            min: valid[0],
            max: valid[valid.length - 1]
        };
    }

    function percentile(sortedValues, ratio) {
        if (!Array.isArray(sortedValues) || sortedValues.length === 0) {
            return 0;
        }

        const clampedRatio = Math.min(Math.max(ratio, 0), 1);
        const index = (sortedValues.length - 1) * clampedRatio;
        const lower = Math.floor(index);
        const upper = Math.ceil(index);
        if (lower === upper) {
            return sortedValues[lower];
        }

        const weight = index - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }

    function hexToRgba(hex, alpha) {
        if (typeof hex !== 'string') {
            return `rgba(124, 139, 161, ${alpha ?? 1})`;
        }

        const normalized = hex.replace('#', '').trim();
        if (normalized.length !== 6) {
            return `rgba(124, 139, 161, ${alpha ?? 1})`;
        }

        const r = parseInt(normalized.slice(0, 2), 16);
        const g = parseInt(normalized.slice(2, 4), 16);
        const b = parseInt(normalized.slice(4, 6), 16);
        const safeAlpha = Number.isFinite(alpha) ? alpha : 1;
        return `rgba(${r}, ${g}, ${b}, ${safeAlpha})`;
    }

    function colorWithAlpha(color, alpha) {
        if (typeof color !== 'string' || color.trim().length === 0) {
            return `rgba(37, 99, 235, ${alpha ?? 1})`;
        }

        const trimmed = color.trim();
        if (trimmed.startsWith('#')) {
            return hexToRgba(trimmed, alpha);
        }

        const rgbaMatch = trimmed.match(/^rgba\(([^)]+)\)$/i);
        if (rgbaMatch) {
            const parts = rgbaMatch[1].split(',').map(part => part.trim());
            if (parts.length >= 4) {
                return `rgba(${parts[0]}, ${parts[1]}, ${parts[2]}, ${alpha ?? parts[3]})`;
            }
        }

        const rgbMatch = trimmed.match(/^rgb\(([^)]+)\)$/i);
        if (rgbMatch) {
            const parts = rgbMatch[1].split(',').map(part => part.trim());
            if (parts.length >= 3) {
                return `rgba(${parts[0]}, ${parts[1]}, ${parts[2]}, ${alpha ?? 1})`;
            }
        }

        return trimmed;
    }

    function resolveEdgeType(edge) {
        const raw = String(edge?.edgeType ?? edge?.EdgeType ?? EdgeTypeTopology).toLowerCase();
        if (raw === EdgeTypeDependency) {
            return EdgeTypeDependency;
        }
        if (raw === EdgeTypeEffort) {
            return EdgeTypeEffort;
        }
        if (raw === EdgeTypeThroughput) {
            return EdgeTypeThroughput;
        }
        if (raw === EdgeTypeTerminal) {
            return EdgeTypeTerminal;
        }
        return EdgeTypeTopology;
    }

function setHoveredEdge(state, edgeId) {
        if (!state) {
            return false;
        }

        const normalized = edgeId ?? null;
        if (state.hoveredEdgeId === normalized) {
            return false;
        }

        state.hoveredEdgeId = normalized;
        if (state.dotNetRef && state.lastEdgeHoverId !== normalized) {
            state.lastEdgeHoverId = normalized;
            state.hoverStats ??= createHoverStats();
            state.hoverStats.interopDispatches = (state.hoverStats.interopDispatches ?? 0) + 1;
            state.hoverStats.windowDispatches = (state.hoverStats.windowDispatches ?? 0) + 1;
            const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
                ? performance.now()
                : Date.now();
            state.hoverStats.lastDispatchTimestamp = now;
            state.dotNetRef.invokeMethodAsync('OnEdgeHoverChanged', normalized);
            logHoverInteropDispatch(state, { kind: 'edge', id: normalized });
        }

        updateDiagnosticsHud(state);
        return true;
}

    function registerNodeHitbox(state, meta) {
        if (!state || !meta) {
            return;
        }

        const width = Number(meta.width ?? meta.Width ?? 0);
        const height = Number(meta.height ?? meta.Height ?? 0);
        const centerX = Number(meta.x ?? meta.X ?? 0);
        const centerY = Number(meta.y ?? meta.Y ?? 0);

        if (!Number.isFinite(width) || width <= 0 || !Number.isFinite(height) || height <= 0) {
            return;
        }

        if (!Number.isFinite(centerX) || !Number.isFinite(centerY)) {
            return;
        }

        const padding = NODE_HALO_PADDING;
        const paddedWidth = width + padding * 2;
        const paddedHeight = height + padding * 2;
        const left = centerX - (width / 2) - padding;
        const top = centerY - (height / 2) - padding;

        if (!Array.isArray(state.nodeHitboxes)) {
            state.nodeHitboxes = [];
        }

        state.nodeHitboxes.push({
            id: meta.id ?? meta.Id ?? null,
            x: left,
            y: top,
            width: paddedWidth,
            height: paddedHeight
        });
    }

    function notifyNodeHoverChanged(state) {
        if (!state) {
            return;
        }

        const normalized = state.hoveredNodeId ?? null;
        if (state.lastNodeHoverId === normalized) {
            return;
        }

        state.lastNodeHoverId = normalized;
        if (state.inspectorVisible) {
            state.pendingInspectorHoverId = normalized;
            scheduleInspectorDispatch(state);
        } else {
            state.pendingInspectorHoverId = null;
            cancelInspectorDispatch(state);
        }
        updateDiagnosticsHud(state);
    }

    function scheduleInspectorDispatch(state) {
        if (!state || state.inspectorDispatchHandle !== null) {
            return;
        }

        state.inspectorDispatchHandle = window.requestAnimationFrame(() => {
            state.inspectorDispatchHandle = null;
            flushInspectorDispatch(state);
        });
    }

    function cancelInspectorDispatch(state) {
        if (!state) {
            return;
        }

        if (state.inspectorDispatchHandle !== null) {
            cancelAnimationFrame(state.inspectorDispatchHandle);
            state.inspectorDispatchHandle = null;
        }
    }

    function flushInspectorDispatch(state) {
        if (!state || !state.inspectorVisible || !state.dotNetRef?.invokeMethodAsync) {
            return;
        }

        const targetId = state.pendingInspectorHoverId ?? state.hoveredNodeId ?? null;
        if (targetId === state.lastInspectorDispatchId) {
            state.pendingInspectorHoverId = null;
            return;
        }

        state.pendingInspectorHoverId = null;
        state.lastInspectorDispatchId = targetId;
        state.hoverStats ??= createHoverStats();
        state.hoverStats.interopDispatches = (state.hoverStats.interopDispatches ?? 0) + 1;
        state.hoverStats.windowDispatches = (state.hoverStats.windowDispatches ?? 0) + 1;
        const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
            ? performance.now()
            : Date.now();
        state.hoverStats.lastDispatchTimestamp = now;
        state.lastNodeHoverDispatchTime = now;
        const dispatchStarted = performance.now ? performance.now() : Date.now();
        logHoverInteropDispatch(state, { kind: 'node', id: targetId });
        state.dotNetRef.invokeMethodAsync('OnNodeHoverChanged', targetId)
            ?.then(() => {
                const dispatchEnded = performance.now ? performance.now() : Date.now();
                const duration = dispatchEnded - dispatchStarted;
                if (duration > 16) {
                    debugLog(state, '[Topology] hover dispatch completed', { nodeId: targetId, durationMs: Number(duration.toFixed(3)) });
                }
            });
        updateDiagnosticsHud(state);
    }

    function hasHoverVisualDelta(state) {
        if (!state) {
            return false;
        }

        const nodeChanged = (state.hoveredNodeId ?? null) !== (state.lastDrawnHoverNodeId ?? null);
        const chipChanged = (state.hoveredChipId ?? null) !== (state.lastDrawnHoverChipId ?? null);
        const edgeChanged = (state.hoveredEdgeId ?? null) !== (state.lastDrawnHoverEdgeId ?? null);
        return nodeChanged || chipChanged || edgeChanged;
    }

    function setInspectorEdgeHoverState(canvas, edgeId) {
        const state = getState(canvas);
        if (!state) {
            return;
        }
        const normalized = edgeId ?? null;
        if (state.inspectorEdgeHoverId === normalized) {
            return;
        }

        state.inspectorEdgeHoverId = normalized;
        draw(canvas, state);
    }

    function focusEdgeOnCanvas(canvas, edgeId, centerOnEdge) {
        const state = getState(canvas);
        if (!state) {
            return;
        }
        const normalized = edgeId ?? null;
        state.focusedEdgeId = normalized;
        if (centerOnEdge && normalized) {
            centerEdgeOnCanvas(canvas, state, normalized);
        } else {
            draw(canvas, state);
        }
    }

    function centerEdgeOnCanvas(canvas, state, edgeId) {
        if (!edgeId) {
            draw(canvas, state);
            return;
        }

        const hitbox = Array.isArray(state.edgeHitboxes)
            ? state.edgeHitboxes.find(edge => edge?.id === edgeId)
            : null;
        let anchor = hitbox ? computeEdgeAnchor(hitbox.points) : null;

        if (!anchor) {
            const edges = state.scenePayload?.edges ?? state.scenePayload?.Edges ?? [];
            const edge = edges.find(e => (e.id ?? e.Id) === edgeId);
            if (edge) {
                const fromX = Number(edge.fromX ?? edge.FromX ?? 0);
                const toX = Number(edge.toX ?? edge.ToX ?? fromX);
                const fromY = Number(edge.fromY ?? edge.FromY ?? 0);
                const toY = Number(edge.toY ?? edge.ToY ?? fromY);
                anchor = { x: (fromX + toX) / 2, y: (fromY + toY) / 2 };
            }
        }

        if (!anchor) {
            draw(canvas, state);
            return;
        }

        const ratio = Number(state.deviceRatio ?? window.devicePixelRatio ?? 1) || 1;
        const width = state.canvasWidth ?? (canvas.width / ratio);
        const height = state.canvasHeight ?? (canvas.height / ratio);

        state.offsetX = (width / 2) - anchor.x * state.scale;
        state.offsetY = (height / 2) - anchor.y * state.scale;
        state.userAdjusted = true;
        updateWorldCenter(state);
        draw(canvas, state);
        emitViewportChanged(canvas, state, { immediate: true });
    }

    function computeEdgeAnchor(points) {
        const segments = buildPolylineSegments(points);
        const totalLength = segments.reduce((sum, seg) => sum + seg.length, 0);
        if (!Number.isFinite(totalLength) || totalLength <= 0) {
            return null;
        }

        const midpoint = pointAlongSegments(segments, totalLength / 2);
        return midpoint?.point ?? null;
    }

    function drawSparkline(ctx, nodeMeta, sparkline, overlaySettings, defaultColor, overrides) {
        const opts = overrides ?? {};
        const neutralize = Boolean(opts.neutralize);
        const basis = Number(overlaySettings.colorBasis ?? 0);
        const nodeKind = String(nodeMeta?.kind ?? nodeMeta?.Kind ?? '').trim().toLowerCase();
        const nodeLogicalType = String(nodeMeta?.logicalType ?? nodeMeta?.LogicalType ?? '').trim().toLowerCase();
        const seriesBasis = isQueueLikeKind(nodeKind, nodeLogicalType) ? 3 : basis;
        const series = selectSeriesForBasis(sparkline, seriesBasis);
        if (!Array.isArray(series) || series.length < 2) {
            return;
        }

        const { min, max, isFlat } = computeSeriesBounds(series);
        if (!Number.isFinite(min) || !Number.isFinite(max)) {
            return;
        }

        const mode = overlaySettings.sparklineMode === 'bar' ? 'bar' : 'line';
        const thresholds = overlaySettings.thresholds ?? {
            slaSuccess: 0.95,
            slaWarning: 0.8,
            utilizationWarning: 0.9,
            utilizationCritical: 0.95,
            errorWarning: 0.02,
            errorCritical: 0.05
        };
        const startIndex = Number(sparkline.startIndex ?? sparkline.StartIndex ?? 0);
        const selectedBin = Number(overlaySettings.selectedBin ?? -1);
        const highlightIndexRaw = Number.isFinite(selectedBin) ? selectedBin - startIndex : -1;
        const highlightIndex = (highlightIndexRaw >= 0 && highlightIndexRaw < series.length) ? highlightIndexRaw : -1;

        const width = nodeMeta.width ?? 54;
        const portCenterX = (nodeMeta.x ?? 0) - (width / 2);
        const labelRightX = portCenterX - 8;
        const defaultSparkWidth = Math.max(width - 6, 16);
        const defaultSparkHeight = mode === 'bar' ? 12 : 10;
        const baseWidth = Number.isFinite(opts.width) ? opts.width : defaultSparkWidth;
        const sparkWidth = computeAdaptiveWidth(
            series.length,
            baseWidth,
            {
                min: Number.isFinite(opts.minWidth) ? opts.minWidth : Math.max(baseWidth, 20),
                max: Number.isFinite(opts.maxWidth) ? opts.maxWidth : 140,
                scale: Number.isFinite(opts.scale) ? opts.scale : (mode === 'bar' ? 11 : 9)
            });
        const sparkHeight = Math.max(opts.height ?? defaultSparkHeight, 6);
        const rightEdge = typeof opts.right === 'number' ? opts.right : labelRightX;
        const left = typeof opts.left === 'number' ? opts.left : (rightEdge - sparkWidth);
        const top = opts.top ?? ((nodeMeta.y ?? 0) - sparkHeight - 10);

        ctx.save();
        ctx.translate(left, top);

        const step = sparkWidth / Math.max(series.length - 1, 1);
        const range = Math.max(max - min, 1e-6);

        const drawAsBars = mode === 'bar';
        let highlightPoint = null;
        let highlightColor = defaultColor;
        let highlightBar = null;
        let previousPoint = null;
        let previousColor = defaultColor;

        const darkMode = isDarkTheme();
        ctx.fillStyle = darkMode ? 'rgba(15, 23, 42, 0.85)' : 'rgba(203, 213, 225, 0.2)';
        ctx.fillRect(0, 0, sparkWidth, sparkHeight);
        ctx.strokeStyle = darkMode ? 'rgba(148, 163, 184, 0.5)' : 'rgba(148, 163, 184, 0.35)';
        ctx.lineWidth = 0.5;
        ctx.strokeRect(0, 0, sparkWidth, sparkHeight);

        series.forEach((raw, index) => {
            if (raw === null || raw === undefined) {
                previousPoint = null;
                return;
            }

            const numeric = Number(raw);
            if (!Number.isFinite(numeric)) {
                previousPoint = null;
                return;
            }

            let normalized = clamp((numeric - min) / range, 0, 1);
            if (isFlat) {
                normalized = numeric >= min ? 1 : 0;
            }

            const x = index * step;
            const y = sparkHeight - (sparkHeight * normalized);
            const sampleColor = neutralize ? defaultColor : resolveSampleColor(basis, index, sparkline, thresholds, defaultColor);

            if (drawAsBars) {
                const barWidth = Math.max(step * 0.6, 1.5);
                ctx.fillStyle = sampleColor;
                const clampedY = Math.min(Math.max(y, 0), sparkHeight);
                const barHeight = Math.max(sparkHeight - clampedY, 1);
                const barTop = sparkHeight - barHeight;
                ctx.fillRect(x - barWidth / 2, barTop, barWidth, barHeight);
                if (index === highlightIndex) {
                    highlightColor = neutralize ? defaultColor : sampleColor;
                    highlightBar = {
                        x,
                        width: Math.max(barWidth, 3)
                    };
                }
            } else {
                if (previousPoint) {
                    ctx.beginPath();
                    ctx.strokeStyle = previousColor ?? sampleColor;
                    ctx.lineWidth = 1.4;
                    ctx.moveTo(previousPoint.x, previousPoint.y);
                    ctx.lineTo(x, y);
                    ctx.stroke();
                }
                previousPoint = { x, y };
                previousColor = neutralize ? defaultColor : sampleColor;
                if (index === highlightIndex) {
                    highlightPoint = { x, y };
                    highlightColor = neutralize ? defaultColor : sampleColor;
                }
            }
        });

        if (!drawAsBars && highlightPoint) {
            ctx.beginPath();
            ctx.fillStyle = highlightColor ?? defaultColor;
            ctx.arc(highlightPoint.x, highlightPoint.y, 1.8, 0, Math.PI * 2);
            ctx.fill();
        }

        if (drawAsBars && highlightBar) {
            const apexY = sparkHeight + 1;
            const baseY = apexY + 5;
            const pointerWidth = Math.max(highlightBar.width + 4, 8);
            const halfWidth = pointerWidth / 2;

            ctx.beginPath();
            ctx.moveTo(highlightBar.x, apexY);
            ctx.lineTo(highlightBar.x - halfWidth, baseY);
            ctx.lineTo(highlightBar.x + halfWidth, baseY);
            ctx.closePath();
            ctx.fillStyle = highlightColor ?? defaultColor;
            ctx.fill();
        }

        ctx.restore();
    }

    function selectSeriesForBasis(sparkline, basis) {
        if (!sparkline) {
            return [];
        }

        const keyCandidates = basis === 1
            ? ['utilization']
            : basis === 2
                ? ['errorRate']
                : basis === 3
                    ? ['queue', 'queueDepth']
                    : basis === 4
                        ? ['serviceTimeMs', 'serviceTime']
                        : [];

        for (const candidate of keyCandidates) {
            const slice = getSeriesSlice(sparkline, candidate);
            const values = slice?.values ?? slice?.Values;
            if (Array.isArray(values) && values.length > 0) {
                return values;
            }
        }

        switch (basis) {
            case 1:
                return sparkline.utilization ?? sparkline.Utilization ?? sparkline.values ?? sparkline.Values ?? [];
            case 2:
                return sparkline.errorRate ?? sparkline.ErrorRate ?? sparkline.values ?? sparkline.Values ?? [];
            case 3:
                return sparkline.queueDepth ?? sparkline.QueueDepth ?? sparkline.values ?? sparkline.Values ?? [];
            case 4:
                return sparkline.serviceTimeMs ?? sparkline.ServiceTimeMs ?? sparkline.values ?? sparkline.Values ?? [];
            default:
                return sparkline.values ?? sparkline.Values ?? [];
        }
    }

    function sampleThroughputValue(nodeMeta, overlays) {
        if (!nodeMeta) {
            return null;
        }

        const semantics = normalizeSemantics(nodeMeta.semantics ?? nodeMeta.Semantics ?? null);
        return sampleMetricFromNode(nodeMeta, overlays, 'served', semantics.served, ['served']);
    }

    function sampleMetricFromNode(nodeMeta, overlays, defaultKey, semanticEntry, extraKeys) {
        if (!nodeMeta) {
            return null;
        }

        const spark = nodeMeta.sparkline ?? nodeMeta.Sparkline ?? null;
        const metricSnapshot = nodeMeta.metrics ?? nodeMeta.Metrics ?? null;
        const rawMetrics = normalizeRawMetricMap(metricSnapshot?.raw ?? metricSnapshot?.Raw ?? null);
        const sampleBin = resolveSampleBinForNode(spark, overlays);

        const keys = [];
        const pushKey = (candidate) => {
            if (candidate === null || candidate === undefined) {
                return;
            }
            const text = String(candidate).trim();
            if (text.length === 0) {
                return;
            }
            if (!keys.includes(text)) {
                keys.push(text);
            }
        };

        pushKey(defaultKey);

        if (semanticEntry) {
            pushKey(semanticEntry.key);
            pushKey(semanticEntry.canonical);
            pushKey(semanticEntry.reference);
            pushKey(semanticEntry.label);
        }

        if (Array.isArray(extraKeys)) {
            for (const candidate of extraKeys) {
                pushKey(candidate);
            }
        }

        const lookupRawMetric = (candidate) => {
            if (!rawMetrics || !candidate) {
                return null;
            }

            const text = String(candidate).trim();
            if (text.length === 0) {
                return null;
            }

            const normalized = text.toLowerCase();
            if (!Object.prototype.hasOwnProperty.call(rawMetrics, normalized)) {
                return null;
            }

            const rawValue = rawMetrics[normalized];
            if (rawValue === null || rawValue === undefined) {
                return null;
            }

            const numeric = Number(rawValue);
            return Number.isFinite(numeric) ? numeric : null;
        };

        for (const candidate of keys) {
            let value = null;
            if (spark) {
                value = sampleSeriesValueAt(spark, candidate, sampleBin);
            }

            if (value === null || value === undefined) {
                value = lookupRawMetric(candidate);
            }

            if (value !== null && value !== undefined) {
                return value;
            }
        }

        return lookupRawMetric(defaultKey);
    }

    function resolveSampleBinForNode(spark, overlays) {
        const selectedBin = Number(overlays?.selectedBin ?? overlays?.SelectedBin ?? -1);
        if (Number.isFinite(selectedBin) && selectedBin >= 0) {
            return selectedBin;
        }

        if (!spark) {
            return selectedBin;
        }

        const baseStart = Number(spark.startIndex ?? spark.StartIndex ?? 0);
        const baseValues = spark.values ?? spark.Values;
        if (Array.isArray(baseValues) && baseValues.length > 0) {
            return baseStart + baseValues.length - 1;
        }

        const map = spark.series ?? spark.Series;
        if (map) {
            for (const slice of Object.values(map)) {
                if (!slice) {
                    continue;
                }

                const sliceValues = slice.values ?? slice.Values;
                if (Array.isArray(sliceValues) && sliceValues.length > 0) {
                    const sliceStart = Number(slice.startIndex ?? slice.StartIndex ?? baseStart);
                    return sliceStart + sliceValues.length - 1;
                }
            }
        }

        return baseStart;
    }

    function computeSeriesBounds(series) {
        let min = Number.POSITIVE_INFINITY;
        let max = Number.NEGATIVE_INFINITY;
        let hasValue = false;

        for (const sample of series) {
            if (sample === null || sample === undefined) {
                continue;
            }
            const numeric = Number(sample);
            if (!Number.isFinite(numeric)) {
                continue;
            }
            hasValue = true;
            if (numeric < min) min = numeric;
            if (numeric > max) max = numeric;
        }

        if (!hasValue) {
            min = 0;
            max = 1;
        }

        if (!Number.isFinite(min)) {
            min = 0;
        }
        if (!Number.isFinite(max)) {
            max = min + 1;
        }

        let isFlat = Math.abs(max - min) < 1e-6;
        if (isFlat) {
            max = min + 0.001;
        }

        return { min, max, isFlat };
    }

    function emitViewportChanged(canvas, state, options) {
        if (!state?.dotNetRef) {
            cancelViewportDebounce(state);
            return;
        }
        const details = buildViewportPayload(state);
        if (!details) {
            return;
        }
        if (details.signature === state.lastViewportSignature || details.withinTolerance) {
            return;
        }
        const immediate = options?.immediate === true;
        if (immediate) {
            cancelViewportDebounce(state);
            flushViewportChange(state, details);
            return;
        }
        scheduleViewportChange(state, details);
    }

    function scheduleViewportChange(state, details) {
        if (!state) {
            return;
        }
        state.pendingViewportPayload = details;
        if (state.viewportDebounceHandle !== null) {
            (window?.clearTimeout ?? clearTimeout)(state.viewportDebounceHandle);
        }
        const schedule = window?.setTimeout ?? setTimeout;
        state.viewportDebounceHandle = schedule(() => {
            state.viewportDebounceHandle = null;
            const pending = state.pendingViewportPayload;
            state.pendingViewportPayload = null;
            if (!pending) {
                return;
            }
            flushViewportChange(state, pending);
        }, VIEWPORT_EMIT_DEBOUNCE_MS);
    }

    function cancelViewportDebounce(state) {
        if (!state) {
            return;
        }
        if (state.viewportDebounceHandle !== null) {
            (window?.clearTimeout ?? clearTimeout)(state.viewportDebounceHandle);
            state.viewportDebounceHandle = null;
        }
        state.pendingViewportPayload = null;
    }

    function flushViewportChange(state, details) {
        if (!state || !details) {
            return;
        }
        state.lastViewportSignature = details.signature;
        state.lastViewportPayload = details.payload;
        state.dotNetRef?.invokeMethodAsync('OnViewportChanged', details.payload);
    }

    function buildViewportPayload(state) {
        if (!state) {
            return null;
        }

        const baseScale = Number.isFinite(state.baseScale) && state.baseScale > 0
            ? state.baseScale
            : (Number.isFinite(state.overlayScale) && state.overlayScale > 0
                ? state.scale / state.overlayScale
                : state.scale);

        const payload = {
            Scale: Number(state.scale ?? 1),
            OffsetX: Number(state.offsetX ?? 0),
            OffsetY: Number(state.offsetY ?? 0),
            WorldCenterX: Number(state.worldCenterX ?? 0),
            WorldCenterY: Number(state.worldCenterY ?? 0),
            OverlayScale: Number(state.overlayScale ?? 1),
            BaseScale: Number(baseScale ?? state.scale ?? 1)
        };

        const signature = [
            payload.Scale.toFixed(4),
            payload.OffsetX.toFixed(2),
            payload.OffsetY.toFixed(2),
            payload.WorldCenterX.toFixed(4),
            payload.WorldCenterY.toFixed(4),
            payload.OverlayScale.toFixed(4),
            payload.BaseScale.toFixed(4)
        ].join('|');

        const prev = state.lastViewportPayload;
        const scaleTolerance = 0.001;
        const offsetTolerance = 0.5;
        const centerTolerance = 0.5;

        let isWithinTolerance = false;
        if (prev) {
            const deltaScale = Math.abs(payload.Scale - prev.Scale);
            const deltaOffsetX = Math.abs(payload.OffsetX - prev.OffsetX);
            const deltaOffsetY = Math.abs(payload.OffsetY - prev.OffsetY);
            const deltaCenterX = Math.abs(payload.WorldCenterX - prev.WorldCenterX);
            const deltaCenterY = Math.abs(payload.WorldCenterY - prev.WorldCenterY);
            const deltaOverlay = Math.abs(payload.OverlayScale - prev.OverlayScale);
            const deltaBase = Math.abs(payload.BaseScale - prev.BaseScale);
            isWithinTolerance =
                deltaScale < scaleTolerance &&
                deltaOverlay < scaleTolerance &&
                deltaBase < scaleTolerance &&
                deltaOffsetX < offsetTolerance &&
                deltaOffsetY < offsetTolerance &&
                deltaCenterX < centerTolerance &&
                deltaCenterY < centerTolerance;
        }

        return {
            payload,
            signature,
            withinTolerance: isWithinTolerance
        };
    }

    function restoreViewport(canvas, snapshot) {
        if (!canvas || !snapshot) {
            return;
        }

        const state = getState(canvas);
        if (!state) {
            return;
        }
        state.preserveViewportRequest = true;
        const scale = Number(snapshot.Scale ?? snapshot.scale);
        const overlayScale = Number(snapshot.OverlayScale ?? snapshot.overlayScale);
        const baseScale = Number(snapshot.BaseScale ?? snapshot.baseScale);

        if (Number.isFinite(scale) && scale > 0) {
            state.scale = scale;
        }

        if (Number.isFinite(overlayScale) && overlayScale > 0) {
            state.overlayScale = overlayScale;
        }

        if (Number.isFinite(baseScale) && baseScale > 0) {
            state.baseScale = baseScale;
        } else if (Number.isFinite(state.overlayScale) && state.overlayScale > 0) {
            state.baseScale = state.scale / state.overlayScale;
        }

        const width = state.canvasWidth ?? (canvas.width / state.deviceRatio);
        const height = state.canvasHeight ?? (canvas.height / state.deviceRatio);

        const worldCenterX = Number(snapshot.WorldCenterX ?? snapshot.worldCenterX);
        const worldCenterY = Number(snapshot.WorldCenterY ?? snapshot.worldCenterY);

        if (Number.isFinite(worldCenterX) && Number.isFinite(worldCenterY) && Number.isFinite(width) && Number.isFinite(height)) {
            state.worldCenterX = worldCenterX;
            state.worldCenterY = worldCenterY;
            state.offsetX = (width / 2) - worldCenterX * state.scale;
            state.offsetY = (height / 2) - worldCenterY * state.scale;
        } else {
            const offsetX = Number(snapshot.OffsetX ?? snapshot.offsetX);
            const offsetY = Number(snapshot.OffsetY ?? snapshot.offsetY);
            if (Number.isFinite(offsetX)) {
                state.offsetX = offsetX;
            }
            if (Number.isFinite(offsetY)) {
                state.offsetY = offsetY;
            }
            updateWorldCenter(state);
        }

        state.userAdjusted = true;
        state.viewportApplied = true;
        state.lastViewportSignature = null;
        state.lastViewportPayload = null;

        draw(canvas, state);
        emitViewportChanged(canvas, state, { immediate: true });
    }

    function loadDiagnosticsCollapsePref() {
        try {
            return window.localStorage?.getItem(DIAGNOSTICS_COLLAPSE_STORAGE_KEY) === '1';
        } catch {
            return false;
        }
    }

    function saveDiagnosticsCollapsePref(collapsed) {
        try {
            window.localStorage?.setItem(DIAGNOSTICS_COLLAPSE_STORAGE_KEY, collapsed ? '1' : '0');
        } catch {
            // Ignore storage failures
        }
    }

    function setDiagnosticsHudCollapsed(state, collapsed) {
        if (!state) {
            return;
        }
        state.diagnosticsHudCollapsed = collapsed;
        saveDiagnosticsCollapsePref(collapsed);
        applyDiagnosticsHudVisibility(state);
    }

    function applyDiagnosticsHudVisibility(state) {
        if (!state?.diagnosticsHud) {
            return;
        }

        const collapsed = !!state.diagnosticsHudCollapsed;
        if (state.diagnosticsHud.root) {
            state.diagnosticsHud.root.style.display = collapsed ? 'none' : '';
        }
        if (state.diagnosticsHud.collapsedChip) {
            state.diagnosticsHud.collapsedChip.style.display = collapsed ? '' : 'none';
        }
        if (state.diagnosticsHud.binDumpChip) {
            const chip = state.diagnosticsHud.binDumpChip;
            if (collapsed) {
                chip.style.display = '';
                chip.style.right = '';
                chip.classList.remove('topology-diag-panel__collapsed-chip--inline');
            } else {
                chip.style.display = '';
                const panelWidth = state.diagnosticsHud.root?.offsetWidth ?? 220;
                chip.style.right = `${panelWidth + 24}px`;
                chip.classList.add('topology-diag-panel__collapsed-chip--inline');
            }
        }
        updateBinDumpChipState(state);
    }

    function createCollapsedChip(host, state) {
        const chip = document.createElement('button');
        chip.type = 'button';
        chip.className = 'topology-diag-panel__collapsed-chip';
        chip.textContent = 'Diagnostics';
        chip.addEventListener('click', () => setDiagnosticsHudCollapsed(state, false));
        host.appendChild(chip);
        chip.style.display = 'none';
        return chip;
    }

    function createBinDumpChip(host, state) {
        const chip = document.createElement('button');
        chip.type = 'button';
        chip.className = 'topology-diag-panel__collapsed-chip topology-diag-panel__collapsed-chip--bin';
        chip.textContent = 'Dump bin';
        chip.addEventListener('click', () => {
            const dotNetRef = state?.dotNetRef;
            if (!dotNetRef) {
                return;
            }
            const nodeId = state?.focusedNodeId ?? null;
            if (!nodeId) {
                return;
            }
            dotNetRef.invokeMethodAsync('OnDumpBinRequested', nodeId);
        });
        host.appendChild(chip);
        chip.style.display = 'none';
        return chip;
    }

    function updateBinDumpChipState(state) {
        const chip = state?.diagnosticsHud?.binDumpChip;
        if (!chip) {
            return;
        }
        const selectedNodeId = typeof state.focusedNodeId === 'string' && state.focusedNodeId.length > 0
            ? state.focusedNodeId
            : null;
        const enabled = Boolean(selectedNodeId);
        if (state.lastBinDumpSelectionId === selectedNodeId && chip.disabled === !enabled) {
            return;
        }
        state.lastBinDumpSelectionId = selectedNodeId;
        chip.disabled = !enabled;
        if (enabled) {
            chip.removeAttribute('aria-disabled');
        } else {
            chip.setAttribute('aria-disabled', 'true');
        }
    }

    function applyDiagnosticsOptions(canvas, state, options) {
        if (!state) {
            return;
        }

        const normalized = options
            ? {
                enabled: Boolean(options.enabled),
                runId: typeof options.runId === 'string' && options.runId.length > 0 ? options.runId : null,
                buildHash: typeof options.buildHash === 'string' && options.buildHash.length > 0 ? options.buildHash : null,
                uploadUrl: typeof options.uploadUrl === 'string' && options.uploadUrl.length > 0 ? options.uploadUrl : null,
                uploadIntervalMs: Number.isFinite(options.uploadIntervalMs) && options.uploadIntervalMs > 0
                    ? Number(options.uploadIntervalMs)
                    : 0,
                disableHoverCache: Boolean(options.disableHoverCache),
                operationalViewOnly: Boolean(options.operationalViewOnly),
                inspectorVisible: Boolean(options.inspectorVisible),
                debugLogging: typeof options.debugLogging === 'boolean' ? Boolean(options.debugLogging) : undefined
            }
            : null;

        state.diagnosticsOptions = normalized;
        state.runId = normalized?.runId ?? state.runId ?? null;
        state.buildHash = normalized?.buildHash ?? state.buildHash ?? null;
        const disableHoverCache = normalized?.disableHoverCache ?? false;
        const cacheSettingChanged = state.disableHoverCache !== disableHoverCache;
        state.disableHoverCache = disableHoverCache;
        if (cacheSettingChanged) {
            resetHoverCache(state);
        }

        state.operationalViewOnly = Boolean(normalized?.operationalViewOnly);
        state.inspectorVisible = Boolean(normalized?.inspectorVisible);
        if (typeof normalized?.debugLogging === 'boolean') {
            state.debugEnabled = normalized.debugLogging;
        }

        if (!normalized?.enabled) {
            destroyDiagnosticsHud(state);
        } else if (!state.diagnosticsHud) {
            state.diagnosticsHud = createDiagnosticsHud(canvas, state);
        } else {
            updateDiagnosticsHud(state);
        }

        configureDiagnosticsUploader(state);
    }

    function setInspectorVisibility(canvas, isVisible) {
        const state = registry.get(canvas);
        if (!state) {
            return;
        }

        const normalized = Boolean(isVisible);
        if (state.inspectorVisible === normalized) {
            return;
        }

        state.inspectorVisible = normalized;
        state.diagnosticsOptions ??= {};
        state.diagnosticsOptions.inspectorVisible = normalized;
        if (normalized) {
            state.lastInspectorDispatchId = null;
            state.pendingInspectorHoverId = state.hoveredNodeId ?? null;
            scheduleInspectorDispatch(state);
        } else {
            cancelInspectorDispatch(state);
            state.pendingInspectorHoverId = null;
        }
        updateDiagnosticsHud(state);
    }

    function destroyDiagnosticsHud(state) {
        const hud = state?.diagnosticsHud;
        if (!hud) {
            return;
        }

        if (hud.updateTimerId) {
            clearInterval(hud.updateTimerId);
        }
        hud.root?.remove();
        hud.collapsedChip?.remove();
        hud.binDumpChip?.remove();
        state.diagnosticsHud = null;
        state.diagnosticsHudCollapsed = false;
    }

    function createDiagnosticsHud(canvas, state) {
        if (!canvas || !state) {
            return null;
        }

        const host = canvas.parentElement ?? canvas;
        const panel = document.createElement('div');
        panel.className = 'topology-diag-panel';

        const header = document.createElement('div');
        header.className = 'topology-diag-panel__header';
        const title = document.createElement('span');
        title.className = 'topology-diag-panel__title';
        title.textContent = 'Hover diagnostics';
        const dumpButton = document.createElement('button');
        dumpButton.type = 'button';
        dumpButton.className = 'topology-diag-panel__chip';
        dumpButton.textContent = 'Dump';
        dumpButton.addEventListener('click', () => {
            const hoverPayload = buildHoverDiagnosticsPayload(state, 'manual');
            downloadDiagnosticsPayload(hoverPayload);
            uploadDiagnosticsPayload(state, hoverPayload);
            const canvasPayload = buildCanvasDiagnosticsPayload(state, 'manual-canvas');
            uploadDiagnosticsPayload(state, canvasPayload);
            clearHoverStats(state);
            resetCanvasStats(state);
        });

        const collapseButton = document.createElement('button');
        collapseButton.type = 'button';
        collapseButton.className = 'topology-diag-panel__chip topology-diag-panel__chip--ghost';
        collapseButton.textContent = 'Hide';
        collapseButton.addEventListener('click', () => setDiagnosticsHudCollapsed(state, true));

        const actionGroup = document.createElement('div');
        actionGroup.className = 'topology-diag-panel__actions';
        actionGroup.append(dumpButton, collapseButton);
        header.append(title, actionGroup);
        panel.appendChild(header);

        const metrics = document.createElement('dl');
        metrics.className = 'topology-diag-panel__metrics';
        const fields = [
            { key: 'total', label: 'Samples', defaultValue: '0' },
            { key: 'frame', label: 'Frame (avg/max)', defaultValue: '0 / 0 ms' },
            { key: 'fps', label: 'Frame rate', defaultValue: '0 fps' },
            { key: 'throttle', label: 'Throttle', defaultValue: '0%' },
            { key: 'pointer', label: 'Pointer (ok/recv/drop)', defaultValue: '0 / 0 / 0' },
            { key: 'scene', label: 'Scene / Overlay', defaultValue: '0 / 0' },
            { key: 'layout', label: 'Layout Reads', defaultValue: '0' },
            { key: 'edgeCandidates', label: 'Edge candidates (avg/last)', defaultValue: '0 / 0' },
            { key: 'edgeCache', label: 'Edge cache (hit/miss)', defaultValue: '0 / 0' },
            { key: 'inp', label: 'Pointer INP (avg/max)', defaultValue: '0 / 0 ms' },
            { key: 'elapsed', label: 'Elapsed', defaultValue: '0 ms' }
        ];
        const valueRefs = {};
        for (const field of fields) {
            const labelEl = document.createElement('dt');
            labelEl.className = 'topology-diag-panel__label';
            labelEl.textContent = field.label;
            const valueEl = document.createElement('dd');
            valueEl.className = 'topology-diag-panel__value';
            valueEl.textContent = field.defaultValue;
            metrics.append(labelEl, valueEl);
            valueRefs[field.key] = valueEl;
        }
        panel.appendChild(metrics);

        host.appendChild(panel);
        const collapsedChip = createCollapsedChip(host, state);
        const binDumpChip = createBinDumpChip(host, state);

        const hud = {
            root: panel,
            values: valueRefs,
            updateTimerId: null,
            collapsedChip,
            binDumpChip
        };

        state.diagnosticsHud = hud;
        hud.updateTimerId = window.setInterval(() => updateDiagnosticsHud(state), DIAGNOSTICS_UPDATE_INTERVAL_MS);
        clearHoverStats(state);
        state.diagnosticsHudCollapsed = loadDiagnosticsCollapsePref();
        applyDiagnosticsHudVisibility(state);
        configureDiagnosticsUploader(state);
        return hud;
    }

    function updateDiagnosticsHud(state) {
        const hud = state?.diagnosticsHud;
        const stats = state?.hoverStats;
        if (!hud || !stats) {
            return;
        }

        const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
            ? performance.now()
            : Date.now();
        const windowStart = Number.isFinite(stats.windowStartTimestamp) ? stats.windowStartTimestamp : now;
        const elapsedMs = Math.max(0, now - windowStart);
        const elapsedSeconds = elapsedMs / 1000;
        const windowDispatches = stats.windowDispatches ?? stats.interopDispatches ?? 0;
        const rate = elapsedSeconds > 0
            ? windowDispatches / elapsedSeconds
            : 0;

        if (hud.values.total) {
            hud.values.total.textContent = (stats.interopDispatches ?? 0).toString();
        }
        if (hud.values.elapsed) {
            hud.values.elapsed.textContent = elapsedMs >= 1000
                ? `${(elapsedMs / 1000).toFixed(2)} s`
                : `${elapsedMs.toFixed(0)} ms`;
        }
        const drawStats = state?.drawStats ?? createDrawStats();
        const frames = drawStats.frames || 0;
        const avgDraw = frames > 0 ? drawStats.totalDurationMs / frames : 0;
        const maxDraw = drawStats.maxDurationMs || 0;
        if (hud.values.frame) {
            hud.values.frame.textContent = `${avgDraw.toFixed(1)} / ${maxDraw.toFixed(1)} ms`;
            setSeverityClass(hud.values.frame, maxDraw, 8, 16);
        }
        if (hud.values.fps) {
            const lastSampleTime = state.lastHudSampleTime ?? now;
            const deltaMs = Math.max(1, now - lastSampleTime);
            const frameDelta = Math.max(0, frames - (state.lastHudFrameCount ?? 0));
            let smoothedFps;
            if (frameDelta === 0) {
                smoothedFps = state.smoothedFps = 0;
            } else {
                const instantaneousFps = (frameDelta * 1000) / deltaMs;
                smoothedFps = state.smoothedFps = typeof state.smoothedFps === 'number'
                    ? (state.smoothedFps * 0.8) + (instantaneousFps * 0.2)
                    : instantaneousFps;
            }
            hud.values.fps.textContent = `${(smoothedFps ?? 0).toFixed(1)} fps`;
            state.lastHudFrameCount = frames;
            state.lastHudSampleTime = now;
            setSeverityClass(hud.values.fps, smoothedFps ?? 0, 30, 20, true, 45);
        }

        const throttleSkips = state?.pointerThrottleSkips ?? 0;
        const pointerStats = state?.pointerStats ?? createPointerStats();
        const totalSamples = throttleSkips + (pointerStats.received ?? 0);
        const throttlePct = totalSamples > 0
            ? (throttleSkips / totalSamples) * 100
            : 0;
        if (hud.values.throttle) {
            hud.values.throttle.textContent = `${throttlePct.toFixed(1)}%`;
            setSeverityClass(hud.values.throttle, throttlePct, 60, 85, false, 20);
        }

        const pointerReceived = pointerStats.received ?? 0;
        const pointerProcessed = pointerStats.processed ?? 0;
        const pointerDrops = pointerStats.queueDrops ?? 0;
        const intentSkips = pointerStats.intentSkips ?? 0;
        const dropPct = pointerReceived > 0 ? (pointerDrops / pointerReceived) * 100 : 0;
        if (hud.values.pointer) {
            const intentPart = intentSkips > 0 ? ` (intent ${intentSkips})` : '';
            hud.values.pointer.textContent = `${pointerProcessed} / ${pointerReceived} / ${pointerDrops}${intentPart}`;
            setSeverityClass(hud.values.pointer, dropPct, 25, 50, false, 10);
        }

        const sceneStats = state?.sceneStats ?? createSceneStats();
        if (hud.values.scene) {
            const rebuilds = sceneStats.rebuilds ?? 0;
            const overlayUpdates = sceneStats.overlayUpdates ?? 0;
            hud.values.scene.textContent = `${rebuilds} / ${overlayUpdates}`;
            setSeverityClass(hud.values.scene, null);
        }

        const layoutStats = state?.layoutStats ?? createLayoutStats();
        if (hud.values.layout) {
            const reads = layoutStats.reads ?? 0;
            hud.values.layout.textContent = `${reads}`;
            setSeverityClass(hud.values.layout, null);
        }

        const edgeStats = collectEdgeSpatialDiagnostics(state);
        if (hud.values.edgeCandidates) {
            const avgCandidates = Number(edgeStats.edgeCandidatesAverage ?? 0);
            const lastCandidates = Number(edgeStats.edgeCandidatesLast ?? 0);
            hud.values.edgeCandidates.textContent = `${avgCandidates.toFixed(2)} / ${lastCandidates.toFixed(2)} (cell ${edgeStats.edgeGridCellSize ?? '?'}px)`;
            setSeverityClass(hud.values.edgeCandidates, avgCandidates, 8, 16, false, 4);
        }
        if (hud.values.edgeCache) {
            const hits = Number(edgeStats.edgeCacheHits ?? 0);
            const misses = Number(edgeStats.edgeCacheMisses ?? 0);
            hud.values.edgeCache.textContent = `${hits} / ${misses}`;
            const cacheMissTotal = hits + misses;
            const missRatioPct = cacheMissTotal > 0 ? (misses / cacheMissTotal) * 100 : 0;
            setSeverityClass(hud.values.edgeCache, missRatioPct, 50, 75, false, 15);
        }

        const pointerInpStats = state?.pointerInpStats ?? createPointerInpStats();
        const inpSamples = pointerInpStats.samples ?? 0;
        const inpAvg = inpSamples > 0
            ? pointerInpStats.totalMs / inpSamples
            : pointerInpStats.lastMs ?? 0;
        const inpMax = pointerInpStats.maxMs ?? 0;
        if (hud.values.inp) {
            hud.values.inp.textContent = `${inpAvg.toFixed(1)} / ${inpMax.toFixed(1)} ms`;
            setSeverityClass(hud.values.inp, inpAvg, 50, 100, false, 16);
        }

        const panDistance = state?.panStats?.distance ?? 0;
        const zoomEvents = state?.zoomStats?.events ?? 0;
        if (hud.values.panzoom) {
            hud.values.panzoom.textContent = `${Math.round(panDistance)}px / ${zoomEvents}`;
            setSeverityClass(hud.values.panzoom, null);
        }
    }

    function setSeverityClass(element, value, warnThreshold, badThreshold, reverse = false, goodThreshold) {
        if (!element) {
            return;
        }
        element.classList.remove('topology-diag-panel__value--warn', 'topology-diag-panel__value--bad', 'topology-diag-panel__value--good');
        if (value === null || value === undefined || !Number.isFinite(value)) {
            return;
        }

        let applied = false;
        if (reverse) {
            if (badThreshold !== undefined && value <= badThreshold) {
                element.classList.add('topology-diag-panel__value--bad');
                applied = true;
            } else if (warnThreshold !== undefined && value <= warnThreshold) {
                element.classList.add('topology-diag-panel__value--warn');
                applied = true;
            }
            if (!applied && goodThreshold !== undefined && value >= goodThreshold) {
                element.classList.add('topology-diag-panel__value--good');
                applied = true;
            }
            return;
        }

        if (badThreshold !== undefined && value >= badThreshold) {
            element.classList.add('topology-diag-panel__value--bad');
            applied = true;
        } else if (warnThreshold !== undefined && value >= warnThreshold) {
            element.classList.add('topology-diag-panel__value--warn');
            applied = true;
        }
        if (!applied && goodThreshold !== undefined && value <= goodThreshold) {
            element.classList.add('topology-diag-panel__value--good');
        }
    }

    function formatRunDisplay(runId) {
        if (!runId || typeof runId !== 'string') {
            return 'n/a';
        }

        const trimmed = runId.trim();
        if (!trimmed) {
            return 'n/a';
        }

        if (trimmed.length <= 8) {
            return trimmed;
        }

        const lastChunk = trimmed.slice(-8);
        return `…${lastChunk}`;
    }

    function configureDiagnosticsUploader(state) {
        const hud = state?.diagnosticsHud;
        const options = state?.diagnosticsOptions;

        if (state?.diagnosticsUploadHandle) {
            clearInterval(state.diagnosticsUploadHandle);
            state.diagnosticsUploadHandle = null;
        }

        if (!options || !options.uploadUrl || options.uploadIntervalMs <= 0) {
            if (hud?.statusValue) {
                hud.statusValue.textContent = 'Off';
            }
            return;
        }

        const interval = Math.max(DIAGNOSTICS_MIN_UPLOAD_INTERVAL_MS, options.uploadIntervalMs);
        if (hud?.statusValue) {
            hud.statusValue.textContent = `${(interval / 1000).toFixed(0)}s`;
        }

        state.diagnosticsUploadHandle = window.setInterval(() => {
            const hoverPayload = buildHoverDiagnosticsPayload(state, 'auto');
            uploadDiagnosticsPayload(state, hoverPayload);
            const canvasPayload = buildCanvasDiagnosticsPayload(state, 'auto-canvas');
            uploadDiagnosticsPayload(state, canvasPayload);
        }, interval);
    }

    function collectEdgeSpatialDiagnostics(state) {
        const stats = state?.edgeSpatialStats ?? createEdgeSpatialStats(state?.edgeSpatialIndex?.cellSize ?? EDGE_SPATIAL_INDEX_CELL_SIZE);
        const averageCandidates = stats.samples > 0
            ? stats.candidateTotal / stats.samples
            : 0;

        return {
            edgeCandidatesLast: Number((stats.lastCandidates ?? 0).toFixed(2)),
            edgeCandidatesAverage: Number(averageCandidates.toFixed(2)),
            edgeCandidateSamples: Number(stats.samples ?? 0),
            edgeCandidateFallbacks: Number(stats.fallbackSamples ?? 0),
            edgeGridCellSize: Number((stats.cellSize ?? state?.edgeSpatialIndex?.cellSize ?? EDGE_SPATIAL_INDEX_CELL_SIZE).toFixed(2)),
            edgeCacheHits: Number(stats.cacheHits ?? 0),
            edgeCacheMisses: Number(stats.cacheMisses ?? 0)
        };
    }

    function buildHoverDiagnosticsPayload(state, source) {
        const stats = state?.hoverStats ?? createHoverStats();
        const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
            ? performance.now()
            : Date.now();
        const windowStart = Number.isFinite(stats.windowStartTimestamp) ? stats.windowStartTimestamp : now;
        const elapsedMs = Math.max(0, now - windowStart);
        const elapsedSeconds = elapsedMs / 1000;
        const dispatches = stats.windowDispatches ?? stats.interopDispatches ?? 0;
        const rate = elapsedSeconds > 0
            ? dispatches / elapsedSeconds
            : 0;
        const canvasWidth = state?.canvasWidth ?? null;
        const canvasHeight = state?.canvasHeight ?? null;
        const operationalOnly = state?.operationalViewOnly === true;
        const zoomPercent = Number(state?.overlaySettings?.zoomPercent ?? (state?.scale ?? 1) * 100);

        const pointerThrottleSkips = Number(state?.pointerThrottleSkipsSinceUpload ?? state?.pointerThrottleSkips ?? 0);

        const pointerStats = state?.pointerStatsSinceUpload ?? state?.pointerStats ?? createPointerStats();
        const dragStats = state?.dragStatsSinceUpload ?? state?.dragStats ?? createDragStats();
        const dragFrames = dragStats.frames || 0;
        const dragAvgMs = dragFrames > 0 ? dragStats.totalDurationMs / dragFrames : 0;
        const sceneStats = state?.sceneStatsSinceUpload ?? state?.sceneStats ?? createSceneStats();
        const layoutStats = state?.layoutStatsSinceUpload ?? state?.layoutStats ?? createLayoutStats();
        const pointerInpStats = state?.pointerInpStatsSinceUpload ?? state?.pointerInpStats ?? createPointerInpStats();
        const pointerInpSamples = pointerInpStats.samples ?? 0;
        const pointerInpAvg = pointerInpSamples > 0
            ? pointerInpStats.totalMs / pointerInpSamples
            : pointerInpStats.lastMs ?? 0;
        const pointerInpMax = pointerInpStats.maxMs ?? 0;

        const payload = {
            runId: state?.runId ?? null,
            buildHash: state?.buildHash ?? null,
            payloadSignature: state?.lastViewportSignature ?? null,
            interopDispatches: dispatches,
            durationMs: Number(elapsedMs.toFixed(2)),
            totalDispatches: stats.interopDispatches ?? dispatches,
            ratePerSecond: Number(rate.toFixed(2)),
            timestampUtc: new Date().toISOString(),
            source: source ?? 'hover',
            canvasWidth,
            canvasHeight,
            operationalOnly,
            mode: operationalOnly ? 'operational' : 'full',
            neighborEmphasis: Boolean(state?.overlaySettings?.neighborEmphasis),
            zoomPercent: Number.isFinite(zoomPercent) ? Number(zoomPercent.toFixed(2)) : null,
            hoverCacheDisabled: state?.disableHoverCache === true,
            hoveredNodeId: state?.hoveredNodeId ?? null,
            focusedNodeId: state?.focusedNodeId ?? null,
            nodeCount: state?.lastNodeCount ?? null,
            edgeCount: state?.lastEdgeCount ?? null,
            inspectorVisible: Boolean(state?.inspectorVisible),
            pointerThrottleSkips,
            pointerEventsReceived: Number(pointerStats.received ?? 0),
            pointerEventsProcessed: Number(pointerStats.processed ?? 0),
            pointerQueueDrops: Number(pointerStats.queueDrops ?? 0),
            pointerIntentSkips: Number(pointerStats.intentSkips ?? 0),
            dragFrameCount: dragFrames,
            dragTotalDurationMs: Number(dragStats.totalDurationMs.toFixed(3)),
            dragAverageFrameMs: Number(dragAvgMs.toFixed(3)),
            dragMaxFrameMs: Number((dragStats.maxDurationMs ?? 0).toFixed(3)),
            sceneRebuilds: Number(sceneStats.rebuilds ?? 0),
            overlayUpdates: Number(sceneStats.overlayUpdates ?? 0),
            layoutReads: Number(layoutStats.reads ?? 0),
            pointerInpSampleCount: Number(pointerInpSamples),
            pointerInpAverageMs: Number((pointerInpAvg ?? 0).toFixed(3)),
            pointerInpMaxMs: Number(pointerInpMax.toFixed(3))
        };

        Object.assign(payload, collectEdgeSpatialDiagnostics(state));

        payload.canvas = {
            width: canvasWidth,
            height: canvasHeight
        };

        stats.windowStartTimestamp = now;
        stats.windowDispatches = 0;
        state.pointerThrottleSkipsSinceUpload = 0;
        resetPointerStatsSinceUpload(state);
        resetSceneStatsSinceUpload(state);
        resetLayoutStatsSinceUpload(state);
        resetPointerInpStatsSinceUpload(state);

        return payload;
    }

    function buildCanvasDiagnosticsPayload(state, source) {
        const canvasWidth = state?.canvasWidth ?? null;
        const canvasHeight = state?.canvasHeight ?? null;
        const operationalOnly = state?.operationalViewOnly === true;
        const zoomPercent = Number(state?.overlaySettings?.zoomPercent ?? (state?.scale ?? 1) * 100);
        const drawStats = state?.drawStatsSinceUpload ?? state?.drawStats ?? createDrawStats();
        const frames = drawStats.frames || 0;
        const avgDrawMs = frames > 0 ? drawStats.totalDurationMs / frames : 0;
        const dragStats = state?.dragStatsSinceUpload ?? state?.dragStats ?? createDragStats();
        const dragFrames = dragStats.frames || 0;
        const dragAvgMs = dragFrames > 0 ? dragStats.totalDurationMs / dragFrames : 0;

        const payload = {
            runId: state?.runId ?? null,
            buildHash: state?.buildHash ?? null,
            payloadSignature: state?.lastViewportSignature ?? null,
            nodeCount: state?.lastNodeCount ?? null,
            edgeCount: state?.lastEdgeCount ?? null,
            avgDrawMs: Number(avgDrawMs.toFixed(3)),
            maxDrawMs: Number(drawStats.maxDurationMs.toFixed(3)),
            lastDrawMs: Number(drawStats.lastDurationMs.toFixed(3)),
            frameCount: frames,
            panDistance: Number((state?.panStatsSinceUpload?.distance ?? state?.panStats?.distance ?? 0).toFixed(2)),
            zoomEvents: state?.zoomStatsSinceUpload?.events ?? state?.zoomStats?.events ?? 0,
            dragFrameCount: dragFrames,
            dragTotalDurationMs: Number(dragStats.totalDurationMs.toFixed(3)),
            dragAverageFrameMs: Number(dragAvgMs.toFixed(3)),
            dragMaxFrameMs: Number((dragStats.maxDurationMs ?? 0).toFixed(3)),
            timestampUtc: new Date().toISOString(),
            source: source ?? 'canvas',
            canvasWidth,
            canvasHeight,
            operationalOnly,
            mode: operationalOnly ? 'operational' : 'full',
            neighborEmphasis: Boolean(state?.overlaySettings?.neighborEmphasis),
            zoomPercent: Number.isFinite(zoomPercent) ? Number(zoomPercent.toFixed(2)) : null,
            inspectorVisible: Boolean(state?.inspectorVisible),
            ...collectEdgeSpatialDiagnostics(state)
        };

        payload.canvas = {
            width: canvasWidth,
            height: canvasHeight
        };

        resetCanvasStatsSinceUpload(state);

        return payload;
    }

    function downloadDiagnosticsPayload(payload) {
        if (!payload) {
            return;
        }

        const json = JSON.stringify(payload, null, 2);
        const timestamp = (payload.timestampUtc || new Date().toISOString()).replace(/[:.]/g, '-');
        const runSegment = (payload.runId || 'hover').replace(/[^\w.-]+/g, '_');
        const fileName = `hover-diagnostics_${runSegment}_${timestamp}.json`;
        const blob = new Blob([json], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
    }

    function uploadDiagnosticsPayload(state, payload) {
        if (!state?.diagnosticsOptions?.uploadUrl || !payload || typeof fetch !== 'function') {
            return;
        }

        try {
            fetch(state.diagnosticsOptions.uploadUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            }).catch(() => { /* ignored */ });
        } catch {
            // Swallow network errors; diagnostics should never block user interaction.
        }
    }

    const hotkeyHandlers = new Map();
    let hotkeyCounter = 0;

    function registerHotkeys(dotNetRef) {
        if (!dotNetRef) {
            return 0;
        }

        const handler = (event) => {
            if (!event.altKey) {
                return;
            }

            const key = event.key || event.code;
            if (!key) {
                return;
            }

            if (key.toLowerCase() === 't') {
                event.preventDefault();
                dotNetRef.invokeMethodAsync('ToggleFeatureBar');
            }
        };

        const id = ++hotkeyCounter;
        hotkeyHandlers.set(id, { handler });
        window.addEventListener('keydown', handler, true);
        return id;
    }

    function unregisterHotkeys(id) {
        const entry = hotkeyHandlers.get(id);
        if (!entry) {
            return;
        }

        window.removeEventListener('keydown', entry.handler, true);
        hotkeyHandlers.delete(id);
    }

    function getHoverDiagnostics(canvas) {
        const state = registry.get(canvas);
        if (!state) {
            return null;
        }

        return buildHoverDiagnosticsPayload(state, 'snapshot');
    }

    function resetHoverDiagnostics(canvas) {
        const state = registry.get(canvas);
        if (!state) {
            return;
        }

        clearHoverStats(state);
        resetCanvasStats(state);
    }

    function getCanvasDiagnostics(canvas, source) {
        const state = registry.get(canvas);
        if (!state) {
            return null;
        }

        return buildCanvasDiagnosticsPayload(state, source ?? 'snapshot-canvas');
    }

    window.FlowTime = window.FlowTime || {};
    window.FlowTime.TopologyCanvas = {
        render,
        renderScene,
        applyOverlayDelta,
        dispose,
        restoreViewport,
        fitToViewport,
        resetViewportState,
        getHoverDiagnostics,
        getCanvasDiagnostics,
        dumpHoverDiagnostics: (canvas) => {
            const state = registry.get(canvas);
            if (!state) {
                return null;
            }
            const payload = buildHoverDiagnosticsPayload(state, 'api');
            downloadDiagnosticsPayload(payload);
            uploadDiagnosticsPayload(state, payload);
            const canvasPayload = buildCanvasDiagnosticsPayload(state, 'api-canvas');
            uploadDiagnosticsPayload(state, canvasPayload);
            return payload;
        },
        resetHoverDiagnostics,
        setInspectorEdgeHover: (canvas, edgeId) => setInspectorEdgeHoverState(canvas, edgeId),
        focusEdge: (canvas, edgeId, centerOnEdge) => focusEdgeOnCanvas(canvas, edgeId, centerOnEdge),
        registerHandlers: (canvas, dotNetRef, diagnosticsOptions) => {
            const state = getState(canvas);
            if (!state) {
                return;
            }
            state.dotNetRef = dotNetRef;
            applyDiagnosticsOptions(canvas, state, diagnosticsOptions);
        },
        setInspectorVisible: (canvas, isVisible) => setInspectorVisibility(canvas, isVisible),
        setDebugLoggingEnabled: (enabled) => setGlobalDebugLogging(Boolean(enabled))
    };
    window.FlowTime.TopologyHotkeys = {
        register: registerHotkeys,
        unregister: unregisterHotkeys
    };
})();
