// Watches the active tab. On a distracting site it tells the game (which breaks the room and drains
// coins during a study session) and, when blocking applies (a covered session or the schedule), sends
// the tab to the blocked page. On a study site it reports focus (+1 XP at most once a minute).
importScripts("shared.js");

const CHECK_MINUTES = 0.5; // Chrome's shortest alarm; the game waits 45 s before a distraction counts as over

chrome.runtime.onInstalled.addListener(() => chrome.alarms.create("check", { periodInMinutes: CHECK_MINUTES }));
chrome.runtime.onStartup.addListener(() => chrome.alarms.create("check", { periodInMinutes: CHECK_MINUTES }));
chrome.alarms.onAlarm.addListener(alarm => { if (alarm.name === "check") check(); });
chrome.tabs.onActivated.addListener(() => check());
chrome.tabs.onUpdated.addListener((tabId, change, tab) => { if (change.url && tab.active) check(); });
chrome.windows.onFocusChanged.addListener(() => check());

async function check() {
  const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  if (!tab || !tab.url || !/^https?:/.test(tab.url)) return;
  const host = hostOf(tab.url);
  const settings = await loadSettings();
  const status = await gameStatus();
  showBadge(status);
  const distracting = isDistracting(host, settings);

  // Block when a session or the schedule says so, unless a "remind me" page was clicked through for this site.
  if (distracting && blockingNow(status, settings)) {
    const { passes = {} } = await chrome.storage.session.get("passes");
    if (!(passes[host] > Date.now()))
      chrome.tabs.update(tab.id, { url: chrome.runtime.getURL("blocked.html") + "?site=" + encodeURIComponent(host) });
  }
  if (!status) return; // game not running: nothing to report

  // The worker sleeps between alarms and loses globals, so the last report lives in session storage.
  const { lastState = "" } = await chrome.storage.session.get("lastState");
  let next = lastState;
  try {
    // allow-only covers every other website, so it only counts against you mid-session
    if (distracting && (!settings.allowOnly || (status.running && status.studying))) {
      await send("distracted", { site: host });
      next = "distracted";
    } else {
      const study = matches(host, settings.study);
      if (study) await send("focus");                              // ends any distraction, +1 XP a minute
      else if (lastState === "distracted") await send("back");     // ends it, no XP
      next = study ? "focus" : "";
    }
    await chrome.storage.session.set({ lastState: next });
  } catch {
    // The game closed between the status check and the report; the next check will notice.
  }
}
