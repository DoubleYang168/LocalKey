# LocalKey {{VERSION}}

Windows 本地账号密码填写工具，采用 MIT 协议。

## 下载哪个文件

| 文件 | 用途 |
| --- | --- |
| `LocalKey-{{VERSION}}-windows.exe` | 单文件程序，下载后直接运行，无需编译或安装 |
| `LocalKey-{{VERSION}}-windows.zip` | 便携包，包含同一 EXE、许可证和操作手册，推荐普通用户下载 |
| `LocalKey-{{VERSION}}-source.zip` | 源码包，供开发者构建与修改 |
| `SHA256SUMS.txt` | 上述三个文件的 SHA-256 校验值 |

## 运行

Windows 10 / 11，需 .NET Framework 4.8。便携包解压后打开 `LocalKey.exe` 或 `启动LocalKey.cmd`。升级前先从托盘退出旧实例。同一 Windows 用户下会使用原来的本地密码库。

Ctrl+Alt+Space 唤起，选中记录后填写。密码框无法识别时，使用「定点填密码（3秒）」或主动复制粘贴。程序不会自动提交表单。

当前程序未做代码签名。校验值用于检查文件完整性，不等于发布者身份验证或安全审计。功能变化见此版本标签中的 `CHANGELOG.md`。

## 数据保护

无独立主密码，使用 Windows DPAPI CurrentUser 加密。能操作当前已登录 Windows 会话的人也能打开程序。密码库依赖 Windows 用户密钥，复制文件不能单独完成跨电脑或重装迁移。

项目未经过独立安全审计。剪贴板清理为尽力执行；定点模式选到普通文本框时可能明文显示密码。完整说明见仓库 README 和安全政策。
