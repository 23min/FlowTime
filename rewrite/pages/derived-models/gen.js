// Rehearsal data for the Derived Models page: the building-blocks toy with held workers plays the real system,
// and the same toy without them plays a first-draft model fed the same demand.
const { run } = require(__dirname + "/../../evidence/toy/model2.js");
const truth = run({ holdWorkers: true }), draft = run();
const r2 = (x) => Math.round(x * 100) / 100;
const pick = (o) => ({ latency: o.latency.map(r2), apiQueue: o.api.map((r) => Math.round(r.backlog)), apiInUse: o.apiInUse.map(r2), timeouts: o.pricing.map((r) => r2(r.timedOutRate)) });
const out = { truth: pick(truth), draft: pick(draft) };
require("fs").writeFileSync(__dirname + "/data.json", JSON.stringify(out));
const mean = (xs) => xs.reduce((a, b) => a + b, 0) / xs.length;
const calm = [0, 78];
for (const k of ["latency", "apiInUse"]) console.log(k, "calm 10:00-10:13 truth", mean(out.truth[k].slice(...calm)).toFixed(3), "draft", mean(out.draft[k].slice(...calm)).toFixed(3));
console.log("peaks truth", Math.max(...out.truth.latency), Math.max(...out.truth.apiQueue), Math.max(...out.truth.apiInUse), Math.max(...out.truth.timeouts));
console.log("peaks draft", Math.max(...out.draft.latency), Math.max(...out.draft.apiQueue), Math.max(...out.draft.apiInUse), Math.max(...out.draft.timeouts));
