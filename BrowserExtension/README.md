# lofilife Focus (browser extension)

A Chrome extension that connects your browser to the lofilife desktop game while it runs. It works in Chrome, Edge, Brave and other Chromium browsers (version 120 or newer). The web and iOS versions of the game don't talk to it.

- **Popup:** Focus, Stats and Blocking tabs along the bottom. Focus shows the game's timer, what you're studying (click it to change the tag), one play/pause button, Reset, and the session length (pick 15 to 90 minutes while the timer is stopped).
- **Stats:** your week as a night skyline: one building a day, as tall as the time you studied, with a lit window for every finished session. Plus your streak, today's time, most studied tag, and level and XP.
- **Graphs and tags:** "Open full stats" opens a page with focus time by day, week, month and year, stacked by tag, plus totals, averages and a per-tag breakdown (click a tag to show only it). The tag is the game's study subject: set it in the popup (or pick a recent one) and each finished session is logged under it.
- **Look:** clean and light with lofi colours (lavender, dusty pink, sage) and a night sky for stats.
- **Badge:** the toolbar icon shows the minutes left: lavender while studying, green on a break, grey when paused, blank when the game isn't running.
- **Block lists:** ready-made lists you switch on (social media, video and streaming, news, shopping, games) plus your own list. During a study session, being on a listed site makes the room in the game fall apart and drains coins until you leave.
- **Blocking:** listed sites are blocked during strict focus sessions by default, during every study session if you choose, or never. Pick how strict: *Remind me* (a page you can click through for 5 minutes), *Block it*, or *Lock it* (the settings can't be changed until the session ends). When blocking ends, the blocked page offers a link back.
- **Allow only:** block every site except your allowed ones while blocking is on.
- **Focus hours:** a schedule (days and times) that blocks listed sites even when the game is closed.
- **Study sites** (Google Docs, Notion, Wikipedia and so on, the same list as Allow only): these earn focus XP, at most 1 XP a minute.

## Install for development
1. Open `chrome://extensions` (or `edge://extensions`).
2. Turn on **Developer mode**.
3. Click **Load unpacked** and pick this `BrowserExtension` folder.
4. Start the game. The popup says "Connected" when it finds it.

## Publish an update to the Chrome Web Store
1. Raise `version` in `manifest.json` (the store rejects a version it has already seen).
2. Zip the **contents** of this folder, so `manifest.json` is at the top of the zip. `store/` and this README can be left out.
3. In the Chrome Web Store developer dashboard, open the item, go to **Package**, upload the zip, and submit for review.

The listing text, permission justifications and images are in [`store/`](store/).

## How it talks to the game
The extension uses plain HTTP on `http://localhost:8080/`, which `ChromeWebEx.cs` serves while the game runs.

- **GET** returns `{"running","studying","secondsLeft","phaseSeconds","mode","strict","distracted"}`, plus stats (`level`, `xp`, `xpNext`, `todayMinutes`, `todaySessions`, `distractionsToday`, `streak`, `week` (7 days, today last), `topSubject`) and `theme` (the menu palette as hex). Older game builds leave the stats out and the popup hides them. Also `tag` (current) and `tags` (used before, most studied first).
- **GET /stats** returns `{"entries":[{"day","subject","minutes","sessions"}]}`, the whole study log, for the graphs page.
- **POST** sends a JSON body `{"action": ...}`. The actions are:
  - `start`, `pause`, `reset`
  - `set` with `minutes`
  - `focus` (study site, gives XP)
  - `distracted` with `site`, re-sent every 30 s while you stay on the site; the game ends it after 45 s with no report
  - `back` (you left the distracting site; no XP)
  - `tag` with `tag` (what the next sessions are logged under, the game's study subject)
  - `ping`

The game can't see your browser on its own, so detecting and blocking sites only works with this extension installed.

The Jersey 15 font in `fonts/` is under the SIL Open Font License (`fonts/OFL.txt`).
