// All eight combinations of the Building Blocks switches (held workers, abandoned calls served, 24 or 32 pricing servers): peaks and recovery times.
const { run, BINS, BIN_S } = require("./model2.js");
const pk = (xs) => Math.max(...xs), f = (x, d = 1) => +x.toFixed(d);
const clock = (b) => { const s = 36000 + b * BIN_S; return `${Math.floor(s / 3600)}:${String(Math.floor(s / 60) % 60).padStart(2, "0")}`; };
for (const holdWorkers of [false, true]) for (const serveAbandoned of [false, true]) for (const pricingServers of [24, 32]) {
  const t0 = process.hrtime.bigint(); const o = run({ pricingServers, holdWorkers, serveAbandoned }); const ms = Number(process.hrtime.bigint() - t0) / 1e6;
  const lastQ = Math.max(o.api.findLastIndex((r) => r.backlog > 1), o.pricing.findLastIndex((r) => r.backlog > 1));
  console.log(`hold ${holdWorkers ? "on " : "off"} abandoned ${serveAbandoned ? "on " : "off"} pricing ${pricingServers}:`, JSON.stringify({
    lat: f(pk(o.latency)), failed: f(pk(o.failed)), retries: f(pk(o.retries)), wasted: f(pk(o.pricing.map((r) => r.wastedRate))),
    apiQ: Math.round(pk(o.api.map((r) => r.backlog))), prQ: Math.round(pk(o.pricing.map((r) => r.backlog))), emailQ: Math.round(pk(o.email.map((r) => r.backlog))),
    apiInUse: f(pk(o.apiInUse)), queuesUntil: lastQ < 0 ? "-" : clock(lastQ + 1), failedAt1045: f(o.failed[270]), ms: f(ms, 0) }));
}
