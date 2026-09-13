param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path $Executable).Path
$results = Join-Path $PSScriptRoot '../test-results'
New-Item -ItemType Directory -Force $results | Out-Null
$results = (Resolve-Path $results).Path
$archive = Join-Path $env:TEMP 'flp-ffmpeg.7z'
Invoke-WebRequest 'https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260901/ffmpeg-x86_64-git-b1f564bda.7z' -OutFile $archive
if ((Get-FileHash $archive).Hash -ne 'f3e64c10b36d86c88d9cd08c5f36a453b9dba57a80ee38480aba94f1782a0fc2') { throw 'FFmpeg checksum mismatch.' }
& 7z x $archive "-o$results/ffmpeg" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'FFmpeg extraction failed.' }
$ffmpeg = (Get-ChildItem "$results/ffmpeg" -Filter ffmpeg.exe -Recurse | Select-Object -First 1).FullName
Copy-Item (Join-Path (Split-Path $Executable) 'vulkan-1.dll') (Split-Path $ffmpeg)
$media = Join-Path $results 'local video 日本語.mkv'
& $ffmpeg -y -f lavfi -i 'testsrc2=size=320x180:rate=24' -f lavfi -i 'sine=frequency=440:sample_rate=48000' -f lavfi -i 'sine=frequency=880:sample_rate=48000' -map 0:v -map 1:a -map 2:a -t 40 -c:v mpeg4 -c:a pcm_s16le -metadata:s:a:0 'title=English main' -metadata:s:a:0 'language=eng' -metadata:s:a:1 'title=Japanese alternate' -metadata:s:a:1 'language=jpn' $media
if ($LASTEXITCODE -ne 0) { throw "Fixture generation failed: $LASTEXITCODE" }
$second = Join-Path $results 'source video.mkv'
& $ffmpeg -y -f lavfi -i 'color=c=blue:size=320x180:rate=24' -f lavfi -i 'sine=frequency=660:sample_rate=48000' -t 40 -c:v mpeg4 -c:a pcm_s16le -metadata:s:a:0 'title=Source main' $second
if ($LASTEXITCODE -ne 0) { throw "Second fixture generation failed: $LASTEXITCODE" }
& $ffmpeg -y -i $media -map 0:v -c copy -an "$results/video-only.mkv"
if ($LASTEXITCODE -ne 0) { throw 'Video-only fixture failed.' }
& $ffmpeg -y -i $media -map 0:a:0 -c copy -vn "$results/audio-only.mka"
if ($LASTEXITCODE -ne 0) { throw 'Audio-only fixture failed.' }
New-Item -ItemType Directory -Force "$results/hls" | Out-Null
& $ffmpeg -y -i $media -map 0:v -map 0:a:0 -c:v mpeg2video -g 24 -c:a mp2 -f hls -hls_time 2 -hls_playlist_type vod -hls_segment_filename "$results/hls/seg%02d.ts" "$results/hls/index.m3u8"
if ($LASTEXITCODE -ne 0) { throw "HLS fixture generation failed: $LASTEXITCODE" }
@'
1
00:00:00,000 --> 00:00:11,000
Full-Length Player subtitle test
'@ | Set-Content ([IO.Path]::ChangeExtension($media, '.srt')) -Encoding utf8
$report = Join-Path $results 'playback.json'
$p = Start-Process $Executable -ArgumentList @('--verify-playback', "`"$media`"", "`"$report`"") -WorkingDirectory $env:TEMP -PassThru
try {
    if (!$p.WaitForExit(300000)) { throw 'Playback test timed out.' }
    if ($p.ExitCode -ne 0 -or !(Test-Path $report)) {
        if (Test-Path "$report.error.txt") { Get-Content "$report.error.txt" }
        throw "Playback test failed: $($p.ExitCode)"
    }
    Get-Content $report
} finally { if (!$p.HasExited) { $p.Kill() }; $p.Dispose() }
