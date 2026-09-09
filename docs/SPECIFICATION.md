# Authoritative product specification

Gonz's explicit decisions in the September 2026 conversation override the former
README and code. This document preserves that agreement; future work must not
mistake the roadmap for features already delivered.

## Platform and media

- Windows executable first. iOS/iPadOS are not current targets.
- MPV/libmpv is the required playback backend, replacing LibVLC and browser video.
- Reaction is player A, the timeline master. Source movie/show is player B.
- B normally loads a high-quality local file. A accepts reaction files, unlisted
  YouTube links and Patreon/HLS streams as later milestones land.
- Both audio tracks play simultaneously, with independent volume and mute controls.
- Preserve MPV's codec capabilities; validate AV1, HEVC, embedded audio/subtitle
  selection and external subtitles against actual sample media.
- Patreon playback must supply a configurable HTTP Referer, defaulting to
  `https://www.patreon.com`, and support required headers. YouTube needs URL
  resolution into streams MPV can consume. Do not log signed URLs or credentials.

## Synchronization

- Master play/pause, timeline seeking, relative jumps and speed changes affect both.
- Independent per-player transport stays available for initial/manual alignment.
- Preserve `B time = A time + offset`; the offset may be negative.
- Numeric offset and ±0.10-second nudges, with ongoing drift correction.
- Retain master ±10-second buttons from the original concept and Left/Right ±5-second
  keyboard jumps. Space toggles playback, comma/period nudge offset, F11 toggles fullscreen.
- Fine details such as out-of-range targets, buffering, independent edits while sync
  is locked, and drift thresholds must be resolved and tested at the sync milestone.
  Old 150/750 ms thresholds are historical ideas, not fixed product requirements.
- Automatic alignment should eventually match source audio against the muffled,
  room-recorded source audio mixed with reactor voices. Manual sync is acceptable
  for the MVP. Auto-sync must be evaluated against real recordings.

## Composition

- Reaction is a crop/mask/repositionable background layer. Typically trim wasted
  space from its top and move the useful reactor area upward, leaving black below.
- Source is a foreground overlay, always horizontally centered and pinned to the
  top or bottom canvas edge via a toggle.
- Corner resizing preserves source aspect ratio, horizontal centering and the
  selected edge. Arbitrary dragging is superseded by these constraints.
- Enlarge the source until it covers unimportant legs/table space while preserving
  reactors' faces and upper bodies around it.
- Support useful compositions in both 16:9 and 4:3 canvases.

## Retained conveniences (later)

- Separate audio/subtitle track selection for each player; external SRT/ASS/SSA/VTT.
- Drag-and-drop files; file and URL inputs. Folder-first-video loading is optional.
- Remember sources, offset, volumes, mute states, speed and layout between sessions.
- Do not import old settings implicitly into the window foundation.

## “What Did They Say?” (later; no implementation now)

Button and keyboard shortcut (key assignment deferred):

1. Capture reaction trigger time, shared speed, both volumes and both mute states.
2. Rewind both videos ten seconds, preserving their offset.
3. Set both to 1.0×, mute source, set reaction volume to 100% and unmute reaction.
4. Play until A reaches the original trigger time.
5. Restore the actual captured speed, volumes and mute states exactly; continue forward.

Re-triggering, manual seeks during replay, paused activation, buffering and proximity
to the start/end require explicit behavior and tests at that milestone. They must not
silently overwrite the original saved state or leave the temporary mix active.

## Delivery gates

Each milestone needs a downloadable Windows build and evidence for its behavior.
Do not proceed past a failed or unconfirmed foundation.

| Milestone | Scope | Required verification |
| --- | --- | --- |
| 0 (confirmed) | Plain black Windows window | Packaged EXE launches, responds, resizes, closes, relaunches; Gonz confirms locally |
| 1 (confirmed) | One embedded MPV surface, local media | AV1/HEVC, audio and subtitles on Windows |
| 2 (confirmed) | Two independent MPV instances | Both videos visible and both audio tracks audible |
| 3 (confirmed) | Shared transport | Master play/pause and seeking affect both |
| 4 (current) | Offset and drift correction | Offset maintained through seeking; ±0.1s nudging |
| 5 | Shared speed, independent audio/subtitles | Speed changes preserve alignment; independent track/volume controls |
| 6 | Composition | Reaction crop/pan and centered top/bottom source resizing |
| 7 | Patreon/HLS and headers | Authorized real stream playback with correct Referer |
| 8 | YouTube resolution | Unlisted reaction URL playback |
| 9 | Audio-assisted automatic alignment | Confidence and accuracy against real reaction recordings |
| 10 | Reaction conveniences | What Did They Say restores exact state at trigger time; persistence and remaining conveniences |

## Foundation decisions

Use a small WinForms shell with a bundled .NET runtime. WinForms is only the native
window/UI layer; it does not choose the media backend. No native player dependency
is loaded at milestone 0. Introduce libmpv in a separate component at milestone 1,
keeping startup, playback, synchronization, composition and persistence separate.
Retain the old commit history for reference; do not resurrect the monolithic form.

## Progress — 2026-09-09

Milestone 0 is confirmed on Gonz's Windows PC: opens, resizes, closes and reopens.
Milestone 1 adds a single embedded libmpv player and local video test controls.
The README now describes C#/.NET 8 WinForms + direct libmpv C API integration.
Gonz confirmed milestone 1 with HEVC and AV1 anime: video/audio/subtitles, seeking,
volume and replacement loading all work. He requested named track lists instead
of cycling; milestone 2 includes per-player named audio/subtitle dropdown menus.
Milestone 2 uses temporary side-by-side panes to verify two independent native
players and both audio streams. Do not implement shared transport (milestone 3)
until Gonz confirms this dual-player build on his PC.

## Milestone 3 progress

Gonz confirmed milestone 2 on his PC. Shared transport is now implemented with
a Reaction A timeline, ±10-second buttons, Space/arrow shared shortcuts and
Shift-modified independent shortcuts. Each shared seek moves the current pair
by an equal delta, clamped at either file's start/end. Mixed play/pause states
converge to both paused on master toggle. No persistent offset or drift loop yet.
Gonz's confirmation of milestone 3 gates milestone 4.

## Milestone 4 decisions and progress

Gonz confirmed shared transport and reported drift after repeated master seeks.
Lock current alignment captures B−A once; locked seeks compute B from A's target
and that fixed value. Offsets/nudges are session state, not saved settings yet.
Independent pause/seek and media replacement unlock; volume/track edits do not.
Re-locking explicitly captures the new alignment. Numeric edits and ±0.1 s nudges
enable lock. Reject offsets without a common playable interval.

Drift above 80 ms is corrected by seeking B only, no more than once per two seconds,
with settling delays after seeks and transport changes. Corrections wait during
seeking/buffering or mismatched pause states. Shared seeks clamp to the common
range; reaching its end pauses both. No speed modulation in this milestone.
Assess audible correction skips with real media before milestone 5.

## Requested keyboard layout (planned; milestone 4 still awaiting user test)

These requests describe future controls, not functionality in the milestone 4 EXE.
Implement speed controls with the speed milestone after milestone 4 is confirmed.

| Key | Requested behavior |
| --- | --- |
| A | Toggle both players between 1.0× and the previously active speed; repeated presses alternate (e.g. 1.5× ↔ 1.0×). |
| S | Decrease shared speed; preferred increment 0.25×. |
| D | Increase shared speed; preferred increment 0.25×. |
| F | Preferred additional fullscreen binding, alongside F11; user left F open to another use if needed. |
| G | Toggle both players between a configurable favorite speed and the speed active before entering it; suggested favorite 2.0× (e.g. 1.5× ↔ 2.0× or 1.0× ↔ 2.0×). |
| J | Shared seek backward 5 seconds. |
| K | Shared play/pause, same behavior as Space. |
| L | Shared seek forward 5 seconds. |
| Comma / period | Decrease/increase the sync offset by 0.1 seconds; retain offset nudging rather than frame stepping. |
| Shift + playback shortcut | Apply the applicable playback action only to the player under the mouse. |

Unmodified playback shortcuts remain master controls regardless of mouse location.
Hover is a preferred target selector for Shift-modified independent playback controls,
not a reason to turn ordinary shortcuts into per-player actions. Extend the same
targeting convention to volume shortcuts once their actual keys are chosen; the user
has not specified volume keys yet. Retain F1/F2 explicit selection as a useful fallback;
when neither pane is hovered, falling back to that selection is a proposed behavior,
not a finalized user requirement. The precise modifier/targeting UX can be settled later.

A and G must restore the actual previous speed rather than a hard-coded fallback.
Their toggle memories must not be overwritten by the temporary destination speed.
Define and test how intervening S/D changes, switching between A and G, already being
at the target speed, and independent speed adjustments affect toggle memory. Speed
bounds and interaction with locked sync also remain implementation decisions.
Do not invent final rules for these edge cases or silently change the stored offset.

Keep normal text/numeric editing and menu navigation intact when a control has focus.
Fullscreen applies to the full composition/window; per-player fullscreen was not
requested. These preferences do not authorize advancing beyond milestone 4's test gate.
