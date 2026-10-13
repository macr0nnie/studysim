// Shared by the background worker and the pages. The game listens on localhost:8080.
const GAME = "http://localhost:8080/";

const DEFAULTS = {
  distracting: ["youtube.com", "tiktok.com", "instagram.com", "x.com", "twitter.com", "reddit.com", "facebook.com", "netflix.com", "twitch.tv"],
  study: ["docs.google.com", "notion.so", "wikipedia.org", "khanacademy.org", "quizlet.com", "scholar.google.com"],
  block: "strict", // "off" | "strict" (only strict-focus sessions) | "always" (every study session)
};

async function loadSettings() {
  const saved = await chrome.storage.sync.get(DEFAULTS);
  return { ...DEFAULTS, ...saved };
}

// "www.youtube.com" matches "youtube.com"; "notyoutube.com" doesn't.
function matches(host, list) {
  host = (host || "").toLowerCase();
  return list.some(d => host === d || host.endsWith("." + d));
}

function hostOf(url) {
  try { return new URL(url).hostname; } catch { return ""; }
}

async function send(action, extra = {}) {
  const res = await fetch(GAME, { method: "POST", body: JSON.stringify({ action, ...extra }), signal: AbortSignal.timeout(2000) });
  return res.text();
}

// {running, studying, secondsLeft, mode, strict, distracted}, or null when the game isn't running.
async function gameStatus() {
  try {
    // A short timeout so a stuck port reads as offline instead of "Connecting" forever.
    const res = await fetch(GAME, { cache: "no-store", signal: AbortSignal.timeout(2000) });
    const status = await res.json();
    return status && typeof status.secondsLeft === "number" ? status : null; // something else on port 8080 isn't the game
  } catch {
    return null;
  }
}

// Toolbar badge: minutes left while connected (red = studying, green = break, grey = paused), blank otherwise.
function showBadge(status) {
  chrome.action.setBadgeText({ text: status ? String(Math.ceil(Math.max(0, status.secondsLeft) / 60)) : "" });
  if (status) chrome.action.setBadgeBackgroundColor({ color: !status.running ? "#786657" : status.studying ? "#c24d47" : "#5c8f52" });
}

// The game sends its menu palette, so a colour picked in Paint > Menus carries over to the popup. It's cached so pages open in the right colours.
function applyTheme(t) {
  if (!t || !t.panel || t.light) return; // the default cream menus keep the extension's own night look
  const vars = { sky: t.panel, bg: t.panel, card: t.card, well: t.tab, track: t.tab, text: t.text, muted: t.muted, accent: t.accent, line: t.selected,
    button: t.tab, primary: t.button, "on-primary": t.text, ok: t.xp, bad: t.danger };
  for (const [k, v] of Object.entries(vars)) document.documentElement.style.setProperty("--" + k, v);
  document.documentElement.style.colorScheme = t.light ? "light" : "dark";
}
