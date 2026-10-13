chrome.storage.local.get("theme").then(r => applyTheme(r.theme));
const site = new URLSearchParams(location.search).get("site");
if (site) document.getElementById("site").textContent = site;

// Show how long the session has left, so the block has a visible end.
async function tick() {
  const status = await gameStatus();
  const clock = document.getElementById("clock");
  clock.hidden = !(status && status.running && status.studying);
  // Once the session ends (or the game closes) the block no longer applies, so offer the way back.
  const back = document.getElementById("back");
  back.hidden = !clock.hidden || !/^[a-z0-9.-]+$/i.test(site || "");
  if (!back.hidden) { back.href = "https://" + site; back.textContent = `Session over. Go to ${site}`; }
  if (clock.hidden) return;
  const s = Math.max(0, status.secondsLeft);
  clock.textContent = `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")} left`;
}
tick();
setInterval(tick, 5000);
