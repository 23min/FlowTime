(function () {
    const registry = new WeakMap();
    const EdgeTypeTopology = 'topology';
    const EdgeTypeDependency = 'dependency';
    const MIN_ZOOM_PERCENT = 25;
    const MAX_ZOOM_PERCENT = 200;
    const MIN_SCALE = MIN_ZOOM_PERCENT / 100;
    const MAX_SCALE = MAX_ZOOM_PERCENT / 100;
    const LEAF_CIRCLE_SCALE = 1.5;
    const LEAF_CIRCLE_RING_WIDTH = 3;
    const LEAF_CIRCLE_GUTTER = 2;
    const LEAF_CIRCLE_FILL = '#E2E8F0';
    const LEAF_CIRCLE_STROKE = '#1F2937';

    function getState(canvas) {
        let state = registry.get(canvas);
        if (!state) {
            const ctx = canvas.getContext('2d');
            state = {
                ctx,
                payload: null,
                offsetX: 0,
                offsetY: 0,
                scale: 1,
                dragging: false,
                dragStart: { x: 0, y: 0 },
                panStart: { x: 0, y: 0 },
                deviceRatio: window.devicePixelRatio || 1,
                overlayScale: 1,
                lastOverlayZoom: null,
                overlaySettings: null,
                userAdjusted: false,
                viewportSignature: null,
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
                chipHitboxes: [],
                hoveredChipId: null,
                hoveredChip: null
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

            updateWorldCenter(state);

            draw(canvas, state);

            emitViewportChanged(canvas, state);
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

        const clearHover = () => {
            if (state.hoveredChipId !== null || state.hoveredChip !== null) {
                state.hoveredChipId = null;
                state.hoveredChip = null;
                draw(canvas, state);
            }
        };

        const updateHover = (event) => {
            if (!state.payload || !state.chipHitboxes || state.chipHitboxes.length === 0) {
                if (state.hoveredChipId !== null || state.hoveredChip !== null) {
                    state.hoveredChipId = null;
                    state.hoveredChip = null;
                    draw(canvas, state);
                }
                return;
            }

            const rect = canvas.getBoundingClientRect();
            const clientX = event.clientX - rect.left;
            const clientY = event.clientY - rect.top;
            const worldX = (clientX - state.offsetX) / state.scale;
            const worldY = (clientY - state.offsetY) / state.scale;
            const hit = hitTestChip(state, worldX, worldY);
            const nextId = hit ? hit.id : null;

            if (nextId !== state.hoveredChipId) {
                state.hoveredChipId = nextId;
                draw(canvas, state);
            }
        };

        const pointerDown = (event) => {
            canvas.setPointerCapture(event.pointerId);
            state.dragging = true;
            state.dragStart = { x: event.clientX, y: event.clientY };
            state.panStart = { x: state.offsetX, y: state.offsetY };
            state.userAdjusted = true;
            clearHover();
        };

        const pointerMove = (event) => {
            if (!state.dragging) {
                updateHover(event);
                return;
            }
            const dx = event.clientX - state.dragStart.x;
            const dy = event.clientY - state.dragStart.y;
            state.offsetX = state.panStart.x + dx;
            state.offsetY = state.panStart.y + dy;
            draw(canvas, state);
        };

        const pointerUp = (event) => {
            canvas.releasePointerCapture(event.pointerId);
            state.dragging = false;
            updateWorldCenter(state);
            emitViewportChanged(canvas, state);
            updateHover(event);
        };

        const pointerLeave = (event) => {
            if (canvas.hasPointerCapture?.(event.pointerId)) {
                canvas.releasePointerCapture(event.pointerId);
            }
            const wasDragging = state.dragging;
            state.dragging = false;
            if (wasDragging) {
                updateWorldCenter(state);
                emitViewportChanged(canvas, state);
            }
            clearHover();
        };

        const wheel = (event) => {
            event.preventDefault();

            const { offsetX, offsetY, deltaY } = event;
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

            updateWorldCenter(state);

            draw(canvas, state);
            updateHover(event);
        };

        canvas.addEventListener('pointerdown', pointerDown);
        canvas.addEventListener('pointermove', pointerMove);
        canvas.addEventListener('pointerup', pointerUp);
        canvas.addEventListener('pointerleave', pointerLeave);
        canvas.addEventListener('wheel', wheel, { passive: false });

        state.cleanup = () => {
            window.removeEventListener('resize', resize);
            canvas.removeEventListener('pointerdown', pointerDown);
            canvas.removeEventListener('pointermove', pointerMove);
            canvas.removeEventListener('pointerup', pointerUp);
            canvas.removeEventListener('pointerleave', pointerLeave);
            canvas.removeEventListener('wheel', wheel);
            state.resizeObserver?.disconnect?.();
            state.resizeObserver = null;
            state.dotNetRef = null;
        };
    }

    function draw(canvas, state) {
        if (!state.payload) {
            console.info('[TopologyCanvas] draw skipped (no payload)');
            clear(state);
            state.chipHitboxes = [];
            state.hoveredChipId = null;
            state.hoveredChip = null;
            return;
        }

        const ctx = state.ctx;
        clear(state);
        state.chipHitboxes = [];

        const nodes = state.payload.nodes ?? state.payload.Nodes ?? [];
        const edges = state.payload.edges ?? state.payload.Edges ?? [];
        console.info('[TopologyCanvas] draw', { nodes: nodes.length, edges: edges.length });
        const overlaySettings = state.overlaySettings ?? parseOverlaySettings(state.payload.overlays ?? state.payload.Overlays ?? {});
        const tooltip = state.payload.tooltip ?? state.payload.Tooltip ?? null;

        // Sync overlay DOM (proxies + tooltip) with canvas pan/zoom so hover/focus hitboxes and callouts align
        applyOverlayTransform(canvas, state);

        const outgoingCounts = new Map();
        for (const edge of edges) {
            const type = String(edge.edgeType ?? edge.EdgeType ?? EdgeTypeTopology).toLowerCase();
            if (type !== EdgeTypeTopology) {
                continue;
            }

            const fromId = edge.from ?? edge.From;
            if (!fromId) {
                continue;
            }

            outgoingCounts.set(fromId, (outgoingCounts.get(fromId) ?? 0) + 1);
        }

        const nodeMap = new Map();
        for (const n of nodes) {
            const identifier = n.id ?? n.Id;
            const outgoing = outgoingCounts.get(identifier) ?? 0;

            nodeMap.set(identifier, {
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
                fill: n.fill ?? n.Fill ?? '#7A7A7A',
                focusLabel: n.focusLabel ?? n.FocusLabel ?? '',
                semantics: n.semantics ?? n.Semantics ?? null,
                distribution: n.distribution ?? n.Distribution ?? (n.semantics?.distribution ?? n.Semantics?.Distribution ?? null),
                leaf: outgoing === 0
            });
        }

        // Synthesize sparklines from inline values for const nodes when not provided
        for (const [id, meta] of nodeMap.entries()) {
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

        const emphasisEnabled = overlaySettings.neighborEmphasis && focusedId;
        const neighborNodes = emphasisEnabled ? computeNeighborNodes(edges, focusedId) : null;
        const neighborEdges = emphasisEnabled ? computeNeighborEdges(edges, focusedId) : null;

        const defaultEdgeAlpha = 0.85;

        for (const edge of edges) {
            const fromX = edge.fromX ?? edge.FromX;
            const fromY = edge.fromY ?? edge.FromY;
            const toX = edge.toX ?? edge.ToX;
            const toY = edge.toY ?? edge.ToY;

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

            const edgeType = String(edge.edgeType ?? edge.EdgeType ?? EdgeTypeTopology).toLowerCase();
            if (edgeType === EdgeTypeDependency) {
                const field = String(edge.field ?? edge.Field ?? '').toLowerCase();
                if (!shouldRenderDependencyEdge(field, overlaySettings)) {
                    continue;
                }
            }

            const edgeId = edge.id ?? edge.Id ?? `${fromId}->${toId}`;
            const highlightEdge = !emphasisEnabled || (neighborEdges?.has(edgeId) ?? false);
            const edgeAlpha = highlightEdge ? defaultEdgeAlpha : defaultEdgeAlpha * 0.25;
            const strokeColor = highlightEdge ? '#9AA1AC' : 'rgba(154, 161, 172, 0.35)';

            const offset = edgeLaneOffset(edge);
            let pathPoints;
            let startPoint;
            let endPoint;

        ctx.save();
        ctx.globalAlpha = edgeAlpha;
        const isDependency = edgeType === EdgeTypeDependency;
        ctx.lineWidth = isDependency ? 1.4 : 3;
        ctx.strokeStyle = strokeColor;
        if (isDependency) {
            ctx.setLineDash([4, 3]);
        }

            if (overlaySettings.edgeStyle === 'bezier') {
                const path = computeBezierPath(fromNode, toNode, offset, portPadding);
                ctx.beginPath();
                ctx.moveTo(path.start.x, path.start.y);
                ctx.bezierCurveTo(path.cp1.x, path.cp1.y, path.cp2.x, path.cp2.y, path.end.x, path.end.y);
                ctx.stroke();
                pathPoints = path.samples;
                startPoint = path.start;
                endPoint = path.end;
            } else {
                const dx = toX - fromX;
                const dy = toY - fromY;
                const len = Math.hypot(dx, dy) || 1;
                const ux = dx / len;
                const uy = dy / len;

                const startShrink = computePortOffset(fromNode, ux, uy, portPadding);
                const endShrink = computePortOffset(toNode, -ux, -uy, portPadding);
                const sx = fromX + ux * startShrink;
                const sy = fromY + uy * startShrink;
                const ex = toX - ux * endShrink;
                const ey = toY - uy * endShrink;

                const path = computeElbowPath(sx, sy, ex, ey, offset);

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

            if (isDependency) {
                ctx.setLineDash([]);
            }

            ctx.restore();
        }

        ctx.globalAlpha = 1;

        const dimmedAlpha = 0.35;

        for (const node of nodes) {
            const id = node.id ?? node.Id;
            const meta = nodeMap.get(id);
            const isVisible = meta?.visible !== false;

            if (!isVisible) {
                continue;
            }

            const x = node.x ?? node.X;
            const y = node.y ?? node.Y;
            const width = node.width ?? node.Width ?? 54;
            const height = node.height ?? node.Height ?? 24;
            const cornerRadius = node.cornerRadius ?? node.CornerRadius ?? 3;
            const fill = node.fill ?? node.Fill ?? '#7A7A7A';
            const stroke = node.stroke ?? node.Stroke ?? '#262626';

            const highlightNode = !emphasisEnabled || (neighborNodes?.has(id) ?? false);
            const nodeAlpha = highlightNode ? 1 : dimmedAlpha;

            ctx.save();
            ctx.globalAlpha = nodeAlpha;

            const nodeMeta = nodeMap.get(id);
            const kind = String(meta?.kind ?? node.kind ?? node.Kind ?? 'service').toLowerCase();
            const isLeafNode = !!nodeMeta?.leaf;
            const isComputedNode = isComputedKind(kind);
            const drawCircle = isLeafNode && isComputedNode;

            let focusLabelWidth = Math.max(width - 14, 18);
            let fillForText = fill;

            if (drawCircle) {
                const baseRadius = Math.min(width, height) / 2;
                const outerRadius = baseRadius * LEAF_CIRCLE_SCALE;
                const ringWidth = LEAF_CIRCLE_RING_WIDTH;
                const gutter = LEAF_CIRCLE_GUTTER;
                const innerRadius = Math.max(outerRadius - ringWidth - gutter, outerRadius * 0.6);

                ctx.save();
                ctx.lineWidth = ringWidth;
                ctx.strokeStyle = LEAF_CIRCLE_STROKE;
                ctx.beginPath();
                ctx.arc(x, y, outerRadius - (ringWidth / 2), 0, Math.PI * 2);
                ctx.stroke();
                ctx.restore();

                ctx.beginPath();
                ctx.fillStyle = LEAF_CIRCLE_FILL;
                ctx.arc(x, y, innerRadius, 0, Math.PI * 2);
                ctx.fill();

                if (nodeMeta) {
                    nodeMeta.fill = LEAF_CIRCLE_FILL;
                    nodeMeta.outerRadius = outerRadius;
                    nodeMeta.innerRadius = innerRadius;
                }

                focusLabelWidth = Math.max((innerRadius * 2) - 10, 18);
                fillForText = LEAF_CIRCLE_FILL;
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
                ctx.fillStyle = fill;
                ctx.strokeStyle = stroke;
                ctx.lineWidth = 0.9;
                ctx.fill();
                ctx.stroke();
                if (kind === 'queue') {
                    const pad = 3;
                    ctx.save();
                    ctx.fillStyle = 'rgba(17, 17, 17, 0.08)';
                    ctx.fillRect(x - width / 2 + pad, y + height / 2 - 6 - pad, width - 2 * pad, 4);
                    ctx.restore();
                }

                if (nodeMeta) {
                    nodeMeta.fill = fill;
                }
            }

            if (!drawCircle && (kind === 'service' || kind === 'queue')) {
                drawServiceDecorations(ctx, nodeMeta, overlaySettings, state);
            } else if (!drawCircle && kind === 'pmf' && nodeMeta?.distribution) {
                drawPmfDistribution(ctx, nodeMeta, nodeMeta.distribution);
            } else if (!drawCircle && (kind === 'const' || kind === 'constant') && overlaySettings.showSparklines && nodeMeta?.sparkline) {
                drawInputSparkline(ctx, nodeMeta, overlaySettings);
            }

            if (!drawCircle && kind === 'pmf') {
                ctx.save();
                ctx.fillStyle = isDarkColor(fill) ? '#FFFFFF' : '#0F172A';
                ctx.font = '600 11px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText('PMF', x, y);
                ctx.restore();
            }

            if (overlaySettings.showLabels) {
                ctx.save();
                const label = String(id);
                ctx.fillStyle = '#0F172A';
                ctx.globalAlpha = highlightNode ? 1 : 0.75;
                ctx.font = '10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
                ctx.textAlign = 'right';
                ctx.textBaseline = 'middle';
                const labelX = x - (width / 2) - 8;
                const maxWidth = 140;
                drawFittedText(ctx, label, labelX, y, maxWidth);
                ctx.restore();
            }

            const focusLabel = String(nodeMeta?.focusLabel ?? '').trim();
            if (focusLabel) {
                ctx.save();
                ctx.fillStyle = isDarkColor(fillForText) ? '#FFFFFF' : '#0F172A';
                ctx.globalAlpha = highlightNode ? 1 : 0.85;
                ctx.font = '600 12px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                drawFittedText(ctx, focusLabel, x, y + 1, focusLabelWidth);
                ctx.restore();
            }

            ctx.restore();
        }

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

        ctx.restore();

        if (tooltip) {
            tryDrawTooltip(ctx, nodeMap, tooltip, state);
        }

        if (state.hoveredChip) {
            drawChipTooltip(ctx, state);
        }
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

    function tryDrawTooltip(ctx, nodeMap, tooltip, state) {
        if (!tooltip || !nodeMap || nodeMap.size === 0) {
            return;
        }

        let focusedId = null;
        let focusedMeta = null;
        for (const [id, meta] of nodeMap.entries()) {
            if (meta.isFocused) {
                focusedId = id;
                focusedMeta = meta;
                break;
            }
        }

        if (!focusedMeta) {
            return;
        }

        const title = String(tooltip.title ?? tooltip.Title ?? '');
        const subtitleRaw = String(tooltip.subtitle ?? tooltip.Subtitle ?? '');
        const subtitle = subtitleRaw.trim().length > 0 ? subtitleRaw : '';
        const lines = (tooltip.lines ?? tooltip.Lines ?? [])
            .map(line => String(line).trim())
            .filter(line => line.length > 0);

        const ratio = Number(state.deviceRatio ?? window.devicePixelRatio ?? 1) || 1;
        const toDevice = (value) => Math.round(value * ratio * 1000) / 1000;
        const paddingX = toDevice(12);
        const paddingY = toDevice(6);
        const lineHeight = toDevice(16);
        const fontSizePx = 12 * ratio;
        const fontRegular = `${fontSizePx}px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;
        const fontStrong = `600 ${fontSizePx}px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;
        const overlays = state.overlaySettings ?? {};
        const kind = String(focusedMeta.kind ?? '').toLowerCase();
        const rawSparkline = focusedMeta.sparkline ?? null;
        const preparedSparkline = kind === 'expr' || kind === 'expression'
            ? prepareTooltipSparklineData(rawSparkline)
            : null;
        const hasSparkline = preparedSparkline !== null;
        const sparklineWidth = hasSparkline ? toDevice(90) : 0;
        const sparklineHeight = hasSparkline ? toDevice(26) : 0;
        const sparklineMarginTop = hasSparkline ? toDevice(6) : 0;
        const sparklineMarginBottom = hasSparkline ? toDevice(4) : 0;
        const sparklineBlockHeight = hasSparkline ? sparklineHeight + sparklineMarginTop + sparklineMarginBottom : 0;

        ctx.save();
        ctx.setTransform(1, 0, 0, 1, 0, 0);

        let width = 0;
        ctx.font = fontStrong;
        width = Math.max(width, ctx.measureText(title).width);
        ctx.font = fontRegular;
        if (subtitle) {
            width = Math.max(width, ctx.measureText(subtitle).width);
        }
        for (const line of lines) {
            width = Math.max(width, ctx.measureText(line).width);
        }
        if (hasSparkline) {
            width = Math.max(width, sparklineWidth);
        }

        const textLineCount = 1 + (subtitle ? 1 : 0) + lines.length;
        const boxWidth = Math.ceil(width + paddingX * 2);
        const boxHeight = Math.ceil(paddingY * 2 + textLineCount * lineHeight + sparklineBlockHeight);

        const scale = Number(state.scale ?? 1);
        const offsetX = Number(state.offsetX ?? 0);
        const offsetY = Number(state.offsetY ?? 0);

        const nodeWidth = Number(focusedMeta.width ?? 54);
        const nodeHeight = Number(focusedMeta.height ?? 24);
        const nodeX = Number(focusedMeta.x ?? 0);
        const nodeY = Number(focusedMeta.y ?? 0);
        const nodeCenterScreenX = toDevice(offsetX + scale * nodeX);
        const halfWidthScreen = toDevice((nodeWidth * scale) / 2);

        let badgeBottomWorld = nodeY - (nodeHeight / 2) - 6;
        const spark = focusedMeta.sparkline ?? null;
        if (spark && overlays.showSparklines) {
            const mode = overlays.sparklineMode === 'bar' ? 'bar' : 'line';
            const sparkHeight = mode === 'bar' ? 16 : 12;
            badgeBottomWorld -= (sparkHeight + 6);
        }

        // Position tooltip at constant screen distance to the left of the node, vertically centered
        const nodeCenterScreenY = toDevice(offsetY + scale * nodeY);
        const tooltipTop = Math.round(nodeCenterScreenY - (boxHeight / 2));
        const gap = toDevice(8); // constant px gap from node edge
        let tooltipX = Math.round((nodeCenterScreenX - halfWidthScreen - gap) - boxWidth);
        if (!Number.isFinite(tooltipX)) {
            tooltipX = 0;
        }
        const minMargin = toDevice(12);
        if (tooltipX < minMargin) {
            tooltipX = minMargin;
        }

        const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        const bg = prefersDark ? 'rgba(15, 23, 42, 0.96)' : 'rgba(255, 255, 255, 0.97)';
        const fg = prefersDark ? '#F8FAFC' : '#0F172A';
        const subtitleColor = prefersDark ? '#94A3B8' : '#4B5563';
        const border = prefersDark ? 'rgba(148, 163, 184, 0.35)' : 'rgba(15, 23, 42, 0.12)';

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
            textY += lineHeight;
            ctx.fillStyle = subtitleColor;
            ctx.fillText(subtitle, tooltipX + paddingX, textY);
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
    }

    function prepareTooltipSparklineData(sparkline) {
        if (!sparkline) {
            return null;
        }

        const rawValues = sparkline.values ?? sparkline.Values;
        if (!Array.isArray(rawValues) || rawValues.length === 0) {
            return null;
        }

        const values = new Array(rawValues.length);
        let min = Infinity;
        let max = -Infinity;
        let hasValue = false;

        for (let i = 0; i < rawValues.length; i++) {
            const sample = rawValues[i];
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
            raw: sparkline
        };
    }

    function drawTooltipSparkline(ctx, sparklineData, overlays, x, y, width, height) {
        if (!sparklineData) {
            return false;
        }

        const { values, min, max, length, startIndex, raw } = sparklineData;
        if (!Array.isArray(values) || length === 0) {
            return false;
        }

        const defaultColor = resolveSparklineColor(overlays.colorBasis ?? 0);
        const thresholds = overlays.thresholds ?? {};
        const lastIndex = length - 1;
        const range = max - min;

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

        for (let i = 0; i < length; i++) {
            const sample = values[i];
            if (sample === null || sample === undefined) {
                segmentActive = false;
                continue;
            }

            const fraction = lastIndex <= 0 ? 0 : i / lastIndex;
            const xPos = fraction * width;
            const normalized = range <= 0 ? 0.5 : clamp((sample - min) / range, 0, 1);
            const yPos = height - (normalized * height);

            if (!segmentActive) {
                ctx.moveTo(xPos, yPos);
                segmentActive = true;
            } else {
                ctx.lineTo(xPos, yPos);
            }
        }

        if (!segmentActive && length === 1 && values[0] !== null && values[0] !== undefined) {
            const yPos = height / 2;
            ctx.moveTo(0, yPos);
            ctx.lineTo(width, yPos);
        }

        ctx.stroke();

        const selectedBin = overlays.selectedBin ?? -1;
        const highlightIndex = selectedBin - startIndex;
        if (Number.isInteger(highlightIndex) && highlightIndex >= 0 && highlightIndex < length) {
            const highlightValue = values[highlightIndex];
            if (highlightValue !== null && highlightValue !== undefined) {
                const fraction = lastIndex <= 0 ? 0 : highlightIndex / lastIndex;
                const xPos = fraction * width;
                const normalized = range <= 0 ? 0.5 : clamp((highlightValue - min) / range, 0, 1);
                const yPos = height - (normalized * height);
                const highlightColor = resolveSampleColor(overlays.colorBasis ?? 0, highlightIndex, raw, thresholds, defaultColor);
                ctx.beginPath();
                ctx.fillStyle = highlightColor;
                ctx.strokeStyle = '#FFFFFF';
                ctx.lineWidth = 1;
                ctx.arc(xPos, yPos, 3, 0, Math.PI * 2);
                ctx.fill();
                ctx.stroke();
            }
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

        const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        const background = prefersDark ? 'rgba(15, 23, 42, 0.95)' : 'rgba(255, 255, 255, 0.97)';
        const foreground = prefersDark ? '#F8FAFC' : '#0F172A';
        const border = prefersDark ? 'rgba(148, 163, 184, 0.45)' : 'rgba(15, 23, 42, 0.18)';

        ctx.save();
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.font = `500 ${fontSize}px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;
        ctx.textBaseline = 'middle';
        ctx.textAlign = 'left';

        const textWidth = ctx.measureText(rawText).width;
        const boxWidth = Math.ceil(textWidth + paddingX * 2);
        const boxHeight = Math.ceil(fontSize + paddingY * 2);

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
        ctx.fillText(rawText, bubbleX + paddingX, bubbleY + boxHeight / 2);

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
        const state = getState(canvas);

        try {
            const nodeCount = payload?.nodes?.length ?? payload?.Nodes?.length ?? 0;
            const edgeCount = payload?.edges?.length ?? payload?.Edges?.length ?? 0;
            console.info('[TopologyCanvas] render start', { nodeCount, edgeCount });
        } catch (logError) {
            console.warn('[TopologyCanvas] render logging failed', logError);
        }

        const preserveViewport = !!(payload?.preserveViewport ?? payload?.PreserveViewport);
        const overlaySettings = parseOverlaySettings(payload?.overlays ?? payload?.Overlays ?? {});
        state.overlaySettings = overlaySettings;

        const savedViewport = payload?.savedViewport ?? payload?.SavedViewport ?? null;
        if (preserveViewport && savedViewport) {
            applyViewportSnapshot(state, savedViewport);
        }

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

        const viewport = getViewport(payload);
        const signature = computeViewportSignature(viewport);
        if (signature && signature !== state.viewportSignature) {
            state.viewportSignature = signature;
            if (!preserveViewport) {
                const needAutoFit = !state.viewportApplied || !state.userAdjusted;
                state.viewportApplied = false;
                state.baseScale = null;
                if (needAutoFit) {
                    state.userAdjusted = false;
                }
            }
        }

        if (preserveViewport) {
            state.payload = payload;
            draw(canvas, state);
            emitViewportChanged(canvas, state);
            console.info('[TopologyCanvas] render finished (preserve viewport)');
            return;
        }

        if (viewport && (!state.userAdjusted || !state.viewportApplied)) {
            applyViewport(canvas, state, viewport);
        }

        state.payload = payload;
        draw(canvas, state);
        emitViewportChanged(canvas, state);
        console.info('[TopologyCanvas] render finished');
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
        const m = hex.trim().match(/^#?([a-fA-F0-9]{6})$/);
        if (!m) return null;
        const intVal = parseInt(m[1], 16);
        return {
            r: (intVal >> 16) & 255,
            g: (intVal >> 8) & 255,
            b: intVal & 255
        };
    }

    function isDarkColor(hex) {
        const rgb = hexToRgb(hex);
        if (!rgb) return false;
        const srgb = [rgb.r, rgb.g, rgb.b].map(v => {
            v /= 255;
            return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
        });
        const L = 0.2126 * srgb[0] + 0.7152 * srgb[1] + 0.0722 * srgb[2];
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

        let edgeStyle = 'orthogonal';
        if (typeof edgeStyleRaw === 'string') {
            const normalized = edgeStyleRaw.trim().toLowerCase();
            if (normalized === 'bezier') {
                edgeStyle = 'bezier';
            }
        } else {
            edgeStyle = Number(edgeStyleRaw) === 1 ? 'bezier' : 'orthogonal';
        }

        return {
            showLabels: boolOr(raw.showLabels ?? raw.ShowLabels, true),
            showEdgeArrows: boolOr(raw.showEdgeArrows ?? raw.ShowEdgeArrows, true),
            showEdgeShares: boolOr(raw.showEdgeShares ?? raw.ShowEdgeShares, false),
            showSparklines: boolOr(raw.showSparklines ?? raw.ShowSparklines, true),
            sparklineMode: sparkMode === 1 ? 'bar' : 'line',
            edgeStyle,
            colorBasis: raw.colorBasis ?? raw.ColorBasis ?? 0,
            zoomPercent,
            manualScale,
            neighborEmphasis: boolOr(raw.neighborEmphasis ?? raw.NeighborEmphasis, true),
            enableFullDag: boolOr(raw.enableFullDag ?? raw.EnableFullDag, false),
            includeServiceNodes: boolOr(raw.includeServiceNodes ?? raw.IncludeServiceNodes, true),
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
                errorCritical
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

    function resolveSampleColor(basis, index, sparkline, thresholds, defaultColor) {
        const value = getBasisValue(basis, index, sparkline);
        if (value === null || value === undefined || !Number.isFinite(value)) {
            return 'rgba(148, 163, 184, 0.55)';
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
        const isLeafNode = !!(node.leaf ?? node.Leaf);
        let boundary;

        if (isLeafNode && isComputedKind(kind)) {
            const baseRadius = Math.min(width, height) / 2;
            boundary = baseRadius * LEAF_CIRCLE_SCALE;
        } else if (kind === 'expr' || kind === 'expression') {
            const halfW = width / 2;
            const halfH = height / 2;
            const denom = (absUx / halfW) + (absUy / halfH);
            boundary = denom < epsilon ? Math.max(halfW, halfH) : 1 / denom;
        } else {
            const halfW = width / 2;
            const halfH = height / 2;
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

    function drawServiceDecorations(ctx, nodeMeta, overlaySettings, state) {
        if (!nodeMeta) return;

        const overlays = overlaySettings ?? {};
        const semanticsRaw = nodeMeta.semantics ?? null;
        const spark = nodeMeta.sparkline ?? null;

        const semantics = normalizeSemantics(semanticsRaw);
        const hasSemantics = Object.values(semantics).some(value => value);
        const hasSpark = spark !== null;
        const shouldDrawSparkline = overlays.showSparklines && spark;

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

        const sampleValueFor = (defaultKey, semanticEntry, extraKeys) => {
            if (!spark || !hasSelectedBin) {
                return null;
            }

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

            for (const candidate of keys) {
                const value = sampleSeriesValueAt(spark, candidate, selectedBin);
                if (value !== null && value !== undefined) {
                    return value;
                }
            }

            return null;
        };

        const fallbackLabel = (entry) => entry?.label ?? null;

        if (shouldDrawSparkline) {
            const defaultSpark = (nodeMeta.fill ?? resolveSparklineColor(overlays.colorBasis));
            drawSparkline(ctx, nodeMeta, spark, overlays, defaultSpark, {
                top: topRowTop,
                height: chipH,
                right: (x - (nodeWidth / 2) - 8)
            });
        }

        if (!hasSemantics) {
            return;
        }

        ctx.save();
        ctx.font = '11px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';

        let topLeft = x - (nodeWidth / 2) + gap;
        let topRight = x + (nodeWidth / 2) + gap;
        let bottomLeft = topLeft;
        let bottomRight = topRight;

        if (overlays.showArrivalsDependencies !== false) {
            const arrivalValue = sampleValueFor('arrivals', semantics.arrivals);
            const arrivalLabel = arrivalValue !== null ? formatMetricValue(arrivalValue) : fallbackLabel(semantics.arrivals);
            if (arrivalLabel) {
                const drawn = drawChip(ctx, topLeft, topRowTop + chipH, arrivalLabel, '#1976D2', '#FFFFFF', paddingX, chipH);
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric: 'arrivals',
                    placement: 'top',
                    tooltip: semantics.arrivals?.label ?? toPascal('arrivals'),
                    x: topLeft,
                    y: topRowTop,
                    width: drawn,
                    height: chipH
                });
                topLeft += drawn + gap;
            }
        }

        if (semantics.series) {
            const outValue = sampleValueFor('series', semantics.series);
            const outLabel = outValue !== null ? formatMetricValue(outValue) : fallbackLabel(semantics.series);
            if (outLabel) {
                const drawn = drawChip(ctx, topLeft, topRowTop + chipH, outLabel, '#5C6BC0', '#FFFFFF', paddingX, chipH);
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric: 'series',
                    placement: 'top',
                    tooltip: semantics.series?.label ?? toPascal('series'),
                    x: topLeft,
                    y: topRowTop,
                    width: drawn,
                    height: chipH
                });
                topLeft += drawn + gap;
            }
        }

        if (overlays.showServedDependencies !== false) {
            const servedValue = sampleValueFor('served', semantics.served);
            const servedLabel = servedValue !== null ? formatMetricValue(servedValue) : fallbackLabel(semantics.served);
            if (servedLabel) {
                const drawn = drawChip(ctx, bottomLeft, bottomRowTop + chipH, servedLabel, '#2E7D32', '#FFFFFF', paddingX, chipH);
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric: 'served',
                    placement: 'bottom-left',
                    tooltip: semantics.served?.label ?? toPascal('served'),
                    x: bottomLeft,
                    y: bottomRowTop,
                    width: drawn,
                    height: chipH
                });
                bottomLeft += drawn + gap;
            }
        }

        if (overlays.showCapacityDependencies !== false) {
            const capacityValue = sampleValueFor('capacity', semantics.capacity, ['cap']);
            const capacityLabel = capacityValue !== null ? formatMetricValue(capacityValue) : fallbackLabel(semantics.capacity);
            if (capacityLabel) {
                const drawn = drawChip(ctx, bottomLeft, bottomRowTop + chipH, capacityLabel, '#FFB300', '#1F2937', paddingX, chipH);
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric: 'capacity',
                    placement: 'bottom-left',
                    tooltip: semantics.capacity?.label ?? toPascal('capacity'),
                    x: bottomLeft,
                    y: bottomRowTop,
                    width: drawn,
                    height: chipH
                });
                bottomLeft += drawn + gap;
            }
        }

        if (overlays.showErrorsDependencies !== false) {
            const errorRateValue = sampleValueFor('errorRate', semantics.errors, ['error_rate']);
            let errorLabel = null;
            let bg = '#C62828';
            let fg = '#FFFFFF';

            if (errorRateValue !== null) {
                if (errorRateValue <= 0) {
                    bg = '#E5E7EB';
                    fg = '#1F2937';
                } else if (errorRateValue >= thresholds.errorCritical) {
                    bg = '#B71C1C';
                } else if (errorRateValue >= thresholds.errorWarning) {
                    bg = '#FB8C00';
                    fg = '#1F2937';
                }

                errorLabel = formatPercent(errorRateValue);
            } else {
                const errorCount = sampleValueFor('errors', semantics.errors);
                if (errorCount !== null) {
                    if (errorCount <= 0) {
                        bg = '#E5E7EB';
                        fg = '#1F2937';
                    }

                    errorLabel = formatMetricValue(errorCount);
                } else {
                    errorLabel = fallbackLabel(semantics.errors);
                }
            }

            if (errorLabel) {
                const drawn = drawChip(ctx, bottomLeft, bottomRowTop + chipH, errorLabel, bg, fg, paddingX, chipH);
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric: 'errors',
                    placement: 'bottom-left',
                    tooltip: semantics.errors?.label ?? toPascal('errors'),
                    x: bottomLeft,
                    y: bottomRowTop,
                    width: drawn,
                    height: chipH
                });
                bottomLeft += drawn + gap;
            }
        }

        if (overlays.showQueueDependencies !== false) {
            const queueValue = sampleValueFor('queue', semantics.queue);
            const queueLabel = queueValue !== null ? formatMetricValue(queueValue) : fallbackLabel(semantics.queue);
            if (queueLabel) {
                const drawn = drawChip(ctx, bottomRight, bottomRowTop + chipH, queueLabel, '#8E24AA', '#FFFFFF', paddingX, chipH);
                registerChipHitbox(state, {
                    nodeId: nodeMeta.id ?? null,
                    metric: 'queue',
                    placement: 'bottom-right',
                    tooltip: semantics.queue?.label ?? toPascal('queue'),
                    x: bottomRight,
                    y: bottomRowTop,
                    width: drawn,
                    height: chipH
                });
                bottomRight += drawn + gap;
            }
        }

        ctx.restore();
    }

    function drawInputSparkline(ctx, nodeMeta, overlaySettings) {
        const spark = nodeMeta.sparkline ?? null;
        if (!spark) {
            return;
        }

        const mode = overlaySettings.sparklineMode === 'bar' ? 'bar' : 'line';
        const chipH = mode === 'bar' ? 14 : 12;
        const gap = 6;
        const nodeHeight = nodeMeta.height ?? 24;
        const nodeWidth = nodeMeta.width ?? 54;
        const top = (nodeMeta.y ?? 0) - (nodeHeight / 2) - chipH - gap;
        const seriesLength = (spark.values ?? spark.Values ?? []).length;
        const desiredWidth = computeAdaptiveWidth(seriesLength, nodeWidth, {
            min: 32,
            max: 140,
            scale: mode === 'bar' ? 11 : 10
        });
        const center = nodeMeta.x ?? 0;

        drawSparkline(ctx, nodeMeta, spark, overlaySettings, '#3B82F6', {
            top,
            height: chipH,
            left: center - desiredWidth / 2,
            minWidth: desiredWidth,
            maxWidth: desiredWidth
        });
    }

    function drawPmfDistribution(ctx, nodeMeta, distribution) {
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
        const top = (nodeMeta.y ?? 0) - (nodeHeight / 2) - chartHeight - gap;
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
                queue: null,
                capacity: null,
                series: null,
                distribution: null
            };
        }

        return {
            arrivals: normalizeSemanticValue(raw.arrivals ?? raw.Arrivals),
            served: normalizeSemanticValue(raw.served ?? raw.Served),
            errors: normalizeSemanticValue(raw.errors ?? raw.Errors),
            queue: normalizeSemanticValue(raw.queue ?? raw.Queue),
            capacity: normalizeSemanticValue(raw.capacity ?? raw.Capacity),
            series: normalizeSemanticValue(raw.series ?? raw.Series),
            distribution: normalizeDistribution(raw.distribution ?? raw.Distribution),
            inline: normalizeInlineSeries(raw.inlineValues ?? raw.InlineValues)
        };
    }

    function normalizeSemanticValue(raw) {
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
        const canonical = canonicalizeSeriesKey(rawIdentifier);
        const key = extractSeriesKey(rawIdentifier);
        const label = simplifySemanticLabel(rawIdentifier);

        return {
            key,
            label,
            reference: rawIdentifier,
            canonical
        };
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
        let values = slice?.values ?? slice?.Values ?? null;
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

    function toPascal(name) {
        if (typeof name !== 'string' || name.length === 0) {
            return name;
        }
        return name.charAt(0).toUpperCase() + name.slice(1);
    }

    function registerChipHitbox(state, chip) {
        if (!state) {
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

    function drawChip(ctx, x, y, text, bg, fg, paddingX, h) {
        const w = Math.ceil(ctx.measureText(text).width) + paddingX * 2;
        ctx.save();
        ctx.fillStyle = bg;
        ctx.strokeStyle = 'rgba(15, 23, 42, 0.35)';
        ctx.lineWidth = 1;
        // rounded rect
        const r = h / 2;
        const bx = Math.round(x);
        const by = Math.round(y - h);
        ctx.beginPath();
        ctx.moveTo(bx + r, by);
        ctx.arcTo(bx + w, by, bx + w, by + h, r);
        ctx.arcTo(bx + w, by + h, bx, by + h, r);
        ctx.arcTo(bx, by + h, bx, by, r);
        ctx.arcTo(bx, by, bx + w, by, r);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
        ctx.fillStyle = fg;
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        const textY = by + (h / 2) + 0.5;
        ctx.fillText(text, bx + paddingX, textY);
        ctx.restore();
        return w;
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

    function computeBezierPath(fromNode, toNode, laneOffset, padding) {
        const anchorsFrom = createAnchors(fromNode);
        const anchorsTo = createAnchors(toNode);

        const fromCenterX = fromNode.x ?? fromNode.X ?? 0;
        const fromCenterY = fromNode.y ?? fromNode.Y ?? 0;
        const toCenterX = toNode.x ?? toNode.X ?? 0;
        const toCenterY = toNode.y ?? toNode.Y ?? 0;
        const dx = toCenterX - fromCenterX;
        const dy = toCenterY - fromCenterY;

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

        const useVertical = Math.abs(dy) > Math.abs(dx);
        let selected = useVertical ? vertical : horizontal;

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
        const baseTension = Math.max(24, Math.min(120, span * 0.45));
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

        const segments = [];
        for (let i = 0; i < points.length - 1; i++) {
            const start = points[i];
            const end = points[i + 1];
            const dx = end.x - start.x;
            const dy = end.y - start.y;
            const length = Math.hypot(dx, dy);
            segments.push({ x1: start.x, y1: start.y, x2: end.x, y2: end.y, length });
        }

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

    function drawSparkline(ctx, nodeMeta, sparkline, overlaySettings, defaultColor, overrides) {
        const opts = overrides ?? {};
        const basis = Number(overlaySettings.colorBasis ?? 0);
        const series = selectSeriesForBasis(sparkline, basis);
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

        ctx.fillStyle = 'rgba(148, 163, 184, 0.08)';
        ctx.fillRect(0, 0, sparkWidth, sparkHeight);
        ctx.strokeStyle = 'rgba(148, 163, 184, 0.25)';
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
            const sampleColor = resolveSampleColor(basis, index, sparkline, thresholds, defaultColor);

            if (drawAsBars) {
                const barWidth = Math.max(step * 0.6, 1.5);
                ctx.fillStyle = sampleColor;
                const clampedY = Math.min(Math.max(y, 0), sparkHeight);
                const barHeight = Math.max(sparkHeight - clampedY, 1);
                const barTop = sparkHeight - barHeight;
                ctx.fillRect(x - barWidth / 2, barTop, barWidth, barHeight);
                if (index === highlightIndex) {
                    highlightColor = sampleColor;
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
                previousColor = sampleColor;
                if (index === highlightIndex) {
                    highlightPoint = { x, y };
                    highlightColor = sampleColor;
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
            default:
                return sparkline.values ?? sparkline.Values ?? [];
        }
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

    function emitViewportChanged(canvas, state) {
        if (!state?.dotNetRef) {
            return;
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

        if (signature === state.lastViewportSignature || isWithinTolerance) {
            console.info('[TopologyCanvas] viewport unchanged (tolerance)', signature);
            return;
        }

        state.lastViewportSignature = signature;
        state.lastViewportPayload = payload;
        console.info('[TopologyCanvas] viewport changed', signature);
        state.dotNetRef.invokeMethodAsync('OnViewportChanged', payload);
    }

    function restoreViewport(canvas, snapshot) {
        if (!canvas || !snapshot) {
            return;
        }

        const state = getState(canvas);
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
        emitViewportChanged(canvas, state);
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

    window.FlowTime = window.FlowTime || {};
    window.FlowTime.TopologyCanvas = {
        render,
        dispose,
        restoreViewport,
        registerHandlers: (canvas, dotNetRef) => {
            const state = getState(canvas);
            state.dotNetRef = dotNetRef;
        }
    };
    window.FlowTime.TopologyHotkeys = {
        register: registerHotkeys,
        unregister: unregisterHotkeys
    };
})();
