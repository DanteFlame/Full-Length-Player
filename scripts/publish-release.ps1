param([Parameter(Mandatory)][string]$Commit)
$ErrorActionPreference = 'Stop'
$repo = 'DanteFlame/Full-Length-Player'
[xml]$project = Get-Content 'src/FullLengthPlayer/FullLengthPlayer.csproj'
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^0\.[0-9]+\.[0-9]+(-beta\.[1-9][0-9]*)?$') { throw 'A numbered stable or beta version is required.' }
# Stable builds are published from main; feature branches publish only betas.
if (($env:GITHUB_REF -eq 'refs/heads/main' -and $version.Contains('-beta.')) -or
    ($env:GITHUB_REF -like 'refs/heads/feature/*' -and !$version.Contains('-beta.'))) {
    Write-Host 'Version is not intended for publication from this branch.'; exit 0
}
$tag = 'v' + $version
$notes = "releases/$tag.md"
if (!(Test-Path $notes)) { throw 'Release notes missing.' }
# An existing published version is immutable to this script. Increment the version for changed code.
$existingJson = gh release view $tag --repo $repo --json tagName,isDraft,targetCommitish 2>$null
if ($LASTEXITCODE -eq 0) {
    $existing = $existingJson | ConvertFrom-Json
    if ($existing.targetCommitish -ne $Commit) { throw 'Version already belongs to another commit; increment the version.' }
    if (!$existing.isDraft) { Write-Host "Already published: $tag"; exit 0 }
}
else {
    gh release create $tag --repo $repo --draft --target $Commit --title "$tag — Full Length Player" --notes-file $notes
    if ($LASTEXITCODE -ne 0) { throw 'Release creation failed.' }
}
# Resolve the exact tag through the CLI's draft-aware lookup, not the release list.
$draftJson = gh release view $tag --repo $repo --json databaseId,tagName,isDraft,targetCommitish
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect exact draft release.' }
$release = $draftJson | ConvertFrom-Json
$releaseId = $release.databaseId
if (!$release.isDraft -or $release.tagName -ne $tag -or $release.targetCommitish -ne $Commit -or
    [string]$releaseId -notmatch '^[1-9][0-9]*$') { throw 'Draft identity or target mismatch.' }
$name = 'FullLengthPlayer-win-x64.zip'
$file = "publish/$name"
$size = (Get-Item $file).Length
$hash = 'sha256:' + (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
$uploaded = $false
for ($attempt = 1; $attempt -le 3; $attempt++) {
    $assets = gh api "repos/$repo/releases/$releaseId/assets" | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect draft assets.' }
    $asset = @($assets | Where-Object name -eq $name)
    if ($asset.Count -gt 1) { throw 'Ambiguous release asset.' }
    if ($asset.Count -eq 1) {
        if ($asset[0].state -eq 'uploaded') {
            if ($asset[0].size -ne $size -or $asset[0].digest -ne $hash) { throw 'Existing asset differs from this package; refusing replacement.' }
            $uploaded = $true; break
        }
        if ($asset[0].state -ne 'starter') { throw 'Unexpected asset state.' }
        gh api --method DELETE "repos/$repo/releases/assets/$($asset[0].id)"
        if ($LASTEXITCODE -ne 0) { throw 'Cannot clear incomplete draft upload.' }
    }
    Write-Host "Upload attempt $attempt of 3; $size bytes."
    curl.exe --http1.1 --connect-timeout 20 --max-time 180 --silent --show-error --fail-with-body --request POST --header "Authorization: Bearer $env:GH_TOKEN" --header 'Content-Type: application/zip' --header 'Accept: application/vnd.github+json' --data-binary "@$file" --output upload-response.json --write-out 'HTTP %{http_code}; sent %{size_upload} bytes in %{time_total}s\n' "https://uploads.github.com/repos/$repo/releases/$releaseId/assets?name=$name"
    if ($LASTEXITCODE -eq 0) {
        $asset = Get-Content upload-response.json -Raw | ConvertFrom-Json
        if ($asset.state -ne 'uploaded' -or $asset.size -ne $size -or $asset.digest -ne $hash) { throw 'Uploaded asset verification failed.' }
        $uploaded = $true; break
    }
}
if (!$uploaded) { throw 'Upload failed after three bounded attempts; draft retained.' }
if ($version.Contains('-beta.')) { gh release edit $tag --repo $repo --draft=false --prerelease --latest=false }
else { gh release edit $tag --repo $repo --draft=false --prerelease=false --latest=true }
if ($LASTEXITCODE -ne 0) { throw 'Release publication failed.' }
Write-Host "Published: https://github.com/$repo/releases/tag/$tag"
