# MicrosoftPinyinCleaner

**微软拼音清理工具**是一个面向 Windows 10/11 x64 的轻量 WPF 程序，用于在 Windows 更新重新加入微软拼音后，从**当前用户**的输入法列表中安全移除它。

程序不会删除中文语言包、系统文件或系统级输入法注册信息，也不会修改搜狗拼音、微信输入法等其他输入法。默认以普通用户权限运行，不申请管理员权限。

## 主要功能

- 检测当前用户输入法列表中是否存在微软拼音。
- 仅过滤微软拼音的精确 TIP 标识：
  `0804:{81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E}{FA550B04-5AD7-411F-A5AC-CA038EC515D7}`。
- 删除前确认至少保留一个其他中文输入法和至少一个输入法；不满足条件时拒绝操作。
- 通过 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 开启或关闭当前用户登录自动清理。
- 自动清理使用带引号的完整 EXE 路径和 `--silent-clean` 参数，支持路径中的空格与中文。
- 静默模式等待约 10 秒后执行，不显示窗口或消息框；最近一次简短日志写入 `%LOCALAPPDATA%\MicrosoftPinyinCleaner\logs\latest.log`。
- 打开 Windows“语言和区域”设置，方便安装或管理备用输入法。

## 环境要求

- Windows 10 或 Windows 11 x64
- 源码构建需要 .NET 8 SDK 或更高版本的兼容 SDK
- 系统需提供 Windows PowerShell 及 `Get-WinUserLanguageList`、`Set-WinUserLanguageList`

## 构建、测试与发布

在仓库根目录执行：

```powershell
# Debug 构建
dotnet build -c Debug

# Release 构建
dotnet build -c Release

# 运行两个单元测试
dotnet test -c Release --no-build

# x64 自包含单文件发布
dotnet publish .\MicrosoftPinyinCleaner\MicrosoftPinyinCleaner.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

默认发布目录：

```text
MicrosoftPinyinCleaner\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\
```

发布后可直接运行 `MicrosoftPinyinCleaner.exe`，无需另装 .NET 运行时。

## 使用说明

1. 建议先安装并确认搜狗拼音、微信输入法或其他中文输入法可以正常使用。
2. 打开程序，点击“立即检测”刷新状态。
3. 检测到微软拼音后，点击“立即删除微软拼音”。若它是唯一中文输入法，程序会拒绝删除。
4. 需要每次登录检查时，点击“开启登录自动清理”；程序移动到新路径后需重新点击此按钮来更新启动项。
5. 点击“关闭登录自动清理”只会移除本程序的当前用户启动项，不会删除程序。

## 人工测试步骤

以下操作会读取或更改真实的当前用户 Windows 配置，因此需由用户在目标机器上手动验证：

1. 当前存在微软拼音时启动程序，点击“立即检测”，确认显示“已安装”。
2. 确认另一个中文输入法可用后，点击“立即删除微软拼音”，再检查系统输入法切换列表。
3. 在测试账户中仅保留微软拼音作为中文输入法，确认程序拒绝删除并给出安全提示。
4. 点击“开启登录自动清理”，用注册表编辑器检查当前用户 Run 项；再点击“关闭登录自动清理”，确认仅对应值被删除。
5. 将发布目录复制到包含中文和空格的路径运行，重复开启自动清理并确认启动项命令完整、带引号。
6. 在命令行执行 `MicrosoftPinyinCleaner.exe --silent-clean`，确认约 10 秒内不显示主窗口，随后进程退出并更新 `latest.log`。
7. 开启自动清理后注销并重新登录，确认微软拼音存在且有其他中文输入法时会被自动移除。

## 项目结构

```text
MicrosoftPinyinCleaner.sln
├─ MicrosoftPinyinCleaner/
│  ├─ App.xaml(.cs)                 程序入口与静默模式
│  ├─ MainWindow.xaml(.cs)          主窗口与异步交互
│  ├─ Assets/pinyin.ico             Windows EXE 与任务栏图标
│  ├─ Models/                       状态与操作结果模型
│  └─ Services/
│     ├─ InputMethodService.cs      PowerShell 输入法读取和安全删除
│     ├─ InputMethodPolicy.cs       TIP 精确识别、过滤和安全分析
│     ├─ PowerShellRunner.cs        隐藏进程、输出捕获与超时
│     ├─ StartupService.cs          HKCU 启动项管理
│     ├─ StartupCommandBuilder.cs   Windows 命令行路径转义
│     ├─ SettingsLauncher.cs        语言设置启动器
│     └─ AppLogger.cs               最近一次静默操作日志
└─ MicrosoftPinyinCleaner.Tests/
   ├─ InputMethodPolicyTests.cs
   └─ StartupCommandBuilderTests.cs
```

窗口图标使用仓库根目录的 `pinyin.png`，发布时由项目内的多尺寸 `Assets/pinyin.ico` 嵌入 EXE。

## 安全设计说明

输入法变更脚本在同一次 PowerShell 进程内完成读取、精确匹配、安全计数、过滤与设置。只有在微软拼音存在，并且移除后仍有其他中文输入法和至少一个输入法时，才调用 `Set-WinUserLanguageList`。任何读取失败、超时、PowerShell 不可用或安全条件不满足的情况都不会主动写入语言列表。

程序不联网，不含自动更新、账户、遥测、托盘常驻、多语言或复杂设置页面。
