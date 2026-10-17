const $ = id => document.getElementById(id);
const fmt = m => m >= 60 ? `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, "0")}m` : `${m}m`;
const PLAY = '<svg viewBox="0 0 24 24"><path d="M8 5.5v13a1 1 0 0 0 1.5.9l10-6.5a1 1 0 0 0 0-1.7l-10-6.5A1 1 0 0 0 8 5.5z"/></svg>';
const PAUSE = '<svg viewBox="0 0 24 24"><rect x="6" y="5" width="4" height="14" rx="1"/><rect x="14" y="5" width="4" height="14" rx="1"/></svg>';
const LENGTHS = [15, 25, 45, 60, 90];
let status = null, settings = null, view = "focus", studyMinutes = 25;

// ---- Tabs. Blocking works without the game (it has its own schedule); Focus and Stats need it.
function show(v) {
  view = v;
  for (const tab of document.querySelectorAll(".tabbar button")) tab.setAttribute("aria-selected", tab.dataset.view === v);
  $("heading").textContent = v === "block" ? "Blocking" : v === "stats" ? "Stats" : "lofilife";
  $("view-focus").hidden = v !== "focus" || !status;
  $("view-stats").hidden = v !== "stats" || !status;
  $("view-block").hidden = v !== "block";
  $("offline").hidden = !!status || v === "block";
  if (v === "stats" && status) loadHistory();
}
for (const tab of document.querySelectorAll(".tabbar button"))
  tab.addEventListener("click", () => { show(tab.dataset.view); chrome.storage.local.set({ view: tab.dataset.view }); });

// ---- Focus
async function refresh() {
  status = await gameStatus();
  showBadge(status);
  $("pill").textContent = status ? "Connected" : "Offline";
  $("pill").className = "pill " + (status ? "on" : "off");
  show(view);
  if (settings) {
    showLock();
    const lists = Object.keys(settings.lists).filter(k => settings.lists[k] && (k !== "custom" || settings.custom.length)).length;
    $("blockChip").textContent = settings.allowOnly ? "Allow-only on" : settings.block === "off" ? "Blocking off" : `${lists} block list${lists === 1 ? "" : "s"} on`;
  }
  if (!status) return;
  const running = status.running, s = Math.max(0, status.secondsLeft);
  if (status.studying && status.phaseSeconds) studyMinutes = Math.round(status.phaseSeconds / 60);
  $("clock").textContent = `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
  $("phaseBar").style.width = status.phaseSeconds ? `${Math.min(100, 100 * (1 - s / status.phaseSeconds))}%` : "0";
  const phase = !running ? "Paused" : status.studying ? "Study" : "Break";
  const hasTag = status.tag !== undefined; // older game builds don't send tags
  $("tagBtn").textContent = phase + (hasTag && status.tag ? ` · ${status.tag}` : "") + (hasTag ? " ▾" : "");
  $("tagBtn").disabled = !hasTag;
  $("mode").textContent = status.mode + (status.strict ? " · strict focus" : "");
  $("toggle").innerHTML = running ? PAUSE : PLAY;
  $("toggle").setAttribute("aria-label", running ? "Pause" : "Start");
  $("lenBtn").textContent = `${studyMinutes} min`;
  $("lenBtn").disabled = running; // the game only takes a new length while the timer is stopped
  if (running) $("lengths").hidden = true;
  $("distracted").hidden = !(status.distracted && running && status.studying);
  $("strictChip").hidden = !status.strict;
  if (hasTag && JSON.stringify(status.tags) !== $("tagOptions").dataset.key) {
    $("tagOptions").dataset.key = JSON.stringify(status.tags);
    $("tagOptions").replaceChildren(...status.tags.map(t => Object.assign(document.createElement("option"), { value: t })));
  }
  if (status.level !== undefined) showStats(status);
}

async function act(action, extra) { try { await send(action, extra); } catch {} refresh(); }
$("toggle").addEventListener("click", () => act(status && status.running ? "pause" : "start"));
$("reset").addEventListener("click", () => act("reset"));
$("lenBtn").addEventListener("click", () => {
  $("lengths").hidden = !$("lengths").hidden;
  $("lengths").replaceChildren(...LENGTHS.map(m => {
    const b = Object.assign(document.createElement("button"), { textContent: `${m}` });
    b.setAttribute("aria-pressed", m === studyMinutes);
    b.addEventListener("click", () => { $("lengths").hidden = true; act("set", { minutes: m }); });
    return b;
  }));
});
$("tagBtn").addEventListener("click", () => {
  $("tagBtn").hidden = true;
  $("tagForm").hidden = false;
  $("tag").value = status && status.tag || "";
  $("tag").focus();
  $("tag").select();
});
function closeTag() { $("tagForm").hidden = true; $("tagBtn").hidden = false; }
$("tag").addEventListener("keydown", e => { if (e.key === "Escape") { e.preventDefault(); closeTag(); } });
$("tagForm").addEventListener("submit", e => {
  e.preventDefault();
  const tag = $("tag").value.trim();
  closeTag();
  if (tag) act("tag", { tag });
});

// ---- Stats
function showStats(st) {
  $("level").textContent = `Level ${st.level}`;
  $("xpText").textContent = `${st.xp} / ${st.xpNext} XP`;
  $("xpBar").style.width = `${Math.min(100, 100 * st.xp / Math.max(1, st.xpNext))}%`;
  $("streak").textContent = st.streak;
  $("today").textContent = fmt(st.todayMinutes);
  $("top").textContent = st.topSubject || "–";
}

// The week as a skyline: a building a day, as tall as the time studied, with a lit window per finished session.
let historyAt = 0;
async function loadHistory() {
  if (Date.now() - historyAt < 10000) return;
  historyAt = Date.now();
  let entries = [];
  try { entries = (await (await fetch(GAME + "stats", { cache: "no-store", signal: AbortSignal.timeout(2000) })).json()).entries || []; } catch {}
  const today = new Date(), days = [];
  for (let i = 6; i >= 0; i--) { const d = new Date(today); d.setDate(d.getDate() - i); days.push({ key: dayKey(d), d, minutes: 0, sessions: 0 }); }
  for (const e of entries) { const day = days.find(x => x.key === e.day); if (day) { day.minutes += e.minutes; day.sessions += e.sessions; } }
  const max = Math.max(60, ...days.map(d => d.minutes)), total = days.reduce((a, d) => a + d.sessions, 0);
  $("skyNote").textContent = total ? `${total} sessions · ${fmt(days.reduce((a, d) => a + d.minutes, 0))} · each lit window is a session` : "Finish a session to light your first window";
  $("skyline").setAttribute("aria-label", "Sessions in the last 7 days: " + days.map(d => d.sessions).join(", "));
  $("skyline").replaceChildren(...days.map((day, i) => {
    const col = document.createElement("div"), tower = document.createElement("div"), label = document.createElement("div");
    col.className = "day" + (i === 6 ? " today" : "");
    tower.className = "tower";
    const px = 130 * (0.3 + 0.7 * day.minutes / max), windows = 2 * Math.max(1, Math.floor((px - 13) / 13));
    tower.style.height = `${px}px`;
    tower.title = `${fmt(day.minutes)}, ${day.sessions} sessions`;
    // windows fill top to bottom, so the lit ones go last and sit at the bottom
    for (let w = windows - 1; w >= 0; w--) tower.append(Object.assign(document.createElement("i"), { className: "w" + (w < day.sessions ? " lit" : "") }));
    label.className = "d";
    label.textContent = "SMTWTFS"[day.d.getDay()];
    col.append(tower, label);
    return col;
  }));
}
$("allStats").addEventListener("click", () => chrome.tabs.create({ url: chrome.runtime.getURL("stats.html") }));

// ---- Blocking. Every change saves straight away.
const save = patch => { Object.assign(settings, patch); chrome.storage.sync.set(patch); drawBlocking(); };
const cleanSite = text => text.trim().toLowerCase().replace(/^https?:\/\//, "").replace(/^www\./, "").replace(/\/.*$/, "");
const STRICT_HELP = {
  remind: "A gentle page you can click through, and the site stays open for 5 minutes.",
  block: "The site is blocked until blocking ends.",
  lock: "The site is blocked, and these settings can't be changed until the session ends.",
};
let openList = "";

function siteChips(list, key) {
  const box = document.createElement("div");
  box.className = "sites";
  box.append(...list.map(site => {
    const chip = Object.assign(document.createElement("span"), { className: "site" + (key ? "" : " fixed"), textContent: site });
    if (key) {
      const x = Object.assign(document.createElement("button"), { type: "button", textContent: "×", title: `Remove ${site}` });
      x.addEventListener("click", () => save({ [key]: settings[key].filter(s => s !== site) }));
      chip.append(x);
    }
    return chip;
  }));
  return box;
}

function drawBlocking() {
  const rows = [...Object.entries(PRESETS), ["custom", { name: "My list", emoji: "✏️", sites: settings.custom }]];
  $("lists").replaceChildren(...rows.map(([key, p]) => {
    const row = document.createElement("div");
    row.className = "list";
    row.innerHTML = `<div class="list-head"><button type="button"><span class="emo"></span><span class="list-text"><b></b><span></span></span></button><input type="checkbox" class="switch"></div>`;
    row.querySelector(".emo").textContent = p.emoji;
    row.querySelector("b").textContent = p.name;
    row.querySelector(".list-text span").textContent = p.sites.length ? p.sites.join(", ") : "Add your own sites";
    const sw = row.querySelector(".switch");
    sw.checked = !!settings.lists[key];
    sw.setAttribute("aria-label", `Block ${p.name}`);
    sw.addEventListener("change", () => save({ lists: { ...settings.lists, [key]: sw.checked } }));
    row.querySelector(".list-head button").addEventListener("click", () => { openList = openList === key ? "" : key; drawBlocking(); });
    if (openList === key) {
      const body = Object.assign(document.createElement("div"), { className: "list-body" });
      body.append(siteChips(p.sites, key === "custom" ? "custom" : ""));
      if (key === "custom") body.append(addForm("custom", "Add a site, e.g. twitch.tv"));
      row.append(body);
    }
    return row;
  }));
  for (const seg of document.querySelectorAll("[data-setting]"))
    for (const b of seg.children) b.setAttribute("aria-pressed", settings[seg.dataset.setting] === b.dataset.value);
  $("strictHelp").textContent = STRICT_HELP[settings.strictness];
  $("allowOnly").checked = settings.allowOnly;
  $("studySites").replaceWith(Object.assign(siteChips(settings.study, "study"), { id: "studySites" }));
  $("schedOn").checked = settings.schedule.on;
  $("from").value = settings.schedule.from;
  $("to").value = settings.schedule.to;
  $("days").replaceChildren(...[1, 2, 3, 4, 5, 6, 0].map(d => {
    const b = Object.assign(document.createElement("button"), { type: "button", textContent: "SMTWTFS"[d] });
    b.setAttribute("aria-pressed", settings.schedule.days.includes(d));
    b.addEventListener("click", () => {
      const days = settings.schedule.days.includes(d) ? settings.schedule.days.filter(x => x !== d) : [...settings.schedule.days, d];
      save({ schedule: { ...settings.schedule, days } });
    });
    return b;
  }));
  showLock();
}

function addForm(key, placeholder) {
  const form = Object.assign(document.createElement("form"), { className: "add" });
  form.dataset.list = key;
  form.innerHTML = `<input aria-label="Add a site"><button class="btn">Add</button>`;
  form.querySelector("input").placeholder = placeholder;
  return form;
}
// One submit handler for every add form, including ones drawn later.
document.addEventListener("submit", e => {
  const key = e.target.dataset && e.target.dataset.list;
  if (!key) return;
  e.preventDefault();
  const site = cleanSite(e.target.querySelector("input").value);
  if (!/^[a-z0-9-]+(\.[a-z0-9-]+)+$/.test(site) || settings[key].includes(site)) return;
  save({ [key]: [...settings[key], site] });
  const again = document.querySelector(`form[data-list="${key}"] input`);
  if (again) again.focus();
});

// "Lock it": no changes while blocking is on during a session.
function showLock() {
  const locked = settings.strictness === "lock" && !!status && status.running && status.studying && blockingNow(status, settings);
  $("lockNote").hidden = !locked;
  $("blockForm").disabled = locked;
}

for (const tab of document.querySelectorAll(".seg[role=tablist] button"))
  tab.addEventListener("click", () => {
    for (const t of tab.parentElement.children) t.setAttribute("aria-selected", t === tab);
    for (const pane of ["lists", "allow", "schedule"]) $("pane-" + pane).hidden = pane !== tab.dataset.pane;
  });
for (const seg of document.querySelectorAll("[data-setting]"))
  for (const b of seg.children) b.addEventListener("click", () => save({ [seg.dataset.setting]: b.dataset.value }));
$("allowOnly").addEventListener("change", () => save({ allowOnly: $("allowOnly").checked }));
$("schedOn").addEventListener("change", () => save({ schedule: { ...settings.schedule, on: $("schedOn").checked } }));
for (const id of ["from", "to"]) $(id).addEventListener("change", () => { if ($(id).value) save({ schedule: { ...settings.schedule, [id]: $(id).value } }); });

loadSettings().then(s => { settings = s; drawBlocking(); });
chrome.storage.local.get("view").then(r => show(r.view || "focus"));
refresh();
setInterval(refresh, 1000);
