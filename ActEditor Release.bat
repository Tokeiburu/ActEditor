REM --add the following to the top of your bat file--

set currentdir=%CD%
echo "dir: %currentdir%"


@echo off

:: BatchGotAdmin
:-------------------------------------
REM  --> Check for permissions
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"

REM --> If error flag set, we do not have admin.
if '%errorlevel%' NEQ '0' (
    echo Requesting administrative privileges...
    goto UACPrompt
) else ( goto gotAdmin )

:UACPrompt
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    set params = %*:"=""
    echo UAC.ShellExecute "cmd.exe", "/c %~s0 ""c:\test.bat""", "", "runas", 1 >> "%temp%\getadmin.vbs"
	
    "%temp%\getadmin.vbs"
    del "%temp%\getadmin.vbs"
    exit /B
	
	
	REM powershell Start-Process -FilePath "%0" -ArgumentList "%cd%" -verb runas >NUL 2>&1
    REM exit /b

:gotAdmin
    pushd "%CD%"
    CD /D "%~dp0"
:--------------------------------------
cd /d %~dp0

set /P version=Enter id: %=%
set SoftwareName=Act Editor
set OutputFolderName=ActEditor v%version%
mkdir "%OutputFolderName%"
set outputdir="%CD%\%OutputFolderName%"
set SolutionPath=C:\tktoolsuite\ActEditor
set Installer=%SoftwareName% Installer.exe
set Log=changelog.txt
set Executable="%SoftwareName%.exe"

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe"  "%SolutionPath%\ActEditor.iss" /DVERSION_NAME=%version% /DOUTPUT_DIR=%outputdir%

copy "%Installer%" "%OutputFolderName%\%Installer%"
copy "%SolutionPath%\%Log%" "%OutputFolderName%\%Log%"
cd "%OutputFolderName%"
"C:\Program Files\WinRAR\WinRAR.exe" a -afzip "%OutputFolderName%.zip" "%Installer%" "%Log%"

set /P Exit=Press any key to exit %=%