#define AppName "DTCL_DPS"
#define AppVersion "1.3"
#define AppPublisher "ISquare Systems"
#define AppURL "https://github.com/isquaresystems/DTCL_DPS"
#define AppExeName "DTCL_DPS.exe"

[Setup]
AppId={{7AE233D7-8AB0-4B2E-8CCB-157257E32195}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName=D:\S-WAVE SYSTEMS\{#AppName}
DisableDirPage=no
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=DTCL_DPS_Setup_v{#AppVersion}
SetupIconFile=..\DPS_DTCL\Resources\swave_icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\swave_icon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main executable
Source: "..\DPS_DTCL\bin\Release\DTCL_DPS.exe"; DestDir: "{app}"; Flags: ignoreversion

; All DLL dependencies
Source: "..\DPS_DTCL\bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion

; Configuration files
Source: "..\DPS_DTCL\bin\Release\*.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\DPS_DTCL\bin\Release\Default.txt"; DestDir: "{app}"; Flags: ignoreversion

; Image files and icons
Source: "..\DPS_DTCL\bin\Release\MirageJet.jpg"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\DPS_DTCL\bin\Release\swave_icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\DPS_DTCL\bin\Release\swave_icon.png"; DestDir: "{app}"; Flags: ignoreversion

; Data folders (D1, D2, D3, PopUpMessage)
Source: "..\DPS_DTCL\bin\Release\D1\*"; DestDir: "{app}\D1"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\DPS_DTCL\bin\Release\D2\*"; DestDir: "{app}\D2"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\DPS_DTCL\bin\Release\D3\*"; DestDir: "{app}\D3"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\DPS_DTCL\bin\Release\PopUpMessage\*"; DestDir: "{app}\PopUpMessage"; Flags: ignoreversion recursesubdirs createallsubdirs

; TestConsole CLI tool
Source: "..\TestConsole\bin\Release\TestConsole.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\TestConsole\bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion

; Documentation
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\swave_icon.ico"
Name: "{group}\DTCL TestConsole"; Filename: "{app}\TestConsole.exe"; IconFilename: "{app}\swave_icon.ico"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\swave_icon.ico"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\swave_icon.ico"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNetInstalled: Boolean;
var
  Release: Cardinal;
begin
  // Check if .NET Framework 4.8 is installed (Release >= 528040)
  Result := RegQueryDWordValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
    'Release', Release) and (Release >= 528040);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsDotNetInstalled then
  begin
    MsgBox('.NET Framework 4.8 is required but not installed.' + #13#10 +
           'Please install .NET Framework 4.8 and try again.' + #13#10#13#10 +
           'Download from: https://dotnet.microsoft.com/download/dotnet-framework/net48',
           mbCriticalError, MB_OK);
    Result := False;
  end;
end;
