param([switch]$CheckPackages, [string]$PackageDirectory = 'artifacts\releases')
$ErrorActionPreference = 'Stop'
$root = (Split-Path -Parent $PSScriptRoot).TrimEnd('\', '/')
$files = @(& (Join-Path $PSScriptRoot 'Get-PublicFiles.ps1'))
$names = @($files | ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\', '/') })
$errors = New-Object 'System.Collections.Generic.List[string]'
$sensitive = '-----BEGIN (?:[A-Z ]+ )?PRIVATE KEY-----|github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|sk-proj-[A-Za-z0-9_-]{20,}'
foreach ($file in $files) {
    $relative = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
    if ($relative -match '(^|/)(\.local|dist|artifacts|\.git)(/|$)|\.lkey|\.(bak|pfx|p12|pem|key)$|(^|/)\.env') { $errors.Add('Private file: ' + $relative) }
    if ($file.Extension -eq '.png') { continue }
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    if ($content -match '(?i)[a-z]:[\\/]+(?:Users|workspace)[\\/]') { $errors.Add('Personal absolute path: ' + $relative) }
    if ($content -match $sensitive) { $errors.Add('Possible secret token: ' + $relative) }
    if ($file.Extension -ne '.md') { continue }
    foreach ($link in [regex]::Matches($content, '!?\[[^\]]*\]\(([^)]+)\)')) {
        $target = $link.Groups[1].Value.Trim('<', '>')
        if ($target -match '^(https?://|mailto:|#)') { continue }
        $target = [Uri]::UnescapeDataString(($target -split '#', 2)[0])
        if (-not $target) { continue }
        $absolute = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName $target))
        if (-not $absolute.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { $errors.Add('Link outside project: ' + $relative); continue }
        $linkedName = $absolute.Substring($root.Length + 1).Replace('\', '/')
        if (-not (Test-Path -LiteralPath $absolute) -or $names -notcontains $linkedName) { $errors.Add('Missing public link: ' + $relative + ' -> ' + $target) }
    }
}
if ((Test-Path -LiteralPath (Join-Path $root '.git')) -and (Get-Command git -ErrorAction SilentlyContinue)) {
    $candidates = @(git -c core.quotepath=false -C $root ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }
    foreach ($candidate in $candidates) { if ($names -notcontains $candidate) { $errors.Add('Unexpected Git candidate: ' + $candidate) } }
}
if ($CheckPackages) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $version = & (Join-Path $PSScriptRoot 'Get-Version.ps1')
    $directory = if ([System.IO.Path]::IsPathRooted($PackageDirectory)) { $PackageDirectory } else { Join-Path $root $PackageDirectory }
    $standalone = Join-Path $directory ("LocalKey-$version-windows.exe")
    $standaloneHash = (Get-FileHash -LiteralPath $standalone -Algorithm SHA256).Hash.ToLowerInvariant()
    if ((Get-Item -LiteralPath $standalone).VersionInfo.FileVersion -ne ($version + '.0')) { $errors.Add('Standalone EXE version mismatch.') }
    $licenseText = [System.IO.File]::ReadAllText((Join-Path $root 'LICENSE'), [System.Text.Encoding]::UTF8)
    $exeText = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($standalone))
    if (-not $exeText.Contains($licenseText)) { $errors.Add('Standalone EXE does not include the full MIT license.') }
    if (-not (Test-Path -LiteralPath (Join-Path $directory 'RELEASE_NOTES.md'))) { $errors.Add('Release notes missing.') }
    foreach ($kind in @('source', 'windows')) {
        $path = Join-Path $directory ("LocalKey-$version-$kind.zip")
        $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
        try {
            $entries = @($zip.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName })
            foreach ($entry in $entries) {
                if ($entry -match '(^|/)(\.local|\.git|tests/output|artifacts)(/|$)|\.lkey|\.(bak|previous|pfx|pem|key)$|(^|/)\.env|(^|/)\.\.(/|$)|:|\\') { $errors.Add('Private/unsafe ZIP entry: ' + $entry) }
            }
            if ($kind -eq 'source') {
                foreach ($name in $names) { if ($entries -notcontains $name) { $errors.Add('Source ZIP missing: ' + $name) } }
                foreach ($entry in $entries) { if ($names -notcontains $entry) { $errors.Add('Unexpected source ZIP entry: ' + $entry) } }
            } else {
                foreach ($required in @('LocalKey.exe', 'LocalKey.exe.sha256', '启动LocalKey.cmd', 'LICENSE', '操作手册.md')) { if ($entries -notcontains $required) { $errors.Add('Binary ZIP missing: ' + $required) } }
                $exeEntry = $zip.GetEntry('LocalKey.exe')
                $hashEntry = $zip.GetEntry('LocalKey.exe.sha256')
                if ($exeEntry -and $hashEntry) {
                    $hasher = [System.Security.Cryptography.SHA256]::Create()
                    $exeStream = $exeEntry.Open()
                    try { $innerHash = [BitConverter]::ToString($hasher.ComputeHash($exeStream)).Replace('-', '').ToLowerInvariant() }
                    finally { $exeStream.Dispose(); $hasher.Dispose() }
                    $reader = New-Object System.IO.StreamReader($hashEntry.Open())
                    try { $innerChecksum = $reader.ReadToEnd().Trim() } finally { $reader.Dispose() }
                    if ($innerHash -ne $standaloneHash) { $errors.Add('ZIP EXE differs from standalone EXE.') }
                    if ($innerChecksum -ne ($innerHash + '  LocalKey.exe')) { $errors.Add('Internal EXE checksum mismatch.') }
                }
            }
        } finally { $zip.Dispose() }
    }
    $expectedAssets = @("LocalKey-$version-windows.exe", "LocalKey-$version-windows.zip", "LocalKey-$version-source.zip")
    $checksumLines = @(Get-Content -LiteralPath (Join-Path $directory 'SHA256SUMS.txt') -Encoding UTF8)
    if ($checksumLines.Count -ne $expectedAssets.Count) { $errors.Add('Checksum manifest must contain exactly three assets.') }
    $seenAssets = @()
    foreach ($line in $checksumLines) {
        if ($line -notmatch '^([a-fA-F0-9]{64})  ([^\\/]+\.(?:zip|exe))$') { $errors.Add('Invalid checksum line.'); continue }
        $expected = $Matches[1]; $asset = $Matches[2]
        if ($expectedAssets -notcontains $asset -or $seenAssets -contains $asset) { $errors.Add('Unexpected or repeated checksum asset: ' + $asset); continue }
        $seenAssets += $asset
        $path = Join-Path $directory $asset
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $expected) { $errors.Add('Checksum mismatch: ' + [System.IO.Path]::GetFileName($path)) }
    }
    foreach ($asset in $expectedAssets) { if ($seenAssets -notcontains $asset) { $errors.Add('Missing checksum: ' + $asset) } }
}
if ($errors.Count) { throw ($errors -join [Environment]::NewLine) }
Write-Output ("Publication checks passed for {0} public files." -f $names.Count)
if ($CheckPackages) { Write-Output 'Standalone EXE, source ZIP, binary ZIP and checksums passed.' }
