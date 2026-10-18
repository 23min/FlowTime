// Can the held-worker loop be computed purely forward (next state from previous state only)?
// Same toy (blocks/model.js step() and demand(), constants swapped per step size), api holding a worker per call.
//   explicit: hold for this step = api's 20 ms + downstream time computed from the previous step's state
//   solved:   hold for this step found by bisection inside the step (the earlier held2.js approach)
// Timeout fixed at 30 s in every run, so only the step size and the scheme vary.
const fs = require("fs");
const src = fs.readFileSync(__dirname + "/../model.js", "utf8");
function load(dt) {
  const tb = Math.round(30 / dt);
  const s = src.replace("BIN_S = 10, BINS = 360, MAX_AGE = 90", `BIN_S = ${dt}, BINS = ${Math.round(3600 / dt)}, MAX_AGE = ${tb + 12}`)
               .replace("timeout: 3", `timeout: ${tb}`);
  const m = { exports: {} };
  new Function("module", s + "\nmodule.exports = { step, demand, BASE, BIN_S, BINS, MAX_AGE, RETRY_SHARE };")(m);
  return m.exports;
}
const total = (a) => a.reduce((x, y) => x + y, 0);
function run(dt, scheme) {
  const { step, demand, BASE, BIN_S, BINS, MAX_AGE, RETRY_SHARE } = load(dt);
  const P = JSON.parse(JSON.stringify(BASE));
  const q = {}; for (const k in P) q[k] = { p: { ...P[k] }, ages: new Float64Array(MAX_AGE) };
  const clone = () => { const c = {}; for (const k in q) c[k] = { p: { ...q[k].p }, ages: Float64Array.from(q[k].ages) }; return c; };
  const vwait = (p, before, r) => ((before + r.backlog) / 2) / (p.servers / p.svc) + r.randomWait;
  const capT = 30 + BIN_S;
  let retryNext = 0, down = P.pricing.svc;
  const o = { t: [], latency: [], hold: [], apiBacklog: [] };
  const evalAt = (hold, lam, s) => {
    const b = {}; for (const k in s) b[k] = total(s[k].ages);
    s.api.p.svc = hold;
    const api = step(s.api, lam);
    const cart = step(s.cart, api.servedRate * BIN_S);
    const db = step(s.db, cart.servedRate * BIN_S);
    const pricing = step(s.pricing, 3 * api.servedRate * BIN_S + retryNext);
    step(s.email, api.servedRate * BIN_S);
    const d = Math.min(capT, Math.max(vwait(P.cart, b.cart, cart) + P.cart.svc + vwait(P.db, b.db, db) + P.db.svc, vwait(P.pricing, b.pricing, pricing) + P.pricing.svc));
    return { s, api, pricing, down: d, apiWait: vwait(s.api.p, b.api, api) };
  };
  const t0 = process.hrtime.bigint();
  for (let t = 0; t < BINS; t++) {
    const lam = demand(t) * BIN_S;
    let hold, r;
    if (scheme === "explicit") { hold = P.api.svc + down; r = evalAt(hold, lam, q); }
    else {
      let lo = P.api.svc + P.pricing.svc, hi = P.api.svc + capT;
      for (let i = 0; i < 60 && hi - lo > 1e-7; i++) { const mid = (lo + hi) / 2; if (P.api.svc + evalAt(mid, lam, clone()).down > mid) lo = mid; else hi = mid; }
      hold = hi; r = evalAt(hold, lam, clone()); for (const k in q) q[k].ages = r.s[k].ages;
    }
    down = r.down;
    retryNext = RETRY_SHARE * r.pricing.timedOutRate * BIN_S;
    o.t.push(t * BIN_S); o.latency.push(r.apiWait + hold); o.hold.push(hold); o.apiBacklog.push(r.api.backlog);
  }
  o.ms = Number(process.hrtime.bigint() - t0) / 1e6;
  return o;
}
module.exports = { run };
if (require.main === module) {
const f = (x, d = 2) => +x.toFixed(d);
for (const [dt, scheme] of [[10, "solved"], [1, "solved"], [10, "explicit"], [1, "explicit"], [0.5, "explicit"], [0.25, "explicit"], [0.1, "explicit"]]) {
  const o = run(dt, scheme);
  const win = o.t.map((t, i) => [t, i]).filter(([t]) => t >= 1200 && t < 1800).map(([, i]) => i);  // 10:20 to 10:30
  const holds = win.map((i) => o.hold[i]);
  const mean = total(holds) / holds.length;
  console.log(`${scheme.padEnd(8)} step ${String(dt).padEnd(4)} s  hold 10:20-10:30 mean ${f(mean, 3)} s, min ${f(Math.min(...holds), 3)}, max ${f(Math.max(...holds), 3)}` +
    `  | latency peak ${f(Math.max(...o.latency), 1)} s  api backlog peak ${Math.round(Math.max(...o.apiBacklog))}  | ${f(o.ms, 0)} ms`);
}
}
