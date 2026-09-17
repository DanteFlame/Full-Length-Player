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
4. Capture/nudge alignment and open the audio analysis dialog from Sync.
5. Hide Setup, enter fullscreen, then return; repeat with each setup tab selected.
6. Check text and controls at your usual Windows scaling on 4:3 and 16:10 displays.

This is a first polish beta. Stable 0.14.0 remains the fallback while the new layout is tested.
