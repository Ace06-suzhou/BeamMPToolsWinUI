# BeamMPTools
CN ver :[READMEZHCN.md](https://github.com/Ace06-suzhou/BeamMPToolsWinUI/blob/master/READMEZHCN.md)

**A lightweight Windows toolkit for BeamNG.drive and BeamMP.**

BeamMPTools is a Windows desktop utility designed to simplify BeamNG.drive and BeamMP environment detection, launcher installation, updates, and game launching.

Built with **C# and WinUI 3**, BeamMPTools uses the native Windows Fluent Design System and supports the system's light and dark appearance modes.

## Features

- **Automatic Environment Detection**
  - Detect installed BeamNG.drive installations.
  - Locate existing BeamMP Launcher installations.
  - Identify relevant game and launcher directories.

- **BeamMP Launcher Installation and Updates**
  - Install or update the BeamMP Launcher.
  - Choose between the default installation directory and a custom location.
  - Automatically use the detected installation directory when appropriate.
  - Download and validate launcher files before installation.

- **Steam Integration**
  - Launch BeamNG.drive through Steam.
  - Launch BeamMP with Steam integration.
  - Allow Steam to track BeamNG.drive playtime.

- **Path Management**
  - Automatically detect installation paths.
  - Manually specify a BeamMP installation directory when automatic detection fails.

- **Logging**
  - Display application activities and operation results.
  - Provide error information for troubleshooting.

- **Native Windows Interface**
  - Built with WinUI 3.
  - Uses Fluent Design controls.
  - Follows Windows light and dark mode settings.

## Screenshots

Screenshots of BeamMPTools will be added here.

<!-- Add application screenshots below.
Example:
![Main Window](docs/images/main-window.png)
![Installation Dialog](docs/images/installation-dialog.png)
-->

## Requirements

- Windows 10 / Windows 11 (depending on the minimum version supported by the application build).
- Steam installed for Steam-based game launching and playtime tracking.
- BeamNG.drive installed through Steam for the intended Steam integration.
- Internet connection for downloading or updating BeamMP Launcher.

## Usage

### 1. Environment Detection

Launch BeamMPTools.

The application will scan the system for existing BeamNG.drive and BeamMP installations.

If an installation cannot be detected automatically, you can manually specify the BeamMP installation directory.

### 2. Install or Update BeamMP

Select **Install / Update BeamMP**.

You will be prompted to choose an installation location:

- **Default Location** — Uses the detected installation directory when available, or the default BeamMP Launcher directory for a new installation.
- **Custom Location** — Opens the Windows folder picker so you can select your preferred installation directory.

Review the selected directory and confirm the operation.

### 3. Launch BeamNG.drive

Select **Launch BeamNG** to start the game through Steam.

Steam integration allows your BeamNG.drive playtime to be tracked normally.

### 4. Launch BeamMP

Select **Launch BeamMP** to initialize the BeamMP Launcher and start BeamNG.drive through Steam.

This allows Steam to track your BeamNG.drive playtime while using BeamMP.

## Building from Source

### Development Environment

- Visual Studio with WinUI 3 development support.
- .NET SDK matching the project's target framework.
- Windows SDK required by the project.
- Windows App SDK.

### Build Steps

1. Clone or download this repository.
2. Open `BeamMPTools.sln` in Visual Studio.
3. Restore NuGet packages.
4. Select the `BeamMPTools.App` project as the startup project.
5. Select the appropriate platform configuration, such as `x64`.
6. Build and run the solution.

## Project Structure

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

- **BeamMPTools.App** — WinUI 3 application, user interface, and MVVM presentation logic.
- **BeamMPTools.Core** — Core functionality, including environment detection, installation, launching, and logging.

## Troubleshooting

If you encounter problems:

1. Check whether BeamNG.drive and Steam are installed correctly.
2. Verify the BeamMP Launcher installation directory.
3. Try manually specifying the BeamMP installation directory if automatic detection fails.
4. Check the application log for relevant error messages.
5. Report reproducible issues with the corresponding error messages.

## Contributing

Bug reports, feature suggestions, and code contributions are welcome.

Please provide clear reproduction steps when reporting bugs.

## License

See the `LICENSE` file for licensing terms.

## Disclaimer

BeamMPTools is an independent community-developed utility.

It is not an official product of BeamMP, BeamNG GmbH, or Valve Corporation.

BeamNG.drive, BeamMP, and Steam are trademarks or product names of their respective owners.
