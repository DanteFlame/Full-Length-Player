<h1><img src="docs/teal-orange-solid.png" width="80" alt="Full Length Player icon"> Full Length Player</h1>

**The reaction. The show. Perfectly paired.**

Watch full-length reactions alongside your own high-quality movie or episode in one Windows player. Keep both soundtracks audible, choose your audio and subtitle tracks, and control both videos together with a single timeline. Powered by MPV, Full Length Player handles formats such as AV1 and HEVC, styled ASS subtitles, local files, YouTube links, and direct streams with configurable Patreon Referer support.

Make the composition yours: crop and pan the reaction, snap the source to any corner or edge, and fill a 16:9, 4:3, or 16:10 canvas. Find alignment with audio matching, fine-tune it in 0.05-second steps, and let the sync lock keep the videos together—even when changing speed. Missed a comment? **What Did They Say?** replays ten seconds at normal speed, brings the reaction forward in the mix, then restores your settings. Save the whole session, go fullscreen, and enjoy a clean viewing experience with keyboard, mouse, or Moonlight-streamed touch controls.

[**Download for Windows**](https://github.com/DanteFlame/Full-Length-Player/releases/latest) · [All releases and betas](https://github.com/DanteFlame/Full-Length-Player/releases) · [Report a problem](https://github.com/DanteFlame/Full-Length-Player/issues)

## Get watching

1. Download the Windows x64 ZIP, extract it, and run **FullLengthPlayer.exe**. Keep the accompanying files together; no separate .NET installation is needed.
2. In **Setup → Media**, load **Reaction A** and **Source B**. Both open paused. Use a local file, a YouTube video link, or a direct media/stream URL—not a Patreon post URL.
3. Line up a shared moment, then choose **Setup → Sync → Lock current alignment**. Or analyze audio while both playheads are near the same scene; select the same dialogue language the reactor watched. Matching may need another sample when commentary obscures the audio.
4. In **Layout**, choose your canvas, crop the reaction, and position/resize the source. Hide Setup or enter fullscreen when ready.

**Session** lets you save, restore, or start fresh. Saved sessions protect stream URLs and headers with your Windows account and are intended for the same account on the same PC. Expired stream links may need refreshing.

## Keyboard controls

| Key | Action |
| --- | --- |
| **Space / K** | Play or pause both videos |
| **J / ←** · **L / →** | Back · forward 5 seconds |
| **A** | Toggle 1× and your previous speed |
| **S / D** | Decrease / increase shared speed by 0.25× |
| **G** | Toggle your favorite speed and the previous speed |
| **H** | What Did They Say? — 10-second commentary replay |
| **, / .** | Nudge the sync offset by − / +0.05 seconds |
| **F / F11** · **Esc** | Toggle fullscreen · leave fullscreen |
| **Shift + playback keys** | Play/pause or seek only the hovered player; falls back to the selected player |
| **Shift + mouse wheel** | Adjust the hovered player's volume |
| **F1 / F2** | Select reaction / source |
| **Ctrl + O** | Open a local file in the selected player |

Playback speed always changes **both** players, even with Shift. Shortcuts yield to editing fields and menus where appropriate.

## Fullscreen gestures

- Tap the middle third to play/pause; double-tap the left/right third to seek five seconds.
- Hold the left third for **1×**, or the right third for your **favorite speed**. Release to restore the previous speed.
- Move the pointer to reveal the timeline and **Exit fullscreen**. Controls and pointer hide again while viewing.

## Under the hood

Windows x64 · C# / .NET 8 WinForms · two libmpv playback engines · yt-dlp + Deno for YouTube resolution.

Build on Windows with the .NET 8 SDK: run `./build.ps1` in PowerShell. See the [UI guide](docs/UI.md), [design specification](docs/SPECIFICATION.md), and [third-party components and notices](docs/THIRD_PARTY.md).

Use media you have permission to access. DRM-protected services and videos requiring sign-in are not supported; online availability and buffering depend on the provider and connection.
