# ClipPeek

<p align="center">
  <img src="assets/banner.png" alt="ClipPeek — see exactly what you copied" width="880">
</p>

<h2 align="center">ClipPeek</h2>

<p align="center">
  <a href="README.md">中文</a> | English
</p>

<p align="center">
  <img alt="Version" src="https://img.shields.io/badge/version-v1.0.0-brightgreen">
  <img alt="License" src="https://img.shields.io/badge/license-MIT-blue">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6">
  <img alt="Size" src="https://img.shields.io/badge/exe-57%20KB-success">
  <a href="https://github.com/shilohjun/ClipPeek/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/shilohjun/ClipPeek/total"></a>
</p>

> Press Ctrl+C and actually see what you copied.

Copying on Windows has no feedback at all. You press Ctrl+C, then you are not sure it worked, so you switch windows and paste into a scratchpad just to check. Or you copied three different things five minutes ago and cannot remember which one is on the clipboard now.

ClipPeek adds feedback and memory to that one keystroke:

- **The moment you copy**: a small bubble appears in the corner of the screen — `Copied · Text · 42 chars` plus a two-line preview. Images get a thumbnail, multiple files get a count.
- **Whenever you look back**: a draggable floating icon on the desktop. One click opens a history panel with 200 entries; click any entry to put it back on the clipboard.

A single 57 KB executable. No runtime to install, no registry writes, no administrator rights.

---

## Why this exists

The trigger was mundane: in a terminal, Ctrl+C sometimes copies and sometimes interrupts. After copying a long log you have no way to confirm you actually grabbed it except pasting somewhere.

Every other clipboard manager makes you **press a hotkey first** to see the result — the order is backwards.

ClipPeek flips it: **copying is the subject, feedback is automatic**. You do nothing and the bubble tells you what happened; the history panel is only there for when you want to dig through the past.

---

## What it does

| | |
|---|---|
| ✅ **Copy confirmation bubble** | Type + size + up to two preview lines; fades after 2.5 s, pauses on hover, click to open history |
| ✅ **Image thumbnails** | Screenshots and images get a thumbnail with dimensions and size |
| ✅ **Draggable floating icon** | Put it anywhere, position is remembered; click to open, right-click for menu, 4 px threshold separates drag from click |
| ✅ **History panel** | Last 200 entries with type badge, time and first line; click to restore, `Del` to delete, `Esc` to close |
| ✅ **Global hotkey** | `Ctrl+Alt+V` opens the panel even if the icon is covered |
| ✅ **Tray fallback** | If the floating icon gets dragged off-screen or hidden behind a fullscreen app, the tray icon is still there |
| ✅ **Respects sensitive content** | Anything flagged with Windows' "Clipboard Viewer Ignore" (password managers set it) is never recorded and never shown |
| ✅ **Steps aside for fullscreen** | Automatically hides for games, fullscreen video and presentations; comes back within a second |
| ✅ **Crisp on high DPI** | Declares DPI awareness, so it is not a blurry bitmap stretched at 200% |
| ✅ **Follows your theme** | Reads the system light/dark setting and matches it |

---

## Screenshots

| History panel | Text bubble |
|---|---|
| ![History panel](screenshots/panel.png) | ![Text bubble](screenshots/bubble-text.png) |

| Image bubble | Floating icon |
|---|---|
| ![Image bubble](screenshots/bubble-image.png) | ![Floating icon](screenshots/icon.png) |

---

## Install

### Option 1: installer (recommended)

Download `ClipPeek-Setup-1.0.0.exe` from [Releases](https://github.com/shilohjun/ClipPeek/releases) and run it.

- Installs to `%LOCALAPPDATA%\Programs\ClipPeek` — **no administrator rights required**
- Start menu shortcut is always created; desktop shortcut and "start with Windows" are optional (autostart is checked by default)
- Uninstall from Settings → Apps. **Your history is kept.**

### Option 2: portable

Download `ClipPeek-1.0.0-portable.zip`, extract anywhere, run `ClipPeek.exe`.

The archive contains `portable.txt`, which makes the app keep `history.json` / `settings.json` next to the executable, so the whole folder can live on a USB stick.

### Option 3: build it yourself

See [Build from source](#build-from-source). All you need is the .NET Framework compiler that ships with Windows.

> ⚠️ **No code signing certificate**: the installer and the exe will trigger a SmartScreen warning on first run ("Windows protected your PC"). Click "More info" → "Run anyway". If that bothers you, build from source — every line is in this repository.

---

## Usage

| Action | Result |
|---|---|
| Copy anything | Icon flashes green + bubble appears (type / size / preview / thumbnail) |
| **Click** the floating icon | Open / close the history panel |
| **Drag** the floating icon | Move it anywhere; position is remembered across restarts |
| **Right-click** the floating icon | History / pause / autostart / hide / reset position / open data folder / clear history / quit |
| Click the tray icon | Same as clicking the floating icon (fallback) |
| Double-click the tray icon | Bring back a hidden floating icon |
| `Ctrl+Alt+V` | Global hotkey for the history panel |

**Inside the panel**: `↑` `↓` select · `Enter` copy back · `Del` delete · `Esc` close · click to copy · right-click for menu · clicking outside closes it.

---

## Data and privacy

| File | Location | Contents |
|---|---|---|
| `history.json` | Installed: `%APPDATA%\ClipPeek\`<br>Portable: next to the exe | Last 200 entries |
| `settings.json` | Same | Icon position, pause state, autostart flag |
| `clip-peek.log` | Same | Written **only on errors**; normally does not exist |

**Fully local**: no network requests, no accounts, no telemetry. Everything stays on your disk.

What gets skipped:

- Content flagged with "Clipboard Viewer Ignore" (1Password, Bitwarden and friends) → not recorded, no bubble
- Images are stored as dimensions only — **the image itself is never written to disk**
- Text longer than 200,000 characters → still recorded, but no bubble (to avoid freezing the UI)

To remove everything: quit the app, delete the data folder, delete the install folder. The uninstaller deliberately keeps the data folder.

---

## Trade-offs

- **Images cannot be restored**: history only keeps "1920×1080" plus a size, so clicking it says the image itself was not saved. In exchange, screenshots never silently pile up on disk.
- **Duplicate content is merged**: copying the same text twice updates the existing entry instead of filling the list with repeats.
- **No search box**: under 200 entries, scrolling and type badges are enough; a search box would steal keyboard focus and fight the "click outside to close" behaviour.
- **Comments are in Chinese**: the source keeps Chinese comments, which is why this English README exists.

---

## FAQ

**The icon disappeared.**
Double-click the tray icon (or right-click the tray → "show floating icon"). It hides on purpose during fullscreen games, video and presentations, and returns within a second.

**A copy did not produce a bubble.**
Three possibilities: the text exceeded 200,000 characters (history only), the content carried a password manager's "do not record" flag (not recorded at all), or "pause monitoring" is checked in the tray menu.

**After changing resolution or display scaling the icon ended up somewhere odd.**
Right-click the icon → "reset position".

**Dual monitors?**
The icon starts on the right edge of the primary display; drag it to the other monitor and it will stay there.

**The hotkey conflicts with something else.**
Change `0x56` (the letter V) in `Native.RegisterHotKey(..., 0x56)` in `src/ClipPeekContext.cs` and rebuild.

**I do not want it to start with Windows.**
Uncheck "start with Windows" in the tray/icon menu; that removes the shortcut from your Startup folder.

---

## Build from source

Only Windows' built-in .NET Framework compiler (`csc.exe`) is needed — no Visual Studio, no .NET SDK.

```powershell
# build dist\ClipPeek.exe
powershell -ExecutionPolicy Bypass -File build.ps1

# run the self-test, then produce the portable zip and the installer
# (the installer step needs Inno Setup 6's ISCC.exe)
powershell -ExecutionPolicy Bypass -File package.ps1
```

The self-test runs 10 checks (history dedupe, JSON round trip, clipboard capture, bubble show/hide, panel show/hide, restore, image-not-restorable, …); any failure aborts `package.ps1`.

The icon, logo and banner are generated by calling the app's own drawing code — not hand-drawn:

```powershell
powershell -ExecutionPolicy Bypass -File tools\make-assets.ps1
```

### Version number

There is exactly one source of truth: `AppVersion` in `installer/version.iss`. Both `package.ps1` and the installer script read it.

---

## Repository layout

```
ClipPeek/
├── src/                     C# sources (C# 5, .NET Framework 4.x compatible)
│   ├── Program.cs           entry point, single instance, CLI flags
│   ├── ClipPeekContext.cs   controller: clipboard polling, tray, hotkey, menu, self-test
│   ├── IconForm.cs          draggable floating icon (drag vs click)
│   ├── BubbleForm.cs        the confirmation bubble (fade in / hold / fade out)
│   ├── HistoryForm.cs       history panel (owner-drawn list, keyboard)
│   ├── ClipboardWatcher.cs  clipboard reads (retries, ignore flag, thumbnails)
│   ├── HistoryStore.cs      history storage (JSON, dedupe, 200 entry cap)
│   ├── Settings.cs          settings storage
│   ├── Paths.cs             data location policy (portable vs installed)
│   ├── Theme.cs             light/dark palette and icon rendering
│   ├── Ui.cs                DPI scaling
│   ├── Native.cs            Win32 P/Invoke
│   ├── Autostart.cs         startup-folder shortcut
│   └── Log.cs               error-only logging
├── installer/               Inno Setup script + version
├── assets/                  icon, logo, README banner (generated)
├── screenshots/             real captures
├── tools/make-assets.ps1    generates icon and banner
├── build.ps1                compile
├── package.ps1              self-test + portable zip + installer + checksums
├── dist/                    artifacts (dist/ClipPeek.exe is committed for convenience)
└── .github/workflows/       CI: build + self-test + package
```

> `dist/ClipPeek.exe` is committed as a convenience snapshot and may lag behind `src/`. Trust the files attached to Releases.

---

## Compatibility

- Windows 10 (1809+) / Windows 11, x64 and ARM64 (AnyCPU)
- Requires .NET Framework 4.x (bundled with Windows 10 1903 and later)
- High DPI (including 200% scaling) and multi-monitor setups
- No network access whatsoever

---

## License

[MIT](LICENSE)
