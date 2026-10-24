$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$assemblyText = Get-Content -LiteralPath (Join-Path $projectRoot 'src\AssemblyInfo.cs') -Raw -Encoding UTF8
$versionMatch = [regex]::Match($assemblyText, 'AssemblyFileVersion\("(\d+\.\d+\.\d+)\.\d+"\)')
if (-not $versionMatch.Success) { throw 'Cannot read project version from AssemblyInfo.cs.' }
$versionMatch.Groups[1].Value
