# lofilife Focus (browser extension)

A Chrome extension that connects your browser to the lofilife desktop game while it runs. It works in Chrome, Edge, Brave and other Chromium browsers (version 120 or newer). The web and iOS versions of the game don't talk to it.

- **Popup:** shows the game's timer. You can start, pause and reset it, and set the study length (5 to 120 minutes, while the timer is stopped).
- **Focus stats:** level and XP, minutes focused, sessions and distractions today, your day streak, a bar chart of the last 7 days and your most studied subject this week.
- **Graphs and tags:** "See all stats" opens a page with focus time by day, week, month and year, stacked by tag, plus totals, averages and a per-tag breakdown (click a tag to show only it). The tag is the game's study subject: set it in the popup (or pick a recent one) and each finished session is logged under it.
- **Game colours:** the popup uses the game's menu colours and follows Paint > Menus when you change them.
- **Badge:** the toolbar icon shows the minutes left: terracotta while studying, green on a break, grey when paused, blank when the game isn't running.
- **Distracting sites** (YouTube, TikTok, Reddit and so on, editable in the popup): during a study session, being on one makes the room in the game fall apart and drains coins until you leave.
- **Blocking:** the extension can also block distracting sites. It does this during strict focus sessions by default, during every study session if you choose, or never.
- **Study sites** (Google Docs, Notion, Wikipedia and so on): these earn focus XP, at most 1 XP a minute.

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
