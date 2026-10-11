const $ = id => document.getElementById(id);
let running = false;

async function refresh() {
  const status = await gameStatus();
  showBadge(status);
  $("pill").textContent = status ? "Connected" : "Offline";
  $("pill").className = "pill " + (status ? "on" : "off");
  $("timer").hidden = !status;
  $("offline").hidden = !!status;
  if (!status) return;
  running = status.running;
  const s = Math.max(0, status.secondsLeft);
  $("clock").textContent = `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
  $("phase").textContent = !running ? "Paused" : status.studying ? "Study" : "Break";
  $("phase").className = "phase" + (!running ? " paused" : status.studying ? "" : " break");
  $("mode").textContent = status.mode + (status.strict ? " · strict focus" : "");
  $("toggle").textContent = running ? "Pause" : "Start";
  $("setForm").hidden = running; // the game only accepts a new length while the timer is stopped
  $("distracted").hidden = !(status.distracted && running && status.studying);
}

async function act(action, extra) { try { await send(action, extra); } catch {} refresh(); }
$("toggle").addEventListener("click", () => act(running ? "pause" : "start"));
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
