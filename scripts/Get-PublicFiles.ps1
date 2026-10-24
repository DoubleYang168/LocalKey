param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\', '/')
$required = @(
    '.editorconfig', '.gitattributes', '.gitignore',
    'LICENSE', 'README.md', 'README.en.md', 'CHANGELOG.md', 'CONTRIBUTING.md', 'SECURITY.md', 'THIRD_PARTY_NOTICES.md',
    '操作手册.md', '启动LocalKey.cmd', 'app.manifest', 'build.ps1', 'test.ps1',
    'docs\BUILDING.md', 'docs\TESTING.md', 'docs\ARCHITECTURE.md', 'docs\PRIVACY.md', 'docs\PUBLISHING.md', 'docs\RELEASE_TEMPLATE.md',
    'docs\images\main-window.png', 'docs\images\account-editor.png'
)
$result = @()
foreach ($relative in $required) {
    $item = Get-Item -LiteralPath (Join-Path $ProjectRoot $relative)
    if ($item.PSIsContainer) { throw ('Expected a file: ' + $relative) }
    $result += $item
}
foreach ($folder in @('src', 'tests', 'scripts', '.github')) {
    $extensions = if ($folder -eq '.github') { @('.yml', '.md') } elseif ($folder -eq 'scripts') { @('.ps1') } else { @('.cs') }
    $result += @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot $folder) -Recurse -File | Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() })
}
$result | Sort-Object -Property FullName -Unique
