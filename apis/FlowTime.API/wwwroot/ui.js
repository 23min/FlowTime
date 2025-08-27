// Minimal JS for FlowTime UI M0 Preview
const d = sel => document.querySelector(sel);
const statusEl = d('#status');
const errorsEl = d('#errors');
const modelEl = d('#model');
const runBtn = d('#runBtn');
const csvInput = d('#csvFile');
const csvPreview = d('#csvPreview');
const canvas = d('#chartCanvas');
const legend = d('#legend');
const ctx = canvas.getContext('2d');

// Resize canvas to container
function resize() {
  const rect = canvas.parentElement.getBoundingClientRect();
  canvas.width = rect.width * devicePixelRatio;
  canvas.height = rect.height * devicePixelRatio;
  canvas.style.width = rect.width + 'px';
  canvas.style.height = rect.height + 'px';
}
window.addEventListener('resize', () => { resize(); draw(); });

let seriesData = {}; // name -> array<number>
let gridInfo = { binMinutes: 0 };

function draw() {
  resize();
  const names = Object.keys(seriesData);
  ctx.clearRect(0,0,canvas.width, canvas.height);
  if (!names.length) return;
  const colors = ['#1f77b4','#ff7f0e','#2ca02c','#d62728','#9467bd','#8c564b'];
  const allValues = names.flatMap(n => seriesData[n]);
  const maxV = Math.max(...allValues);
  const minV = Math.min(...allValues);
  const pad = 20 * devicePixelRatio;
  const w = canvas.width - pad*2;
  const h = canvas.height - pad*2;
  ctx.lineWidth = 2 * devicePixelRatio;
  names.forEach((n,i) => {
    const data = seriesData[n];
    if (!legend.querySelector(`[data-name="${n}"]`).dataset.enabled) return;
    ctx.strokeStyle = colors[i % colors.length];
    ctx.beginPath();
    data.forEach((v,idx) => {
      const x = pad + (idx/(data.length-1))*w;
      const y = pad + (1 - (v-minV)/(maxV-minV || 1))*h;
      if (idx===0) ctx.moveTo(x,y); else ctx.lineTo(x,y);
    });
    ctx.stroke();
  });
}

function setStatus(msg) { statusEl.textContent = msg; }
function setError(msg) { errorsEl.textContent = msg || ''; }

// Populate default model
modelEl.value = `grid: { bins: 4, binMinutes: 60 }\nnodes:\n  - id: demand\n    kind: const\n    values: [10, 20, 30, 40]\n  - id: served\n    kind: expr\n    expr: "demand * 0.8"\n`;

runBtn.addEventListener('click', async () => {
  setError('');
  setStatus('Running...');
  try {
    const resp = await fetch('/run', { method:'POST', headers: { 'Content-Type':'application/yaml' }, body: modelEl.value });
    if (!resp.ok) {
      const err = await resp.json().catch(()=>({error: resp.statusText}));
      setError('Run failed: ' + (err.error || resp.status));
      setStatus('Error');
      return;
    }
    const data = await resp.json();
    seriesData = data.series;
    gridInfo = data.grid;
    renderLegend();
    draw();
    setStatus('OK');
  } catch (e) {
    setError(e.message);
    setStatus('Error');
  }
});

function renderLegend() {
  legend.innerHTML='';
  Object.keys(seriesData).forEach(name => {
    const span = document.createElement('span');
    span.textContent = name;
    span.dataset.name = name;
    span.dataset.enabled = 'true';
    span.addEventListener('click', () => {
      span.dataset.enabled = span.dataset.enabled === 'true' ? 'false' : 'true';
      span.style.textDecoration = span.dataset.enabled === 'true' ? 'none' : 'line-through';
      draw();
    });
    legend.appendChild(span);
  });
}

csvInput.addEventListener('change', async () => {
  setError('');
  const file = csvInput.files?.[0];
  if (!file) return;
  const text = await file.text();
  csvPreview.textContent = text.split('\n').slice(0,20).join('\n');
  try {
    const parsed = parseCsv(text);
    // Expect header row: time,value or value only
    const header = parsed[0];
    const valueIdx = header.length === 1 ? 0 : header.length - 1;
    const values = parsed.slice(1).map(r => parseFloat(r[valueIdx])).filter(v => !isNaN(v));
    seriesData = { uploaded: values };
    renderLegend();
    draw();
    setStatus('CSV loaded');
  } catch (e) { setError('CSV parse error: ' + e.message); }
});

function parseCsv(text) {
  // Simple CSV (no quotes) for M0
  return text.trim().split(/\r?\n/).map(line => line.split(','));
}

resize();
setStatus('Ready');
