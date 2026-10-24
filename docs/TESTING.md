# 测试与验证范围

## 核心检查

```powershell
.\test.ps1
```

脚本在独立构建目录编译，运行 `--self-test`，失败或超时返回非零。输出位于 `.local/test-results/版本号/self-test.txt`。默认单项超时 120 秒，可通过 `-TimeoutSeconds` 调整。

20 项检查覆盖 DPAPI 随机化与 Unicode 往返、备份恢复、篡改拒绝、文件结构校验、保存失败保留原文件、编辑 / 删除持久化、缓存释放、控制字符拒绝、随机密码生成及剪贴板清理逻辑 / 格式。

测试使用独立临时密码库和替身剪贴板，不读取真实密码库，不向真实剪贴板复制数据。它不等于跨用户 DPAPI 测试、真实剪贴板软件兼容性检查或安全审计。

## 交互桌面检查

```powershell
.\test.ps1 -Desktop
```

这组测试会打开受控窗口和临时 Edge、移动鼠标、发送快捷键和文本。请在可用的交互桌面中手动执行，不要同时操作工作窗口，也不要在锁屏、服务会话或普通 GitHub 托管 Runner 上运行。

| 参数 | 范围 |
| --- | --- |
| `--integration-test` | 普通 Windows 表单、快捷键和焦点变化拒绝 |
| `--compat-test` | 无密码标记的 Windows 输入框，自动拒绝和单框兼容输入 |
| `--point-test` | 定点倒计时、新点击要求、单框输入与验证码保留 |
| `--browser-test` | Edge 中的标准 HTML 表单 |
| `--browser-compat-test` | Edge 中没有密码标记的可编辑框 |

这些入口都需要第二个参数指定报告文件，例如从已构建的 EXE 单独执行。还存在内部 `--test-target`、`--test-target-compat` 供父测试调用，以及 `--render-preview` 生成虚构账号截图；正常使用不需要这些参数。

Edge 测试使用临时配置、本地 HTML、app 窗口和 `--force-renderer-accessibility`；窗口用随机标记匹配，避免浏览器启动器进程切换造成误选。它不访问生产登录页面，也不能证明所有普通标签页、自绘控件或网站均可用。

快捷键被其他实例占用时，对应检查可能记为跳过；这不能算全局快捷键已通过。切换前台或按键会触发输入保护，可能留下部分文字，必须如实记录中断。超时只停止测试父进程；若遗留受控子窗口，请手动关闭。

## 已知本地验证

0.2.0 开发期间，核心检查、无标记输入框兼容、完整定点倒计时以及 Edge 模拟兼容表单有过通过记录。也出现过前台切到工作软件导致整组检查中止，以及浏览器启动时未暴露目标字段的情况；没有将被中断的整组命令记为全部通过。

界面截图使用虚构账号。曾使用 REA 检查编译产物的元数据与 Windows API 声明；这些静态观察不是执行验证或安全保证。本机路径相关的原始报告没有放入公开源码。

## CI 与发布检查

分支 / PR 的 GitHub Actions 运行核心测试、发行打包和公开文件检查，不运行交互桌面测试。标签 Release 工作流先验证标签等于源码版本，构建检查通过后，再由单独具备 `contents: write` 的 job 创建 GitHub Release 并上传下载附件。

本地验证 workflow 文件与脚本，不代表远程 GitHub Actions 或 Release 发布已执行成功。

本次独立源码解压验证使用 PowerShell 7。旧版 Windows PowerShell 5.1 被本机组策略阻止，没有把该环境记为通过，也没有更改组策略。

```powershell
.\scripts\check-publication.ps1
.\scripts\package.ps1
.\scripts\check-publication.ps1 -CheckPackages
```

公开检查包含文件白名单、个人绝对路径、高置信度令牌模式、Markdown 文件链接、Git 候选文件、ZIP 内容、单文件 EXE 版本和校验值，检查 EXE 内保留完整 MIT 声明，并确认独立 EXE 与便携包内 EXE 一致。它是辅助检查，不替代人工确认真实账号、内部地址、截图或未知类型密钥。

文档变更通常只需检查内容和链接；业务代码变更再按影响范围运行测试。
