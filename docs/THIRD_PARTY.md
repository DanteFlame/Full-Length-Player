# Bundled playback engine

libmpv is from https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20260901
Asset: mpv-dev-x86_64-20260901-git-02a595ddc1.7z
SHA-256: 680feac97f2da3721e331d6b10d2d0e3e02f1113be068fac06aff7f833a165d4

MPV source: https://github.com/mpv-player/mpv/tree/02a595ddc1
Build recipes and dependency definitions: https://github.com/shinchiro/mpv-winbuild-cmake/tree/cd1edc1
MPV licensing: https://github.com/mpv-player/mpv/blob/02a595ddc1/Copyright
MPV documentation: https://mpv.io/manual/master/
Client API: https://github.com/mpv-player/mpv/blob/02a595ddc1/include/mpv/client.h

The original development distribution, including any upstream notices/headers,
is retained in mpv-distribution beside the EXE. The copied libmpv DLL is identical.
MPV and its dependencies retain their respective upstream licenses; bundling them
does not relicense them as application code. Consult upstream Copyright and the
pinned build recipes for the GPL/LGPL and other component terms and source locations.

CI fixture generation uses FFmpeg build b1f564bda from the same release, verified
by SHA-256. FFmpeg is a test tool and is not included in the application artifact.

Vulkan loader: official LunarG runtime components 1.4.357.0, downloaded from
https://sdk.lunarg.com/sdk/download/1.4.357.0/windows/vulkan-runtime-components.zip
Archive SHA-256: A14672EFED15AAFC7F5A16572D35CD3A3416EADF670AEEE3CDF50EE32D5FBF83
The archive hash and the x64 loader's Authenticode signature is verified before bundling. Its original
distribution/notices are retained in vulkan-distribution. Source and licensing:
https://github.com/KhronosGroup/Vulkan-Loader/tree/v1.4.357
The loader is an imported dependency of libmpv, even when using D3D11 output.


## YouTube resolver (milestone 8)

The `youtube` folder bundles yt-dlp 2026.08.19 and Deno 2.9.6 for Windows x64.
Both are downloaded from their official GitHub releases with SHA-256 checks pinned
in `scripts/fetch-youtube.ps1`. No runtime auto-update is performed.

- yt-dlp: https://github.com/yt-dlp/yt-dlp/releases/tag/2026.08.19
  Includes its LICENSE and THIRD_PARTY_LICENSES.txt. The official executable also
  bundles the EJS challenge solver and third-party dependencies covered by those notices.
- Deno: https://github.com/denoland/deno/releases/tag/v2.9.6
  Includes upstream LICENSE.md; use the upstream source tag for source and notices.

MPV remains the decoder/player; yt-dlp only resolves the selected video's stream
metadata. Deno supplies yt-dlp's supported JavaScript runtime.
