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
                deviceRatio: window.devicePixelRatio || 1
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
            draw(canvas, state);
        };

        resize();
        window.addEventListener('resize', resize);

        const pointerDown = (event) => {
            canvas.setPointerCapture(event.pointerId);
            state.dragging = true;
            state.dragStart = { x: event.clientX, y: event.clientY };
            state.panStart = { x: state.offsetX, y: state.offsetY };
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

        ctx.lineWidth = 2;
        ctx.strokeStyle = '#A0A6B1';
        ctx.globalAlpha = 0.85;

        for (const edge of edges) {
            const fromX = edge.fromX ?? edge.FromX;
            const fromY = edge.fromY ?? edge.FromY;
            const toX = edge.toX ?? edge.ToX;
            const toY = edge.toY ?? edge.ToY;

            ctx.beginPath();
            ctx.moveTo(fromX, fromY);
            ctx.lineTo(toX, toY);
            ctx.stroke();
        }

        ctx.globalAlpha = 1;

        for (const node of nodes) {
            const x = node.x ?? node.X;
            const y = node.y ?? node.Y;
            const radius = node.radius ?? node.Radius ?? 18;
            const fill = node.fill ?? node.Fill ?? '#7A7A7A';
            const stroke = node.stroke ?? node.Stroke ?? '#262626';

            ctx.beginPath();
            ctx.fillStyle = fill;
            ctx.strokeStyle = stroke;
            ctx.lineWidth = 2;
            ctx.arc(x, y, radius, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();

            if (node.isFocused ?? node.IsFocused) {
                ctx.beginPath();
                ctx.strokeStyle = '#FFFFFF';
                ctx.lineWidth = 3;
                ctx.setLineDash([6, 4]);
                ctx.arc(x, y, radius + 5, 0, Math.PI * 2);
                ctx.stroke();
                ctx.setLineDash([]);
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

    window.FlowTime = window.FlowTime || {};
    window.FlowTime.TopologyCanvas = {
        render,
        dispose
    };
})();
