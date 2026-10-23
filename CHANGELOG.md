# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-10-23

### Added

- **Launch animation** — on startup, 144 eight-pixel blocks converge from all around into the floating icon's position and assemble it, over a fully transparent background (no card, no backdrop). It never blocks clicks; the panel hotkey skips it. Can be toggled ("play animation on startup") and replayed from the menu.
- **Custom icon** — pick a file via right-click → "change icon…", or drop a PNG / JPG / ICO / BMP onto the floating icon or the history panel. Either way a crop dialog opens (drag to pan, scroll to zoom, live 96 px and 48 px previews). The floating icon, tray icon and launch-animation blocks all switch. "Reset icon" restores the built-in one and deletes the stored file.
- **Start menu shortcut with launch key** — "create Start menu shortcut (Ctrl+Alt+C)" menu item, so ClipPeek can be started even when it is not running.
- `--preview` now also renders launch-animation key frames and the crop dialog for documentation.

### Fixed

- Restoring a file entry used to write only the path text, so pasting into Explorer did nothing. It now puts the real files back on the clipboard (`CF_HDROP`) with a path-text fallback; when the files no longer exist it copies paths instead, says so in the bubble, and the history panel marks such entries with "files gone".
- Installer: the "start with Windows" task was not pre-checked although the README claimed it was; it now uses `checkedonce`.
- Crop dialog used to be centred with `StartPosition.CenterScreen`, which lands off-screen on mixed-DPI multi-monitor setups; it is now centred on the monitor under the cursor.

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
