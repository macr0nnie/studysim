# Automatic builds (CI/CD)

Two GitHub Actions workflows live in `.github/workflows/`:

- **Playtest build** (`playtest-build.yml`) runs on every push to `main` or `release` (or by hand from the Actions tab). It builds lofilife for WebGL and Windows on GitHub's own machines (with game-ci and the Unity version in `ProjectSettings/ProjectVersion.txt`), so no computer of yours needs to be on. It keeps each build as a download on the run page for 14 days. Until the Unity licence secret is added, it skips the build instead of failing.
  - Push to `main`: the **Early Access** pre-release on the repo's Releases page is replaced with the new build (web and Windows zips), and the playtest itch page is updated.
  - Push to `release`: a new GitHub release is published, named after the version in Player Settings plus the build number (e.g. `lofilife 0.1.0 (build 12)`), with a change list made from the merged PRs. It becomes the "Latest" release. Bump the version in Player Settings when you want the number to change.
- **iOS (Xcode project)**, part of the Playtest build, runs only when you start the workflow by hand from the Actions tab or push to `release`. It exports the Xcode project as a download (`lofilife-iOS-xcode-N`). That is as far as GitHub's Linux machines go: building the app, signing it and sending it to TestFlight still needs Xcode on a Mac (yours, a rented cloud Mac, or a macOS GitHub runner set up with your Apple certificates, which isn't done yet).
- **Browser extension** (`extension.yml`) runs when anything in `BrowserExtension/` changes on `main`. It zips the extension (without `store/` and the README) and keeps the zip on the run page. If the `version` in `manifest.json` went up, it uploads the zip to the Chrome Web Store and submits it for review.

Nothing here pushes to the `release` branch or touches the public lofilife itch page; it only reacts when you push to `release`.

## What you do once, by hand

All secrets go in GitHub: repo **Settings > Secrets and variables > Actions**. "Secret" means the Secrets tab, "variable" means the Variables tab. Never paste them into chat or commit them.

### 1. Unity licence (once, from your main PC)

GitHub's machines need a copy of your free Unity licence. You do this once.

1. On the computer where Unity Hub is installed and signed in, open File Explorer, type `C:\ProgramData\Unity` in the address bar and press Enter. Look for `Unity_lic.ulf`.
   - Not there? In Unity Hub: gear icon (Preferences) > Licenses > Add > Get a free personal license, then look again.
2. Right-click `Unity_lic.ulf` > Open with > Notepad. Press Ctrl+A, Ctrl+C.
3. Go to https://github.com/macr0nnie/studysim/settings/secrets/actions > **New repository secret**:
   - Name `UNITY_LICENSE`, paste the file's contents, **Add secret**.
   - Again with name `UNITY_EMAIL` and your Unity account email.
   - Again with name `UNITY_PASSWORD` and your Unity account password.
4. Actions tab > **Playtest build** > **Run workflow**.

If your Unity account uses a login code (two-step), the build may fail at activation; send the log if so.

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

- The repo is public, so GitHub's machines are free for these builds. If it goes private, the free plan has 2,000 minutes a month, and each push uses both builds' time (I'd guess 30 to 60 minutes per push; not measured).
- Builds are kept 14 days. On a private repo, artifact storage counts against 500 MB on the free plan, which a couple of builds would fill.
- The Unity Library is cached between runs (per platform, up to GitHub's 10 GB per repo), so only the first build imports everything.
- A newer push to `main` cancels a build still running.
