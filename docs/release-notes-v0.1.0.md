# WinTool v0.1.0

首个公开版本：一个本地优先的 Windows 工作台，把待办、账号密码和便签放在同一个轻量窗口里。

## 下载

| 文件 | 说明 |
| --- | --- |
| `WinTool.exe` | 自包含单文件，Windows 10/11 x64，双击即可运行 |
| `WinTool-0.1.0-win-x64.zip` | 便携压缩包，解压后运行 |

## 本版亮点

- 待办快速录入、完成、清理，并按月生成 Markdown 留底。
- 本地账号密码增删改查、显示/隐藏和复制。
- 便签编辑与本地保存。
- 深色玻璃界面、胶囊导航、无顶部留海。
- 关闭到悬浮球，点球或按 `Ctrl+Alt+W` 唤回；真正退出走托盘。
- 无账号、无云端、无遥测，离线可用。
- 原子写盘、目录不可写回退、单实例运行。

## 运行要求

- Windows 10 1809+ 或 Windows 11
- x64
- 不需要安装 .NET

建议放在有写入权限的独立目录。程序默认在 EXE 同级创建 `data\` 与 `待办留底\`；不可写时回退到 `%LOCALAPPDATA%\WinTool\data`。

## 重要说明

- **未数字签名**：SmartScreen 可能提示“未知发布者”。请先核对本页 SHA256，再选择运行。
- **密码为本地明文**：`data\vault.json` 是明文 JSON，仅通过文件 ACL 限制为本机当前用户与 SYSTEM 可读，不是加密保险库。不要保存高价值密钥，并自行备份。
- **关闭窗口不是退出**：点击 `×` 会隐藏到屏幕边缘悬浮球；退出请使用托盘图标菜单。

## SHA256

```text
WinTool.exe
58D9D8B6B7F6BB2B32FB47FE08A8A92DF2C03566C46EC86059CEBC5A31B98D7B

WinTool-0.1.0-win-x64.zip
D842317E810290DB2C2EB1D3E41C9A9C73ECE30397EAB46835B9298EF9101BF3
```

校验命令：

```powershell
Get-FileHash .\WinTool.exe -Algorithm SHA256
Get-FileHash .\WinTool-0.1.0-win-x64.zip -Algorithm SHA256
```

## 已知限制

- 暂无安装器、开机自启、自动更新和云同步。
- 暂无密码库加密或主密码。
- 仅提供 Windows x64 构建。
