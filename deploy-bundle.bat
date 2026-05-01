@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
set "CONFIGURATION=%~1"
set "OPTION_1=%~2"
set "OPTION_2=%~3"

if "%CONFIGURATION%"=="" set "CONFIGURATION=Release"

echo.
echo ============================================================
echo  Licorp Export+ build, package, and deploy
echo ============================================================
echo  Project      : %SCRIPT_DIR%
echo  Configuration: %CONFIGURATION%
echo  Artifact     : %SCRIPT_DIR%artifacts\release\LicorpExportPlus.bundle
echo  Revit deploy : C:\ProgramData\Autodesk\ApplicationPlugins\LicorpExportPlus.bundle
echo.

pushd "%SCRIPT_DIR%" || (
    echo ERROR: Cannot open project folder.
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: dotnet was not found in PATH.
    popd
    exit /b 1
)

where powershell >nul 2>nul
if errorlevel 1 (
    echo ERROR: powershell was not found in PATH.
    popd
    exit /b 1
)

if /I "%OPTION_1%"=="SkipDeploy" goto RUN_SCRIPT
if /I "%OPTION_2%"=="SkipDeploy" goto RUN_SCRIPT

:CHECK_REVIT
tasklist /FI "IMAGENAME eq Revit.exe" 2>nul | find /I "Revit.exe" >nul
if not errorlevel 1 (
    echo.
    echo Revit is currently running. Close Revit before deploy so the add-in DLL is not locked.
    echo.
    tasklist /FI "IMAGENAME eq Revit.exe"
    echo.
    choice /C RKC /N /M "Press R to recheck, K to close Revit, or C to cancel: "
    if errorlevel 3 (
        echo Cancelled. No deploy was performed.
        popd
        exit /b 2
    )
    if errorlevel 2 (
        echo Closing Revit...
        taskkill /IM Revit.exe /T /F
        timeout /T 3 /NOBREAK >nul
    )
    goto CHECK_REVIT
)

:RUN_SCRIPT
set "PS_ARGS=-ExecutionPolicy Bypass -NoProfile -File "%SCRIPT_DIR%deploy-bundle.ps1" -Configuration "%CONFIGURATION%""

if /I "%OPTION_1%"=="SkipBuild" (
    set "PS_ARGS=%PS_ARGS% -SkipBuild"
)
if /I "%OPTION_2%"=="SkipBuild" (
    set "PS_ARGS=%PS_ARGS% -SkipBuild"
)
if /I "%OPTION_1%"=="SkipDeploy" (
    set "PS_ARGS=%PS_ARGS% -SkipDeploy"
)
if /I "%OPTION_2%"=="SkipDeploy" (
    set "PS_ARGS=%PS_ARGS% -SkipDeploy"
)

powershell %PS_ARGS%
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" (
    echo FAILED: build/package/deploy did not complete.
    echo Try running this Command Prompt as Administrator if ProgramData deploy is blocked.
    popd
    exit /b %EXIT_CODE%
)

if /I "%OPTION_1%"=="SkipDeploy" goto PACKAGE_ONLY_DONE
if /I "%OPTION_2%"=="SkipDeploy" goto PACKAGE_ONLY_DONE

echo DONE: bundle was built, packaged, and deployed.
echo.
echo Package artifact:
echo   %SCRIPT_DIR%artifacts\release\LicorpExportPlus.bundle
echo.
echo Installed bundle:
echo   C:\ProgramData\Autodesk\ApplicationPlugins\LicorpExportPlus.bundle
echo.

popd
exit /b 0

:PACKAGE_ONLY_DONE
echo DONE: bundle was built, packaged, and verified. Deploy was skipped.
echo.
echo Package artifact:
echo   %SCRIPT_DIR%artifacts\release\LicorpExportPlus.bundle
echo.

popd
exit /b 0
