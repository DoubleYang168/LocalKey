# 第三方组件与素材

项目源码没有 NuGet、npm 或其他打包的第三方库。以下组件由运行环境提供，不包含在发行 ZIP 中：

| 组件 | 用途 | 授权归属 |
| --- | --- | --- |
| Windows / user32.dll | 快捷键、窗口、键鼠输入、剪贴板序列号 | Microsoft 的相应系统授权 |
| .NET Framework 4.8 | WinForms、序列化、DPAPI、UI Automation | Microsoft 的相应框架授权 |
| Windows 系统字体与 `SystemIcons.Shield` | 界面字体、托盘与窗口图标 | 相应系统和字体授权 |

源码引用运行环境中的组件，不复制或再分发其 DLL、字体文件或独立图标文件。Windows、Microsoft、GitHub 等名称属于各自权利人。

`docs/images/` 是本项目界面的截图，示例账号为虚构数据。未收录用户反馈中的真实系统截图。

GitHub Actions 使用 GitHub 提供的 checkout、upload-artifact 和 download-artifact actions，发布 job 使用 Runner 提供的 GitHub CLI；这些只用于构建与发布，不是应用运行依赖。REA 是开发过程中可选的检查工具，不包含在源码或便携包中。

项目自身代码和文档的许可见 [LICENSE](LICENSE)。后续新增依赖或素材时，应在此登记其来源、版本、授权及分发方式。

构建时会把完整 MIT 声明嵌入 EXE 的 `LocalKey.LICENSE` 托管资源，因此单文件下载也保留该声明。便携 ZIP 同时附带独立的 `LICENSE` 文件。
