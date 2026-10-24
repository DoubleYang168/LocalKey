param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskVersion = & (Join-Path $taskRoot 'scripts\Get-Version.ps1')
if (-not $OutputDirectory) { $OutputDirectory = 'dist\' + $taskVersion }
if (-not $env:WINDIR -or -not [Environment]::Is64BitOperatingSystem) { throw 'Build requires 64-bit Windows and .NET Framework 4.8.' }
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskFramework = Split-Path -Parent $taskCompiler
if (-not (Test-Path -LiteralPath $taskCompiler)) { throw 'The .NET Framework C# compiler was not found.' }
$taskOutput = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $taskRoot $OutputDirectory }
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | Sort-Object FullName | ForEach-Object { $_.FullName })
$taskSources += @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'tests') -Filter '*.cs' | Sort-Object FullName | ForEach-Object { $_.FullName })
$taskRefs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Security.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll')
$taskArgs = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/debug-','/utf8output',('/out:' + (Join-Path $taskOutput 'LocalKey.exe')),('/win32manifest:' + (Join-Path $taskRoot 'app.manifest')))
$taskArgs += '/resource:' + (Join-Path $taskRoot 'LICENSE') + ',LocalKey.LICENSE'
foreach ($taskRef in $taskRefs) {
    $taskReference = Join-Path $taskFramework $taskRef
    if (-not (Test-Path -LiteralPath $taskReference)) { throw ('Missing framework reference: ' + $taskRef) }
    $taskArgs += '/reference:' + $taskReference
}
& $taskCompiler @taskArgs @taskSources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed. If the output EXE is running, stop it or use -OutputDirectory.' }
Write-Output (Join-Path $taskOutput 'LocalKey.exe')
