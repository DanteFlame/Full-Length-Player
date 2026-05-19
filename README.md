# Full-Length Player

A Windows desktop rewrite of the original browser-based **DualSync Video Player** concept for watching full-length creator reactions in sync with the movie/show being reacted to.

The original HTML app used two browser `<video>` elements, which made MKV, H.265/x265, embedded subtitle tracks, and some audio codecs unreliable. This desktop version uses **LibVLC** through `LibVLCSharp`, so it is built around a real native media engine instead of Chromium/HTML5 video.

## Current MVP features

- Load a reaction video and a movie/show video from:
  - local file path
  - local folder path, where the first video file is picked automatically
  - URL supported by LibVLC
- Reaction video is Player A / master.
- Movie/show video is Player B / synced overlay.
- Play, pause, stop, seek, jump forward/back, and speed changes apply to both players.
- Sync offset in seconds, including `-0.10s` / `+0.10s` nudging.
- Lock-step follow loop that keeps B aligned to A + offset.
- Draggable overlay window for Player B.
- Overlay size slider.
- Separate volume sliders.
- Embedded audio/subtitle track dropdowns, with a manual refresh button.
- External subtitle loading for `.srt`, `.ass`, `.ssa`, `.vtt`, and common subtitle files.
- Keyboard shortcuts:
  - Space: play/pause
  - Left / Right: jump A and B back/forward 5 seconds
  - `,` / `.`: offset nudge -/+ 0.10 seconds
  - F11: fullscreen toggle
- Saves last paths, offset, volumes, playback speed, lock-step setting, and overlay size to `%APPDATA%\FullLengthPlayer\settings.json`.

## Why LibVLC instead of browser video?

Browser video is the reason the old version struggles with MKV and x265. LibVLC handles MKV, H.265/x265, multi-audio, and subtitle-heavy anime-style media much more reliably.

This first desktop build does **not** directly embed MPC-HC or DirectShow/LAV filters. It should not need them for normal MKV/x265 playback because VLC's codec stack is bundled with the app. If a future build needs to specifically use your MPC-HC/LAV DirectShow setup, that should be added as a second playback backend rather than the default path.

## Build locally

Install the .NET 8 SDK, then from the repository root run:

```powershell
.\build.ps1
```

The runnable app folder will be created at:

```text
publish\FullLengthPlayer-win-x64\
```

Run:

```text
FullLengthPlayer.exe
```

## GitHub Actions build

Every push can produce a Windows x64 build artifact from the `Build Windows` workflow. Download the artifact, unzip it, and run `FullLengthPlayer.exe`.

## Notes / limitations

- The sync model is A-master/B-follower. B is repeatedly corrected to `A time + offset`.
- Very large drift is fixed with a seek. Tiny drift is left alone to avoid constant micro-seeking.
- Track dropdowns are populated from LibVLC after media is opened/played. Use **Refresh tracks** if the lists are blank at first.
- Native VLC/libVLC files are shipped beside the EXE; this is expected. Do not try to move only the EXE by itself.
