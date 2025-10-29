(function () {
    const registry = new WeakMap();
    const EdgeTypeTopology = 'topology';
    const EdgeTypeDependency = 'dependency';

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
                dotNetRef: null
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

        const pointerDown = (event) => {
            canvas.setPointerCapture(event.pointerId);
            state.dragging = true;
            state.dragStart = { x: event.clientX, y: event.clientY };
            state.panStart = { x: state.offsetX, y: state.offsetY };
            state.userAdjusted = true;
        };

        const pointerMove = (event) => {
            if (!state.dragging) {
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
        };

        const wheel = (event) => {
            event.preventDefault();

            const { offsetX, offsetY, deltaY } = event;
            const scaleFactor = deltaY < 0 ? 1.1 : 0.9;
            const focusX = (offsetX - state.offsetX) / state.scale;
            const focusY = (offsetY - state.offsetY) / state.scale;

            state.scale = clamp(state.scale * scaleFactor, 0.2, 6);
            state.offsetX = offsetX - focusX * state.scale;
            state.offsetY = offsetY - focusY * state.scale;
            state.userAdjusted = true;

            const baseScale = state.baseScale || state.scale;
            const manual = clamp(state.scale / baseScale, 0.25, 8);
            state.overlayScale = manual;
            state.lastOverlayZoom = manual;
            const zoomPercent = clamp(manual * 100, 30, 400);
            if (state.dotNetRef) {
                state.dotNetRef.invokeMethodAsync('OnCanvasZoomChanged', zoomPercent);
            }

            updateWorldCenter(state);

            draw(canvas, state);
        };

        canvas.addEventListener('pointerdown', pointerDown);
        canvas.addEventListener('pointermove', pointerMove);
        canvas.addEventListener('pointerup', pointerUp);
        canvas.addEventListener('pointerleave', pointerUp);
        canvas.addEventListener('wheel', wheel, { passive: false });

        state.cleanup = () => {
            window.removeEventListener('resize', resize);
            canvas.removeEventListener('pointerdown', pointerDown);
            canvas.removeEventListener('pointermove', pointerMove);
            canvas.removeEventListener('pointerup', pointerUp);
            canvas.removeEventListener('pointerleave', pointerUp);
            canvas.removeEventListener('wheel', wheel);
            state.resizeObserver?.disconnect?.();
            state.resizeObserver = null;
            state.dotNetRef = null;
        };
    }

    function draw(canvas, state) {
        if (!state.payload) {
            clear(state);
            return;
        }

        const ctx = state.ctx;
        clear(state);

        ctx.save();
        ctx.translate(state.offsetX, state.offsetY);
        ctx.scale(state.scale, state.scale);

        const nodes = state.payload.nodes ?? state.payload.Nodes ?? [];
        const edges = state.payload.edges ?? state.payload.Edges ?? [];
        const overlaySettings = state.overlaySettings ?? parseOverlaySettings(state.payload.overlays ?? state.payload.Overlays ?? {});
        const tooltip = state.payload.tooltip ?? state.payload.Tooltip ?? null;

        // Sync overlay DOM (proxies + tooltip) with canvas pan/zoom so hover/focus hitboxes and callouts align
        applyOverlayTransform(canvas, state);
        const sparkColor = resolveSparklineColor(overlaySettings.colorBasis);
        const nodeMap = new Map();
        for (const n of nodes) {
            nodeMap.set((n.id ?? n.Id), {
                x: n.x ?? n.X,
                y: n.y ?? n.Y,
                width: n.width ?? n.Width ?? 36,
                height: n.height ?? n.Height ?? 24,
                cornerRadius: n.cornerRadius ?? n.CornerRadius ?? 3,
                sparkline: n.sparkline ?? n.Sparkline ?? null,
                isFocused: !!(n.isFocused ?? n.IsFocused),
                visible: !(n.isVisible === false || n.IsVisible === false),
                kind: String(n.kind ?? n.Kind ?? 'service')
            });
        }

        const portRadius = 3.5;
        const portFillColor = '#E7EBF4';
        const portStrokeColor = 'rgba(59, 72, 89, 0.55)';

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

            if (fromNode.visible === false && overlaySettings.showComputeNodes === false) {
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
            ctx.lineWidth = 1;
            ctx.strokeStyle = strokeColor;
            if (edgeType === EdgeTypeDependency) {
                ctx.setLineDash([4, 3]);
            }

            if (overlaySettings.edgeStyle === 'bezier') {
                const path = computeBezierPath(fromNode, toNode, offset, portRadius);
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

                const startShrink = computePortOffset(fromNode, ux, uy, portRadius);
                const endShrink = computePortOffset(toNode, -ux, -uy, portRadius);
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

            drawPort(ctx, startPoint.x, startPoint.y, portRadius, portFillColor, portStrokeColor);
            drawPort(ctx, endPoint.x, endPoint.y, portRadius, portFillColor, portStrokeColor);

            const share = edge.share ?? edge.Share;
            if (overlaySettings.showEdgeShares && share !== null && share !== undefined) {
                drawEdgeShare(ctx, pathPoints, share);
            }

            if (edgeType === EdgeTypeDependency) {
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

            if (!isVisible && !overlaySettings.showComputeNodes) {
                continue;
            }

            const x = node.x ?? node.X;
            const y = node.y ?? node.Y;
            const width = node.width ?? node.Width ?? 36;
            const height = node.height ?? node.Height ?? 24;
            const cornerRadius = node.cornerRadius ?? node.CornerRadius ?? 3;
            const fill = node.fill ?? node.Fill ?? '#7A7A7A';
            const stroke = node.stroke ?? node.Stroke ?? '#262626';

            const highlightNode = !emphasisEnabled || (neighborNodes?.has(id) ?? false);
            const nodeAlpha = highlightNode ? 1 : dimmedAlpha;

            ctx.save();
            ctx.globalAlpha = nodeAlpha;

            // Draw node shape by kind
            const kind = String(meta?.kind ?? node.kind ?? node.Kind ?? 'service').toLowerCase();
            ctx.beginPath();
            if (kind === 'expr' || kind === 'expression') {
                const hw = width / 2, hh = height / 2;
                ctx.moveTo(x, y - hh);
                ctx.lineTo(x + hw, y);
                ctx.lineTo(x, y + hh);
                ctx.lineTo(x - hw, y);
                ctx.closePath();
            } else if (kind === 'const' || kind === 'pmf') {
                const r = Math.min(cornerRadius + 6, Math.min(width, height) / 2);
                traceRoundedRect(ctx, x, y, width, height, r);
            } else {
                traceRoundedRect(ctx, x, y, width, height, cornerRadius);
            }
            ctx.fillStyle = fill;
            ctx.strokeStyle = stroke;
            ctx.lineWidth = 0.9;
            ctx.fill();
            ctx.stroke();
            if (kind === 'queue') {
                // Simple inner bar to suggest buffer
                const pad = 3;
                ctx.save();
                ctx.fillStyle = 'rgba(17, 17, 17, 0.08)';
                ctx.fillRect(x - width / 2 + pad, y + height / 2 - 6 - pad, width - 2 * pad, 4);
                ctx.restore();
            }

            if (node.isFocused ?? node.IsFocused) {
                ctx.save();
                ctx.globalAlpha = 1;
                ctx.beginPath();
                traceRoundedRect(ctx, x, y, width + 10, height + 10, cornerRadius + 4);
                ctx.strokeStyle = '#FFFFFF';
                ctx.lineWidth = 2;
                ctx.setLineDash([6, 4]);
                ctx.stroke();
                ctx.setLineDash([]);
                ctx.restore();
            }

            const nodeMeta = nodeMap.get(id);
            if (overlaySettings.showSparklines && nodeMeta?.sparkline) {
                drawSparkline(ctx, nodeMeta, nodeMeta.sparkline, overlaySettings, sparkColor);
            }

            // Badge rack for service/queue nodes (compute nodes skip badges)
            if (kind === 'service' || kind === 'queue') {
                drawBadges(ctx, id, nodeMeta, overlaySettings, edges, nodeMap);
            }

            if (overlaySettings.showLabels) {
                ctx.save();
                const label = String(id);
                const textColor = isDarkColor(fill) ? '#FFFFFF' : '#111111';
                ctx.fillStyle = textColor;
                ctx.globalAlpha = highlightNode ? 1 : 0.6;
                const m = overlaySettings.manualScaleFactor ?? 1;
                let fontSize = 10;
                if (m > 2.5) fontSize = 14; else if (m > 1.25) fontSize = 12;
                ctx.font = `${fontSize}px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                const maxWidth = Math.max(width - 12, 24);
                drawFittedText(ctx, label, x, y, maxWidth);
                ctx.restore();
            }

            ctx.restore();
        }

        // Draw vector tooltip/callout if present
        if (tooltip) {
            tryDrawTooltip(ctx, nodes, tooltip, state);
        }

        ctx.restore();
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

    function tryDrawTooltip(ctx, nodes, tooltip, state) {
        // Find focused node position
        let focused = null;
        for (const n of nodes) {
            if (n.isFocused || n.IsFocused) { focused = n; break; }
        }
        if (!focused) return;

        const x = focused.x ?? focused.X;
        const y = focused.y ?? focused.Y;
        const dx = 16; // constant offset to the right
        const dy = -20; // above center
        const padding = 8;
        const lineHeight = 14;
        const title = String(tooltip.title ?? tooltip.Title ?? '');
        const subtitle = String(tooltip.subtitle ?? tooltip.Subtitle ?? '');
        const lines = (tooltip.lines ?? tooltip.Lines ?? []).map(l => String(l));

        const all = [title, subtitle, ...lines];
        ctx.save();
        ctx.font = '12px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        let width = 0;
        for (const t of all) width = Math.max(width, ctx.measureText(t).width);
        const height = (2 + lines.length) * lineHeight + padding * 2;
        const boxX = x + dx;
        const boxY = y + dy - height;

        const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        const bg = prefersDark ? 'rgba(30, 41, 59, 0.95)' : 'rgba(255, 255, 255, 0.95)';
        const fg = prefersDark ? '#F8FAFC' : '#111827';
        const border = prefersDark ? 'rgba(148, 163, 184, 0.4)' : 'rgba(17, 24, 39, 0.15)';

        // Callout box
        ctx.fillStyle = bg;
        ctx.strokeStyle = border;
        ctx.lineWidth = 1;
        const bw = Math.ceil(width + padding * 2);
        const bh = Math.ceil(height);
        const bx = Math.round(boxX);
        const by = Math.round(boxY);
        ctx.beginPath();
        ctx.rect(bx, by, bw, bh);
        ctx.fill();
        ctx.stroke();

        // Pointer triangle
        ctx.beginPath();
        ctx.moveTo(x + 6, y - 6);
        ctx.lineTo(x + dx, y - 6);
        ctx.lineTo(x + dx, y - 12);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();

        // Text
        ctx.fillStyle = fg;
        let ty = by + padding + lineHeight;
        ctx.fillText(title, bx + padding, ty);
        ty += lineHeight;
        ctx.fillText(subtitle, bx + padding, ty);
        for (const line of lines) {
            ty += lineHeight;
            ctx.fillText(line, bx + padding, ty);
        }
        ctx.restore();
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

    function render(canvas, payload) {
        const state = getState(canvas);

        const overlaySettings = parseOverlaySettings(payload?.overlays ?? payload?.Overlays ?? {});
        state.overlaySettings = overlaySettings;

        const manualFactor = overlaySettings.manualScaleFactor ?? 1;
        if (state.lastOverlayZoom === null || Math.abs(state.lastOverlayZoom - manualFactor) > 0.001) {
            state.overlayScale = manualFactor;
            state.lastOverlayZoom = manualFactor;
            state.userAdjusted = false;
        }

        const viewport = getViewport(payload);
        const signature = computeViewportSignature(viewport);
        if (signature && signature !== state.viewportSignature) {
            state.viewportSignature = signature;
            state.viewportApplied = false;
            state.baseScale = null;
            state.userAdjusted = false;
        }

        if (viewport && !state.userAdjusted) {
            applyViewport(canvas, state, viewport);
        }

        state.payload = payload;
        draw(canvas, state);
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

        const manualFactor = clamp(state.overlayScale ?? 1, 0.25, 4);
        const baseScale = clamp(state.baseScale ?? safeScale, 0.05, 4);
        const finalScale = clamp(baseScale * manualFactor, 0.1, 6);

        state.scale = finalScale;
        state.offsetX = (width / 2) - (state.worldCenterX * finalScale);
        state.offsetY = (height / 2) - (state.worldCenterY * finalScale);
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
        const zoomPercent = Number.isFinite(zoomPercentRaw) ? clamp(zoomPercentRaw, 30, 400) : 100;
        const manualScaleFactor = clamp(zoomPercent / 100, 0.25, 8);
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
            autoLod: boolOr(raw.autoLod ?? raw.AutoLod, true),
            zoomLowThreshold: Number(raw.zoomLowThreshold ?? raw.ZoomLowThreshold ?? 0.5) || 0.5,
            zoomMidThreshold: Number(raw.zoomMidThreshold ?? raw.ZoomMidThreshold ?? 1.0) || 1.0,
            colorBasis: raw.colorBasis ?? raw.ColorBasis ?? 0,
            zoomPercent,
            manualScaleFactor,
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
            showComputeNodes: boolOr(raw.showComputeNodes ?? raw.ShowComputeNodes, false),
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
        switch (basis) {
            case 1:
                return valueAtArray(sparkline.utilization ?? sparkline.Utilization, index);
            case 2:
                return valueAtArray(sparkline.errorRate ?? sparkline.ErrorRate, index);
            case 3:
                return valueAtArray(sparkline.queueDepth ?? sparkline.QueueDepth, index);
            default:
                return valueAtArray(sparkline.values ?? sparkline.Values, index);
        }
    }

    function valueAtArray(arr, index) {
        if (!arr || index < 0 || index >= arr.length) {
            return null;
        }
        const value = Number(arr[index]);
        return Number.isFinite(value) ? value : null;
    }

    function computePortOffset(node, ux, uy, portRadius) {
        if (!node) {
            return 16;
        }

        const width = node.width ?? node.Width ?? 36;
        const height = node.height ?? node.Height ?? 24;
        const halfW = width / 2;
        const halfH = height / 2;
        const absUx = Math.abs(ux);
        const absUy = Math.abs(uy);
        const epsilon = 1e-4;
        const candidates = [];

        if (absUx > epsilon) {
            candidates.push(halfW / absUx);
        }

        if (absUy > epsilon) {
            candidates.push(halfH / absUy);
        }

        if (candidates.length === 0) {
            candidates.push(Math.max(halfW, halfH));
        }

        const boundary = Math.min(...candidates);
        return boundary + portRadius;
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

    function drawBadges(ctx, nodeId, nodeMeta, overlays, edges, nodeMap) {
        if (!nodeMeta) return;
        const x = nodeMeta.x ?? 0;
        const y = nodeMeta.y ?? 0;
        const width = nodeMeta.width ?? 36;
        const nodeHeight = nodeMeta.height ?? 24;

        // Compute vertical offset (above node, and above sparkline if present)
        let top = y - (nodeHeight / 2) - 6;
        const spark = nodeMeta.sparkline ?? null;
        if (spark && overlays.showSparklines) {
            const mode = overlays.sparklineMode === 'bar' ? 'bar' : 'line';
            const sparkHeight = mode === 'bar' ? 16 : 12;
            top -= (sparkHeight + 6);
        }

        const paddingX = 6;
        const chipH = 12;
        const gap = 4;
        const labelWidth = Math.max(width - 12, 24);
        let cursorX = x - labelWidth / 2; // left align to label area
        let leftCursor = x - labelWidth / 2 - gap; // compute badges extend to the left

        const thresholds = overlays.thresholds || {
            slaSuccess: 0.95,
            slaWarning: 0.8,
            utilizationWarning: 0.9,
            utilizationCritical: 0.95,
            errorWarning: 0.02,
            errorCritical: 0.05
        };

        // helper to get current value by selectedBin
        function currentOf(arr, startIndex) {
            if (!Array.isArray(arr)) return null;
            const sb = Number(overlays.selectedBin ?? -1);
            const idx = Number.isFinite(sb) ? sb - (Number(startIndex) || 0) : -1;
            if (idx >= 0 && idx < arr.length) {
                const v = arr[idx];
                return (v === null || v === undefined) ? null : Number(v);
            }
            return null;
        }

        const startIndex = Number(spark?.startIndex ?? spark?.StartIndex ?? 0);
        const success = currentOf(spark?.values ?? spark?.Values, startIndex);
        const util = currentOf(spark?.utilization ?? spark?.Utilization, startIndex);
        const err = currentOf(spark?.errorRate ?? spark?.ErrorRate, startIndex);
        const q = currentOf(spark?.queueDepth ?? spark?.QueueDepth, startIndex);

        ctx.save();
        ctx.font = '10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';

        // Arrivals/Served: show success ratio if available as A/S chips
        if (success !== null && overlays.showArrivalsDependencies !== false) {
            const label = `A ${(success * 100).toFixed(0)}%`;
            cursorX += drawChip(ctx, cursorX, top, label, '#F3F4F6', '#111827', paddingX, chipH) + gap;
        }
        if (success !== null && overlays.showServedDependencies !== false) {
            const label = `S ${(success * 100).toFixed(0)}%`;
            cursorX += drawChip(ctx, cursorX, top, label, '#E6FFFB', '#0F172A', paddingX, chipH) + gap;
        }

        // Errors
        if (err !== null && overlays.showErrorsDependencies !== false) {
            let bg = '#E6FFFB';
            if (err >= thresholds.errorCritical) bg = '#FDECEC';
            else if (err >= thresholds.errorWarning) bg = '#FFF7E6';
            const label = `E ${(err * 100).toFixed(1)}%`;
            cursorX += drawChip(ctx, cursorX, top, label, bg, '#111827', paddingX, chipH) + gap;
        }

        // Queue
        if (q !== null && overlays.showQueueDependencies !== false) {
            const label = `Q ${Math.round(q)}`;
            cursorX += drawChip(ctx, cursorX, top, label, '#EEF2FF', '#111827', paddingX, chipH) + gap;
        }

        // Capacity (use utilization if more relevant)
        if (util !== null && overlays.showCapacityDependencies !== false) {
            const label = `C ${(util * 100).toFixed(0)}%`;
            cursorX += drawChip(ctx, cursorX, top, label, '#F0FFF4', '#111827', paddingX, chipH) + gap;
        }

        // Compute dependency badges (const/expr/pmf)
        for (const edge of edges) {
            const edgeType = String(edge.edgeType ?? edge.EdgeType ?? '').toLowerCase();
            if (edgeType !== EdgeTypeDependency) {
                continue;
            }

            const toId = edge.to ?? edge.To;
            if (!toId || toId !== nodeId) {
                continue;
            }

            const fromId = edge.from ?? edge.From;
            if (!fromId) {
                continue;
            }

            const source = nodeMap.get(fromId);
            if (!source) {
                continue;
            }

            const sourceKind = String(source.kind ?? '').toLowerCase();
            const isCompute = sourceKind === 'expr' || sourceKind === 'expression' || sourceKind === 'const' || sourceKind === 'pmf';
            if (!isCompute) {
                continue;
            }

            if (overlays.showComputeNodes && source.visible !== false) {
                // compute node is visible on canvas; skip duplicate badge
                continue;
            }

            const field = String(edge.field ?? edge.Field ?? '').toLowerCase();
            let label = fromId;
            if (field && field !== 'expr') {
                label = `${field}: ${fromId}`;
            }
            if (label.length > 14) {
                label = label.slice(0, 11) + '…';
            }

            const chipWidth = drawComputeBadge(ctx, leftCursor, top, label, sourceKind, paddingX, chipH);
            leftCursor -= (chipWidth + gap);
        }

        ctx.restore();
    }

    function drawChip(ctx, x, y, text, bg, fg, paddingX, h) {
        const w = Math.ceil(ctx.measureText(text).width) + paddingX * 2;
        ctx.save();
        ctx.fillStyle = bg;
        ctx.strokeStyle = 'rgba(17, 24, 39, 0.15)';
        ctx.lineWidth = 1;
        // rounded rect
        const r = Math.min(6, h / 2);
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
        ctx.fillText(text, bx + paddingX, by + h / 2);
        ctx.restore();
        return w;
    }

    function drawComputeBadge(ctx, x, y, text, kind, paddingX, h) {
        const palette = {
            expr: '#E0F2FE',
            expression: '#E0F2FE',
            const: '#FDF6B2',
            pmf: '#FCE7F3'
        };
        const bg = palette[kind] ?? '#E5E7EB';
        const fg = '#111827';
        const w = Math.ceil(ctx.measureText(text).width) + paddingX * 2;
        const r = Math.min(6, h / 2);
        const bx = Math.round(x - w);
        const by = Math.round(y - h);

        ctx.save();
        ctx.fillStyle = bg;
        ctx.strokeStyle = 'rgba(17, 24, 39, 0.15)';
        ctx.lineWidth = 1;
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
        ctx.fillText(text, bx + paddingX, by + h / 2);
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

    function computeBezierPath(fromNode, toNode, laneOffset, portRadius) {
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

        const start = offsetAnchor(selected.start, portRadius);
        const end = offsetAnchor(selected.end, portRadius);

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
        const width = node.width ?? node.Width ?? 36;
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

    function offsetAnchor(anchor, amount) {
        const distance = amount ?? 0;
        return {
            x: anchor.x + anchor.normalX * distance,
            y: anchor.y + anchor.normalY * distance,
            normalX: anchor.normalX,
            normalY: anchor.normalY
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

        const arrowLength = 8;
        const arrowWidth = 6;
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

    function drawPort(ctx, x, y, radius, fill, stroke) {
        ctx.save();
        ctx.beginPath();
        ctx.arc(x, y, radius, 0, Math.PI * 2);
        ctx.fillStyle = fill;
        ctx.fill();
        ctx.lineWidth = 0.75;
        ctx.strokeStyle = stroke;
        ctx.stroke();
        ctx.restore();
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

    function drawSparkline(ctx, nodeMeta, sparkline, overlaySettings, defaultColor) {
        const values = sparkline.values ?? sparkline.Values;
        if (!Array.isArray(values) || values.length < 2) {
            return;
        }

        const min = Number(sparkline.min ?? sparkline.Min);
        const max = Number(sparkline.max ?? sparkline.Max);
        const isFlat = !!(sparkline.isFlat ?? sparkline.IsFlat);
        if (!Number.isFinite(min) || !Number.isFinite(max)) {
            return;
        }

        const mode = overlaySettings.sparklineMode === 'bar' ? 'bar' : 'line';
        const basis = Number.isFinite(overlaySettings.colorBasis) ? overlaySettings.colorBasis : 0;
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
        const highlightIndex = (highlightIndexRaw >= 0 && highlightIndexRaw < values.length) ? highlightIndexRaw : -1;

        const width = nodeMeta.width ?? 36;
        const nodeHeight = nodeMeta.height ?? 24;
        const sparkWidth = Math.max(width - 6, 16);
        const sparkHeight = mode === 'bar' ? 16 : 12;
        const left = (nodeMeta.x ?? 0) - (sparkWidth / 2);
        const top = (nodeMeta.y ?? 0) - (nodeHeight / 2) - sparkHeight - 6;

        ctx.save();
        ctx.translate(left, top);

        const step = sparkWidth / Math.max(values.length - 1, 1);
        const range = max - min;
        let started = false;

        const drawAsBars = mode === 'bar';
        let highlightPoint = null;
        let highlightColor = defaultColor;
        let highlightBar = null;

        ctx.fillStyle = 'rgba(148, 163, 184, 0.12)';
        ctx.fillRect(0, 0, sparkWidth, sparkHeight);
        ctx.strokeStyle = 'rgba(148, 163, 184, 0.25)';
        ctx.lineWidth = 0.5;
        ctx.strokeRect(0, 0, sparkWidth, sparkHeight);

        let previousPoint = null;
        let previousColor = defaultColor;

        values.forEach((raw, index) => {
            if (raw === null || raw === undefined) {
                started = false;
                previousPoint = null;
                return;
            }

            const numeric = Number(raw);
            if (!Number.isFinite(numeric)) {
                started = false;
                previousPoint = null;
                return;
            }

            let normalized;
            if (isFlat || range < 1e-6) {
                normalized = 0.8;
            } else {
                normalized = clamp((numeric - min) / range, 0, 1);
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
                started = true;
            } else {
                if (previousPoint) {
                    ctx.beginPath();
                    ctx.strokeStyle = previousColor ?? sampleColor;
                    ctx.lineWidth = 1.4;
                    ctx.moveTo(previousPoint.x, previousPoint.y);
                    ctx.lineTo(x, y);
                    ctx.stroke();
                }
                started = true;
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
            const gap = 2;
            const markerY = sparkHeight + gap;
            ctx.beginPath();
            ctx.strokeStyle = highlightColor ?? defaultColor;
            ctx.lineWidth = 1.2;
            ctx.moveTo(highlightBar.x - highlightBar.width / 2, markerY);
            ctx.lineTo(highlightBar.x + highlightBar.width / 2, markerY);
            ctx.stroke();
        }

        ctx.restore();
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
