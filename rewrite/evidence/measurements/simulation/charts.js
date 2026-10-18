// Builds the two simulation charts from fluid-limit.json: standalone SVGs for the spec, and inline bodies for the HTML page.
const fs = require("fs"), path = require("path");
const [dataFile, styleFile, outDir, inlineDir] = process.argv.slice(2);
const d = JSON.parse(fs.readFileSync(dataFile, "utf8"));
const f1 = v => (Math.round(v * 10) / 10).toString();
const pct = v => (v * 100).toFixed(0) + "%";

// ---------- Figure 5: the fluid limit ----------
function fig5() {
  const W = 270, TOP = 82, BOT = 300, H = BOT - TOP, YMAX = 1.3;
  const X0 = [70, 420, 770];
  const sx = (x0, t) => x0 + (t / 120) * W, sy = v => BOT - (v / YMAX) * H;
  let b = "";
  // legend
  b += `<path class="c-flow" d="M70,22 H98"/><text class="c-t" x="106" y="26">FlowTime, the flow model</text>`;
  b += `<path class="c-sim" d="M300,22 H328"/><text class="c-t" x="336" y="26">simulation, mean of all runs</text>`;
  b += `<rect class="c-band" x="560" y="15" width="28" height="14" rx="2"/><text class="c-t" x="596" y="26">simulation, middle 90% of runs</text>`;
  [1, 10, 100].forEach((scale, i) => {
    const s = d.spike[scale], x0 = X0[i], peak = s.peak, ser = s.series.filter(p => p.t % 2 === 0 || p.t === 90);
    b += `<text class="c-tt" x="${x0}" y="64">${scale}× volume · ${s.runs.toLocaleString("en-US")} runs</text>`;
    for (const v of [0, 0.5, 1]) {
      b += `<line class="${v === 0 ? "c-ax" : "c-grid"}" x1="${x0}" y1="${f1(sy(v))}" x2="${x0 + W}" y2="${f1(sy(v))}"/>`;
      b += `<text class="c-tm" x="${x0 - 6}" y="${f1(sy(v) + 3.5)}" text-anchor="end">${v === 0 ? "0" : pct(v)}</text>`;
    }
    for (const t of [0, 30, 60, 90, 120]) {
      b += `<line class="c-ax" x1="${f1(sx(x0, t))}" y1="${BOT}" x2="${f1(sx(x0, t))}" y2="${BOT + 4}"/>`;
      b += `<text class="c-tm" x="${f1(sx(x0, t))}" y="${BOT + 16}" text-anchor="middle">${t === 120 ? "120 min" : t}</text>`;
    }
    const up = ser.map(p => `${f1(sx(x0, p.t))},${f1(sy(p.p95 / peak))}`), lo = ser.slice().reverse().map(p => `${f1(sx(x0, p.t))},${f1(sy(p.p05 / peak))}`);
    b += `<polygon class="c-band" points="${up.join(" ")} ${lo.join(" ")}"/>`;
    const fluid = [[0, 0], [30, 0], [60, 1], [90, 0], [120, 0]];
    b += `<polyline class="c-flow" points="${fluid.map(([t, v]) => `${f1(sx(x0, t))},${f1(sy(v))}`).join(" ")}"/>`;
    b += `<polyline class="c-sim" points="${ser.map(p => `${f1(sx(x0, p.t))},${f1(sy(p.mean / peak))}`).join(" ")}"/>`;
    const c = s.clear / peak, cx = sx(x0, 90), cy = sy(c);
    b += `<circle class="c-dot" cx="${f1(cx)}" cy="${f1(cy)}" r="3.6"/>`;
    b += `<text class="c-tv" x="${f1(cx + 8)}" y="${f1(cy - 7)}">${(c * 100).toFixed(c < 0.01 ? 2 : 1)}%</text>`;
    const p60 = s.series[60];
    b += `<text class="c-t" x="${x0}" y="${BOT + 40}">middle 90% at the peak: ${pct(p60.p05 / peak).replace("%", "")}–${pct(p60.p95 / peak)}</text>`;
    b += `<text class="c-t" x="${x0}" y="${BOT + 56}">still waiting at 90 min: ${(c * 100).toFixed(c < 0.01 ? 2 : 1)}% of the peak</text>`;
  });
  return { w: 1080, h: 372, title: "The fluid limit: the simulation closes in on the flow model as volume grows", body: b };
}

// ---------- Figure 6: steady load ----------
function fig6() {
  const X0 = 80, W = 520, TOP = 44, BOT = 264, H = BOT - TOP, LMAX = 25;
  const sx = r => X0 + r * W, sy = L => BOT - (L / LMAX) * H;
  let b = "";
  b += `<text class="c-tm" x="${X0}" y="${TOP - 14}">average items in the system</text>`;
  for (const L of [0, 5, 10, 15, 20, 25]) {
    b += `<line class="${L === 0 ? "c-ax" : "c-grid"}" x1="${X0}" y1="${f1(sy(L))}" x2="${X0 + W}" y2="${f1(sy(L))}"/>`;
    b += `<text class="c-tm" x="${X0 - 6}" y="${f1(sy(L) + 3.5)}" text-anchor="end">${L}</text>`;
  }
  for (const r of [0, 0.2, 0.4, 0.6, 0.8, 1]) {
    b += `<line class="c-ax" x1="${f1(sx(r))}" y1="${BOT}" x2="${f1(sx(r))}" y2="${BOT + 4}"/>`;
    b += `<text class="c-tm" x="${f1(sx(r))}" y="${BOT + 16}" text-anchor="middle">${r}</text>`;
  }
  b += `<text class="c-tm" x="${X0 + W / 2}" y="${BOT + 34}" text-anchor="middle">load = arrivals ÷ capacity</text>`;
  const pts = []; for (let r = 0; r <= 0.9615; r += 0.005) pts.push(`${f1(sx(r))},${f1(sy(r / (1 - r)))}`);
  b += `<polyline class="c-theory" points="${pts.join(" ")}"/>`;
  b += `<path class="c-flow" d="M${X0},${BOT} H${X0 + W}"/>`;
  b += `<path class="c-ax" d="M${X0 + 14},${BOT - 26} V${BOT - 2}"/>`;
  b += `<text class="c-tf" x="${X0 + 6}" y="${BOT - 32}">FlowTime's queue: 0 at every load below capacity</text>`;
  b += `<text class="c-t" x="${f1(sx(0.62))}" y="${f1(sy(9.5))}">theory: ρ / (1 − ρ)</text>`;
  for (const p of d.steady) b += `<circle class="c-dot" cx="${f1(sx(p.rho))}" cy="${f1(sy(p.L))}" r="4.2"/>`;
  // table
  const tx = 660;
  b += `<text class="c-tt" x="${tx}" y="${TOP + 6}">Average queue, one server</text>`;
  b += `<text class="c-tm" x="${tx}" y="${TOP + 30}">load</text><text class="c-tm" x="${tx + 120}" y="${TOP + 30}" text-anchor="end">simulated</text><text class="c-tm" x="${tx + 210}" y="${TOP + 30}" text-anchor="end">theory</text><text class="c-tm" x="${tx + 300}" y="${TOP + 30}" text-anchor="end">FlowTime</text>`;
  d.steady.forEach((p, i) => {
    const y = TOP + 52 + i * 20;
    b += `<text class="c-t" x="${tx}" y="${y}">${p.rho}</text><text class="c-t" x="${tx + 120}" y="${y}" text-anchor="end">${p.L.toFixed(2)}</text><text class="c-t" x="${tx + 210}" y="${y}" text-anchor="end">${p.theory.toFixed(2)}</text><text class="c-t" x="${tx + 300}" y="${y}" text-anchor="end">0</text>`;
  });
  const y99 = TOP + 52 + d.steady.length * 20;
  b += `<text class="c-t" x="${tx}" y="${y99}">0.99</text><text class="c-tm" x="${tx + 120}" y="${y99}" text-anchor="end">not run</text><text class="c-t" x="${tx + 210}" y="${y99}" text-anchor="end">99.00</text><text class="c-t" x="${tx + 300}" y="${y99}" text-anchor="end">0</text>`;
  b += `<circle class="c-dot" cx="${tx + 6}" cy="${BOT + 6}" r="4.2"/><text class="c-t" x="${tx + 18}" y="${BOT + 10}">simulation</text>`;
  b += `<path class="c-theory" d="M${tx + 110},${BOT + 6} H${tx + 138}"/><text class="c-t" x="${tx + 146}" y="${BOT + 10}">queueing theory</text>`;
  b += `<path class="c-flow" d="M${tx},${BOT + 28} H${tx + 28}"/><text class="c-t" x="${tx + 36}" y="${BOT + 32}">FlowTime</text>`;
  return { w: 1080, h: 312, title: "At steady load the random queue follows rho/(1-rho), while the flow model reads zero", body: b };
}

const chartCss = `
    .c-grid { stroke: var(--line); stroke-width: 1; }
    .c-ax { stroke: var(--line-2); stroke-width: 1; }
    .c-band { fill: var(--amber); fill-opacity: 0.2; stroke: none; }
    .c-flow { fill: none; stroke: var(--cobalt); stroke-width: 2.2; stroke-linejoin: round; }
    .c-sim { fill: none; stroke: var(--amber); stroke-width: 1.8; stroke-linejoin: round; }
    .c-theory { fill: none; stroke: var(--ink-2); stroke-width: 1.4; stroke-dasharray: 5 4; }
    .c-dot { fill: var(--amber); stroke: var(--surface); stroke-width: 1.5; }
    .c-t { font: 400 11px var(--font-body); fill: var(--ink-2); }
    .c-tm { font: 400 10.5px var(--font-mono); fill: var(--muted); }
    .c-tt { font: 600 12.5px var(--font-body); fill: var(--ink); }
    .c-tv { font: 600 11px var(--font-body); fill: var(--amber); paint-order: stroke; stroke: var(--surface); stroke-width: 4px; stroke-linejoin: round; }
    .c-tf { font: 600 11px var(--font-body); fill: var(--cobalt); paint-order: stroke; stroke: var(--surface); stroke-width: 4px; stroke-linejoin: round; }
  </style>`;
const style = fs.readFileSync(styleFile, "utf8").replace(/\n?  <\/style>\s*$/, chartCss);
for (const [name, fig] of [["4-fluid-limit", fig5()], ["5-steady-load", fig6()]]) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${fig.w} ${fig.h}" width="${fig.w}" height="${fig.h}">
  <title>${fig.title}</title>
${style}
  <rect class="bg" x="0.5" y="0.5" width="${fig.w - 1}" height="${fig.h - 1}" rx="10"/>
  ${fig.body.replace(/></g, ">\n  <")}
</svg>
`;
  fs.writeFileSync(path.join(outDir, name + ".svg"), svg);
  fs.writeFileSync(path.join(inlineDir, name + ".inline.svgbody"), `<svg class="fig" viewBox="0 0 ${fig.w} ${fig.h}" role="img" aria-label="ARIA">\n${fig.body.replace(/></g, ">\n<")}\n</svg>`);
  console.log("wrote", name);
}
