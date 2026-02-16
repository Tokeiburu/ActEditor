; -- Act Editor --

[Setup]
AppName=Act Editor
AppVersion={#VERSION_NAME}
DefaultDirName={pf}\Act Editor
DefaultGroupName=Act Editor
UninstallDisplayIcon={app}\Act Editor.exe
Compression=lzma2
SolidCompression=yes
OutputDir={#OUTPUT_DIR}
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
Source: "ActEditor\bin\Release\Microsoft.CodeAnalysis.CSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\Microsoft.CodeAnalysis.CSharp.Workspaces.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\Microsoft.CodeAnalysis.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\Microsoft.CodeAnalysis.Workspaces.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\PaletteEditor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\System.Buffers.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\System.Collections.Immutable.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\System.Memory.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\System.Numerics.Vectors.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\System.Reflection.Metadata.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\System.Runtime.CompilerServices.Unsafe.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\System.Text.Encoding.CodePages.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\System.Threading.Channels.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\System.Threading.Tasks.Extensions.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\TokeiLibrary.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "ActEditor\bin\Release\Utilities.dll"; DestDir: "{app}"; Flags: ignoreversion
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
function IsDotNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then
  begin
    // 528040 = .NET Framework 4.8
    if Release >= 528040 then
      Result := True;
  end;
end;


function InitializeSetup(): Boolean;
var ErrorCode: Integer;
begin
  if not IsDotNet48Installed then
  begin
    MsgBox('.NET Framework 4.8 is required. The installer will now open the download page.', mbInformation, MB_OK);
    ShellExec('', 'https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;  // cancel setup
  end
  else
    Result := True;   // continue setup
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