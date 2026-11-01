; Cleora installer (Inno Setup 6). Per-user: no admin prompt, installs to %LocalAppData%\Programs\Cleora.
; Built on CI by tools/ci/installer.ps1 from the staged portable folder: iscc /DVersion=x.y.z /DSource=<stage> /DOutput=<dir>

#ifndef Version
  #define Version "0.0.0"
#endif

[Setup]
AppId={{C416DE4F-940D-4DAF-ABF4-99D35BE06C50}
AppName=Cleora
AppVersion={#Version}
AppVerName=Cleora {#Version}
AppPublisher=thaipro.store
AppPublisherURL=https://thaipro.store/phan-mem/cleora
AppSupportURL=https://github.com/HoangThai18/cleora/issues
DefaultDirName={localappdata}\Programs\Cleora
DefaultGroupName=Cleora
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir={#Output}
OutputBaseFilename=cleora-{#Version}-windows-x64-setup
SetupIconFile={#Icon}
UninstallDisplayIcon={app}\Cleora.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
LicenseFile={#Source}\licenses\Cleora-LICENSE.txt

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng ngoài màn hình (Desktop)"; GroupDescription: "Biểu tượng:"

[Files]
Source: "{#Source}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Cleora"; Filename: "{app}\Cleora.exe"
Name: "{group}\Gỡ Cleora"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Cleora"; Filename: "{app}\Cleora.exe"; Tasks: desktopicon

[Run]
; shellexec: Cleora asks for administrator rights when the user has them, and only ShellExecute can show that prompt.
Filename: "{app}\Cleora.exe"; Description: "Mở Cleora"; Flags: nowait postinstall skipifsilent shellexec

; "Cập nhật ngay" runs this installer silently with /RELAUNCH=1: start the new Cleora once it is in place.
Filename: "{app}\Cleora.exe"; Flags: nowait shellexec; Check: IsRelaunch

[Code]
function IsRelaunch: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM Cleora.exe /F"; Flags: runhidden; RunOnceId: "StopCleora"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

