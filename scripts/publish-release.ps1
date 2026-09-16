param([Parameter(Mandatory)][string]$Commit)
$ErrorActionPreference = 'Stop'
$repo = 'DanteFlame/Full-Length-Player'
[xml]$project = Get-Content 'src/FullLengthPlayer/FullLengthPlayer.csproj'
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^0\.[0-9]+\.[0-9]+(-beta\.[1-9][0-9]*)?$') { throw 'A numbered stable or beta version is required.' }
$tag = 'v' + $version
$notes = "releases/$tag.md"
if (!(Test-Path $notes)) { throw 'Release notes missing.' }
# An existing published version is immutable to this script. Increment the version for changed code.
$existingJson = gh release view $tag --repo $repo --json tagName,isDraft,targetCommitish 2>$null
if ($LASTEXITCODE -eq 0) {
    $existing = $existingJson | ConvertFrom-Json
    if ($existing.targetCommitish -ne $Commit) { throw 'Version already belongs to another commit; increment the version.' }
    if (!$existing.isDraft) { Write-Host "Already published: $tag"; exit 0 }
    gh release upload $tag 'publish/FullLengthPlayer-win-x64.zip' --repo $repo --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Draft ZIP upload failed.' }
}
else {
    gh release create $tag 'publish/FullLengthPlayer-win-x64.zip' --repo $repo --draft --target $Commit --title "$tag — Full Length Player" --notes-file $notes
    if ($LASTEXITCODE -ne 0) { throw 'Release creation failed.' }
}
if ($version.Contains('-beta.')) { gh release edit $tag --repo $repo --draft=false --prerelease --latest=false }
else { gh release edit $tag --repo $repo --draft=false --prerelease=false --latest=true }
if ($LASTEXITCODE -ne 0) { throw 'Release publication failed.' }
Write-Host "Published: https://github.com/$repo/releases/tag/$tag"
