# LocalKey

A local credential helper for Windows. Open it with a global shortcut, search your entries, and fill a browser or desktop input field.

[简体中文](README.md) · [User guide, Chinese](操作手册.md) · [Build](docs/BUILDING.md) · [Security](SECURITY.md) · [MIT](LICENSE)

**Downloads:** [Standalone EXE](https://github.com/DoubleYang168/LocalKey/releases/download/v0.2.0/LocalKey-0.2.0-windows.exe) · [Windows portable ZIP](https://github.com/DoubleYang168/LocalKey/releases/download/v0.2.0/LocalKey-0.2.0-windows.zip) · [All releases](https://github.com/DoubleYang168/LocalKey/releases)

![LocalKey with fictional demo entries](docs/images/main-window.png)

## Features

- Store and search account names, usernames, passwords, sites/apps, and notes.
- Open with **Ctrl+Alt+Space**. Fill a username, a password, or a username followed by Tab and a password.
- Use a three-second point-and-fill countdown for editable fields without a password accessibility flag. A fresh click is required; Esc cancels.
- Explicitly copy a username or password. After 20 seconds, try to clear the clipboard only if its sequence still belongs to LocalKey.
- Run in the system tray. Clear cached entries after about five minutes of inactivity or when Windows locks.
- Encrypt the vault with Windows DPAPI `CurrentUser`. No separate master password.

Direct typing does not use the clipboard or submit the form. The application has no network client, telemetry, account registration, or cloud sync.

## Security model

**An unlocked Windows session is the access boundary.** Anyone who can use that session can open LocalKey. It does not protect against malicious programs running as the same Windows user.

DPAPI relies on the Windows user's keys. Moving the vault to another user/computer, reinstalling Windows, or losing those keys may make it unreadable. Copying the encrypted files alone is not a portable migration backup. Cross-device export is not implemented.

This is an early version without an independent security audit. See [privacy and data protection](docs/PRIVACY.md) before storing credentials.

## Install

Requires a Windows 10/11 desktop and .NET Framework 4.8. The build script requires 64-bit Windows. The executable is AnyCPU; x64 Windows is the primary validation environment.

Download from the repository's **Releases** page:

| File | Purpose |
| --- | --- |
| `LocalKey-0.2.0-windows.exe` | Standalone executable; no build or installer required |
| `LocalKey-0.2.0-windows.zip` | Recommended portable package with the same executable, license, and documentation |
| `LocalKey-0.2.0-source.zip` | Source archive for developers |
| `SHA256SUMS.txt` | SHA-256 checksums for those three assets |

Verify the checksum before running the EXE, or extract the portable archive and run `LocalKey.exe`. .NET Framework 4.8 must still be installed. Releases are currently unsigned; a checksum verifies file integrity, not publisher identity or security.

Otherwise, build from a source checkout in PowerShell:

```powershell
.\build.ps1
.\启动LocalKey.cmd
```

The current output is `dist/0.2.0/LocalKey.exe`. No .NET SDK, Visual Studio, or NuGet packages are required. The project uses the C# compiler included with the Windows .NET Framework. Follow your organization's PowerShell policy if scripts are restricted; a direct compiler command is documented in [BUILDING](docs/BUILDING.md).

Closing the window keeps the tray process running. Use the tray menu to exit before upgrading. LocalKey does not install a service, enable startup, or modify browser settings.

## Usage

1. Create an entry in LocalKey.
2. Click the target field. Select existing text first if you want to replace it.
3. Press **Ctrl+Alt+Space**, select the correct entry, and check the target window.
4. Choose a fill action, then review and submit the form yourself.

If the password field is not recognized, choose the point-and-fill button, then click the actual password input within the countdown. This mode only types into the explicitly selected editable field and does not send Tab. Choosing a plain text field may expose the password as visible text.

Manual copy/paste is also available. Clipboard cleanup is best effort and cannot prevent third-party clipboard tools from reading the content.

| Shortcut | Scope | Action |
| --- | --- | --- |
| Ctrl+Alt+Space | Global while running | Open for the current field; hide when the main window is focused |
| Enter / Ctrl+Enter | Main window with a fill target | Password / username then Tab then password |
| Alt+P | Main window, entry selected | Start point-and-fill |
| Ctrl+Shift+C / Ctrl+Shift+U | Main window, entry selected | Copy password / username |
| Up / Down | Search box | Change selection |
| Ctrl+L | Main window | Clear cached entries and fill target |
| Esc | Main window / entry editor / countdown | Hide / cancel edit / cancel fill |

The full user guide and UI are currently in Chinese.

## Storage and limitations

The vault is `%LOCALAPPDATA%\LocalKey\vault.lkey`; its previous-save backup is `vault.lkey.bak` in the same directory. All entry fields are encrypted. The backup may retain deleted entries. Moving or deleting the executable does not move or delete this data.

Never attach vaults, backups, or real account screenshots to public issues or pull requests.

Filling depends on Windows UI Automation. Custom controls, elevated windows, secure desktops, and some browser fields may not work. Focus or key changes stop input, possibly after partial text was entered. Sites are search labels, not automatic domain matching rules.

## Development

```powershell
.\test.ps1
.\test.ps1 -Desktop
.\scripts\package.ps1
.\scripts\check-publication.ps1
```

Desktop tests open controlled windows, move the pointer, and send input; run them only in an available interactive session. Branch/PR CI runs core tests and packaging. Pushing a version-matched tag such as `v0.2.0` creates a public GitHub Release with the executable, portable archive, source archive, and checksums. User downloads use Release assets rather than expiring CI artifacts.

Test drivers are currently compiled into the executable and are invoked only through explicit command-line options.

See [CONTRIBUTING](CONTRIBUTING.md), [architecture](docs/ARCHITECTURE.md), [testing](docs/TESTING.md), [publishing](docs/PUBLISHING.md), and [CHANGELOG](CHANGELOG.md). Additional development documentation is currently in Chinese.

REA was used for optional static artifact inspection. It is not a dependency or a security certification. Local investigation reports and historical binaries are excluded from the source distribution.

## License

[MIT License](LICENSE). Windows, .NET Framework, and system fonts/icons retain their respective licensing terms; see [third-party notices](THIRD_PARTY_NOTICES.md).
