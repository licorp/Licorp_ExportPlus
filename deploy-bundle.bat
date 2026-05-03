@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
set "CONFIGURATION=%~1"
set "OPTION_1=%~2"
set "OPTION_2=%~3"
set "OPTION_3=%~4"

if "%CONFIGURATION%"=="" set "CONFIGURATION=Release"

echo.
echo ============================================================
echo  Licorp Export+ build, package, and deploy
echo ============================================================
echo  Project      : %SCRIPT_DIR%
echo  Configuration: %CONFIGURATION%
echo  Artifact     : %SCRIPT_DIR%artifacts\release\LicorpExportPlus.bundle
echo  Bundle dirs  : Contents\R2020 ... Contents\R2027
echo  Revit deploy : C:\ProgramData\Autodesk\ApplicationPlugins\LicorpExportPlus.bundle
echo  Addin 2020-26: C:\ProgramData\Autodesk\Revit\Addins\{year}\LicorpExportPlus.addin
echo  Addin 2027   : C:\Program Files\Autodesk\Revit 2027\AddIns\LicorpExportPlus\LicorpExportPlus.addin
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
if /I "%OPTION_3%"=="SkipDeploy" goto RUN_SCRIPT

net session >nul 2>nul
if errorlevel 1 (
    if /I "%OPTION_3%"=="__ELEVATED" (
        echo.
        echo ERROR: Administrator permission was still not granted.
        echo Right-click deploy-bundle.bat and choose "Run as administrator".
        echo.
        pause
        popd
        exit /b 1
    )
    echo.
    echo Administrator permission is required for Revit 2027 deployment.
    echo The Revit 2027 manifest must be written to:
    echo   C:\Program Files\Autodesk\Revit 2027\AddIns\LicorpExportPlus\
    echo.
    echo Requesting Administrator permission...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'cmd.exe' -ArgumentList '/k ""%~f0"" ""%CONFIGURATION%"" ""%OPTION_1%"" ""%OPTION_2%"" ""__ELEVATED""' -WorkingDirectory '%SCRIPT_DIR%' -Verb RunAs"
    if errorlevel 1 (
        echo.
        echo ERROR: Elevation request was cancelled or failed.
        echo No deploy was performed.
        echo.
        popd
        exit /b 1
    )
    popd
    exit /b 0
)

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
if /I "%OPTION_3%"=="SkipBuild" (
    set "PS_ARGS=%PS_ARGS% -SkipBuild"
)
if /I "%OPTION_1%"=="SkipDeploy" (
    set "PS_ARGS=%PS_ARGS% -SkipDeploy"
)
if /I "%OPTION_2%"=="SkipDeploy" (
    set "PS_ARGS=%PS_ARGS% -SkipDeploy"
)
if /I "%OPTION_3%"=="SkipDeploy" (
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
if /I "%OPTION_3%"=="SkipDeploy" goto PACKAGE_ONLY_DONE

echo DONE: bundle was built, packaged, and deployed.
echo.
echo Package artifact:
echo   %SCRIPT_DIR%artifacts\release\LicorpExportPlus.bundle
echo   Contents\R2020 ... Contents\R2027
echo.
echo Installed bundle:
echo   C:\ProgramData\Autodesk\ApplicationPlugins\LicorpExportPlus.bundle
echo.
echo Revit manifests:
echo   2020-2026: C:\ProgramData\Autodesk\Revit\Addins\{year}\LicorpExportPlus.addin
echo   2027     : C:\Program Files\Autodesk\Revit 2027\AddIns\LicorpExportPlus\LicorpExportPlus.addin
echo.
if exist "C:\Program Files\Autodesk\Revit 2027\AddIns\LicorpExportPlus\LicorpExportPlus.addin" (
    echo Revit 2027 manifest:
    echo   C:\Program Files\Autodesk\Revit 2027\AddIns\LicorpExportPlus\LicorpExportPlus.addin
) else (
    echo WARNING: Revit 2027 manifest was not found in Program Files AddIns folder.
)
echo.

popd
exit /b 0

:PACKAGE_ONLY_DONE
echo DONE: bundle was built, packaged, and verified. Deploy was skipped.
echo.
echo Package artifact:
echo   %SCRIPT_DIR%artifacts\release\LicorpExportPlus.bundle
echo   Contents\R2020 ... Contents\R2027
echo.

popd
exit /b 0
