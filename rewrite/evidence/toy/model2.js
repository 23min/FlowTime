// Toy per-bin model for the building-blocks page: averages, backlog by age, and a per-bin Erlang C waiting estimate.
// Two optional mechanisms: api holds one of its workers for the whole synchronous call (solved per bin by bisection,
// since a longer hold lets less traffic through and so shortens pricing's wait), and pricing still serves calls
// whose caller has already given up.
const BIN_S = 10, BINS = 360, MAX_AGE = 90, RETRY_SHARE = 0.5, TIMEOUT_S = 30;
const BASE = {
  api:     { servers: 16, svc: 0.020 },
  cart:    { servers: 4,  svc: 0.010 },
  db:      { servers: 4,  svc: 0.002 },
  pricing: { servers: 24, svc: 0.150, timeout: 3 },
  email:   { servers: 3,  svc: 0.050 },
};
function demand(t) {
  const m = t * BIN_S / 60;
  const up = Math.min(1, Math.max(0, (m - 13) / 2)), down = Math.min(1, Math.max(0, (32 - m) / 2));
  return 40 + 30 * Math.min(up, down);
}
function erlangC(c, a) {
  let b = 1;
  for (let k = 1; k <= c; k++) b = a * b / (k + a * b);
  const rho = a / c;
  return b / (1 - rho * (1 - b));
}
// One bin at one component. A is this bin's arrivals. The wait is what a call arriving in this bin sees:
// the bin's average backlog over the service rate, plus the Erlang C term below capacity, capped at the timeout.
function step(q, A) {
  const { servers, svc, timeout, serveAbandoned } = q.p, a = q.ages;
  let before = 0; for (let k = 1; k < MAX_AGE; k++) before += a[k];
  let timedOut = 0, wasted = 0;
  if (timeout && serveAbandoned) timedOut = a[timeout + 1];
  else if (timeout) for (let k = timeout + 1; k < MAX_AGE; k++) { timedOut += a[k]; a[k] = 0; }
  const cap = servers * BIN_S / svc;
  let left = cap, served = 0;
  for (let k = MAX_AGE - 1; k >= 1 && left > 0; k--) {
    const s = Math.min(a[k], left); a[k] -= s; left -= s; served += s;
    if (timeout && k > timeout) wasted += s;
  }
  const sNew = Math.min(A, left); served += sNew;
  a[MAX_AGE - 1] += a[MAX_AGE - 2];
  for (let k = MAX_AGE - 2; k >= 2; k--) a[k] = a[k - 1];
  a[1] = A - sNew;
  let backlog = 0; for (let k = 1; k < MAX_AGE; k++) backlog += a[k];
  let older = 0; for (let k = 4; k < MAX_AGE; k++) older += a[k];
  const lam = A / BIN_S, off = lam * svc;
  const randomWait = off < servers ? Math.min(60, erlangC(servers, off) / (servers / svc - lam)) : 0;
  let wait = (before + backlog) / 2 / (servers / svc) + randomWait;
  if (timeout) wait = Math.min(wait, TIMEOUT_S);
  return { inRate: lam, servedRate: served / BIN_S, busy: Math.min(1, served / cap), backlog,
    ages: [a[1], a[2], a[3], older], timedOutRate: timedOut / BIN_S, wastedRate: wasted / BIN_S, wait };
}
function run({ pricingServers = 24, holdWorkers = false, serveAbandoned = false } = {}) {
  const P = JSON.parse(JSON.stringify(BASE));
  P.pricing.servers = pricingServers; P.pricing.serveAbandoned = serveAbandoned;
  let q = {}; for (const k in P) q[k] = { p: { ...P[k] }, ages: new Float64Array(MAX_AGE) };
  const copy = () => { const c = {}; for (const k in q) c[k] = { p: { ...q[k].p }, ages: Float64Array.from(q[k].ages) }; return c; };
  const out = { api: [], cart: [], db: [], pricing: [], email: [], checkout: [], retries: [], failed: [], latency: [], hold: [], apiInUse: [] };
  let retryNext = 0;
  for (let t = 0; t < BINS; t++) {
    const lam = demand(t) * BIN_S, retries = retryNext;
    const evalAt = (hold) => {
      const s = copy(); s.api.p.svc = hold;
      const api = step(s.api, lam);
      const cart = step(s.cart, api.servedRate * BIN_S);
      const db = step(s.db, cart.servedRate * BIN_S);
      const pricing = step(s.pricing, 3 * api.servedRate * BIN_S + retries);
      const email = step(s.email, api.servedRate * BIN_S);
      const down = Math.max(cart.wait + P.cart.svc + db.wait + P.db.svc, pricing.wait + P.pricing.svc);
      return { s, api, cart, db, pricing, email, down };
    };
    let hold, r;
    if (!holdWorkers) { r = evalAt(P.api.svc); hold = P.api.svc + r.down; }
    else {
      let lo = P.api.svc + P.pricing.svc, hi = P.api.svc + TIMEOUT_S + P.pricing.svc;
      for (let i = 0; i < 40 && hi - lo > 1e-6; i++) { const mid = (lo + hi) / 2; if (P.api.svc + evalAt(mid).down > mid) lo = mid; else hi = mid; }
      hold = hi; r = evalAt(hold);
    }
    q = r.s;
    const to = r.pricing.timedOutRate * BIN_S;
    retryNext = RETRY_SHARE * to;
    for (const k of ["api", "cart", "db", "pricing", "email"]) out[k].push(r[k]);
    out.checkout.push(lam / BIN_S); out.retries.push(retries / BIN_S); out.failed.push((1 - RETRY_SHARE) * to / 3 / BIN_S);
    out.latency.push(r.api.wait + P.api.svc + r.down);
    out.hold.push(hold); out.apiInUse.push(r.api.servedRate * (holdWorkers ? hold : P.api.svc));
  }
  return out;
}
if (typeof module !== "undefined") module.exports = { run, BASE, BIN_S, BINS };
