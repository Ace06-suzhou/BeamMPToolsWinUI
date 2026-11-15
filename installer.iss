
#define MyAppName "BeamMPTools"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "BeamMPTools Project"
#define MyAppExeName "BeamMPTools.App.exe"
#define PublishDir AddBackslash(SourcePath) + "src\BeamMPTools.App\bin\Release\Publish"

[Setup]
AppId={{6F93C8E2-7B41-4F0D-9D35-62B7F81A9C40}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

SetupIconFile={#SourcePath}\src\BeamMPTools.App\Assets\logo.ico

MinVersion=10.0.19041
PrivilegesRequired=admin

OutputDir={#SourcePath}\artifacts\installer
OutputBaseFilename=BeamMPTools-Setup-{#MyAppVersion}

SetupLogging=yes
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项："; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 BeamMPTools"; Flags: nowait postinstall skipifsilent
