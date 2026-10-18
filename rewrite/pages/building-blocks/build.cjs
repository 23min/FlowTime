// Assemble the Building Blocks page: model, Figure 5 curves and the Census Engine link into the page source.
// usage: node build.cjs [census-engine url]   writes ../out/flowtime-building-blocks.html
const fs = require("fs");
const d = __dirname, [engineUrl = "https://claude.ai/artifact/4VE6Fo381wjFxvtJwojnvm"] = process.argv.slice(2);
let s = fs.readFileSync(d + "/page.src.html", "utf8");
const fig5 = Object.fromEntries(fs.readFileSync(d + "/fig5.txt", "utf8").split("\n").filter(Boolean).map((l) => { const i = l.indexOf(" "); return [l.slice(0, i), l.slice(i + 1)]; }));
s = s.replace("{{MODEL}}", () => fs.readFileSync(d + "/../../evidence/toy/model2.js", "utf8"));
for (const k of ["PAST", "DOTS", "PROJ", "BAND", "WHATIF"]) s = s.replace(`{{${k}}}`, () => fig5[k]);
s = s.split("{{ENGINE_URL}}").join(engineUrl);
if (/\{\{[A-Z_]+\}\}/.test(s)) { console.error("placeholder left:", s.match(/\{\{[A-Z_]+\}\}/)[0]); process.exit(1); }
fs.mkdirSync(d + "/../out", { recursive: true });
fs.writeFileSync(d + "/../out/flowtime-building-blocks.html", s);
console.log("wrote", s.length, "bytes");
