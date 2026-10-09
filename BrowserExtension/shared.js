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
  const res = await fetch(GAME, { method: "POST", body: JSON.stringify({ action, ...extra }) });
  return res.text();
}

// {running, studying, secondsLeft, mode, strict, distracted}, or null when the game isn't running.
async function gameStatus() {
  try {
    const res = await fetch(GAME, { cache: "no-store" });
    return await res.json();
  } catch {
    return null;
  }
}
