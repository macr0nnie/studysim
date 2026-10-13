const $ = id => document.getElementById(id);
const fmt = m => m >= 60 ? `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, "0")}m` : `${m}m`;
// Same colours as habits in the game's planner.
const PALETTE = ["#E86A6A", "#F2A33A", "#D9B52B", "#5BBF7A", "#39B5A8", "#4A90D9", "#8E7CD6", "#D870B0", "#8A9A5B", "#C98A5E"];
let entries = [], range = "days", only = null;

chrome.storage.local.get("theme").then(r => applyTheme(r.theme));

async function load() {
  const status = await gameStatus();
  let history = null;
  if (status) try { history = await (await fetch(GAME + "stats", { cache: "no-store" })).json(); } catch {}
  $("pill").textContent = history ? "Connected" : "Offline";
  $("pill").className = "pill " + (history ? "on" : "off");
  $("offline").hidden = !!history;
  $("content").hidden = !history;
  if (status && status.theme) applyTheme(status.theme);
  if (!history) return;
  entries = history.entries || [];
  draw();
}

function draw() {
  const order = tagOrder(entries), color = t => PALETTE[order.indexOf(t) % PALETTE.length];
  const list = buckets(entries, range);
  const value = b => only ? (b.byTag[only] || 0) : b.total;
  const total = list.reduce((a, b) => a + value(b), 0);

  // Summary tiles
  $("total").textContent = fmt(total);
  $("sessions").textContent = only ? "–" : list.reduce((a, b) => a + b.sessions, 0);
  $("average").textContent = fmt(Math.round(total / daysCovered(entries, range)));
  const best = list.reduce((a, b) => value(b) > value(a) ? b : a, list[0]);
  const unit = { days: "day", weeks: "week", months: "month", years: "year" }[range];
  $("best").textContent = best && value(best) ? fmt(value(best)) : "–";
  $("bestLabel").textContent = best && value(best) ? `best ${unit} (${best.label})` : `best ${unit}`;
  $("rangeLabel").textContent = RANGES[range].label;
  $("filterNote").textContent = only ? `Showing only ${only}` : "";

  // Stacked bar chart, one colour per tag
  const max = Math.max(30, ...list.map(value));
  const every = list.length > 14 ? 3 : 1; // label every third day so 30 labels don't crowd
  $("chart").setAttribute("aria-label", `${RANGES[range].label}: ` + list.map(b => `${b.label} ${fmt(value(b))}`).join(", "));
  $("chart").replaceChildren(...list.map((b, i) => {
    const col = document.createElement("div"), stack = document.createElement("div"), label = document.createElement("div");
    col.className = "col" + (i === list.length - 1 ? " now" : "");
    col.title = `${b.label}: ${fmt(value(b))}` + (only ? "" : Object.entries(b.byTag).map(([t, m]) => `\n${t}: ${fmt(m)}`).join(""));
    if (value(b) === 0) stack.className = "empty";
    else {
      stack.className = "stack";
      stack.style.height = `${(value(b) / max) * 100}%`;
      for (const t of order) {
        const m = only ? (t === only ? b.byTag[t] || 0 : 0) : b.byTag[t] || 0;
        if (!m) continue;
        const seg = document.createElement("div");
        seg.style.flex = String(m);
        seg.style.background = color(t);
        stack.append(seg);
      }
    }
    label.className = "d";
    label.textContent = (list.length - 1 - i) % every === 0 ? b.short : "";
    col.append(stack, label);
    return col;
  }));

  // Tag breakdown for the range
  const tagTotals = {};
  for (const b of list) for (const [t, m] of Object.entries(b.byTag)) tagTotals[t] = (tagTotals[t] || 0) + m;
  const tags = Object.keys(tagTotals).sort((a, b) => tagTotals[b] - tagTotals[a]);
  const all = tags.reduce((a, t) => a + tagTotals[t], 0);
  $("noTags").hidden = tags.length > 0;
  $("tagList").replaceChildren(...tags.map(t => {
    const row = document.createElement("button");
    row.className = "tag" + (only === t ? " active" : "");
    row.setAttribute("aria-pressed", only === t);
    row.innerHTML = `<span class="sw"></span><span class="name"></span><span class="t"></span><span class="pct"><div></div></span>`;
    row.querySelector(".sw").style.background = color(t);
    row.querySelector(".name").textContent = t;
    row.querySelector(".t").textContent = fmt(tagTotals[t]);
    row.querySelector(".pct div").style.cssText = `width:${(100 * tagTotals[t]) / all}%;background:${color(t)}`;
    row.addEventListener("click", () => { only = only === t ? null : t; draw(); });
    return row;
  }));
}

for (const tab of document.querySelectorAll(".tabs button"))
  tab.addEventListener("click", () => {
    range = tab.dataset.range;
    for (const t of document.querySelectorAll(".tabs button")) t.setAttribute("aria-selected", t === tab);
    draw();
  });

load();
setInterval(load, 30000); // the log only changes when a session ends
