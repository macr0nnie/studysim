const $ = id => document.getElementById(id);
let running = false, studyMinutes = 25, connected = false, view = "timer";
const fmt = m => m >= 60 ? `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, "0")}m` : `${m}m`;
const PLAY = '<svg viewBox="0 0 24 24"><path d="M8 5.5v13a1 1 0 0 0 1.5.9l10-6.5a1 1 0 0 0 0-1.7l-10-6.5A1 1 0 0 0 8 5.5z"/></svg>';
const PAUSE = '<svg viewBox="0 0 24 24"><rect x="6" y="5" width="4" height="14" rx="1"/><rect x="14" y="5" width="4" height="14" rx="1"/></svg>';
const RING = 2 * Math.PI * 88;
$("arc").style.strokeDasharray = RING;

chrome.storage.local.get(["theme", "view"]).then(r => { applyTheme(r.theme); show(r.view || "timer"); });
let lastTheme = "";

// Sites works offline (it's the extension's own settings); Timer and Stats need the game.
function show(v) {
  view = v;
  for (const tab of document.querySelectorAll(".seg button")) tab.setAttribute("aria-selected", tab.dataset.view === v);
  $("view-timer").hidden = v !== "timer" || !connected;
  $("view-stats").hidden = v !== "stats" || !connected;
  $("view-sites").hidden = v !== "sites";
  $("offline").hidden = connected || v === "sites";
}
for (const tab of document.querySelectorAll(".seg button"))
  tab.addEventListener("click", () => { show(tab.dataset.view); chrome.storage.local.set({ view: tab.dataset.view }); });

function showStats(st) {
  $("level").textContent = `Level ${st.level}`;
  $("xpText").textContent = `${st.xp} / ${st.xpNext} XP`;
  $("xpBar").style.width = `${Math.min(100, 100 * st.xp / Math.max(1, st.xpNext))}%`;
  $("today").textContent = $("todayMin").textContent = fmt(st.todayMinutes);
  $("sessions").textContent = $("todaySessions").textContent = st.todaySessions;
  $("distractions").textContent = st.distractionsToday;
  $("subject").textContent = st.topSubject ? `Most studied this week: ${st.topSubject}` : "";
  $("streak").textContent = $("todayStreak").textContent = st.streak;
  const week = st.week || [], max = Math.max(30, ...week), names = ["S", "M", "T", "W", "T", "F", "S"], now = new Date();
  $("weekTotal").textContent = fmt(week.reduce((a, b) => a + b, 0));
  $("week").setAttribute("aria-label", "Minutes studied in the last 7 days: " + week.join(", "));
  $("week").replaceChildren(...week.map((m, i) => {
    const col = document.createElement("div"), bar = document.createElement("div"), day = document.createElement("div");
    col.className = "col" + (i === 6 ? " today" : "");
    bar.className = "b" + (m > 0 ? " on" : "");
    bar.style.height = `${(m / max) * 100}%`;
    bar.title = fmt(m);
    day.className = "d";
    day.textContent = names[(now.getDay() - 6 + i + 7) % 7];
    col.append(bar, day);
    return col;
  }));
}

async function refresh() {
  const status = await gameStatus();
  showBadge(status);
  connected = !!status;
  $("pill").textContent = status ? "Connected" : "Offline";
  $("pill").className = "pill " + (status ? "on" : "off");
  const hasStats = status && status.level !== undefined; // older game builds don't send stats
  document.querySelector('[data-view="stats"]').hidden = status && !hasStats;
  show(view === "stats" && status && !hasStats ? "timer" : view);
  if (!status) return;
  const theme = JSON.stringify(status.theme || "");
  if (theme !== lastTheme) { lastTheme = theme; applyTheme(status.theme); if (status.theme) chrome.storage.local.set({ theme: status.theme }); }
  if (hasStats) showStats(status);
  $("todayRow").hidden = !hasStats;
  running = status.running;
  if (status.studying && status.phaseSeconds) studyMinutes = Math.round(status.phaseSeconds / 60);
  const s = Math.max(0, status.secondsLeft), done = status.phaseSeconds ? Math.min(1, 1 - s / status.phaseSeconds) : 0;
  $("arc").style.strokeDashoffset = RING * (1 - done);
  $("clock").textContent = `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
  $("phase").textContent = !running ? "Paused" : status.studying ? "Study" : "Break";
  $("phase").className = "phase" + (!running ? " paused" : status.studying ? "" : " break");
  $("mode").textContent = status.mode + (status.strict ? " · strict" : "");
  $("toggle").innerHTML = running ? PAUSE : PLAY;
  $("toggle").setAttribute("aria-label", running ? "Pause" : "Start");
  // the game only accepts a new length while the timer is stopped
  $("minus").disabled = running || studyMinutes <= 5;
  $("plus").disabled = running || studyMinutes >= 120;
  showTags(status);
  $("distracted").hidden = !(status.distracted && running && status.studying);
}

// The tag (the game's study subject) is what each finished session is logged under.
let lastTags = "";
function showTags(status) {
  const supported = status.tag !== undefined; // older game builds
  $("tagline").hidden = !supported || !$("tagForm").hidden;
  if (!supported) { $("tagForm").hidden = true; return; }
  $("tagChip").textContent = status.tag ? `${status.tag} ▾` : "Add a tag ▾";
  $("tagChip").classList.toggle("empty", !status.tag);
  const key = JSON.stringify(status.tags);
  if (key === lastTags) return;
  lastTags = key;
  $("tagOptions").replaceChildren(...status.tags.map(t => Object.assign(document.createElement("option"), { value: t })));
}
$("tagChip").addEventListener("click", () => {
  $("tagline").hidden = true;
  $("tagForm").hidden = false;
  $("tag").value = $("tagChip").classList.contains("empty") ? "" : $("tagChip").textContent.replace(/ ▾$/, "");
  $("tag").focus();
  $("tag").select();
});
function closeTagForm() { $("tagForm").hidden = true; $("tagline").hidden = false; }
$("tag").addEventListener("keydown", e => { if (e.key === "Escape") { e.preventDefault(); closeTagForm(); } });
$("tagForm").addEventListener("submit", e => {
  e.preventDefault();
  const tag = $("tag").value.trim();
  closeTagForm();
  if (tag) act("tag", { tag });
});

async function act(action, extra) { try { await send(action, extra); } catch {} refresh(); }
$("toggle").addEventListener("click", () => act(running ? "pause" : "start"));
$("reset").addEventListener("click", () => act("reset"));
$("minus").addEventListener("click", () => act("set", { minutes: Math.max(5, studyMinutes - 5) }));
$("plus").addEventListener("click", () => act("set", { minutes: Math.min(120, studyMinutes + 5) }));
$("allStats").addEventListener("click", () => chrome.tabs.create({ url: chrome.runtime.getURL("stats.html") }));

const lines = text => text.split("\n").map(l => l.trim().toLowerCase().replace(/^https?:\/\//, "").replace(/\/.*$/, "")).filter(Boolean);

$("save").addEventListener("click", async () => {
  await chrome.storage.sync.set({ block: $("block").value, distracting: lines($("distracting").value), study: lines($("study").value) });
  $("saved").textContent = "Saved.";
  setTimeout(() => $("saved").textContent = "", 2000);
});

loadSettings().then(s => {
  $("block").value = s.block;
  $("distracting").value = s.distracting.join("\n");
  $("study").value = s.study.join("\n");
});
refresh();
setInterval(refresh, 1000);
