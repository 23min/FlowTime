// Assemble the Derived Models page: shared styles from the Building Blocks source, figures drawn here.
// usage: node build.cjs [building-blocks url] [census-engine url]   writes ../out/flowtime-derived-models.html
const fs = require("fs");
const d = __dirname;
const [bbUrl = "https://claude.ai/artifact/PwGNkMKjdG8Kb7C2pySafj", ceUrl = "https://claude.ai/artifact/4VE6Fo381wjFxvtJwojnvm"] = process.argv.slice(2);
const data = JSON.parse(fs.readFileSync(d + "/data.json", "utf8"));
const css = fs.readFileSync(d + "/../building-blocks/page.src.html", "utf8").split("<style>\n")[1].split("\n</style>")[0];

const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;");
const f1 = (x) => x.toFixed(1);
const t = (x, y, s, cls = "", extra = "") => `<text x="${f1(x)}" y="${f1(y)}"${cls ? ` class="${cls}"` : ""}${extra}>${esc(s)}</text>`;
const marker = (id, cls) => `<marker id="${id}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path class="${cls}" d="M0,0 L10,5 L0,10 z"/></marker>`;
const svg = (w, h, label, body) => `      <svg class="fig" viewBox="0 0 ${w} ${h}" role="img" aria-label="${esc(label).replace(/"/g, "&quot;")}">\n${body}\n      </svg>`;
const clock = (b) => { const s = 36000 + b * 10; return `${Math.floor(s / 3600)}:${String(Math.floor(s / 60) % 60).padStart(2, "0")}`; };

// Figure 2: from incident to prediction.
function fig2() {
  const o = ["<defs>", marker("d2-ink", "mk-ink"), "</defs>"];
  const X = [20, 288, 556, 824], W = 220, R = [40, 196], H = 92;
  const box = (c, r, title, l2, l3, cls = "box") => {
    const x = X[c], y = R[r];
    return [`<rect class="${cls}" x="${x}" y="${y}" width="${W}" height="${H}" rx="6"/>`, t(x + 14, y + 26, title, "b s13" + (cls === "box-cob" ? " c-cob" : cls === "box-amb-d" ? " c-amb" : "")), t(x + 14, y + 47, l2, "s11"), t(x + 14, y + 65, l3, "s11 mut")].join("");
  };
  o.push(box(0, 0, "Incident telemetry", "metrics, traces, logs", "stress a calm day never shows"));
  o.push(box(1, 0, "Replay", "observed demand in", "the model runs the incident"));
  o.push(box(2, 0, "Residuals", "model minus telemetry", "per component and bin", "box-cob"));
  o.push(box(3, 0, "Mechanism chosen", "where the residuals point", "or added to the library"));
  o.push(box(3, 1, "Counterfactuals", "on the incident window", "what if the breaker opened"));
  o.push(box(2, 1, "Coverage map", "what is checked, at what load", "grows with each incident"));
  o.push(box(1, 1, "Predictions", "inside checked ground", "unchecked assumptions listed"));
  o.push(box(0, 1, "Built models", "for systems not built yet", "assumed throughout", "box-amb-d"));
  const a = (dd) => `<path class="ln" d="${dd}" marker-end="url(#d2-ink)"/>`;
  const gap = (c) => (X[c] + W + X[c + 1]) / 2;
  o.push(a(`M${X[0] + W},86 H${X[1] - 2}`), t(gap(0), 78, "read", "s11 halo", ` text-anchor="middle"`));
  o.push(a(`M${X[1] + W},86 H${X[2] - 2}`), t(gap(1), 78, "compare", "s11 halo", ` text-anchor="middle"`));
  o.push(a(`M${X[2] + W},86 H${X[3] - 2}`), t(gap(2), 78, "explain", "s11 halo", ` text-anchor="middle"`));
  o.push(a(`M${X[3] + 110},132 V194`), t(X[3] + 120, 168, "replay again", "s11 halo"));
  o.push(a(`M${X[3]},242 H${X[2] + W + 2}`), t(gap(2), 234, "checks", "s11 halo", ` text-anchor="middle"`));
  o.push(a(`M${X[2]},242 H${X[1] + W + 2}`), t(gap(1), 234, "bounds", "s11 halo", ` text-anchor="middle"`));
  o.push(a(`M${X[2] + 110},196 V164 H${X[0] + 110} V134`), t((X[0] + X[2]) / 2 + 110, 159, "the next incident", "s11 halo", ` text-anchor="middle"`));
  return svg(1080, 304, "Two rows. Top, left to right: incident telemetry is read into a replay with observed demand, compared into residuals per component and bin, and explained by choosing or adding a mechanism. Bottom, right to left: the mechanism is replayed again for counterfactuals on the incident window, which check a coverage map of what is checked at what load, which bounds predictions that list their unchecked assumptions. An arrow from the coverage map back to incident telemetry is labeled the next incident. Apart, outlined in amber, built models serve systems not built yet and are assumed throughout.", o.join("\n"));
}

// Figure 3: rehearsal; telemetry as dots each minute, the draft model as a line.
function fig3() {
  const X0 = 70, X1 = 980, bx = (b) => X0 + b / 359 * (X1 - X0);
  const panels = [
    { key: "latency", title: "checkout latency, log scale", top: 56, bot: 150, y: (v, p) => p.bot - (Math.log10(Math.min(1000, Math.max(0.1, v))) + 1) / 4 * (p.bot - p.top), ticks: [[0.1, "0.1 s"], [1, "1 s"], [10, "10 s"], [100, "100 s"], [1000, "1000 s"]] },
    { key: "apiQueue", title: "requests queued at the api", top: 190, bot: 280, y: (v, p) => p.bot - v / 20000 * (p.bot - p.top), ticks: [[0, "0"], [10000, "10,000"], [20000, "20,000"]] },
    { key: "apiInUse", title: "api workers in use, of 16", top: 320, bot: 410, y: (v, p) => p.bot - v / 16 * (p.bot - p.top), ticks: [[0, "0"], [8, "8"], [16, "16"]] },
  ];
  const o = [];
  o.push(`<circle class="tdot" cx="${X0 + 6}" cy="16" r="3.4"/>`, t(X0 + 18, 20, "telemetry: the toy with held workers, one dot a minute", "s11"));
  o.push(`<path class="k-model" d="M${X0 + 400},16 H${X0 + 426}"/>`, t(X0 + 434, 20, "draft model: the same toy without held workers", "s11"));
  for (const p of panels) {
    o.push(t(X0, p.top - 12, p.title, "s11 mut"));
    for (const [v, s] of p.ticks) o.push(`<path class="c-grid" d="M${X0},${f1(p.y(v, p))} H${X1}"/>`, t(X0 - 8, p.y(v, p) + 4, s, "tm s10 mut", ` text-anchor="end"`));
    o.push(`<polyline class="k-model" points="${data.draft[p.key].map((v, i) => `${f1(bx(i))},${f1(p.y(v, p))}`).join(" ")}"/>`);
    for (let i = 0; i < 360; i += 6) o.push(`<circle class="tdot" cx="${f1(bx(i))}" cy="${f1(p.y(data.truth[p.key][i], p))}" r="2.6"/>`);
  }
  const p3 = panels[2];
  o.push(t(bx(4), p3.y(12.2, p3), "before the surge: about 7 in use, the draft says 0.8", "s11 halo"));
  for (const [s, b] of [["10:00", 0], ["10:15", 90], ["10:30", 180], ["10:45", 270], ["11:00", 359]]) o.push(t(bx(b), 432, s, "tm s10 mut", ` text-anchor="middle"`));
  return svg(1000, 442, "Three charts over the hour, telemetry as dots and the draft model as a line. Checkout latency: both at 0.17 seconds before the surge; telemetry then climbs to about 6 minutes and stays high until about 10:58, while the draft peaks near 30 seconds and recovers at 10:34. Requests queued at the api: telemetry rises to about 18,000, the draft stays at zero. api workers in use: telemetry about 7 before the surge and 16 of 16 during it; the draft 0.8 before and 1.4 at most.", o.join("\n"));
}

const fill = { CSS: css, FIG2: fig2(), FIG3: fig3(), BB_URL: bbUrl, CE_URL: ceUrl };
let s = fs.readFileSync(d + "/derived.src.html", "utf8");
s = s.replace(/\{\{([A-Z_0-9]+)\}\}/g, (m, k) => { if (!(k in fill)) throw new Error("no value for " + k); return fill[k]; });
fs.mkdirSync(d + "/../out", { recursive: true });
fs.writeFileSync(d + "/../out/flowtime-derived-models.html", s);
console.log("wrote", s.length, "bytes");
