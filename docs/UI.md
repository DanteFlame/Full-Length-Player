# Compact interface — milestone 15

The playback engine is unchanged from stable 0.14.0. This pass rearranges its controls.

- **Session**: New, Save, Open, Resume last session, and icon appearance.
- **Setup**: show or hide the side panel to give the preview more room.
- **Media**: Reaction A and Source B file/URL loading, independent transport, timeline, volume, audio/subtitle menus and external subtitles. Extra items overflow if the window is narrow.
- **Layout**: fixed canvas ratio, source anchor/size and reaction crop/zoom/pan. The preview still represents the fullscreen composition.
- **Sync**: capture/unlock alignment, enter an offset, nudge by 0.05 seconds, analyze audio and view drift.
- **Bottom transport**: shared playback, five-second skips, speed menu, commentary replay and master timeline.
- **Speed menu**: rates plus the existing step/toggle/favorite settings actions.

Keyboard shortcuts, fullscreen taps/holds, Shift-wheel volume, transient feedback and session files are unchanged. Setup keeps its selected tab and visibility when returning from fullscreen. Very narrow windows may require the panel's scrollbars or toolbar overflow menus; hiding Setup gives the video its full window width.

## Test checklist

1. Restore a saved session and compare composition, audio balance and alignment.
2. Open local and remote media from Media; check track menus and independent seeking.
3. Adjust every Layout field, resize a source corner and change canvas aspect.
4. Capture/nudge alignment and open the audio-analysis dialog.
5. Check fullscreen gestures, timeline and feedback, then return to Setup.

## Teal and orange polish

Reaction A uses teal, Source B uses orange. Their volume percentages are visible in Media and the matching colour appears in fullscreen volume feedback. Play/pause buttons reflect the current state; hovering controls shows shortcuts.

Timelines show a timestamp while hovering or dragging. On the locked shared timeline, a thin orange band marks where both videos overlap. The tooltip identifies the shared section and either video's solo material. These are time labels, not thumbnail previews.

Layout now groups canvas/source, reaction crop and reaction framing. Each Reset affects only that group. Cropping resets to zero; framing resets zoom to 100% and pan to zero. Canvas/source resets to the nearest display aspect, bottom anchor and 70% source size. Offset nudges are grouped together.

App-owned URL, audio-analysis, favorite-speed and appearance dialogs use the same dark styling. Session open/save and media file pickers continue to follow Windows styling.

## Eight-position source anchoring

Choose a corner or edge centre in Layout's 3×3 picker; orange marks the selected position.
Top positions place the reaction at the bottom, and bottom positions place it at the top.
Left/right edge centres retain the reaction's previous vertical anchor, without moving it sideways.
Resize using a free corner; a fully pinned corner has no resize handle. Sessions save both
source position and reaction anchoring. Old top/bottom sessions still load. New anchor
sessions require v0.15.0-beta.4 or later.

Milestone 15 is user-approved and released as v0.15.0.
