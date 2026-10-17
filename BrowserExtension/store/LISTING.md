# Chrome Web Store listing: lofilife Focus

Paste each block into the matching field in the Chrome Web Store developer dashboard. Lines in [brackets] are notes, not copy.

## Store listing tab

**Name** (comes from `manifest.json`): lofilife Focus

**Summary** (comes from the manifest description, 132 characters max):
Focus companion for the lofilife desktop game: control your study timer, earn XP on study sites, and block distractions.

**Description:**

> lofilife Focus connects Chrome to lofilife, the cozy study game where focused time turns into furniture for your room.
>
> • See your study timer in the toolbar. The icon shows the minutes left.
> • Start, pause, reset or change the length of a session from any tab.
> • Watch your week light up: each day is a building in a night skyline, and every finished session lights a window.
> • Graphs of your focus by day, week, month and year, split by tags you give each session (Maths, Biology, ...).
> • Open a distracting site during a study session and your room in the game starts falling apart until you leave.
> • Study sites like Google Docs, Notion and Wikipedia earn focus XP.
> • Switch on ready-made block lists (social media, video, news, shopping, games) or make your own.
> • Choose how strict: a gentle reminder, a block, or a lock you can't undo until the session ends.
> • Allow-only mode: block everything except the sites you need.
> • Focus hours: block distracting sites on a schedule, even when the game is closed.
>
> You need the lofilife desktop game running on the same computer (Windows). The extension talks only to the game on your own computer, at localhost. It has no accounts, no ads and no tracking, and it sends nothing to any server.

**Category:** Productivity [I believe "Workflow & Planning" is the closest sub-category in the current dashboard; pick whatever it offers]
**Language:** English

**Graphics** (all in this folder):
- Store icon 128×128: `../icons/icon128.png`
- Screenshots 1280×800: `screenshot-1-timer.png`, `screenshot-2-distractions.png`, `screenshot-3-blocked.png`, `screenshot-4-stats.png`
- Small promo tile 440×280: `promo-small-440x280.png`

## Privacy practices tab

**Single purpose:**
A focus companion for the lofilife desktop game: it shows and controls the game's study timer and tells the game when the active tab is a study site or a distracting site, optionally blocking distracting sites during study sessions or scheduled focus hours.

**Permission justifications:**
- `tabs`: reads the host name of the active tab (for example youtube.com) to check it against the user's lists of distracting and study sites. Nothing else from the tab is read.
- `storage`: saves the user's block lists, allowed sites, blocking choices and schedule, and remembers the last report between background checks.
- `alarms`: re-checks the active tab every 30 seconds so the game knows the user is still on a distracting site, and so focus hours start and stop on time.
- Host permission `http://localhost:8080/*`: the lofilife game runs a small server on the user's own computer at this address. The extension reads the timer from it and sends timer commands and site reports to it. No other host is contacted.

**Remote code:** No, I am not using remote code. [Everything is in the package.]

**Data usage:** tick **Web history** (the extension reads the active tab's address). Then tick all three certifications: not sold to third parties, not used for unrelated purposes, not used for creditworthiness or lending.

**Privacy policy URL:** [Host `PRIVACY.md` publicly once the contact email is filled in. The GitHub link to the file works if the repo is public.]
