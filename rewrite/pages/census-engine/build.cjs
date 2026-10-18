// Assemble the Census Engine page: shared styles from the Building Blocks source, figures drawn here from data.json.
// usage: node build.cjs [building-blocks url] [blueprint url] [derived-models url]   writes ../out/flowtime-census-engine.html
const fs = require("fs");
const d = __dirname;
const [bbUrl = "https://claude.ai/artifact/PwGNkMKjdG8Kb7C2pySafj", bpUrl = "https://claude.ai/artifact/86cuHmBJpxjY13m38VnKoV", derivedUrl = "https://claude.ai/artifact/HNqTFgnjGuhE8S3buNRnFd"] = process.argv.slice(2);
const data = JSON.parse(fs.readFileSync(d + "/data.json", "utf8"));
const css = fs.readFileSync(d + "/../building-blocks/page.src.html", "utf8").split("<style>\n")[1].split("\n</style>")[0];

const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;");
const f1 = (x) => x.toFixed(1);
const t = (x, y, s, cls = "", extra = "") => `<text x="${f1(x)}" y="${f1(y)}"${cls ? ` class="${cls}"` : ""}${extra}>${esc(s)}</text>`;
const marker = (id, cls) => `<marker id="${id}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path class="${cls}" d="M0,0 L10,5 L0,10 z"/></marker>`;
const svg = (w, h, label, body) => `      <svg class="fig" viewBox="0 0 ${w} ${h}" role="img" aria-label="${esc(label).replace(/"/g, "&quot;")}">\n${body}\n      </svg>`;
const clock = (b) => { const s = 36000 + b * 10; return `${Math.floor(s / 3600)}:${String(Math.floor(s / 60) % 60).padStart(2, "0")}`; };

// Figure 1: four mechanism sets, latency (log) and failed checkouts.
function fig1() {
  const X0 = 64, X1 = 980, bx = (b) => X0 + b / 359 * (X1 - X0);
  const LT = 60, LB = 180, ly = (v) => LB - (Math.log10(Math.min(1000, Math.max(0.1, v))) + 1) / 4 * (LB - LT);
  const FT = 228, FB = 298, fy = (v) => FB - v / 80 * (FB - FT);
  const o = [];
  const keys = [["A", "k-a", "as on Building Blocks"], ["B", "k-b", "+ api holds a worker"], ["C", "k-c", "2 s timeout"], ["D", "k-d", "2 s timeout, pricing serves abandoned calls"]];
  let lx = X0;
  for (const [, cls, lab] of keys) { o.push(`<path class="${cls}" d="M${lx},16 H${lx + 26}"/>`, t(lx + 34, 20, lab, "s11")); lx += 34 + lab.length * 6.2 + 34; }
  o.push(`<rect class="surge" x="${f1(bx(78))}" y="${LT}" width="${f1(bx(192) - bx(78))}" height="${LB - LT}"/>`);
  o.push(`<rect class="surge" x="${f1(bx(78))}" y="${FT}" width="${f1(bx(192) - bx(78))}" height="${FB - FT}"/>`);
  o.push(t(X0, 48, "checkout latency, log scale", "s11 mut"), t(X0, 216, "failed checkouts per second", "s11 mut"));
  for (const [v, s] of [[0.1, "0.1 s"], [1, "1 s"], [10, "10 s"], [100, "100 s"], [1000, "1000 s"]]) o.push(`<path class="c-grid" d="M${X0},${f1(ly(v))} H${X1}"/>`, t(X0 - 8, ly(v) + 4, s, "tm s10 mut", ` text-anchor="end"`));
  for (const v of [0, 40, 80]) o.push(`<path class="c-grid" d="M${X0},${f1(fy(v))} H${X1}"/>`, t(X0 - 8, fy(v) + 4, String(v), "tm s10 mut", ` text-anchor="end"`));
  for (const [s, b] of [["10:00", 0], ["10:15", 90], ["10:30", 180], ["10:45", 270], ["11:00", 359]]) o.push(t(bx(b), 316, s, "tm s10 mut", ` text-anchor="middle"`));
  o.push(`<path class="ln-mut" d="M${f1(bx(78))},326 V332 H${f1(bx(192))} V326"/>`, t((bx(78) + bx(192)) / 2, 346, "demand at 70 checkouts a second, 40 before and after", "s11 mut", ` text-anchor="middle"`));
  const pts = (arr, f) => arr.map((v, i) => `${f1(bx(i))},${f1(f(v))}`).join(" ");
  for (const k of ["D", "A", "C", "B"]) {
    const cls = keys.find((x) => x[0] === k)[1];
    o.push(`<polyline class="${cls}" points="${pts(data.four[k].latency, ly)}"/>`, `<polyline class="${cls}" points="${pts(data.four[k].failed, fy)}"/>`);
  }
  return svg(1000, 356, "Two charts over the hour from 10:00 to 11:00, for four sets of mechanisms on the same demand. Checkout latency on a log scale: as on Building Blocks it rises to about 30 seconds during the surge; with the api holding a worker it climbs to about 6 minutes and stays high until 10:58; with a 2-second timeout it stays near 2 seconds, in both timeout runs. Failed checkouts per second: about 17 during the surge as on Building Blocks and with a 2-second timeout, none with the api holding a worker, and 70, every checkout, when pricing serves abandoned calls, falling only to 40 after the surge, still every checkout.", o.join("\n"));
}

// Figure 2: one checkout's journey as stages.
function fig2() {
  const o = ["<defs>", marker("c2-ink", "mk-ink"), marker("c2-red", "mk-red"), "</defs>"];
  const glyph = (x, y, w, h) => [10, 8, 6, 4, 2].map((bh, i) => `<rect class="gl" x="${x + w - 46 + i * 8}" y="${y + h - 8 - bh}" width="5" height="${bh}"/>`).join("");
  const box = (x, y, w, title, sub, cls = "box") => [`<rect class="${cls}" x="${x}" y="${y}" width="${w}" height="64" rx="6"/>`, t(x + 12, y + 22, title, "b s13" + (cls === "box-red" ? " c-red" : "")), t(x + 12, y + 39, sub, "s11 mut"), glyph(x, y, w, 64)].join("");
  o.push(`<rect class="band" x="152" y="30" width="756" height="192" rx="10"/>`, t(166, 50, "holds one of the api's 16 workers", "s11 b c-cob"));
  o.push(box(20, 140, 112, "queued", "at api"), box(168, 140, 124, "working", "at api, 20 ms"));
  o.push(box(345, 56, 160, "waiting on cart", "cart, then its db"), box(345, 140, 160, "waiting on pricing", "3 calls, attempt 1"));
  o.push(box(560, 140, 104, "backoff", "1 s"), box(725, 140, 160, "waiting on pricing", "attempts 2 and 3"));
  o.push(box(945, 56, 118, "done", "all replies in"), box(945, 140, 118, "failed", "after attempt 3", "box-red"));
  o.push(box(230, 250, 150, "email sent", "holds nothing"));
  o.push(box(560, 330, 150, "queued", "at pricing"), box(760, 330, 150, "working", "at pricing, 150 ms"));
  const a = (d, cls = "ln", m = "c2-ink") => `<path class="${cls}" d="${d}" marker-end="url(#${m})"/>`;
  o.push(a("M132,172 H166"), t(149, 164, "start", "s11 halo", ` text-anchor="middle"`));
  o.push(`<path class="ln" d="M292,172 H318"/>`, `<circle cx="318" cy="172" r="3.5" class="mk-ink"/>`);
  o.push(a("M318,172 V88 H343"), a("M318,172 H343"), a("M318,172 V248"), t(326, 232, "no wait", "s11 halo"));
  o.push(a("M505,88 H943"), t(725, 82, "reply", "s11 halo", ` text-anchor="middle"`));
  o.push(a("M470,140 V128 H925 V112 H943"), `<path class="ln" d="M805,140 V128"/>`, t(690, 124, "replies", "s11 halo", ` text-anchor="middle"`));
  o.push(a("M505,172 H558"), t(532, 164, "timed out", "s11 halo", ` text-anchor="middle"`));
  o.push(a("M664,172 H723"), t(694, 164, "retry", "s11 halo", ` text-anchor="middle"`));
  o.push(a("M885,172 H943", "ln-red", "c2-red"), t(914, 164, "timed out", "s11 halo c-red", ` text-anchor="middle"`));
  o.push(a("M470,204 V362 H558", "ln-d"), a("M745,204 V292 H635 V328", "ln-d"), t(480, 262, "each call enters pricing's own stages", "s11 halo"));
  o.push(a("M710,362 H758"), t(734, 354, "start", "s11 halo", ` text-anchor="middle"`));
  o.push(a("M835,330 V206", "ln-d"), t(845, 270, "a reply ends the wait", "s11 halo"));
  o.push(glyph(20, 404, 46, 50), t(76, 448, "count of requests by age", "s11"));
  o.push(a("M290,444 H322"), t(330, 448, "a transition, counted each step", "s11"));
  o.push(`<rect class="band" x="560" y="435" width="28" height="16" rx="3"/>`, t(596, 448, "holds an api worker", "s11"));
  o.push(a("M770,444 H802", "ln-d"), t(810, 448, "enters another component's stages", "s11"));
  return svg(1080, 470, "One checkout's journey as stages. Queued at api, then working at api. A fork sends three branches: waiting on cart, which replies to done; waiting on pricing, attempt 1, which replies to done or times out into a 1-second backoff and then attempts 2 and 3, which reply to done or time out into failed; and email sent, which holds nothing. A shaded band marks the stages that hold one of the api's 16 workers: working, waiting on cart, waiting on pricing, backoff. Each pricing call enters pricing's own stages, queued and working at pricing, and a reply ends the wait. Each stage shows a small histogram: its count of requests by age.", o.join("\n"));
}

// Figure 3: three kinds of edge.
function fig3() {
  const o = ["<defs>", marker("c3-cob", "mk-cob"), marker("c3-amb", "mk-amb"), marker("c3-mut", "mk-mut"), "</defs>"];
  for (const [x, k] of [[16, "1 · requests move"], [372, "2 · resources are held"], [728, "3 · information is delayed"]]) o.push(`<rect class="box-in" x="${x}" y="16" width="336" height="268" rx="8"/>`, t(x + 16, 40, k, "up"));
  // 1
  o.push(`<rect class="box" x="40" y="80" width="100" height="48" rx="6"/>`, t(52, 109, "api", "b s13"));
  o.push(`<rect class="box" x="224" y="80" width="104" height="48" rx="6"/>`, t(236, 109, "pricing", "b s13"));
  o.push(`<path class="ln-cob" d="M140,96 H222" marker-end="url(#c3-cob)"/>`, t(181, 89, "calls", "s11 halo", ` text-anchor="middle"`));
  o.push(`<path class="ln-cob" d="M224,114 H142" marker-end="url(#c3-cob)"/>`, t(183, 131, "replies", "s11 halo", ` text-anchor="middle"`));
  o.push(t(32, 176, "Conserved: arrivals equal", "s13"), t(32, 194, "departures plus the change in", "s13"), t(32, 212, "the census, every step.", "s13"));
  o.push(t(32, 240, "Delay: the time spent in each stage.", "s11 mut"), t(32, 258, "Retries: unrolled by attempt.", "s11 mut"));
  // 2
  o.push(`<rect class="box" x="392" y="62" width="150" height="88" rx="6"/>`, t(404, 82, "api", "b s13"));
  for (let i = 0; i < 16; i++) o.push(`<rect x="${404 + (i % 8) * 16}" y="${92 + Math.floor(i / 8) * 15}" width="12" height="10" rx="2" class="${i < 2 ? "slot-on" : i < 11 ? "slot-held" : "slot-off"}"/>`);
  o.push(t(404, 140, "11 of 16 in use", "s11 mut"));
  o.push(`<rect class="box" x="584" y="82" width="104" height="48" rx="6"/>`, t(596, 111, "pricing", "b s13"));
  o.push(`<path class="ln-cob" d="M542,98 H582" marker-end="url(#c3-cob)"/>`);
  o.push(`<path class="ln-amb-d" d="M636,130 V166 H470 V152" marker-end="url(#c3-amb)"/>`, t(482, 184, "slower replies hold workers longer", "s11 c-amb"));
  o.push(t(388, 214, "Coupled through the state:", "s13"), t(388, 232, "the workers in use.", "s13"), t(388, 258, "Steep when it reacts faster than the step.", "s11 mut"));
  // 3
  o.push(`<rect class="box" x="752" y="116" width="100" height="44" rx="6"/>`, t(764, 143, "pricing", "b s13"));
  o.push(`<rect class="box" x="930" y="116" width="114" height="44" rx="6"/>`, t(942, 143, "autoscaler", "b s13"));
  o.push(`<path class="ln-mut-d" d="M802,116 V84 H986 V114" marker-end="url(#c3-mut)"/>`, t(894, 76, "60 s average, read every 15 s", "s11 halo", ` text-anchor="middle"`));
  o.push(`<path class="ln-mut-d" d="M986,160 V194 H802 V162" marker-end="url(#c3-mut)"/>`, t(894, 212, "new pods ready 90 s later", "s11 halo", ` text-anchor="middle"`));
  o.push(t(744, 240, "Real delays, declared on the mechanism.", "s13"), t(744, 258, "Exact when the step is no longer than the delay.", "s11 mut"));
  return svg(1080, 300, "Three panels. One, requests move: api calls pricing and pricing replies; arrivals equal departures plus the change in the census in every step; delay is the time spent in each stage; retries are unrolled by attempt. Two, resources are held: the api has 16 workers, 2 working and 9 held by calls waiting on pricing; slower replies from pricing hold workers longer; the coupling goes through the state. Three, information is delayed: the autoscaler reads pricing's 60-second average every 15 seconds, and new pods are ready 90 seconds later; real delays are declared on the mechanism.", o.join("\n"));
}

// Figure 4: the ladder of loop speeds on a log time axis.
function fig4() {
  const A0 = 120, A1 = 1040, lo = -2, hi = Math.log10(86400), x = (s) => A0 + (Math.log10(s) - lo) / (hi - lo) * (A1 - A0);
  const o = [];
  for (const [s, lab] of [[10, "10 s step, the toy"], [300, "5-minute bin, 11 of 12 templates"]]) o.push(`<path class="ln-mut-d" d="M${f1(x(s))},42 V300"/>`, t(x(s), 32, lab, "s11 mut", ` text-anchor="middle"`));
  const rows = [
    ["held workers and connections", 0.02, 1, "bar-res"],
    ["timeouts and retries", 1, 30, "bar-req"],
    ["load balancer's view of queues", 1, 10, "bar-inf"],
    ["circuit breaker", 10, 60, "bar-inf"],
    ["autoscaler", 60, 600, "bar-inf"],
    ["cache warmth", 60, 3600, "bar-in"],
    ["demand over the day", 3600, 86400, "bar-in"],
  ];
  rows.forEach(([lab, a, b, cls], i) => {
    const y = 64 + i * 34, xa = x(a), xb = x(b), right = xb + 10 + lab.length * 6.4 < 1068;
    o.push(`<rect class="${cls}" x="${f1(xa)}" y="${y - 8}" width="${f1(xb - xa)}" height="16" rx="4"/>`);
    o.push(right ? t(xb + 10, y + 4, lab, "s13 halo") : t(xa - 10, y + 4, lab, "s13 halo", ` text-anchor="end"`));
  });
  o.push(`<path class="c-ax" d="M${A0},300 H${A1}"/>`);
  for (const [s, lab] of [[0.01, "10 ms"], [0.1, "100 ms"], [1, "1 s"], [10, "10 s"], [60, "1 min"], [600, "10 min"], [3600, "1 h"], [86400, "1 day"]]) o.push(`<path class="c-ax" d="M${f1(x(s))},300 V306"/>`, t(x(s), 320, lab, "tm s10 mut", ` text-anchor="middle"`));
  let lx = A0;
  for (const [cls, lab] of [["bar-req", "through requests"], ["bar-res", "through resources"], ["bar-inf", "through information"], ["bar-in", "state or input"]]) { o.push(`<rect class="${cls}" x="${lx}" y="338" width="24" height="12" rx="3"/>`, t(lx + 32, 348, lab, "s11")); lx += 32 + lab.length * 6.2 + 40; }
  return svg(1080, 360, "Loops placed on a log time axis from 10 milliseconds to 1 day. Held workers and connections, through resources, 20 milliseconds to 1 second. Timeouts and retries, through requests, 1 to 30 seconds. The load balancer's view of queues, 1 to 10 seconds, the circuit breaker, 10 to 60 seconds, and the autoscaler, 1 to 10 minutes, all through information. Cache warmth, 1 minute to 1 hour, and demand over the day, 1 hour to 1 day. Dashed lines mark the toy's 10-second step and the 5-minute bin that 11 of 12 templates use.", o.join("\n"));
}

// Figure 5: holding time, forward only versus solved, 10:10 to 10:40.
function fig5() {
  const B0 = 60, B1 = 240, X0 = 64, X1 = 980, bx = (b) => X0 + (b - B0) / (B1 - B0) * (X1 - X0);
  const T = 50, Bt = 200, y = (v) => Bt - (Math.log10(Math.min(10, Math.max(0.1, v))) + 1) / 2 * (Bt - T);
  const o = [];
  let lx = X0;
  for (const [cls, lab] of [["k-d k-thin", "forward only, 10 s steps"], ["k-e", "solve inside the step, 10 s steps"], ["k-c", "forward only, 0.1 s steps"]]) { o.push(`<path class="${cls}" d="M${lx},16 H${lx + 26}"/>`, t(lx + 34, 20, lab, "s11")); lx += 34 + lab.length * 6.2 + 34; }
  o.push(t(X0, 40, "api holding time per call, log scale", "s11 mut"));
  for (const [v, s] of [[0.1, "0.1 s"], [0.3, "0.3 s"], [1, "1 s"], [3, "3 s"], [10, "10 s"]]) o.push(`<path class="c-grid" d="M${X0},${f1(y(v))} H${X1}"/>`, t(X0 - 8, y(v) + 4, s, "tm s10 mut", ` text-anchor="end"`));
  for (const b of [60, 120, 180, 240]) o.push(t(bx(b), 222, clock(b), "tm s10 mut", ` text-anchor="middle"`));
  const pts = (arr) => arr.slice(B0, B1 + 1).map((v, i) => `${f1(bx(B0 + i))},${f1(y(v))}`).join(" ");
  o.push(`<polyline class="k-e" points="${pts(data.forward.solved10.hold)}"/>`);
  o.push(`<polyline class="k-d k-thin" points="${pts(data.forward.explicit10.hold)}"/>`);
  o.push(`<polyline class="k-c" points="${pts(data.forward.explicit01.hold)}"/>`);
  return svg(1000, 236, "The api's holding time per call from 10:10 to 10:40 on a log scale. Forward only with 10-second steps, it swings every step between 0.17 and 4 seconds. Solved inside the step with 10-second steps, it holds at 0.31 seconds through the surge. Forward only with 0.1-second steps, averaged per 10 seconds, it lies on the solved line at 0.30 seconds.", o.join("\n"));
}

const tb = data.table, n0 = (x) => Math.round(x).toLocaleString("en-US"), sec = (x) => x < 120 ? x.toFixed(1) + " s" : (x / 60).toFixed(1) + " min", rate = (x) => x.toFixed(1) + "/s";
const fill = {
  CSS: css, FIG1: fig1(), FIG2: fig2(), FIG3: fig3(), FIG4: fig4(), FIG5: fig5(), BB_URL: bbUrl, BP_URL: bpUrl, DERIVED_URL: derivedUrl,
  A_PQ: n0(tb.A.pricingQ), A_LAT: sec(tb.A.latencyPeak), A_FAIL: rate(tb.A.failedPeak), A_FAILEND: tb.A.failingUntil, A_QEND: tb.A.queuesUntil,
  B_AQ: n0(tb.B.apiQ), B_LAT: sec(tb.B.latencyPeak), B_QEND: tb.B.queuesUntil,
  C_PQ: n0(tb.C.pricingQ), C_LAT: sec(tb.C.latencyPeak), C_FAIL: rate(tb.C.failedPeak), C_QEND: `failures stop and queue gone ${tb.C.queuesUntil}`,
  D_PQ: n0(tb.D.pricingQ), D_LAT: sec(tb.D.latencyPeak), D_FAIL: rate(tb.D.failedPeak).replace(".0/s", "/s"),
};
let s = fs.readFileSync(d + "/census.src.html", "utf8");
s = s.replace(/\{\{([A-Z_0-9]+)\}\}/g, (m, k) => { if (!(k in fill)) throw new Error("no value for " + k); return fill[k]; });
s = s.replace("</style>\n<style>", "</style>\n<style>\n.slot-held { fill: var(--amber); }");
fs.mkdirSync(d + "/../out", { recursive: true });
fs.writeFileSync(d + "/../out/flowtime-census-engine.html", s);
console.log("wrote", s.length, "bytes; checks:", JSON.stringify({ A: tb.A.failingUntil + "/" + tb.A.queuesUntil, C: tb.C.failingUntil, D: tb.D.failingUntil }));
