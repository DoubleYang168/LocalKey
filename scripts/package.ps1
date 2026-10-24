param([string]$OutputDirectory = 'artifacts\releases')
$ErrorActionPreference = 'Stop'
$projectRoot = (Split-Path -Parent $PSScriptRoot).TrimEnd('\', '/')
$version = & (Join-Path $PSScriptRoot 'Get-Version.ps1')
$publicFiles = @(& (Join-Path $PSScriptRoot 'Get-PublicFiles.ps1'))
$buildDirectory = Join-Path $projectRoot ('.local\release-build\' + $version)
& (Join-Path $projectRoot 'build.ps1') -OutputDirectory $buildDirectory | Write-Host
$exe = Join-Path $buildDirectory 'LocalKey.exe'
$output = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $projectRoot $OutputDirectory }
New-Item -ItemType Directory -Path $output -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Package([string]$Path, [object[]]$Entries) {
    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    $zip = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($entry in ($Entries | Sort-Object Name)) {
            if ($entry.Name.StartsWith('/') -or $entry.Name -match '(^|/)\.\.(/|$)|:|\\') { throw 'Unsafe package entry.' }
            if ($entry.Path) {
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.Path, $entry.Name, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            } else {
                $member = $zip.CreateEntry($entry.Name)
                $writer = New-Object System.IO.StreamWriter($member.Open(), (New-Object System.Text.UTF8Encoding($false)))
                try { $writer.Write($entry.Text) } finally { $writer.Dispose() }
            }
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
}

$sourceEntries = @($publicFiles | ForEach-Object {
    [pscustomobject]@{ Name = $_.FullName.Substring($projectRoot.Length + 1).Replace('\', '/'); Path = $_.FullName; Text = $null }
})
$binaryEntries = @($sourceEntries | Where-Object { $_.Name.EndsWith('.md') -or $_.Name.StartsWith('docs/images/') -or $_.Name -eq 'LICENSE' })
$exeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$binaryEntries += [pscustomobject]@{ Name = 'LocalKey.exe'; Path = $exe; Text = $null }
$binaryEntries += [pscustomobject]@{ Name = '启动LocalKey.cmd'; Path = $null; Text = "@echo off`r`nstart `"`" `"%~dp0LocalKey.exe`"`r`n" }
$binaryEntries += [pscustomobject]@{ Name = 'LocalKey.exe.sha256'; Path = $null; Text = "$exeHash  LocalKey.exe`n" }
$sourceZip = Join-Path $output ("LocalKey-$version-source.zip")
$binaryZip = Join-Path $output ("LocalKey-$version-windows.zip")
$standaloneExe = Join-Path $output ("LocalKey-$version-windows.exe")
Write-Package $sourceZip $sourceEntries
Write-Package $binaryZip $binaryEntries
Copy-Item -LiteralPath $exe -Destination $standaloneExe -Force
$notesTemplate = Get-Content -LiteralPath (Join-Path $projectRoot 'docs\RELEASE_TEMPLATE.md') -Raw -Encoding UTF8
[System.IO.File]::WriteAllText((Join-Path $output 'RELEASE_NOTES.md'), $notesTemplate.Replace('{{VERSION}}', $version), (New-Object System.Text.UTF8Encoding($false)))
$checksums = @($standaloneExe, $binaryZip, $sourceZip) | ForEach-Object {
    ((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()) + '  ' + [System.IO.Path]::GetFileName($_)
}
[System.IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), [string[]]$checksums, (New-Object System.Text.UTF8Encoding($false)))
Write-Output $standaloneExe
Write-Output $binaryZip
Write-Output $sourceZip
Write-Output (Join-Path $output 'SHA256SUMS.txt')
Write-Output (Join-Path $output 'RELEASE_NOTES.md')
