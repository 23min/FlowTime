// Spike model as M/M/1 at 1x, 10x, 100x volume, sampled every minute; plus steady-load M/M/1 queue vs load.
const fs = require("fs");
let seed = 4242; const rnd = () => (seed = (seed * 1664525 + 1013904223) >>> 0, (seed + 0.5) / 4294967296);
const BIN = 10, base = [100,100,100,300,300,300,100,100,100,100,100,100], T = 120;
function spike(scale, R) {
  const lam = b => base[b] * scale / BIN, mu = 200 * scale / BIN;
  const samples = Array.from({ length: T + 1 }, () => new Int32Array(R));
  for (let r = 0; r < R; r++) {
    let n = 0, t = 0, next = 1;            // samples[0] = 0 (start empty)
    while (next <= T) {
      const b = Math.min(Math.floor(t / BIN), base.length - 1), l = lam(b);
      const rate = l + (n > 0 ? mu : 0), dt = -Math.log(rnd()) / rate;
      const edge = Math.min(next, (b + 1) * BIN);
      if (t + dt >= edge) { t = edge; if (t === next) { samples[next][r] = n; next++; } continue; }
      t += dt; if (rnd() < l / rate) n++; else n--;
    }
  }
  const pct = (a, p) => { const s = Float64Array.from(a).sort(); return s[Math.floor(p * (s.length - 1))]; };
  return samples.map((a, i) => ({ t: i, mean: a.reduce((s, v) => s + v, 0) / R, p05: pct(a, 0.05), p95: pct(a, 0.95) }));
}
function steady(rho, horizon) {            // time-averaged number in system, mu = 1
  let n = 0, t = 0, area = 0;
  const warm = horizon * 0.05;
  while (t < horizon) {
    const rate = rho + (n > 0 ? 1 : 0), dt = -Math.log(rnd()) / rate;
    if (t > warm) area += n * dt;
    t += dt; if (rnd() < rho / rate) n++; else n--;
  }
  return area / (t - warm);
}
const out = { spike: {}, steady: [] };
for (const [scale, R] of [[1, 4000], [10, 2000], [100, 1000]]) {
  const s = spike(scale, R), peak = 300 * scale, sigma = Math.sqrt(scale * ((30 + 20) * 30 + (10 + 20) * 30));
  out.spike[scale] = { runs: R, peak, series: s, peakErr: (s[60].mean - peak) / peak, clear: s[90].mean, clearPred: sigma / Math.sqrt(2 * Math.PI) };
  console.log(`${scale}x runs=${R} peak mean=${s[60].mean.toFixed(1)} (err ${(100 * out.spike[scale].peakErr).toFixed(2)}%)  at 90 min: mean ${s[90].mean.toFixed(1)} = ${(100 * s[90].mean / peak).toFixed(2)}% of peak, predicted sigma/sqrt(2pi) = ${out.spike[scale].clearPred.toFixed(1)}  band at peak ${s[60].p05}-${s[60].p95}`);
}
for (const rho of [0.5, 0.7, 0.8, 0.9, 0.95]) { const L = steady(rho, 4e6); out.steady.push({ rho, L, theory: rho / (1 - rho) }); console.log(`rho=${rho}: simulated ${L.toFixed(2)}  theory ${(rho / (1 - rho)).toFixed(2)}`); }
fs.writeFileSync(process.argv[2], JSON.stringify(out));
