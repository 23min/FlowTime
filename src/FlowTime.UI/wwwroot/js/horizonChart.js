/* Lightweight horizon chart renderer for inspector overviews. */
(function () {
  function clamp(v, min, max) { return Math.min(max, Math.max(min, v)); }
  function isFiniteNumber(v) { return typeof v === 'number' && isFinite(v); }
  function lerp(a, b, t) { return a + (b - a) * t; }
  function hexToRgb(hex) {
    const v = hex.replace('#', '');
    const bigint = parseInt(v.length === 3 ? v.split('').map(c => c + c).join('') : v, 16);
    return { r: (bigint >> 16) & 255, g: (bigint >> 8) & 255, b: bigint & 255 };
  }
  function rgbToHex(r, g, b) {
    const to = (x) => x.toString(16).padStart(2, '0');
    return `#${to(r)}${to(g)}${to(b)}`;
  }
  function withAlpha(hex, alpha) {
    const { r, g, b } = hexToRgb(hex);
    return `rgba(${r},${g},${b},${clamp(alpha, 0, 1)})`;
  }
  function pastelize(hex, amount) {
    const c = hexToRgb(hex);
    const r = Math.round(lerp(c.r, 255, amount));
    const g = Math.round(lerp(c.g, 255, amount));
    const b = Math.round(lerp(c.b, 255, amount));
    return rgbToHex(r, g, b);
  }

  const COLORS = {
    success: '#009E73',
    warning: '#E69F00',
    error: '#D55E00',
    neutral: '#E2E8F0'
  };

  function pickColor(value, basis, t) {
    const s = t || {
      slaSuccess: 0.95,
      slaWarning: 0.8,
      utilWarning: 0.9,
      utilCritical: 0.95,
      errorWarning: 0.02,
      errorCritical: 0.05,
      serviceTimeWarning: 400,
      serviceTimeCritical: 700,
      flowLatencyWarning: 2000,
      flowLatencyCritical: 10000
    };
    if (!isFiniteNumber(value)) return COLORS.neutral;
    if (basis === 'utilization') {
      if (value >= s.utilCritical) return COLORS.error;
      if (value >= s.utilWarning) return COLORS.warning;
      return COLORS.success;
    }
    if (basis === 'errors') {
      if (value >= s.errorCritical) return COLORS.error;
      if (value >= s.errorWarning) return COLORS.warning;
      return COLORS.success;
    }
    if (basis === 'queue') {
      if (value >= 0.8) return COLORS.error;
      if (value >= 0.4) return COLORS.warning;
      return COLORS.success;
    }
    if (basis === 'servicetime') {
      if (value >= s.serviceTimeCritical) return COLORS.error;
      if (value >= s.serviceTimeWarning) return COLORS.warning;
      return COLORS.success;
    }
    if (basis === 'flowlatency') {
      if (value >= s.flowLatencyCritical) return COLORS.error;
      if (value >= s.flowLatencyWarning) return COLORS.warning;
      return COLORS.success;
    }
    // SLA
    if (value >= s.slaSuccess) return COLORS.success;
    if (value >= s.slaWarning) return COLORS.warning;
    return COLORS.error;
  }

  function isDarkTheme() {
    const body = document.body;
    if (!body) return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const datasetTheme = body.dataset ? body.dataset.ftTheme : null;
    return datasetTheme === 'dark'
      || body.getAttribute('data-ft-theme') === 'dark'
      || body.classList.contains('dark-mode')
      || body.classList.contains('mud-theme-dark')
      || body.classList.value.includes('mud-theme-dark')
      || (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
  }

  function renderHorizon(canvas, opts) {
    if (!canvas || !opts) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const data = Array.isArray(opts.data) ? opts.data : [];
    const basis = (opts.basis || 'sla').toString().toLowerCase();
    const thresholds = opts.thresholds || {};
    const bands = Math.max(1, Math.min(6, opts.bands || 3));
    const width = canvas.width || 360;
    const height = canvas.height || (opts.height || 18);
    if (canvas.height !== height) canvas.height = height;

    // Compute min/max with provided overrides
    let min = (typeof opts.min === 'number') ? opts.min : 0;
    let max = (typeof opts.max === 'number') ? opts.max : 0;
    if (!(isFiniteNumber(min) && isFiniteNumber(max))) {
      min = Infinity; max = -Infinity;
      for (let i = 0; i < data.length; i++) {
        const v = data[i];
        if (isFiniteNumber(v)) { if (v < min) min = v; if (v > max) max = v; }
      }
      if (!isFinite(min)) min = 0; if (!isFinite(max)) max = 0;
    }
    if (Math.abs(max - min) < 1e-9) { max = min + 0.001; }

    ctx.clearRect(0, 0, width, height);
    // Background same as sparkline/timeline bg
    ctx.fillStyle = 'rgba(17,24,39,0.02)';
    ctx.fillRect(0, 0, width, height);

    let highlightRect = null;
    if (Number.isInteger(opts.highlightStart) && Number.isInteger(opts.highlightEnd) && data.length > 0) {
      const dark = isDarkTheme();
      const colors = dark
        ? { fill: 'rgba(253,224,71,0.22)', stroke: 'rgba(253,224,71,0.5)' }
        : { fill: 'rgba(251,120,38,0.16)', stroke: 'rgba(194,65,12,0.48)' };
      const start = clamp(opts.highlightStart, 0, data.length - 1);
      const end = clamp(opts.highlightEnd, start, data.length - 1);
      const colWidth = width / Math.max(1, data.length);
      const x = Math.floor(start * colWidth);
      const w = Math.ceil((end - start + 1) * colWidth);
      highlightRect = { x, w, fill: colors.fill, stroke: colors.stroke };
    }

    // Area under line tinted by metric color
    const baseline = 0;
    const range = max - min;
    const colWidth = width / Math.max(1, data.length);
    for (let i = 0; i < data.length; i++) {
      const v = data[i];
      const x = Math.floor(i * colWidth);
      if (!isFiniteNumber(v)) continue;
      const tint = withAlpha(pickColor(v, basis, thresholds), 0.22);
      ctx.fillStyle = tint;
      let y0, h;
      if (min < 0 && max > 0) {
        // Split baseline within chart
        const zeroFrac = clamp((0 - min) / range, 0, 1);
        const baselineY = Math.round((1 - zeroFrac) * height);
        const frac = Math.abs(v) / Math.max(Math.abs(min), Math.abs(max));
        h = Math.max(1, Math.round(frac * height));
        if (v >= 0) { y0 = baselineY - h; } else { y0 = baselineY; }
      } else {
        const frac = clamp((v - min) / range, 0, 1);
        h = Math.max(1, Math.round(frac * height));
        y0 = height - h;
      }
      ctx.fillRect(x, y0, Math.ceil(colWidth + 0.5), h);
    }

    if (highlightRect) {
      ctx.save();
      ctx.fillStyle = highlightRect.fill;
      ctx.fillRect(highlightRect.x, 0, highlightRect.w, height);
      ctx.restore();
    }

    if (highlightRect && highlightRect.stroke) {
      ctx.save();
      ctx.strokeStyle = highlightRect.stroke;
      ctx.lineWidth = 1;
      const inset = 0.5;
      const widthAdj = Math.max(0, highlightRect.w - inset * 2);
      ctx.strokeRect(highlightRect.x + inset, inset, widthAdj, height - inset * 2);
      ctx.restore();
    }

    // Threshold-colored stroke line with segments
    let lastX = null, lastY = null, lastColor = null, pathOpen = false;
    const lineWidth = 1.5;
    for (let i = 0; i < data.length; i++) {
      const v = data[i];
      const x = Math.floor((i + 0.5) * colWidth); // center of bin
      if (!isFiniteNumber(v)) {
        if (pathOpen) { ctx.stroke(); pathOpen = false; lastX = lastY = lastColor = null; }
        continue;
      }
      const color = pickColor(v, basis, thresholds);
      let y;
      if (min < 0 && max > 0) {
        const zeroFrac = clamp((0 - min) / range, 0, 1);
        const baselineY = (1 - zeroFrac) * height;
        const frac = (v - 0) / (v >= 0 ? (max - 0) : (0 - min));
        y = v >= 0 ? baselineY - frac * baselineY : baselineY + Math.abs(frac) * (height - baselineY);
      } else {
        const frac = clamp((v - min) / range, 0, 1);
        y = (1 - frac) * height;
      }

      if (!pathOpen || color !== lastColor) {
        if (pathOpen) { ctx.stroke(); pathOpen = false; }
        ctx.beginPath();
        ctx.lineWidth = lineWidth;
        ctx.lineJoin = 'round';
        ctx.lineCap = 'round';
        ctx.strokeStyle = color;
        ctx.moveTo(x, y);
        pathOpen = true;
      } else {
        ctx.lineTo(x, y);
      }
      lastX = x; lastY = y; lastColor = color;
    }
    if (pathOpen) ctx.stroke();
  }

  window.FlowTime = window.FlowTime || {};
  window.FlowTime.HorizonChart = { renderHorizon };
})();
