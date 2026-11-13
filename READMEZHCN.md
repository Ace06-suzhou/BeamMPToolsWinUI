# BeamMPTools

**一个用于 BeamNG.drive 和 BeamMP 的轻量级 Windows 工具包。**

BeamMPTools 是一个 Windows 桌面实用工具，旨在简化 BeamNG.drive 和 BeamMP 的环境检测、启动器安装、更新以及游戏启动。

BeamMPTools 使用 **C# 和 WinUI 3** 构建，采用原生 Windows Fluent Design 设计系统，并支持系统的浅色和深色外观模式。

## 功能

- **自动环境检测**
  - 检测已安装的 BeamNG.drive。
  - 定位现有的 BeamMP Launcher 安装。
  - 识别相关的游戏和启动器目录。

- **BeamMP Launcher 安装和更新**
  - 安装或更新 BeamMP Launcher。
  - 在默认安装目录和自定义位置之间进行选择。
  - 在适当情况下自动使用检测到的安装目录。
  - 在安装前下载并验证启动器文件。

- **Steam 集成**
  - 通过 Steam 启动 BeamNG.drive。
  - 通过 Steam 集成启动 BeamMP。
  - 允许 Steam 跟踪 BeamNG.drive 的游戏时长。

- **路径管理**
  - 自动检测安装路径。
  - 当自动检测失败时，手动指定 BeamMP 安装目录。

- **日志记录**
  - 显示应用程序活动和操作结果。
  - 提供用于故障排除的错误信息。

- **原生 Windows 界面**
  - 使用 WinUI 3 构建。
  - 使用 Fluent Design 控件。
  - 遵循 Windows 浅色和深色模式设置。

## 截图

BeamMPTools 的截图将在此处添加。

<!-- 在下方添加应用程序截图。
示例：
![主窗口](docs/images/main-window.png)
![安装对话框](docs/images/installation-dialog.png)
-->

## 要求

- Windows 10 / Windows 11（取决于应用程序构建所支持的最低版本）。
- 已安装 Steam，用于基于 Steam 的游戏启动和游戏时长跟踪。
- 通过 Steam 安装 BeamNG.drive，以实现预期的 Steam 集成。
- 需要互联网连接，以下载或更新 BeamMP Launcher。

## 使用方法

### 1. 环境检测

启动 BeamMPTools。

应用程序将扫描系统，以查找现有的 BeamNG.drive 和 BeamMP 安装。

如果无法自动检测到安装，您可以手动指定 BeamMP 安装目录。

### 2. 安装或更新 BeamMP

选择 **Install / Update BeamMP**。

系统将提示您选择安装位置：

- **默认位置** — 在可用时使用检测到的安装目录，或对于全新安装使用默认的 BeamMP Launcher 目录。
- **自定义位置** — 打开 Windows 文件夹选择器，以便您选择首选的安装目录。

检查所选目录并确认操作。

### 3. 启动 BeamNG.drive

选择 **Launch BeamNG**，通过 Steam 启动游戏。

Steam 集成可让您的 BeamNG.drive 游戏时长被正常跟踪。

### 4. 启动 BeamMP

选择 **Launch BeamMP**，初始化 BeamMP Launcher 并通过 Steam 启动 BeamNG.drive。

这允许 Steam 在您使用 BeamMP 时跟踪您的 BeamNG.drive 游戏时长。

## 从源代码构建

### 开发环境

- 支持 WinUI 3 开发的 Visual Studio。
- 与项目目标框架匹配的 .NET SDK。
- 项目所需的 Windows SDK。
- Windows App SDK。

### 构建步骤

1. 克隆或下载此仓库。
2. 在 Visual Studio 中打开 `BeamMPTools.sln`。
3. 还原 NuGet 包。
4. 选择 `BeamMPTools.App` 项目作为启动项目。
5. 选择适当的平台配置，例如 `x64`。
6. 生成并运行解决方案。

## 项目结构

```text
BeamMPTools/
├── src/
│   ├── BeamMPTools.App/
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   └── Assets/
│   │
│   └── BeamMPTools.Core/
│       ├── Detection/
│       ├── Installation/
│       ├── Launching/
│       ├── Logging/
│       ├── Models/
│       └── Services/
│
├── BeamMPTools.sln
└── README.md
```

- **BeamMPTools.App** — WinUI 3 应用程序、用户界面和 MVVM 表示逻辑。
- **BeamMPTools.Core** — 核心功能，包括环境检测、安装、启动和日志记录。

## 故障排除

如果您遇到问题：

1. 检查 BeamNG.drive 和 Steam 是否已正确安装。
2. 验证 BeamMP Launcher 安装目录。
3. 如果自动检测失败，请尝试手动指定 BeamMP 安装目录。
4. 检查应用程序日志中的相关错误消息。
5. 报告可复现的问题，并附上相应的错误消息。

## 贡献

欢迎提交错误报告、功能建议和代码贡献。

报告错误时，请提供清晰的复现步骤。

## 许可证

许可条款请参阅 `LICENSE` 文件。

## 免责声明

BeamMPTools 是一个独立的社区开发实用工具。

它并非 BeamMP、BeamNG GmbH 或 Valve Corporation 的官方产品。

BeamNG.drive、BeamMP 和 Steam 是其各自所有者的商标或产品名称。
