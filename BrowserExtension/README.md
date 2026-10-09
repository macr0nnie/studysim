# Study Sim Focus (browser extension)

This is a Chrome extension that connects your browser to Study Sim while the game is running. It works in Chrome, Edge, Brave and other Chromium browsers.

- **Popup:** shows the game's timer. You can start, pause and reset it from there.
- **Distracting sites** (YouTube, TikTok, Reddit and so on, editable in the popup): during a study session, being on one makes the room in the game fall apart and drains coins until you leave.
- **Blocking:** the extension can also block distracting sites. It does this during strict focus sessions by default, during every study session if you choose, or never.
- **Study sites** (Google Docs, Notion, Wikipedia and so on): these earn focus XP, at most 1 XP a minute.

## Install
1. Open `chrome://extensions` (or `edge://extensions`).
2. Turn on **Developer mode**.
3. Click **Load unpacked** and pick this `BrowserExtension` folder.
4. Start the game. The popup's dot turns green when it connects.

## How it talks to the game
The extension uses plain HTTP on `http://localhost:8080/`, which `ChromeWebEx.cs` serves while the game runs.

- **GET** returns `{"running","studying","secondsLeft","mode","strict","distracted"}`.
- **POST** sends a JSON body `{"action": ...}`. The actions are:
  - `start`, `pause`, `reset`
  - `set` with `minutes`
  - `focus` (study site, gives XP)
  - `distracted` with `site`, re-sent every 30 s while you stay on the site; the game ends it after 45 s with no report
  - `back` (you left the distracting site; no XP)
  - `ping`

The game can't see your browser on its own, so detecting and blocking sites only works with this extension installed.
