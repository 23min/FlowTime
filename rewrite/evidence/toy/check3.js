// The numbers quoted in the Building Blocks live view's prose, computed from the page's own model (model2.js).
const fs = require("fs");
const src = fs.readFileSync(__dirname + "/model2.js", "utf8");
const load = (s) => { const m = { exports: {} }; new Function("module", s)(m); return m.exports; };
const M = load(src), M64 = load(src.replace("api:     { servers: 16,", "api:     { servers: 64,"));
const clock = (b) => { const s = 36000 + b * 10; return `${Math.floor(s / 3600)}:${String(Math.floor(s / 60) % 60).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`; };
const span = (xs, th) => { const a = xs.findIndex((x) => x > th), b = xs.findLastIndex((x) => x > th); return a < 0 ? "none" : `${clock(a)} to ${clock(b + 1)}`; };
const base = M.run(), ab = M.run({ serveAbandoned: true }), w32 = M.run({ pricingServers: 32 });
console.log("failures, plain toy:", span(base.failed, 0.05), " abandoned:", span(ab.failed, 0.05), " failed at last bin:", ab.failed[359].toFixed(1), "of", ab.checkout[359]);
console.log("latency > 2 s, plain toy:", span(base.latency, 2), "  peak", Math.max(...base.latency).toFixed(1));
console.log("workers needed, no holding, pricing 32 (served x (20 ms + downstream)):", Math.max(...w32.api.map((r, i) => r.servedRate * w32.hold[i])).toFixed(1));
console.log("workers needed, no holding, pricing 24:", Math.max(...base.api.map((r, i) => r.servedRate * base.hold[i])).toFixed(0));
const h64 = M64.run({ holdWorkers: true, pricingServers: 32 }), h64b = M64.run({ holdWorkers: true });
console.log("holding, 64 workers, pricing 32: latency peak", Math.max(...h64.latency).toFixed(2), " api backlog peak", Math.max(...h64.api.map((r) => r.backlog)).toFixed(0));
console.log("holding, 64 workers, pricing 24: latency peak", Math.max(...h64b.latency).toFixed(1));
const h = M.run({ holdWorkers: true }), h32 = M.run({ holdWorkers: true, pricingServers: 32 });
console.log("holding 16, pricing 24: latency > 2 s", span(h.latency, 2), " api backlog > 1:", span(h.api.map((r) => r.backlog), 1), " served at 10:25", h.api[150].servedRate.toFixed(1), " pricing wait peak", Math.max(...h.pricing.map((r) => r.wait)).toFixed(2));
console.log("holding 16, pricing 32: latency > 2 s", span(h32.latency, 2), " peak", Math.max(...h32.latency).toFixed(1));
console.log("calm 10:05: latency no-hold", base.latency[30].toFixed(3), " hold", h.latency[30].toFixed(3), " api in use no-hold", base.apiInUse[30].toFixed(2), " hold", h.apiInUse[30].toFixed(2));
