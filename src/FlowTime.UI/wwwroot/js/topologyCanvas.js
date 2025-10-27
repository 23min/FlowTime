(function () {
    const registry = new WeakMap();

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
                userAdjusted: false,
                viewportSignature: null
            };
            setupCanvas(canvas, state);
            registry.set(canvas, state);
        }
        return state;
    }

    function setupCanvas(canvas, state) {
        const resize = () => {
            const rect = canvas.getBoundingClientRect();
            const ratio = state.deviceRatio;
            canvas.width = rect.width * ratio;
            canvas.height = rect.height * ratio;
            state.ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
            const viewport = state.payload ? getViewport(state.payload) : null;
            if (viewport && !state.userAdjusted) {
                applyViewport(canvas, state, viewport);
            }
            draw(canvas, state);
        };

        resize();
        window.addEventListener('resize', resize);

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
        };

        const wheel = (event) => {
            event.preventDefault();

            const delta = -event.deltaY;
            const scaleFactor = delta > 0 ? 1.1 : 0.9;
            const { offsetX, offsetY } = event;

            const x = (offsetX - state.offsetX) / state.scale;
            const y = (offsetY - state.offsetY) / state.scale;

            state.scale = clamp(state.scale * scaleFactor, 0.2, 4);
            state.offsetX = offsetX - x * state.scale;
            state.offsetY = offsetY - y * state.scale;
            state.userAdjusted = true;

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
        const nodeMap = new Map();
        for (const n of nodes) {
            nodeMap.set((n.id ?? n.Id), {
                x: n.x ?? n.X,
                y: n.y ?? n.Y,
                width: n.width ?? n.Width ?? 56,
                height: n.height ?? n.Height ?? 36,
                cornerRadius: n.cornerRadius ?? n.CornerRadius ?? 8
            });
        }

        ctx.lineWidth = 1;
        ctx.strokeStyle = '#9AA1AC';
        ctx.globalAlpha = 0.85;

        for (const edge of edges) {
            const fromX = edge.fromX ?? edge.FromX;
            const fromY = edge.fromY ?? edge.FromY;
            const toX = edge.toX ?? edge.ToX;
            const toY = edge.toY ?? edge.ToY;

            const fromId = edge.from ?? edge.From;
            const toId = edge.to ?? edge.To;
            const fromNode = nodeMap.get(fromId);
            const toNode = nodeMap.get(toId);

            const dx = toX - fromX;
            const dy = toY - fromY;
            const len = Math.hypot(dx, dy) || 1;
            const ux = dx / len;
            const uy = dy / len;

            const startShrink = fromNode ? Math.max(fromNode.width, fromNode.height) / 2 : 12;
            const endShrink = toNode ? Math.max(toNode.width, toNode.height) / 2 + 4 : 14;
            const sx = fromX + ux * startShrink;
            const sy = fromY + uy * startShrink;
            const ex = toX - ux * endShrink;
            const ey = toY - uy * endShrink;

            ctx.beginPath();
            ctx.moveTo(sx, sy);
            ctx.lineTo(ex, ey);
            ctx.stroke();

            // Arrowhead at (ex, ey)
            const arrowLength = 8;
            const arrowWidth = 6;
            const baseX = ex - ux * arrowLength;
            const baseY = ey - uy * arrowLength;
            const perpX = -uy;
            const perpY = ux;

            ctx.beginPath();
            ctx.moveTo(ex, ey);
            ctx.lineTo(baseX + perpX * (arrowWidth / 2), baseY + perpY * (arrowWidth / 2));
            ctx.lineTo(baseX - perpX * (arrowWidth / 2), baseY - perpY * (arrowWidth / 2));
            ctx.closePath();
            ctx.fillStyle = ctx.strokeStyle;
            ctx.fill();
        }

        ctx.globalAlpha = 1;

        for (const node of nodes) {
            const x = node.x ?? node.X;
            const y = node.y ?? node.Y;
            const width = node.width ?? node.Width ?? 56;
            const height = node.height ?? node.Height ?? 36;
            const cornerRadius = node.cornerRadius ?? node.CornerRadius ?? 8;
            const fill = node.fill ?? node.Fill ?? '#7A7A7A';
            const stroke = node.stroke ?? node.Stroke ?? '#262626';

            ctx.beginPath();
            traceRoundedRect(ctx, x, y, width, height, cornerRadius);
            ctx.fillStyle = fill;
            ctx.strokeStyle = stroke;
            ctx.lineWidth = 1.2;
            ctx.fill();
            ctx.stroke();

            if (node.isFocused ?? node.IsFocused) {
                ctx.beginPath();
                traceRoundedRect(ctx, x, y, width + 10, height + 10, cornerRadius + 4);
                ctx.strokeStyle = '#FFFFFF';
                ctx.lineWidth = 2;
                ctx.setLineDash([6, 4]);
                ctx.stroke();
                ctx.setLineDash([]);
            }

            // Label centered inside node
            const id = node.id ?? node.Id;
            if (id) {
                ctx.save();
                const label = String(id);
                const textColor = isDarkColor(fill) ? '#FFFFFF' : '#111111';
                ctx.fillStyle = textColor;
                ctx.font = '10px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                const maxWidth = Math.max(width - 12, 24);
                drawFittedText(ctx, label, x, y, maxWidth);
                ctx.restore();
            }
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
        const viewport = getViewport(payload);
        const signature = computeViewportSignature(viewport);
        if (signature && signature !== state.viewportSignature) {
            state.viewportSignature = signature;
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

        const rect = canvas.getBoundingClientRect();
        const width = rect.width || canvas.width / state.deviceRatio;
        const height = rect.height || canvas.height / state.deviceRatio;

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

        const centerX = minX + (contentWidth / 2);
        const centerY = minY + (contentHeight / 2);

        state.scale = safeScale;
        state.offsetX = (width / 2) - (centerX * safeScale);
        state.offsetY = (height / 2) - (centerY * safeScale);
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

    function parseOverlaySettings(raw, scale) {
        const modeLabels = normalizeMode(raw.labels ?? raw.Labels);
        const modeEdgeArrows = normalizeMode(raw.edgeArrows ?? raw.EdgeArrows ?? 1);
        const modeEdgeShares = normalizeMode(raw.edgeShares ?? raw.EdgeShares);
        const modeSparklines = normalizeMode(raw.sparklines ?? raw.Sparklines);

        const autoLod = !!(raw.autoLod ?? raw.AutoLod ?? true);
        const zoomLow = Number(raw.zoomLowThreshold ?? raw.ZoomLowThreshold ?? 0.5) || 0.5;
        const zoomMid = Number(raw.zoomMidThreshold ?? raw.ZoomMidThreshold ?? 1.0) || 1.0;

        const defaultLabels = autoLod ? scale >= zoomLow : true;
        const defaultEdgeArrows = autoLod ? scale >= zoomLow : true;
        const defaultEdgeShares = autoLod ? scale >= zoomMid : false;
        const defaultSparklines = autoLod ? scale >= zoomMid : false;

        return {
            showLabels: resolveOverlay(modeLabels, defaultLabels),
            showEdgeArrows: resolveOverlay(modeEdgeArrows, defaultEdgeArrows),
            showEdgeShares: resolveOverlay(modeEdgeShares, defaultEdgeShares),
            showSparklines: resolveOverlay(modeSparklines, defaultSparklines),
            autoLod,
            zoomLowThreshold: zoomLow,
            zoomMidThreshold: zoomMid,
            colorBasis: raw.colorBasis ?? raw.ColorBasis ?? 0,
            neighborEmphasis: !!(raw.neighborEmphasis ?? raw.NeighborEmphasis ?? true),
            includeServiceNodes: !!(raw.includeServiceNodes ?? raw.IncludeServiceNodes ?? true),
            includeExpressionNodes: !!(raw.includeExpressionNodes ?? raw.IncludeExpressionNodes ?? false),
            includeConstNodes: !!(raw.includeConstNodes ?? raw.IncludeConstNodes ?? false)
        };
    }

    function normalizeMode(value) {
        if (value === null || value === undefined) {
            return 0;
        }
        const num = Number(value);
        if (!Number.isFinite(num)) {
            return 0;
        }
        if (num === 1) {
            return 1;
        }
        if (num === 2) {
            return 2;
        }
        return 0;
    }

    function resolveOverlay(mode, autoDefault) {
        if (mode === 1) {
            return true;
        }
        if (mode === 2) {
            return false;
        }
        return !!autoDefault;
    }

    window.FlowTime = window.FlowTime || {};
    window.FlowTime.TopologyCanvas = {
        render,
        dispose
    };
})();
