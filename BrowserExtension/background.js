// Watches the active tab. On a distracting site it tells the game (which breaks the room and drains
// coins during a study session) and, when blocking applies, sends the tab to the blocked page. On a
// study site it reports focus (the game gives +1 XP at most once a minute).
importScripts("shared.js");

const CHECK_MINUTES = 0.5; // Chrome's shortest alarm; the game waits 45 s before a distraction counts as over
let lastState = "";

chrome.runtime.onInstalled.addListener(() => chrome.alarms.create("check", { periodInMinutes: CHECK_MINUTES }));
chrome.runtime.onStartup.addListener(() => chrome.alarms.create("check", { periodInMinutes: CHECK_MINUTES }));
chrome.alarms.onAlarm.addListener(alarm => { if (alarm.name === "check") check(); });
chrome.tabs.onActivated.addListener(() => check());
chrome.tabs.onUpdated.addListener((tabId, change, tab) => { if (change.url && tab.active) check(); });
chrome.windows.onFocusChanged.addListener(() => check());

async function check() {
  const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  if (!tab || !tab.url) return;
  const host = hostOf(tab.url);
  const settings = await loadSettings();
  const status = await gameStatus();
  if (!status) { lastState = ""; return; } // game not running: nothing to report or enforce

  try {
    if (matches(host, settings.distracting)) {
      const studying = status.running && status.studying;
      const block = studying && (settings.block === "always" || (settings.block === "strict" && status.strict));
      await send("distracted", { site: host });
      lastState = "distracted";
      if (block) chrome.tabs.update(tab.id, { url: chrome.runtime.getURL("blocked.html") + "?site=" + encodeURIComponent(host) });
    } else {
      const study = matches(host, settings.study);
      if (study) await send("focus");                              // ends any distraction, +1 XP a minute
      else if (lastState === "distracted") await send("back");     // ends it, no XP
      lastState = study ? "focus" : "";
    }
  } catch {
    // The game closed between the status check and the report; the next check will notice.
  }
}
