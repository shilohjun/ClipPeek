# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.2] - 2026-10-23

### Fixed

- **"open data folder" did nothing** — the command passed the folder with a trailing backslash, and `"C:\path\"` escapes its own closing quote on the Windows command line, so Explorer received a broken path. The trailing separator is now stripped and the call uses an explicit `UseShellExecute` process start, with a bubble showing the real error if it still fails.
- **Unsnapping did not restore the icon at startup** — `OnHandleCreated` forced the window back to the full icon size, which wiped the snap applied by `PositionIcon`, so a docked icon stayed half-drawn even after "unsnap". The handle-created path now applies the dock geometry instead of clamping the size.
- **Unsnapping from the right or bottom edge left half the icon off-screen** — the window grows from half to full size around its old position, so it is now clamped back into the working area.
- **Icon changes, shortcut creation and data-folder failures were completely silent** — the exceptions only went to the log. Every such action now reports the reason and the path in a bubble, and the icon-save failure names the data directory explicitly.
- **The data directory is no longer assumed to be writable** — if `%APPDATA%\ClipPeek` refuses writes the app falls back to `%LOCALAPPDATA%\ClipPeek` and then to the folder next to the exe, and says so in a bubble at startup instead of silently losing the history.

## [1.3.1] - 2026-10-23

### Fixed

- The toggle items in the right-click menu ("pause monitoring", "start with Windows", "play the launch animation", "cursor glow on copy") never showed a check mark, because the menu had `ShowImageMargin = false` without `ShowCheckMargin = true` — the check area had nowhere to be drawn, so the items looked like plain commands. The menu now reserves a check column, and all four toggles use `CheckOnClick` so the tick flips visibly and the state is read back from the item.

## [1.3.0] - 2026-10-23

### Added

- **Cursor glow** — every successful copy flashes a green ring around the mouse: it follows the cursor while playing, is click-through and never takes focus, and is hollow in the middle so it does not hide the text under the pointer. One cycle lasts 1.4 s (a quick rise, then a fade) while the ring expands slightly. It covers text, images and files, and re-copying from the history panel glows too. Toggleable from the right-click menu ("cursor glow on copy"), stored in settings, on by default.

## [1.2.0] - 2026-10-23

### Added

- **Edge snapping** — drag the floating icon within 24 px of any screen edge and it snaps flush. While snapped only **half of the icon** stays visible, cut along the edge, and it slowly breathes like an LED (opacity oscillates between 42% and 100% over a 4.2 s cycle). Hovering expands it into the full icon; dragging it away or choosing "unsnap" releases it. The snapped edge is persisted.
- The tray menu gained "unsnap" (enabled only while snapped), and `--preview` now renders the snapping animation key frames.

## [1.1.1] - 2026-10-23

### Changed

- The history panel follows the floating icon by default again: move the icon and the panel opens next to it (an already open panel follows too). It can only be dragged while the floating icon is hidden, because that is the only situation where it needs its own position; the dragged position is remembered for those cases.
- Over-long text no longer suppresses the confirmation bubble. Every copy gives feedback now: the bubble's second line says "truncated · only the first 50,000 characters were saved" when the text was cut, and the history row is marked "truncated".
- Storage for a single text entry is capped at 50,000 characters (was 200,000), so a few huge copies can no longer bloat `history.json`.

## [1.1.0] - 2026-10-23

### Added

- **Launch animation** — on startup, 144 eight-pixel blocks converge from all around into the floating icon's position and assemble it, over a fully transparent background (no card, no backdrop). It never blocks clicks; the panel hotkey skips it. Can be toggled ("play animation on startup") and replayed from the menu.
- **Custom icon** — pick a file via right-click → "change icon…", or drop a PNG / JPG / ICO / BMP onto the floating icon or the history panel. Either way a crop dialog opens (drag to pan, scroll to zoom, live 96 px and 48 px previews). The floating icon, tray icon and launch-animation blocks all switch. "Reset icon" restores the built-in one and deletes the stored file.
- **Start menu shortcut with launch key** — "create Start menu shortcut (Ctrl+Alt+C)" menu item, so ClipPeek can be started even when it is not running.
- `--preview` now also renders launch-animation key frames and the crop dialog for documentation.

- **History panel is draggable** — hold any empty part of the panel (header or footer) to move it; the position is remembered separately from the floating icon and "reset position" clears it.
- **Hiding the icon is recoverable now** — hiding it pops a bubble explaining how to restore it, the panel footer turns into an "icon hidden · click here" link, and a hint appears on the next launch if the icon is still hidden.

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
