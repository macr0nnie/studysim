const $ = id => document.getElementById(id);

async function refresh() {
  const status = await gameStatus();
  $("dot").style.background = status ? "var(--ok)" : "var(--bad)";
  if (!status) {
    $("status").textContent = "The game isn't running, or it isn't listening on localhost:8080.";
    $("timer").hidden = true;
    return;
  }
  $("status").textContent = status.distracted ? "Connected. The game thinks you're distracted!" : "Connected to the game.";
  $("timer").hidden = false;
  const s = Math.max(0, status.secondsLeft);
  $("clock").textContent = `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
  $("mode").textContent = `${status.studying ? "Study" : "Break"} · ${status.mode}${status.strict ? " · strict" : ""}${status.running ? "" : " · paused"}`;
}

for (const action of ["start", "pause", "reset"])
  $(action).addEventListener("click", async () => { try { await send(action); } catch {} refresh(); });

const lines = text => text.split("\n").map(l => l.trim().toLowerCase().replace(/^https?:\/\//, "").replace(/\/.*$/, "")).filter(Boolean);

$("save").addEventListener("click", async () => {
  await chrome.storage.sync.set({ block: $("block").value, distracting: lines($("distracting").value), study: lines($("study").value) });
  $("saved").textContent = "Saved.";
});

loadSettings().then(s => {
  $("block").value = s.block;
  $("distracting").value = s.distracting.join("\n");
  $("study").value = s.study.join("\n");
});
refresh();
setInterval(refresh, 1000);
