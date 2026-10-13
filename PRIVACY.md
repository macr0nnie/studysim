# lofilife privacy policy

Last updated: 2026-10-13

## The game
- lofilife has no accounts, ads, analytics or crash reporting.
- Your saves and settings are stored only on your computer (Unity PlayerPrefs, in the Windows registry).
- On Windows, the game reads the title of the Spotify desktop app's window to show the song that is playing. This stays on your computer.
- **Album covers (off by default).** If you turn on Settings > Sound > Spotify album covers, the game sends the playing song's artist and title to Apple's iTunes Search API (itunes.apple.com) to fetch its cover. Apple's own privacy policy applies to that request.
- The game listens on `http://localhost:8080/` so the browser extension can talk to it. It only accepts requests from your own computer and from browser extensions, never from websites.

## The browser extension (lofilife Focus)
- The extension reads the address of your active tab to check it against your lists of distracting and study sites.
- It sends only the site's host name (for example `youtube.com`) and timer commands to the game on your computer at `localhost:8080`. Nothing is sent to any other server.
- The focus stats page reads your session history and tags from the game on your computer. The extension doesn't keep its own copy.
- The popup remembers which tab you last had open, in the extension's local storage on your computer.
- Your block lists, allowed sites, blocking choices and focus-hours schedule are saved with Chrome's storage (`chrome.storage.sync`), which Chrome may sync between your own browsers if you are signed in to Chrome.
- The extension does not sell, share or transfer any data, and does not use it for anything other than the features above.

## Contact
Questions or requests: open an issue at https://github.com/macr0nnie/studysim/issues
