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

Button and **H** keyboard shortcut (future implementation):

1. Capture reaction trigger time, shared speed, both volumes and both mute states.
2. Rewind both videos ten seconds, preserving their offset.
3. Set both to 1.0×, mute source, set reaction volume to 100% and unmute reaction.
4. Play until A reaches the original trigger time.
5. Restore the actual captured speed, volumes and mute states exactly for both players; continue forward.

Maintain the stored sync offset and lock throughout the replay. H is reserved now;
do not activate it until the reaction-conveniences milestone.

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
| 4 (confirmed, seek/resume refinement in 5) | Offset and drift correction | Offset maintained through seeking; ±0.1s nudging |
| 5 (confirmed) | Shared speed, independent audio/subtitles | Speed changes preserve alignment; independent track/volume controls |
| 6 (confirmed) | Composition | Reaction crop/pan and centered top/bottom source resizing |
| 7 (confirmed) | Patreon/HLS and headers | Authorized real stream playback with correct Referer |
| 8 (current) | YouTube resolution | Unlisted reaction URL playback |
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

## Milestone 5 decisions and user feedback

Gonz confirmed milestone 4 by matching intro songs across episodes. Locked offset
works, but repeated skips can cause asynchronous resumes; a small master timeline
seek often restores near-perfect audible alignment. M5 therefore coordinates shared
seeks/corrections by holding both paused until both decoders reach their targets,
then resuming according to preserved intent. Queued skips accumulate from requested
targets; pause during seek overrides resume. Timeouts leave both paused for retry.
Independent transport cancels a pending shared resume and unlocks. Native pause
mismatches while locked pause the pair rather than suspending correction forever.

Implement A/S/D/G and J/K/L plus Shift-hover independent targeting in M5. Shared rates
0.25–4×, step 0.25×, pitch correction on. Favorite defaults 2× and is user-configurable
and saved. One active speed toggle per target: same key restores prior speed; S/D or
direct selection clears it; switching A/G starts anew from current speed; already
at the destination is a no-op without an active toggle. Main and each pane have
separate toggle histories; rate changes outside a target clear its obsolete history.
F/F11 fullscreen remains planned for composition work. H remains reserved for the
exact replay/mute/volume/speed restoration behavior above.

M4 is accepted with the noted refinement. M5 requires Gonz's Windows playback test
before advancing to composition. No claim of sample-perfect synchronization.


## Milestone 6 decisions (supersede earlier independent-speed notes)

Gonz confirmed milestone 5: speed shortcuts work and coordinated seeking greatly
reduces drift. Speed is ALWAYS shared, including Shift+A/S/D/G. Remove individual
speed controls/history. Shift only makes pause/seek independent; hover selects the
visible foreground B before the reaction background, with F1/F2 fallback.

Implement reaction top/bottom percentage clipping, zoom and X/Y pan; centered source
with top/bottom anchoring, aspect-preserving corner resizing and numeric size.
Offer 16:9 and 4:3 canvases. F/F11 fullscreen hides controls, Esc restores the prior
window. Retain native surface handles and sync state throughout layout changes.
Layout persists within the session only. H remains reserved. User playback and
composition confirmation gates milestone 7.


## Milestone 7 decisions and milestone 6 confirmation

Gonz confirmed all composition controls, fixed aspect previews, fullscreen and sync.
Artistic ASS subtitle rendering scales correctly through MPV. His Windows display
is currently a 4:3 iPad Pro via Moonlight (no physical monitor). Preserve fixed canvas
aspect ratios rather than a free-form window-dependent composition. Add 16:10 to
16:9 and 4:3. The preview should represent the eventual fullscreen composition.

M7 adds direct HTTP/HTTPS media/HLS URL loading on either player, an editable
Patreon Referer preset (default https://www.patreon.com for A) and additional header
fields. Reset per-load network options on replacements including local files; never
persist or log URLs/headers. Keep failures recoverable and generic. Direct media URLs
are required; Patreon page extraction/login and YouTube resolution are not M7.
On-demand seekable streams with known durations are required for shared timelines.
Validate headers on playlists and segments with a local server; a currently valid
user-authorized Patreon stream remains the real-service acceptance test.

After the functionality roadmap, explicitly schedule a UI polish stage: attractive,
compact controls; retain setup preview and controls disappearing in fullscreen.
Functional completion alone is not the end of this project.


## Milestone 8 — confirmed M7 and revised offset precision

Gonz confirmed actual Patreon stream loading, seeking, sync with local video and
shared speed. The 16:10 canvas also filled a MacBook Air screen via Moonlight correctly.

Superseding all earlier 0.1-second requirements: quantize every stored offset to the
nearest 0.05 s, including capture and typed values. Capture applies the rounded
alignment immediately. Display the stored value with two decimal places. Buttons,
comma/period and numeric arrows step ±0.05 s. Halfway ties round away from zero;
negative values follow the same symmetric rule. Measured drift remains distinct.

M8 resolves public/unlisted individual YouTube links with bundled pinned yt-dlp and
Deno. Handle separately served video/audio via MPV. Do not import browser cookies,
personal configs or playlists. Strip playlist/timestamp tracking from recognized
links; begin at zero for alignment. Resolve asynchronously while current playback
continues; support cancellation, replacement and shutdown without stale loads.
Never log extractor output or signed URLs. Dependency updates are explicit builds.
Live and sign-in-required videos remain outside this milestone's acceptance scope.
Local extraction/native split-stream tests are required; a real public/unlisted
YouTube link on Gonz's PC is the final gate before automatic alignment work.
