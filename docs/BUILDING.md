# 构建说明

## 环境

- 64 位 Windows 10 / 11，安装 .NET Framework 4.8。
- 推荐 PowerShell 7，CI 也使用此版本。脚本保持 PowerShell 5.1 兼容语法，并使用 UTF-8 BOM 保存，但 5.1 尚未完成实测。
- 构建依赖 Windows 自带的 `Microsoft.NET\Framework64\v4.0.30319\csc.exe` 及框架程序集，不需要 .NET SDK、Visual Studio、NuGet 或 Node.js。

程序输出为 AnyCPU，主要验证环境为 x64 Windows。当前脚本不支持在 Linux、macOS 或 32 位 Windows 主机上构建。代码保持 C# 5 兼容。

开源整理时，本机组策略禁止启动 Windows PowerShell 5.1，因此没有绕过策略；独立源码验证使用 PowerShell 7。

## 普通构建

在源码根目录执行：

```powershell
.\build.ps1
```

版本来自 `src/AssemblyInfo.cs` 的 `AssemblyFileVersion`，当前输出为 `dist/0.2.0/LocalKey.exe`。正在运行的 EXE 可能不能覆盖，先从托盘退出，或选择另一个输出目录：

```powershell
.\build.ps1 -OutputDirectory '.local\my-build'
```

输出路径可包含空格。构建不复制 Windows 或 .NET Framework DLL，不修改真实密码库。

`src/Program.cs` 当前直接引用 `tests/` 中的测试驱动，因此 `tests/*.cs` 会一并编译；普通启动不会运行测试，显式测试参数参见 [测试说明](TESTING.md)。

## 直接调用编译器

如 PowerShell 脚本执行受到组织策略限制，可在交互 PowerShell 中逐行运行下面的编译命令；不需要更改机器执行策略：

```powershell
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
New-Item -ItemType Directory -Path 'dist\0.2.0' -Force | Out-Null
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Security.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll')
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/debug-','/utf8output','/out:dist\0.2.0\LocalKey.exe','/win32manifest:app.manifest')
$arguments += '/resource:LICENSE,LocalKey.LICENSE'
$arguments += $refs | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$sources = @(Get-ChildItem -LiteralPath 'src','tests' -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
```

更新版本后，调整直接命令中的输出目录；推荐日常使用 `build.ps1` 自动读取版本。

## 测试与发行

```powershell
.\test.ps1
.\scripts\package.ps1
.\scripts\check-publication.ps1 -CheckPackages
```

测试输出保存在 `.local/test-results/`，打包输出在 `artifacts/releases/`，都不提交到源码仓库。输出包括可直接运行的单文件 EXE、便携 ZIP、源码 ZIP、校验值和发行说明。发行脚本构建到独立目录，不覆盖正在使用的程序。

当前编译器和 ZIP 流程不承诺字节级可复现构建；不同构建的哈希可能不同。`SHA256SUMS.txt` 对应本次生成的发行文件，而不是所有人自行构建的通用哈希。
