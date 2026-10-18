// Data for the Census Engine page. Four mechanism sets from the building-blocks toy (model2.js),
// and the forward-only versus solved runs of the held-worker loop (limits/explicit.js), both from evidence/toy.
const fs = require("fs");
const T = __dirname + "/../../evidence/toy";
const src = fs.readFileSync(T + "/model2.js", "utf8");
const load = (s) => { const m = { exports: {} }; new Function("module", s)(m); return m.exports; };
const swap = (s, a, b) => { if (s.split(a).length !== 2) throw new Error("no unique match: " + a); return s.replace(a, b); };
const M10 = load(src);
const M1 = load(swap(swap(src, "BIN_S = 10, BINS = 360, MAX_AGE = 90, RETRY_SHARE = 0.5, TIMEOUT_S = 30", "BIN_S = 1, BINS = 3600, MAX_AGE = 90, RETRY_SHARE = 0.5, TIMEOUT_S = 2"),
  "pricing: { servers: 24, svc: 0.150, timeout: 3 }", "pricing: { servers: 24, svc: 0.150, timeout: 2 }"));
const avg = (xs, n) => { const o = []; for (let i = 0; i < xs.length; i += n) { let s = 0; for (let j = i; j < i + n; j++) s += xs[j]; o.push(s / n); } return o; };
const r3 = (x) => Math.round(x * 1000) / 1000;
const clock = (sec) => `${10 + Math.floor(sec / 3600)}:${String(Math.floor(sec / 60) % 60).padStart(2, "0")}`;
const runs = {
  A: { o: M10.run(), n: 1 },
  B: { o: M10.run({ holdWorkers: true }), n: 1 },
  C: { o: M1.run(), n: 10 },
  D: { o: M1.run({ serveAbandoned: true }), n: 10 },
};
const out = { four: {}, table: {}, forward: {} };
for (const [k, { o, n }] of Object.entries(runs)) {
  const bin = 10 / n, last = o.api.length - 1;
  out.four[k] = { latency: avg(o.latency, n).map(r3), failed: avg(o.failed, n).map(r3) };
  const lastFail = o.failed.findLastIndex((x) => x > 0.05);
  const lastQ = Math.max(o.api.findLastIndex((r) => r.backlog > 1), o.pricing.findLastIndex((r) => r.backlog > 1));
  out.table[k] = { apiQ: Math.round(Math.max(...o.api.map((r) => r.backlog))), pricingQ: Math.round(Math.max(...o.pricing.map((r) => r.backlog))),
    latencyPeak: r3(Math.max(...o.latency)), failedPeak: r3(Math.max(...o.failed)), wastedPeak: r3(Math.max(...o.pricing.map((r) => r.wastedRate))),
    failingUntil: lastFail < 0 ? null : lastFail >= last ? "still at 11:00" : clock((lastFail + 1) * bin),
    queuesUntil: lastQ < 0 ? null : lastQ >= last ? "still at 11:00" : clock((lastQ + 1) * bin) };
}
const { run } = require(T + "/limits/explicit.js");
for (const [k, dt, scheme] of [["solved10", 10, "solved"], ["explicit10", 10, "explicit"], ["explicit1", 1, "explicit"], ["explicit01", 0.1, "explicit"]]) {
  const o = run(dt, scheme), n = Math.round(10 / dt);
  out.forward[k] = { hold: avg(o.hold, n).map(r3), holdMin: Math.min(...o.hold.slice(Math.round(1200 / dt), Math.round(1800 / dt))), holdMax: Math.max(...o.hold.slice(Math.round(1200 / dt), Math.round(1800 / dt))),
    latencyPeak: r3(Math.max(...o.latency)), apiBacklogPeak: Math.round(Math.max(...o.apiBacklog)), ms: Math.round(o.ms) };
}
fs.writeFileSync(__dirname + "/data.json", JSON.stringify(out));
console.log(JSON.stringify(out.table, null, 1));
for (const [k, v] of Object.entries(out.forward)) console.log(k, { holdMin: r3(v.holdMin), holdMax: r3(v.holdMax), latencyPeak: v.latencyPeak, apiBacklogPeak: v.apiBacklogPeak, ms: v.ms });
