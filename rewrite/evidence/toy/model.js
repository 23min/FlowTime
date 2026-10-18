// Toy per-bin model for the building-blocks page: averages, backlog by age, and a per-bin Erlang C waiting estimate.
const BIN_S = 10, BINS = 360, MAX_AGE = 90, RETRY_SHARE = 0.5;
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
function step(q, A) {
  const { servers, svc, timeout } = q.p, a = q.ages;
  let timedOut = 0;
  if (timeout) for (let k = timeout + 1; k < MAX_AGE; k++) { timedOut += a[k]; a[k] = 0; }
  const cap = servers * BIN_S / svc;
  let left = cap, served = 0, waitSum = 0;
  for (let k = MAX_AGE - 1; k >= 1 && left > 0; k--) {
    const s = Math.min(a[k], left); a[k] -= s; left -= s; served += s; waitSum += s * (k - 0.5) * BIN_S;
  }
  const sNew = Math.min(A, left); served += sNew;
  a[MAX_AGE - 1] += a[MAX_AGE - 2];
  for (let k = MAX_AGE - 2; k >= 2; k--) a[k] = a[k - 1];
  a[1] = A - sNew;
  let backlog = 0; for (let k = 1; k < MAX_AGE; k++) backlog += a[k];
  let older = 0; for (let k = 4; k < MAX_AGE; k++) older += a[k];
  const lam = A / BIN_S, off = lam * svc;
  const randomWait = off < servers ? Math.min(60, erlangC(servers, off) / (servers / svc - lam)) : 0;
  const fluidWait = served > 0 ? waitSum / served : 0;
  return { inRate: lam, servedRate: served / BIN_S, busy: Math.min(1, served / cap), backlog,
    ages: [a[1], a[2], a[3], older], timedOutRate: timedOut / BIN_S, wait: fluidWait + randomWait, fluidWait, randomWait };
}
function run(pricingServers) {
  const P = JSON.parse(JSON.stringify(BASE)); P.pricing.servers = pricingServers;
  const q = {}; for (const k in P) q[k] = { p: P[k], ages: new Float64Array(MAX_AGE) };
  const out = { api: [], cart: [], db: [], pricing: [], email: [], checkout: [], retries: [], failed: [], latency: [] };
  let retryNext = 0;
  for (let t = 0; t < BINS; t++) {
    const lam = demand(t) * BIN_S;
    const api = step(q.api, lam);
    const cart = step(q.cart, api.servedRate * BIN_S);
    const db = step(q.db, cart.servedRate * BIN_S);
    const retries = retryNext;
    const pricing = step(q.pricing, 3 * api.servedRate * BIN_S + retries);
    const email = step(q.email, api.servedRate * BIN_S);
    const to = pricing.timedOutRate * BIN_S;
    retryNext = RETRY_SHARE * to;
    out.api.push(api); out.cart.push(cart); out.db.push(db); out.pricing.push(pricing); out.email.push(email);
    out.checkout.push(lam / BIN_S); out.retries.push(retries / BIN_S); out.failed.push((1 - RETRY_SHARE) * to / 3 / BIN_S);
    out.latency.push(api.wait + P.api.svc + Math.max(cart.wait + P.cart.svc + db.wait + P.db.svc, pricing.wait + P.pricing.svc));
  }
  return out;
}
if (typeof module !== "undefined") module.exports = { run, BIN_S, BINS };
