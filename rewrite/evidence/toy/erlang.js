// Same capacity (100 items/s) and same load (90 items/s), split over different numbers of servers.
// Mean wait in queue from Erlang C (M/M/c); response time = wait + one service time.
function erlangC(c, a) { // a = offered load in servers (lambda/mu)
  let term = 1, sum = 1;
  for (let k = 1; k < c; k++) { term *= a / k; sum += term; }
  const last = term * a / c;            // a^c / c!
  const rho = a / c;
  const pc = last / (1 - rho);
  return pc / (sum + pc);
}
const capacity = 100, lambda = 90;
for (const c of [1, 10, 100]) {
  const mu = capacity / c, S = 1 / mu, a = lambda / mu;
  const pw = erlangC(c, a);
  const wq = pw / (c * mu - lambda);
  const lq = lambda * wq;
  console.log(`servers=${c} S=${(S*1000).toFixed(0)}ms P(wait)=${(pw*100).toFixed(1)}% Wq=${(wq*1000).toFixed(1)}ms R=${((wq+S)*1000).toFixed(1)}ms Lq=${lq.toFixed(2)}`);
}
