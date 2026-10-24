# 安全政策 / Security policy

## 支持范围

当前接收最新 **0.2.x** 源码版本的安全报告。旧版本的问题请先确认是否仍影响当前源码。未承诺固定响应期限。

LocalKey 没有独立主密码，Windows 当前用户身份是访问边界。它不是面向不可信共享会话、同用户恶意软件或管理员攻击的隔离方案。完整模型见 [数据与隐私](docs/PRIVACY.md)。

## 私密报告

请优先使用当前 GitHub 仓库的 **Security → Report a vulnerability**，前提是维护者已启用私密漏洞报告。

如果没有该入口，请只开一个不含细节的 Issue 请求私密联系渠道，例如「希望私密提交安全报告，请提供接收方式」。不要在公开 Issue/PR 中提交漏洞细节、真实凭据、密码库、Cookie 或令牌。仓库启用私密入口的方法见 [发布说明](docs/PUBLISHING.md)。

报告建议包含受影响版本、预期与实际行为、影响范围及使用虚构账号的最小复现。若涉及网页，可提供不联网的简化 HTML；不要提供生产系统账号或内部系统地址。

## 当前边界

- DPAPI `CurrentUser` 加密依赖 Windows 用户密钥，不提供额外主密码或可移植恢复密钥。
- 已登录会话中的人和同用户程序仍可能读取数据；进程中的 .NET 字符串无法保证彻底擦除。
- 自动填写有目标与焦点检查，但检查和输入并非事务；输入中止前可能已有部分内容进入目标框。
- 定点模式不要求密码属性标记。选错普通输入框时，密码可能明文显示。
- 剪贴板清理是尽力执行，无法防止第三方软件读取；历史与云同步排除由 Windows 处理。
- 备份可能保留已删除记录，当前不提供跨电脑迁移或安全擦除功能。
- 项目尚未进行独立安全审计；CI、测试或 REA 静态检查都不是安全认证。

## English summary

Use this repository's **Security → Report a vulnerability** if private reporting is enabled. Otherwise, open a detail-free issue asking for a private channel. Do not disclose credentials, vaults, internal URLs, or exploit details publicly.

The current Windows user is the trust boundary. There is no master password, portable recovery key, independent security audit, or protection against malicious programs running as that user. Clipboard cleanup and input checks have the limitations described above.
