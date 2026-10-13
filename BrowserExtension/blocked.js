const $ = id => document.getElementById(id);
const raw = new URLSearchParams(location.search).get("site") || "";
const site = /^[a-z0-9.-]+$/i.test(raw) ? raw : ""; // only ever a hostname we put there
if (site) $("site").textContent = site;
const mmss = s => `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;

// Says why the site is blocked and offers the way back once blocking ends.
async function tick() {
  const [status, s] = await Promise.all([gameStatus(), loadSettings()]);
  const blocking = blockingNow(status, s) && (!site || isDistracting(site, s));
  const session = status && status.running && status.studying;
  $("clock").hidden = !(blocking && session);
  if (!$("clock").hidden) $("clock").textContent = `${mmss(Math.max(0, status.secondsLeft))} left`;
  $("why").lastChild.textContent = !blocking ? " was blocked while you studied." :
    s.allowOnly ? " isn't on your allowed list for this session." :
    session ? " is blocked until your study session ends." : " is blocked during your scheduled focus hours.";
  $("title").textContent = blocking ? "Back to studying" : "Nice work";
  $("back").hidden = blocking || !site;
  if (site) { $("back").href = "https://" + site; $("back").textContent = `Go to ${site}`; }
  $("pass").hidden = !blocking || !site || s.strictness !== "remind";
  $("hint").hidden = s.strictness === "lock" && blocking;
}

// "Remind me" mode: the player can choose to go anyway, and the block stays away for 5 minutes.
$("pass").addEventListener("click", async () => {
  const { passes = {} } = await chrome.storage.session.get("passes");
  passes[site] = Date.now() + 5 * 60 * 1000;
  await chrome.storage.session.set({ passes });
  location.href = "https://" + site;
});

tick();
setInterval(tick, 5000);
