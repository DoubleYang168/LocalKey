param([switch]$Desktop, [string]$BuildDirectory = '.local\test-build', [int]$TimeoutSeconds = 120)
$ErrorActionPreference = 'Stop'
if ($TimeoutSeconds -lt 1) { throw 'TimeoutSeconds must be positive.' }
$taskVersion = & (Join-Path $PSScriptRoot 'scripts\Get-Version.ps1')
$taskBuild = if ([System.IO.Path]::IsPathRooted($BuildDirectory)) { $BuildDirectory } else { Join-Path $PSScriptRoot $BuildDirectory }
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $taskBuild | Write-Host
$taskExe = Join-Path $taskBuild 'LocalKey.exe'
$taskReportDirectory = Join-Path $PSScriptRoot ('.local\test-results\' + $taskVersion)
New-Item -ItemType Directory -Path $taskReportDirectory -Force | Out-Null
$taskModes = @('self-test')
if ($Desktop) { $taskModes += @('integration-test','compat-test','point-test','browser-test','browser-compat-test') }
foreach ($taskMode in $taskModes) {
    $taskReport = Join-Path $taskReportDirectory ($taskMode + '.txt')
    $taskProcess = Start-Process -FilePath $taskExe -ArgumentList @(('--' + $taskMode), ('"' + $taskReport + '"')) -WindowStyle Hidden -PassThru
    try {
        if (-not $taskProcess.WaitForExit($TimeoutSeconds * 1000)) {
            # Only this script's explicitly started test process is stopped.
            $taskProcess.Kill()
            throw ($taskMode + ' timed out. Desktop tests may leave controlled child windows; close them manually.')
        }
        if (-not (Test-Path -LiteralPath $taskReport)) { throw ($taskMode + ' produced no report.') }
        Get-Content -LiteralPath $taskReport -Encoding UTF8
        if ($taskProcess.ExitCode -ne 0) { throw ($taskMode + ' failed.') }
    } finally { $taskProcess.Dispose() }
}
