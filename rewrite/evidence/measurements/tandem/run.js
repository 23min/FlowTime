// Two services in series (A then B), demand near and briefly above A's capacity.
// FlowTime side: the real Rust engine. Random side: tandem M/M/1 -> M/M/1, 2,000 runs.
const { execFileSync } = require("child_process"), fs = require("fs"), path = require("path");
const dir = process.argv[2], ENGINE = "/workspaces/flowtime-vnext/engine/target/release/flowtime-engine";
const demand = [150,150,150, 180,180,180, 190,190,190, 210,210,210, 190,190,190, 180,180,180, 150,150,150,150,150,150];
const capA = 200, capB = 220, BIN = 10, bins = demand.length, arr = v => `[${v.join(", ")}]`;
const yaml = `grid: { bins: ${bins}, binSize: 10, binUnit: minutes }
nodes:
  - { id: demand, kind: const, values: ${arr(demand)} }
  - { id: cap_a, kind: const, values: ${arr(Array(bins).fill(capA))} }
  - { id: cap_b, kind: const, values: ${arr(Array(bins).fill(capB))} }
  - { id: served_a, kind: expr, expr: "MIN(cap_a, demand + SHIFT(qa, 1))" }
  - { id: served_b, kind: expr, expr: "MIN(cap_b, served_a + SHIFT(qb, 1))" }
topology:
  nodes:
    - { id: A, kind: serviceWithBuffer, semantics: { arrivals: demand, served: served_a, capacity: cap_a, queueDepth: qa } }
    - { id: B, kind: serviceWithBuffer, semantics: { arrivals: served_a, served: served_b, capacity: cap_b, queueDepth: qb } }
  edges: []
  constraints: []
`;
fs.writeFileSync(path.join(dir, "tandem.yaml"), yaml);
fs.rmSync(path.join(dir, "out"), { recursive: true, force: true });
execFileSync(ENGINE, ["eval", path.join(dir, "tandem.yaml"), "--output", path.join(dir, "out")], { stdio: "ignore" });
const rd = n => fs.readFileSync(path.join(dir, "out", "series", fs.readdirSync(path.join(dir, "out", "series")).find(f => f.startsWith(n + "@"))), "utf8").trim().split("\n").slice(1).map(l => +l.split(",")[1]);
const ftA = rd("qa"), ftB = rd("qb"), ftLatA = rd("a_latency_min");
let seed = 99; const rnd = () => (seed = (seed * 1664525 + 1013904223) >>> 0, (seed + 0.5) / 4294967296);
const R = 2000, muA = capA / BIN, muB = capB / BIN, sumA = Array(bins).fill(0), sumB = Array(bins).fill(0);
for (let r = 0; r < R; r++) {
  let a = 0, b = 0, t = 0;
  for (let k = 0; k < bins; k++) {
    const lam = demand[k] / BIN, end = (k + 1) * BIN;
    for (;;) {
      const ra = lam, rA = a > 0 ? muA : 0, rB = b > 0 ? muB : 0, tot = ra + rA + rB, dt = -Math.log(rnd()) / tot;
      if (t + dt >= end) { t = end; break; }
      t += dt; const u = rnd() * tot;
      if (u < ra) a++; else if (u < ra + rA) { a--; b++; } else b--;
    }
    sumA[k] += a; sumB[k] += b;
  }
}
const rows = [2, 5, 8, 11, 14, 17, 20];
console.log("bin  end   demand/capA  FlowTime qA  sim qA   FlowTime qB  sim qB   FlowTime latency A (min)  sim wait A (min, Little)");
for (const k of rows) {
  const simA = sumA[k] / R, simB = sumB[k] / R, lam = demand[k] / BIN;
  console.log(`${String(k).padStart(3)} ${String((k + 1) * BIN).padStart(4)}m  ${(demand[k] / capA).toFixed(2).padStart(9)}  ${ftA[k].toFixed(1).padStart(10)}  ${simA.toFixed(1).padStart(7)}  ${ftB[k].toFixed(1).padStart(11)}  ${simB.toFixed(1).padStart(7)}  ${ftLatA[k].toFixed(2).padStart(22)}  ${(simA / Math.min(lam, muA)).toFixed(2).padStart(10)}`);
}
