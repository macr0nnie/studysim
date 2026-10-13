# Automatic builds (CI/CD)

Two GitHub Actions workflows live in `.github/workflows/`:

- **Playtest build** (`playtest-build.yml`) runs on every push to `main` (or by hand from the Actions tab). It builds lofilife for WebGL and Windows in Unity 6000.6.5f1, keeps each build as a download on the run page for 14 days, and pushes it to a playtest page on itch.io.
- **Browser extension** (`extension.yml`) runs when anything in `BrowserExtension/` changes on `main`. It zips the extension (without `store/` and the README) and keeps the zip on the run page. If the `version` in `manifest.json` went up, it uploads the zip to the Chrome Web Store and submits it for review.

Nothing here touches the `release` branch or the public lofilife itch page.

## What you do once, by hand

All secrets go in GitHub: repo **Settings > Secrets and variables > Actions**. "Secret" means the Secrets tab, "variable" means the Variables tab. Never paste them into chat or commit them.

### 1. Unity licence (required for any build)

Free Personal licence (from game.ci's activation guide):

1. In Unity Hub: Preferences > Licenses > Add > Get a free personal license. (Even if a licence already shows, do this, or the file below may not exist.)
2. Open `C:\ProgramData\Unity\Unity_lic.ulf` in Notepad and copy all of it.
3. Add secrets:
   - `UNITY_LICENSE`: the whole contents of that file
   - `UNITY_EMAIL`: your Unity account email
   - `UNITY_PASSWORD`: your Unity account password

If your Unity account uses two-factor login, the first run may fail at activation. game.ci's guide doesn't cover 2FA, so if that happens, send the log.

### 2. itch.io (optional; without it builds still appear on GitHub)

1. On itch.io, create a new project, e.g. `lofilife-playtest`. Set it to **Restricted** (password or invite only) or keep it as a draft, so playtest builds don't show on the real store page.
2. Get an API key at https://itch.io/user/settings/api-keys and add it as secret `BUTLER_API_KEY`.
3. Only if your playtest project's URL isn't `macronnie.itch.io/lofilife-playtest`: add variable `ITCH_TARGET` = `macronnie/<project-name>`.
4. After the first successful run, in the itch project's Edit page, tick **"This file will be played in the browser"** on the `playtest-web` upload. itch remembers it for later pushes.

Channels: `playtest-web` (browser build) and `playtest-windows` (zip download).

### 3. Chrome Web Store (optional)

The API can only update an extension that already exists in the store, so the first submission is done by hand in the Developer Dashboard (listing, privacy tab, screenshots). After that:

1. In Google Cloud Console: create a project, enable the **Chrome Web Store API**, set up the OAuth consent screen, and create an OAuth client (Web application). Your Google account needs 2-step verification.
2. Get a refresh token for the `https://www.googleapis.com/auth/chromewebstore` scope from the OAuth 2.0 Playground (use your own client ID and secret in its settings).
3. Add secrets `CWS_CLIENT_ID`, `CWS_CLIENT_SECRET`, `CWS_REFRESH_TOKEN`.
4. Add variables `CWS_PUBLISHER_ID` (Developer Dashboard > Publisher > Settings) and `CWS_EXTENSION_ID` (the 32-letter ID in the store URL).

Full steps: https://developer.chrome.com/docs/webstore/using-api

To ship an extension update, bump `version` in `BrowserExtension/manifest.json`. A change without a version bump only makes the zip; the store rejects an upload with the same version.

## Cost and limits

- The repo is **public**, so GitHub's standard runners are free with no minute limit. If the repo goes private, the free plan has 2,000 minutes a month, and each push here can use roughly an hour or more across both builds (estimate; the first run will tell).
- Builds are kept 14 days. On a private repo, artifact storage counts against 500 MB on the free plan, which a couple of builds would fill.
- The Unity Library cache is per platform and capped at 10 GB per repo; GitHub deletes old entries on its own.
- The first build of each platform has no cache and imports everything, so it is the slowest.
- A newer push to `main` cancels a build still running.
