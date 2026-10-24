# GitHub 开源与发行

## 公开目录

源码只包含根目录公开文件、`src/`、`tests/*.cs`、`scripts/`、`.github/` 以及本说明库中的文档与示例图片。精确白名单由 `scripts/Get-PublicFiles.ps1` 管理。

以下内容保留在本机，不提交或放入源码 ZIP：

- `.local/`：原始开发记录、REA 证据、独立构建和测试输出。
- `dist/`：当前与历史 EXE。
- `artifacts/`：本次生成的 ZIP 和校验文件，作为 Release 附件单独上传。
- `tests/output/`：早期本地验证记录。
- 密码库、加密备份、`.env`、私钥和本机编辑器状态。

不要通过资源管理器把整个工作目录上传到 GitHub。优先使用经过检查的 Git 文件或源码 ZIP。

## 发布前构建

```powershell
.\test.ps1
.\scripts\package.ps1
.\scripts\check-publication.ps1 -CheckPackages
```

输出：

- `artifacts/releases/LocalKey-0.2.0-windows.exe`：普通用户可以直接运行的单文件程序。
- `artifacts/releases/LocalKey-0.2.0-windows.zip`：便携程序、许可证和用户文档。
- `artifacts/releases/LocalKey-0.2.0-source.zip`：只含白名单文件的源码。
- `artifacts/releases/SHA256SUMS.txt`：EXE 与两个 ZIP 的 SHA-256。
- `artifacts/releases/RELEASE_NOTES.md`：按当前版本生成的 GitHub Release 正文。

便携包里的 `启动LocalKey.cmd` 指向包根目录的 `LocalKey.exe`；源码目录里的启动器指向 `dist/版本号/LocalKey.exe`。包内另有 EXE 的校验值。不要分发 `.local`、`dist` 中的任意旧二进制来代替本次打包结果。

## 创建 GitHub 仓库

1. 在自己的 GitHub 账号下创建空仓库，建议名称 `LocalKey`，选择公开；不要再自动生成另一套 README、许可证或 gitignore。
2. 确认 [LICENSE](../LICENSE) 为 MIT，版权行使用 `LocalKey contributors`。若需要署个人或组织名，发布前自行按实际权利情况调整。
3. 在 GitHub 仓库设置中启用 Issues、Actions 和私密漏洞报告。私密报告入口通常在 Security / Code security 相关设置中；具体名称随 GitHub 界面变化。
4. 在 About 填写简短描述，例如「Windows 本地账号密码填写工具，使用 DPAPI 加密存储」。可选 topics：`windows`、`winforms`、`password-manager`、`autofill`、`dpapi`。

仓库地址和维护者联系方式由实际发布者决定；源码没有填入猜测的账号、邮箱或链接。

## 推送源码

在项目根目录执行。若目录已经有 Git 仓库，不要再次初始化或重置历史。

```powershell
# 仅在尚未初始化时执行
git init -b main

.\scripts\check-publication.ps1 -CheckPackages
git status --short
git add .
git diff --cached --stat
git diff --cached --name-only
git commit -m "chore: prepare LocalKey open-source release"

# 粘贴自己新建仓库的 HTTPS 地址，不要填生产系统地址
$repositoryUrl = Read-Host 'GitHub repository HTTPS URL'
git remote add origin $repositoryUrl
git push -u origin main
```

检查暂存文件时，确认没有密码库、真实截图、`.local`、`dist`、`artifacts` 或本机输出。提交需要自己的 Git 作者身份；按自己的账号配置，不使用本项目虚构的姓名或邮箱。

如不使用 Git 命令，可先解压源码 ZIP 到一个空目录，再将解压后的公开文件上传到空仓库。不要上传便携 EXE ZIP 的内容作为源码。

## 自动发布可下载的 Release

不使用命令行推送标签时，也可以进入 **Actions → Publish Windows release → Run workflow**，选择 `main` 分支，输入与源码一致的版本 `0.2.0`。检查通过后，工作流会为所选提交创建标签和公开 Release。手动触发入口拒绝覆盖已有标签。

`.github/workflows/release.yml` 监听版本标签。提交并推送已审核的源码与工作流后，执行：

```powershell
git tag v0.2.0
git push origin v0.2.0
```

**推送这个标签会触发公开发布。** 工作流依次：

1. 检查标签与 `src/AssemblyInfo.cs` 版本完全一致；不一致直接失败。
2. 运行核心测试、生成附件并校验文件内容与哈希。
3. 通过独立发布 job 的 `contents: write` 权限，用 GitHub CLI 创建 Release，上传 EXE、便携 ZIP、源码 ZIP 和 `SHA256SUMS.txt`，并使用生成的发行说明作为正文。

只推送 `main` 分支不会自动发布；推送版本标签或点击上述 Run workflow 才会发布。已有同名 Release 时流程失败，不自动覆盖已有附件；需维护者核对后处理。默认 GitHub-hosted Runner 提供 GitHub CLI，组织禁用 Actions 或限制令牌权限时，需要按组织策略配置。

发布后检查 Releases 页面能下载这四个附件。普通用户可以下载 EXE 后直接运行，或下载便携 ZIP 后解压运行，无需自己构建；系统仍需 .NET Framework 4.8。

分支 CI 的 workflow artifacts 仅供检查，保留 7 天，不作为普通用户的长期下载地址。GitHub Release 附件用于公开下载，不要求把 EXE 提交进源码仓库或启用 Git LFS。

## 手动发布

如需完全手动发布，先在 Actions 暂停 `Publish Windows release` 工作流，避免新标签同时触发自动流程。然后在 Releases 页面，为审核过的提交创建 `v0.2.0` 发行记录，粘贴 `RELEASE_NOTES.md` 的正文，并上传上述 EXE、两个 ZIP 和 `SHA256SUMS.txt`。若改动附件内容，重新生成对应校验值。若使用自动工作流，不要同时手动创建同名 Release。

从 [CHANGELOG](../CHANGELOG.md) 补充实际版本变化。本地检查不代表远程工作流或公开发布已经完成；没有 GitHub 仓库地址时，本项目只能先准备本地附件和配置。

## 后续版本

更新 `src/AssemblyInfo.cs` 中的版本以及 `app.manifest`、源码启动器和文档中的版本示例。构建与发行脚本会从程序集文件版本读取前三段版本号。重新测试、打包和检查；已运行中的 EXE 不会被发行脚本覆盖，真实密码库也不属于发行文件。
