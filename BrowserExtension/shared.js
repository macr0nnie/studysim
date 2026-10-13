// Shared by the background worker and the pages. The game listens on localhost:8080.
const GAME = "http://localhost:8080/";

// Ready-made block lists the player switches on in the popup. "custom" is their own list.
const PRESETS = {
  social: { name: "Social media", emoji: "📱", sites: ["instagram.com", "tiktok.com", "x.com", "twitter.com", "facebook.com", "reddit.com", "snapchat.com", "pinterest.com", "threads.net", "bsky.app"] },
  video: { name: "Video and streaming", emoji: "▶️", sites: ["youtube.com", "netflix.com", "twitch.tv", "disneyplus.com", "hulu.com", "primevideo.com", "max.com"] },
  news: { name: "News", emoji: "📰", sites: ["news.ycombinator.com", "bbc.com", "bbc.co.uk", "cnn.com", "nytimes.com", "theguardian.com", "foxnews.com"] },
  shopping: { name: "Shopping", emoji: "🛍️", sites: ["amazon.com", "amazon.co.uk", "ebay.com", "etsy.com", "aliexpress.com", "temu.com", "shein.com"] },
  games: { name: "Games", emoji: "🎮", sites: ["store.steampowered.com", "roblox.com", "chess.com", "poki.com", "crazygames.com", "miniclip.com"] },
};

const DEFAULTS = {
  lists: { social: true, video: true, news: false, shopping: false, games: false, custom: true },
  custom: [],
  study: ["docs.google.com", "notion.so", "wikipedia.org", "khanacademy.org", "quizlet.com", "scholar.google.com"], // earn XP; the only sites open in allow-only mode
  allowOnly: false,
  block: "strict", // when sessions block: "off" | "strict" (only strict-focus sessions) | "always" (every study session)
  strictness: "block", // "remind" (a nudge page you can click through) | "block" | "lock" (also no settings changes mid-session)
  schedule: { on: false, days: [1, 2, 3, 4, 5], from: "09:00", to: "17:00" }, // blocks even with the game closed; days 0 = Sunday
};

async function loadSettings() {
  const saved = await chrome.storage.sync.get(null);
  const s = { ...DEFAULTS, ...saved, lists: { ...DEFAULTS.lists, ...saved.lists }, schedule: { ...DEFAULTS.schedule, ...saved.schedule } };
  // 1.2 kept one "distracting" list: anything in it that no preset covers becomes the player's own list.
  if (saved.distracting && !saved.custom) {
    const preset = Object.values(PRESETS).flatMap(p => p.sites);
    s.custom = saved.distracting.filter(d => !preset.includes(d));
  }
  return s;
}

function blockList(s) {
  return [...Object.keys(PRESETS).filter(k => s.lists[k]).flatMap(k => PRESETS[k].sites), ...(s.lists.custom ? s.custom : [])];
}

// Should this host count as a distraction under these settings? Allow-only treats every other website as one.
function isDistracting(host, s) {
  if (!host || host === "localhost" || host === "127.0.0.1") return false;
  if (s.allowOnly) return !matches(host, s.study);
  return matches(host, blockList(s));
}

function inSchedule(sch, now = new Date()) {
  if (!sch.on || !sch.days.includes(now.getDay())) return false;
  const mins = now.getHours() * 60 + now.getMinutes(), at = t => { const [h, m] = t.split(":").map(Number); return h * 60 + m; };
  const from = at(sch.from), to = at(sch.to);
  return from <= to ? mins >= from && mins < to : mins >= from || mins < to; // to before from runs past midnight
}

// Is blocking switched on right now: during a study session the settings cover, or inside the schedule.
function blockingNow(status, s) {
  const studying = status && status.running && status.studying;
  return (studying && (s.block === "always" || (s.block === "strict" && status.strict))) || inSchedule(s.schedule);
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

// Toolbar badge: minutes left while connected (lavender = studying, green = break, grey = paused), blank otherwise.
function showBadge(status) {
  chrome.action.setBadgeText({ text: status ? String(Math.ceil(Math.max(0, status.secondsLeft) / 60)) : "" });
  if (status) chrome.action.setBadgeBackgroundColor({ color: !status.running ? "#7d7894" : status.studying ? "#8b7fd1" : "#6fa77c" });
}
