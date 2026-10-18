// Property: for one queue hit by a demand spike, every bin matches the fluid closed form.
// Generator: random grid, base demand < capacity < spike demand, random spike start/length.
const { execFileSync } = require("child_process"), fs = require("fs"), path = require("path");
const ENGINE = "/workspaces/flowtime-vnext/engine/target/release/flowtime-engine", dir = process.argv[2];
let seed = 20261017; const rnd = () => (seed = (seed * 1664525 + 1013904223) >>> 0) / 4294967296;
const pick = (lo, hi) => lo + Math.floor(rnd() * (hi - lo + 1));
let pass = 0, fail = [];
for (let c = 0; c < 300; c++) {
  const bins = pick(6, 40), mu = pick(50, 500), lo = pick(0, mu - 1), hi = pick(mu + 1, 3 * mu);
  const s = pick(0, bins - 2), k = pick(1, bins - s - 1);
  const demand = Array.from({ length: bins }, (_, t) => (t >= s && t < s + k ? hi : lo));
  // closed form: grows (hi-mu)/bin during the spike, drains (mu-lo)/bin after it, never below 0
  const peak = k * (hi - mu);
  const expected = Array.from({ length: bins }, (_, t) =>
    t < s ? 0 : t < s + k ? (t - s + 1) * (hi - mu) : Math.max(0, peak - (t - (s + k - 1)) * (mu - lo)));
  const yaml = `grid: { bins: ${bins}, binSize: 1, binUnit: minutes }
nodes:
  - { id: demand, kind: const, values: [${demand.join(", ")}] }
  - { id: cap, kind: const, values: [${Array(bins).fill(mu).join(", ")}] }
  - { id: served, kind: expr, expr: "MIN(cap, demand + SHIFT(q, 1))" }
topology:
  nodes:
    - id: P
      kind: serviceWithBuffer
      semantics: { arrivals: demand, served: served, capacity: cap, queueDepth: q }
  edges: []
  constraints: []
`;
  const m = path.join(dir, "m.yaml"), out = path.join(dir, "out"); fs.writeFileSync(m, yaml); fs.rmSync(out, { recursive: true, force: true });
  execFileSync(ENGINE, ["eval", m, "--output", out], { stdio: "ignore" });
  const got = fs.readFileSync(path.join(out, "series", "q@Q@DEFAULT.csv"), "utf8").trim().split("\n").slice(1).map(l => +l.split(",")[1]);
  const bad = expected.findIndex((e, t) => Math.abs(e - got[t]) > 1e-9);
  if (bad < 0) pass++; else fail.push({ bins, mu, lo, hi, s, k, bin: bad, expected: expected[bad], got: got[bad] });
}
console.log(`cases ${pass + fail.length}, pass ${pass}, fail ${fail.length}`); fail.slice(0, 3).forEach(f => console.log(JSON.stringify(f)));
