@echo off
setlocal EnableExtensions EnableDelayedExpansion

title Licorp Export+ - Build + Deploy
cd /d "%~dp0"
set "ROOT=%~dp0"
set "CONFIG=Release"
set "BUNDLE=C:\ProgramData\Autodesk\ApplicationPlugins\LicorpExportPlus"

echo.
echo  ========================================
echo   LICORP EXPORT+ - BUILD + DEPLOY
echo  ========================================
echo.

REM ========================================
REM  STEP 1: BUILD
REM ========================================
if /I "%~1"=="SkipBuild" (
    echo  [1] Build SKIPPED
    goto :deploy
)

echo  [1] Building R2020, R2025, R2027...
for %%v in (R2020 R2025 R2027) do (
    echo      Building %%v...
    dotnet build Source\LicorpExportPlus.%%v\LicorpExportPlus.%%v.csproj -c %CONFIG% --nologo -v q
    if errorlevel 1 (
        echo      [ERROR] %%v FAILED!
        exit /b 1
    )
)
echo      All builds OK!
echo.

REM ========================================
REM  STEP 2: DEPLOY
REM ========================================
:deploy
if /I "%~1"=="SkipDeploy" (
    echo  [2] Deploy SKIPPED
    goto :done
)
if /I "%~2"=="SkipDeploy" (
    echo  [2] Deploy SKIPPED
    goto :done
)

echo  [2] Deploying to %BUNDLE%...

REM Clean old bundle and addins
if exist "%BUNDLE%" rd /s /q "%BUNDLE%" 2>nul
for %%y in (2020 2021 2022 2023 2024 2025 2026 2027) do (
    if exist "%ProgramData%\Autodesk\Revit\Addins\%%y\LicorpExportPlus.addin" (
        del /f /q "%ProgramData%\Autodesk\Revit\Addins\%%y\LicorpExportPlus.addin" 2>nul
    )
)

REM Create bundle root
mkdir "%BUNDLE%" 2>nul

REM Deploy R2020-R2024 (shared .NET Framework 4.8)
set "SRC20=%ROOT%bin\R2020\%CONFIG%\publish\Revit 2020 Release addin\LicorpExportPlus"
for %%v in (R2020 R2021 R2022 R2023 R2024) do (
    set "RV_NUM=%%v"
    set "RV_NUM=!RV_NUM:R=!"
    set "DST=%BUNDLE%\%%v"
    set "ADDIN=%ProgramData%\Autodesk\Revit\Addins\!RV_NUM!"
    
    mkdir "!DST!" 2>nul
    mkdir "!ADDIN!" 2>nul
    
    if exist "%SRC20%\LicorpExportPlus.dll" (
        xcopy "%SRC20%\*" "!DST!\" /E /Y /Q /I >nul 2>&1
        (
        echo ^<?xml version="1.0" encoding="utf-8" standalone="no"?^>
        echo ^<RevitAddIns^>
        echo ^<AddIn Type="Application"^>
        echo ^<Name^>LicorpExportPlus^</Name^>
        echo ^<Assembly^>!DST!\LicorpExportPlus.dll^</Assembly^>
        echo ^<AddInId^>A7E4B1C3-8D2F-4A5E-9F6B-3C1D7E8A2B5F^</AddInId^>
        echo ^<FullClassName^>LicorpExportPlus.ExportPlusApplication^</FullClassName^>
        echo ^<VendorId^>LICORP^</VendorId^>
        echo ^<VendorDescription^>Licorp, licorp.vn^</VendorDescription^>
        echo ^</AddIn^>
        echo ^</RevitAddIns^>
        ) > "!ADDIN!\LicorpExportPlus.addin"
        echo      Revit !RV_NUM! - OK
    ) else (
        echo      Revit !RV_NUM! - [SKIP] Build not found
    )
)

REM Deploy R2025-R2026 (shared .NET 8.0)
set "SRC25=%ROOT%bin\R2025\%CONFIG%\publish\Revit 2025 Release addin\LicorpExportPlus"
for %%v in (R2025 R2026) do (
    set "RV_NUM=%%v"
    set "RV_NUM=!RV_NUM:R=!"
    set "DST=%BUNDLE%\%%v"
    set "ADDIN=%ProgramData%\Autodesk\Revit\Addins\!RV_NUM!"
    
    mkdir "!DST!" 2>nul
    mkdir "!ADDIN!" 2>nul
    
    if exist "%SRC25%\LicorpExportPlus.dll" (
        xcopy "%SRC25%\*" "!DST!\" /E /Y /Q /I >nul 2>&1
        (
        echo ^<?xml version="1.0" encoding="utf-8" standalone="no"?^>
        echo ^<RevitAddIns^>
        echo ^<AddIn Type="Application"^>
        echo ^<Name^>LicorpExportPlus^</Name^>
        echo ^<Assembly^>!DST!\LicorpExportPlus.dll^</Assembly^>
        echo ^<AddInId^>A7E4B1C3-8D2F-4A5E-9F6B-3C1D7E8A2B5F^</AddInId^>
        echo ^<FullClassName^>LicorpExportPlus.ExportPlusApplication^</FullClassName^>
        echo ^<VendorId^>LICORP^</VendorId^>
        echo ^<VendorDescription^>Licorp, licorp.vn^</VendorDescription^>
        echo ^</AddIn^>
        echo ^</RevitAddIns^>
        ) > "!ADDIN!\LicorpExportPlus.addin"
        echo      Revit !RV_NUM! - OK
    ) else (
        echo      Revit !RV_NUM! - [SKIP] Build not found
    )
)

REM Deploy R2027 (.NET 8.0, separate)
set "SRC27=%ROOT%bin\R2027\%CONFIG%\publish\Revit 2027 Release addin\LicorpExportPlus"
set "DST=%BUNDLE%\R2027"
set "ADDIN=%ProgramData%\Autodesk\Revit\Addins\2027"

mkdir "%DST%" 2>nul
mkdir "%ADDIN%" 2>nul

if exist "%SRC27%\LicorpExportPlus.dll" (
    xcopy "%SRC27%\*" "%DST%\" /E /Y /Q /I >nul 2>&1
    (
    echo ^<?xml version="1.0" encoding="utf-8" standalone="no"?^>
    echo ^<RevitAddIns^>
    echo ^<AddIn Type="Application"^>
    echo ^<Name^>LicorpExportPlus^</Name^>
    echo ^<Assembly^>%DST%\LicorpExportPlus.dll^</Assembly^>
    echo ^<AddInId^>A7E4B1C3-8D2F-4A5E-9F6B-3C1D7E8A2B5F^</AddInId^>
    echo ^<FullClassName^>LicorpExportPlus.ExportPlusApplication^</FullClassName^>
    echo ^<VendorId^>LICORP^</VendorId^>
    echo ^<VendorDescription^>Licorp, licorp.vn^</VendorDescription^>
    echo ^</AddIn^>
    echo ^</RevitAddIns^>
    ) > "%ADDIN%\LicorpExportPlus.addin"
    echo      Revit 2027 - OK
) else (
    echo      Revit 2027 - [SKIP] Build not found
)

REM Create PackageContents.xml
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<ApplicationPackage SchemaVersion="1.0"
echo   Name="Licorp Export+"
echo   Description="Professional Batch Export for Revit"
echo   Author="Licorp"
echo   AppVersion="1.0.0"
echo   ProductCode="{A7E4B1C3-8D2F-4A5E-9F6B-3C1D7E8A2B5F}"
echo   ProductType="Application"^>
echo   ^<Company Name="Licorp" /^>
echo   ^<Components^>
echo     ^<ComponentEntry AppName="Licorp Export+" Version="1.0.0"
echo       ModuleName="./R2020/LicorpExportPlus.addin"
echo       AppDescription="Professional Batch Export for Revit"
echo       LoadOnRevitStartup="True"^>
echo       ^<RuntimeRequirements OS="Win64" Platform="Revit" SeriesMin="R2020" SeriesMax="R2024" /^>
echo     ^</ComponentEntry^>
echo     ^<ComponentEntry AppName="Licorp Export+" Version="1.0.0"
echo       ModuleName="./R2025/LicorpExportPlus.addin"
echo       AppDescription="Professional Batch Export for Revit"
echo       LoadOnRevitStartup="True"^>
echo       ^<RuntimeRequirements OS="Win64" Platform="Revit" SeriesMin="R2025" SeriesMax="R2026" /^>
echo     ^</ComponentEntry^>
echo     ^<ComponentEntry AppName="Licorp Export+" Version="1.0.0"
echo       ModuleName="./R2027/LicorpExportPlus.addin"
echo       AppDescription="Professional Batch Export for Revit"
echo       LoadOnRevitStartup="True"^>
echo       ^<RuntimeRequirements OS="Win64" Platform="Revit" SeriesMin="R2027" SeriesMax="R2027" /^>
echo     ^</ComponentEntry^>
echo   ^</Components^>
echo ^</ApplicationPackage^>
) > "%BUNDLE%\PackageContents.xml"

echo.
echo      Bundle: %BUNDLE%
echo.

REM ========================================
REM  DONE
REM ========================================
:done
echo  ========================================
echo   DONE!
echo  ========================================
echo.
echo  Bundle: %BUNDLE%
echo  Manifests: %ProgramData%\Autodesk\Revit\Addins\{year}\LicorpExportPlus.addin
echo.
echo  Restart Revit - Tab "Licorp" - "Export+"
echo.
pause
