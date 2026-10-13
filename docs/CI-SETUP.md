# Automatic builds (CI/CD)

Two GitHub Actions workflows live in `.github/workflows/`:

- **Playtest build** (`playtest-build.yml`) runs on every push to `main` (or by hand from the Actions tab). It runs on your own PC through GitHub's runner app, using the Unity already installed there (the version in `ProjectSettings/ProjectVersion.txt`). It builds WebGL and Windows with the same code as the Study Sim menu items, keeps each build as a download on the run page for 14 days, and pushes it to a playtest page on itch.io. If your PC is off, the build waits until the runner is back.
  - Push to `main`: the **Early Access** pre-release on the repo's Releases page is replaced with the new build (web and Windows zips), and the playtest itch page is updated.
  - Push to `release`: a new GitHub release is published, named after the version in Player Settings plus the build number (e.g. `lofilife 0.1.0 (build 12)`), with a change list made from the merged PRs. It becomes the "Latest" release. Bump the version in Player Settings when you want the number to change.
- **Browser extension** (`extension.yml`) runs when anything in `BrowserExtension/` changes on `main`. It zips the extension (without `store/` and the README) and keeps the zip on the run page. If the `version` in `manifest.json` went up, it uploads the zip to the Chrome Web Store and submits it for review.

Nothing here pushes to the `release` branch or touches the public lofilife itch page; it only reacts when you push to `release`.

## What you do once, by hand

All secrets go in GitHub: repo **Settings > Secrets and variables > Actions**. "Secret" means the Secrets tab, "variable" means the Variables tab. Never paste them into chat or commit them.

### 1. Runner on your PC (required for game builds)

No Unity licence or password goes to GitHub: the builds use your PC's Unity, which is already signed in.

1. Open https://github.com/macr0nnie/studysim/settings/actions/runners/new and pick **Windows**.
2. Open PowerShell (not as admin), and paste the commands from that page one block at a time. Put the runner in a short folder such as `C:\actions-runner`, **not** inside your studysim project folder.
3. When `config.cmd` asks questions, press Enter for the defaults, except **"Would you like to run the runner as service?" answer N**. A service runs as a different Windows account that isn't signed in to Unity.
4. Start it with `.\run.cmd`. Leave that window open while you want builds to run (minimise it). To start it again later, open PowerShell in that folder and run `.\run.cmd`.
5. If Unity isn't installed at `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`, add a variable `UNITY_PATH` with the full path to `Unity.exe`.

The runner checks the project out into its own folder (inside `C:\actions-runner\_work`), separate from the copy you work in, so builds never touch your working files. The first build imports everything; later ones reuse its Library.

The workflow only runs on pushes to `main` and manual runs, never on pull requests, so nobody else's code can run on your PC.

### 2. itch.io (optional; without it builds still appear on GitHub)

1. On itch.io, create a new project, e.g. `lofilife-playtest`. Set it to **Restricted** (password or invite only) or keep it as a draft, so playtest builds don't show on the real store page.
2. Get an API key at https://itch.io/user/settings/api-keys and add it as secret `BUTLER_API_KEY`.
3. Only if your playtest project's URL isn't `macronnie.itch.io/lofilife-playtest`: add variable `ITCH_TARGET` = `macronnie/<project-name>`.
4. After the first successful run, in the itch project's Edit page, tick **"This file will be played in the browser"** on the `playtest-web` upload. itch remembers it for later pushes.

Channels: `playtest-web` (browser build) and `playtest-windows` (zip download).

### 3. Chrome Web Store (optional)

The API can only update an extension that already exists in the store, so the first submission is done by hand in the Developer Dashboard (listing, privacy tab, screenshots). On that first submission, set Visibility to **Private** (trusted testers) until you want it public: the workflow publishes with whatever visibility the dashboard has, so testers get each update and the public doesn't. After that:

1. In Google Cloud Console: create a project, enable the **Chrome Web Store API**, set up the OAuth consent screen, and create an OAuth client (Web application). Your Google account needs 2-step verification.
2. Get a refresh token for the `https://www.googleapis.com/auth/chromewebstore` scope from the OAuth 2.0 Playground (use your own client ID and secret in its settings).
3. Add secrets `CWS_CLIENT_ID`, `CWS_CLIENT_SECRET`, `CWS_REFRESH_TOKEN`.
4. Add variables `CWS_PUBLISHER_ID` (Developer Dashboard > Publisher > Settings) and `CWS_EXTENSION_ID` (the 32-letter ID in the store URL).

Full steps: https://developer.chrome.com/docs/webstore/using-api

To ship an extension update, bump `version` in `BrowserExtension/manifest.json`. A change without a version bump only makes the zip; the store rejects an upload with the same version.

## Cost and limits

- Game builds run on your PC, so they use no GitHub minutes. The extension workflow runs on GitHub's machines and is free on a public repo.
- Builds are kept 14 days. On a private repo, artifact storage counts against 500 MB on the free plan, which a couple of builds would fill.
- Your PC is busy while a build runs. The runner's Library and builds take a few GB on disk.
- A newer push to `main` cancels a build still running.
