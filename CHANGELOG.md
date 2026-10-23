# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-23

First public release.

### Added

- **Copy confirmation bubble** — shows type, character count or image dimensions, and up to two preview lines; images get a thumbnail. Fades out after 2.5 s, pauses while hovered, clicking it opens the history panel.
- **Draggable floating icon** — always-on-top 48 px widget (DPI-scaled); click opens the history panel, drag moves it, right-click opens the menu. Position is persisted.
- **History panel** — last 200 entries with owner-drawn rows, type badges and timestamps. Click to copy back, right-click to delete, `↑` `↓` `Enter` `Del` `Esc` keyboard support, closes when it loses focus.
- **Global hotkey** `Ctrl+Alt+V` to toggle the history panel.
- **Tray icon** with the same menu, plus double-click to restore a hidden floating icon.
- **Sensitive content handling** — content carrying the Windows "Clipboard Viewer Ignore" flag is never recorded or shown.
- **Fullscreen awareness** — the icon and bubble hide during D3D fullscreen, presentation mode, and a non-maximized window covering the whole monitor.
- **DPI awareness** via application manifest, with a single scaling factor for all layout constants.
- **Data location policy** — portable mode (`portable.txt` next to the exe) keeps data beside the executable; otherwise data goes to `%APPDATA%\ClipPeek`, so uninstalling never deletes user history.
- **Inno Setup installer** (per-user, no UAC), portable ZIP, and SHA-256 checksums.
- **Self-test mode** (`--selftest`) covering history dedupe, JSON round trip, clipboard capture, bubble and panel visibility, restore and image handling.
- **CI workflow** that builds with `csc.exe`, runs the self-test, and packages the installer and portable ZIP.

### Known limitations

- Images are recorded as metadata only; they cannot be restored to the clipboard.
- No search box in the history panel.
- Binaries are unsigned, so SmartScreen warns on first run.
