const $ = id => document.getElementById(id);
let running = false;
const fmt = m => m >= 60 ? `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, "0")}m` : `${m}m`;

chrome.storage.local.get("theme").then(r => applyTheme(r.theme));
let lastTheme = "";

function showStats(st) {
  $("level").textContent = `Level ${st.level}`;
  $("xpText").textContent = `${st.xp} / ${st.xpNext} XP`;
  $("xpBar").style.width = `${Math.min(100, 100 * st.xp / Math.max(1, st.xpNext))}%`;
  $("today").textContent = fmt(st.todayMinutes);
  $("sessions").textContent = st.todaySessions;
  $("distractions").textContent = st.distractionsToday;
  $("subject").textContent = st.topSubject ? `Most studied this week: ${st.topSubject}` : "";
  $("streak").textContent = st.streak;
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
  $("pill").textContent = status ? "Connected" : "Offline";
  $("pill").className = "pill " + (status ? "on" : "off");
  $("timer").hidden = !status;
  $("offline").hidden = !!status;
  $("stats").hidden = !status || status.level === undefined; // older game builds don't send stats
  if (!status) return;
  const theme = JSON.stringify(status.theme || "");
  if (theme !== lastTheme) { lastTheme = theme; applyTheme(status.theme); if (status.theme) chrome.storage.local.set({ theme: status.theme }); }
  if (status.level !== undefined) showStats(status);
  $("phaseBar").style.width = status.phaseSeconds ? `${Math.min(100, 100 * (1 - status.secondsLeft / status.phaseSeconds))}%` : "0";
  running = status.running;
  const s = Math.max(0, status.secondsLeft);
  $("clock").textContent = `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
  $("phase").textContent = !running ? "Paused" : status.studying ? "Study" : "Break";
  $("phase").className = "phase" + (!running ? " paused" : status.studying ? "" : " break");
  $("mode").textContent = status.mode + (status.strict ? " · strict focus" : "");
  $("toggle").textContent = running ? "Pause" : "Start";
  $("setForm").hidden = running; // the game only accepts a new length while the timer is stopped
  showTags(status);
  $("distracted").hidden = !(status.distracted && running && status.studying);
}

// The tag (the game's study subject) is what each finished session is logged under.
let lastTags = "";
function showTags(status) {
  if (status.tag === undefined) { $("tagForm").hidden = true; return; } // older game build
  if (document.activeElement !== $("tag")) $("tag").value = status.tag;
  const key = JSON.stringify([status.tag, status.tags]);
  if (key === lastTags) return;
  lastTags = key;
  $("tagOptions").replaceChildren(...status.tags.map(t => Object.assign(document.createElement("option"), { value: t })));
  $("chips").replaceChildren(...status.tags.filter(t => t !== status.tag).slice(0, 4).map(t => {
    const chip = Object.assign(document.createElement("button"), { textContent: t, type: "button" });
    chip.addEventListener("click", () => act("tag", { tag: t }));
    return chip;
  }));
}

async function act(action, extra) { try { await send(action, extra); } catch {} refresh(); }
$("toggle").addEventListener("click", () => act(running ? "pause" : "start"));
$("tagForm").addEventListener("submit", e => {
  e.preventDefault();
  const tag = $("tag").value.trim();
  if (tag) { $("tag").blur(); act("tag", { tag }); }
});
$("allStats").addEventListener("click", () => chrome.tabs.create({ url: chrome.runtime.getURL("stats.html") }));
$("reset").addEventListener("click", () => act("reset"));
$("setForm").addEventListener("submit", e => {
  e.preventDefault();
  const minutes = Number($("minutes").value);
  if (minutes >= 5 && minutes <= 120) act("set", { minutes });
});

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
