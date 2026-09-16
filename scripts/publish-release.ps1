param([Parameter(Mandatory)][string]$Commit)
$ErrorActionPreference = 'Stop'
$repo = 'DanteFlame/Full-Length-Player'
# Promote the exact already-tested milestone 13 download, without rebuilding it.
$baseline = gh api "repos/$repo/releases/389642511" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Cannot read tested baseline release.' }
if ($baseline.target_commitish -ne '693b5b6a6f6fc13d0664c734b57d366526e77e88') { throw 'Baseline commit changed.' }
$asset = @($baseline.assets | Where-Object { $_.name -eq 'FullLengthPlayer-win-x64.zip' -and $_.state -eq 'uploaded' -and $_.size -eq 250009378 })
if ($asset.Count -ne 1) { throw 'Tested baseline ZIP is missing or changed.' }
if ($baseline.draft) {
    $body = @{
        tag_name = 'v0.13.0'; target_commitish = $baseline.target_commitish
        name = 'v0.13.0 — Dual reaction player'
        body = Get-Content 'releases/v0.13.0.md' -Raw
        draft = $false; prerelease = $false; make_latest = 'true'
    }
    $body | ConvertTo-Json -Depth 4 | Set-Content 'publish/baseline-release.json' -Encoding utf8
    gh api --method PATCH "repos/$repo/releases/389642511" --input 'publish/baseline-release.json' --jq '.html_url'
    if ($LASTEXITCODE -ne 0) { throw 'Baseline publication failed.' }
}
elseif ($baseline.tag_name -ne 'v0.13.0') { throw 'Baseline was published under an unexpected version.' }

[xml]$project = Get-Content 'src/FullLengthPlayer/FullLengthPlayer.csproj'
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^0\.14\.0-beta\.[1-9][0-9]*$') { throw 'This milestone publishes explicitly numbered 0.14.0 betas only.' }
$tag = 'v' + $version
$notes = "releases/$tag.md"
if (!(Test-Path $notes)) { throw 'Release notes missing.' }
# An existing published version is immutable to this script. Increment the version for changed code.
$existingJson = gh release view $tag --repo $repo --json tagName,isDraft,targetCommitish 2>$null
if ($LASTEXITCODE -eq 0) {
    $existing = $existingJson | ConvertFrom-Json
    if ($existing.targetCommitish -ne $Commit) { throw 'Version already belongs to another commit; increment the beta number.' }
    if (!$existing.isDraft) { Write-Host "Already published: $tag"; exit 0 }
    gh release upload $tag 'publish/FullLengthPlayer-win-x64.zip' --repo $repo --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Draft ZIP upload failed.' }
}
else {
    gh release create $tag 'publish/FullLengthPlayer-win-x64.zip' --repo $repo --draft --target $Commit --title "$tag — Fullscreen controls and feedback" --notes-file $notes
    if ($LASTEXITCODE -ne 0) { throw 'Beta creation failed.' }
}
gh release edit $tag --repo $repo --draft=false --prerelease --latest=false
if ($LASTEXITCODE -ne 0) { throw 'Beta publication failed.' }
Write-Host "Published: https://github.com/$repo/releases/tag/$tag"
