; ClipPeek 安装包脚本（Inno Setup 6）
; 构建： ISCC.exe installer\clippeek.iss
; 产物： dist\ClipPeek-Setup-1.0.0.exe

#include "version.iss"

#define AppName "ClipPeek"
#define AppPublisher "shilohjun"
#define AppUrl "https://github.com/shilohjun/ClipPeek"
#define AppExeName "ClipPeek.exe"

[Setup]
AppId={{7F3C1B42-9D6A-4C58-9A0E-5B2E7D4C8A31}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=ClipPeek-Setup-{#AppVersion}
SetupIconFile=..\assets\clippeek.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
AppMutex=ClipPeek.SingleInstance
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[CustomMessages]
en.TaskAutostart=Start ClipPeek automatically when Windows starts
en.TaskGroup=Additional tasks:
en.LaunchApp=Launch ClipPeek
zh.TaskAutostart=开机时自动启动 ClipPeek
zh.TaskGroup=附加任务：
zh.LaunchApp=立即运行 ClipPeek

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:TaskGroup}"; Flags: unchecked
Name: "autostart"; Description: "{cm:TaskAutostart}"; GroupDescription: "{cm:TaskGroup}"

[Files]
Source: "..\dist\ClipPeek.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.en.md"; DestDir: "{app}"; DestName: "README.md"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 只清理安装目录里的程序文件；%APPDATA%\ClipPeek 下的用户数据一律保留
Type: files; Name: "{app}\README.md"
