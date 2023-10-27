; -- Act Editor --

[Setup]
AppName=Act Editor
AppVersion=1.2.7
DefaultDirName={pf}\Act Editor
DefaultGroupName=Act Editor
UninstallDisplayIcon={app}\Act Editor.exe
Compression=lzma2
SolidCompression=yes
OutputDir=C:\Users\Tokei\Desktop\Releases\Act Editor
OutputBaseFilename=Act Editor Installer
WizardImageFile=setupBackground.bmp
DisableProgramGroupPage=yes
ChangesAssociations=yes
DisableDirPage=no
DisableWelcomePage=no

[Files]
Source: "ActEditor\bin\Release\ActImaging.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\ColorPicker.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\Encryption.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\ErrorManager.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\Gif.Components.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\GRF.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\GrfToWpfBridge.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\ICSharpCode.AvalonEdit.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\PaletteEditor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\TokeiLibrary.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\Utilities.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\zlib.net.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "doc\*"; DestDir: "{app}\doc"; Flags: ignoreversion recursesubdirs

Source: "ActEditor\bin\Release\Act Editor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\Act Editor.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\Resources\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[UninstallDelete]
Type: files; Name: "{app}\crash.log"
Type: files; Name: "{app}\debug.log"
Type: files; Name: "{app}\app.ico"
Type: filesandordirs; Name: "{app}\tmp"
Type: filesandordirs; Name: "{app}\doc"
Type: files; Name: "{userappdata}\Act Editor\crash.log"
Type: files; Name: "{userappdata}\Act Editor\debug.log"
Type: filesandordirs; Name: "{userappdata}\Act Editor\tmp"
Type: filesandordirs; Name: "{userappdata}\Act Editor\Scripts"

[Icons]
Name: "{group}\Act Editor"; Filename: "{app}\Act Editor.exe"
Name: "{commondesktop}\Act Editor"; Filename: "{app}\Act Editor.exe"

[CustomMessages]
DotNetMissing=Act Editor requires .NET Framework 3.5 Client Profile or higher (SP1). Do you want to download it? Setup will now exit!

[Code]
function IsDotNetDetected(version: string; service: cardinal): boolean;
// Indicates whether the specified version and service pack of the .NET Framework is installed.
//
// version -- Specify one of these strings for the required .NET Framework version:
//    'v1.1.4322'     .NET Framework 1.1
//    'v2.0.50727'    .NET Framework 2.0
//    'v3.0'          .NET Framework 3.0
//    'v3.5'          .NET Framework 3.5
//    'v4\Client'     .NET Framework 4.0 Client Profile
//    'v4\Full'       .NET Framework 4.0 Full Installation
//    'v4.5'          .NET Framework 4.5
//
// service -- Specify any non-negative integer for the required service pack level:
//    0               No service packs required
//    1, 2, etc.      Service pack 1, 2, etc. required
var
    key: string;
    install, release, serviceCount: cardinal;
    check45, success: boolean;
begin
    // .NET 4.5 installs as update to .NET 4.0 Full
    if version = 'v4.5' then begin
        version := 'v4\Full';
        check45 := true;
    end else
        check45 := false;

    // installation key group for all .NET versions
    key := 'SOFTWARE\Microsoft\NET Framework Setup\NDP\' + version;

    // .NET 3.0 uses value InstallSuccess in subkey Setup
    if Pos('v3.0', version) = 1 then begin
        success := RegQueryDWordValue(HKLM, key + '\Setup', 'InstallSuccess', install);
    end else begin
        success := RegQueryDWordValue(HKLM, key, 'Install', install);
    end;

    // .NET 4.0/4.5 uses value Servicing instead of SP
    if Pos('v4', version) = 1 then begin
        success := success and RegQueryDWordValue(HKLM, key, 'Servicing', serviceCount);
    end else begin
        success := success and RegQueryDWordValue(HKLM, key, 'SP', serviceCount);
    end;

    // .NET 4.5 uses additional value Release
    if check45 then begin
        success := success and RegQueryDWordValue(HKLM, key, 'Release', release);
        success := success and (release >= 378389);
    end;

    result := success and (install = 1) and (serviceCount >= service);
end;


function InitializeSetup(): Boolean;
var ErrorCode: Integer;
begin
    if not (IsDotNetDetected('v4\Client', 0) or IsDotNetDetected('v4.5', 0) or IsDotNetDetected('v4\Full', 0) or IsDotNetDetected('v3.5', 0)) then 
    begin
      Result := False;
      if (MsgBox(ExpandConstant('{cm:dotnetmissing}'), mbConfirmation, MB_YESNO) = idYes) then
      begin
        ShellExec('open',
        'http://www.microsoft.com/en-ca/download/details.aspx?id=22',
        '','',SW_SHOWNORMAL,ewNoWait,ErrorCode);
      end;
    end 
    else
        result := true;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    usPostUninstall:
      begin
        if (FileExists(ExpandConstant('{app}\config.txt')) or FileExists(ExpandConstant('{userappdata}\Act Editor\config.txt'))) then
        begin
        if (MsgBox('Program settings have been found, would you like to remove them?', mbConfirmation, MB_YESNO) = idYes) then
          begin
            DeleteFile(ExpandConstant('{app}\config.txt'));
            DeleteFile(ExpandConstant('{userappdata}\Act Editor\config.txt'));
           end
        end
      end;
  end;
end;