@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
set "CONFIGURATION=%~1"
set "TARGET=%~2"

if "%CONFIGURATION%"=="" set "CONFIGURATION=Release"
if "%TARGET%"=="" set "TARGET=All"

echo.
echo ============================================================
echo  Licorp Export+ quick build test
echo ============================================================
echo  Project      : %SCRIPT_DIR%
echo  Configuration: %CONFIGURATION%
echo  Target       : %TARGET%
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

set "BUILD_TEST_DIR=%SCRIPT_DIR%Build Test"
if not exist "%BUILD_TEST_DIR%" mkdir "%BUILD_TEST_DIR%"

if /I "%TARGET%"=="R20" goto BUILD_R20
if /I "%TARGET%"=="R25" goto BUILD_R25
if /I "%TARGET%"=="R27" goto BUILD_R27
if /I "%TARGET%"=="ALL" goto BUILD_ALL

echo ERROR: Unknown target "%TARGET%".
echo Valid values: R20, R25, R27, All
popd
exit /b 1

:BUILD_ALL
call :BuildAndCopy "R20" "Source\LicorpExportPlus.R20\LicorpExportPlus.R20.csproj" "bin\R2020\%CONFIGURATION%"
if errorlevel 1 goto FAIL
call :BuildAndCopy "R25" "Source\LicorpExportPlus.R25\LicorpExportPlus.R25.csproj" "bin\R2025\%CONFIGURATION%"
if errorlevel 1 goto FAIL
call :BuildAndCopy "R27" "Source\LicorpExportPlus.R27\LicorpExportPlus.R27.csproj" "bin\R2027\%CONFIGURATION%"
if errorlevel 1 goto FAIL
goto DONE

:BUILD_R20
call :BuildAndCopy "R20" "Source\LicorpExportPlus.R20\LicorpExportPlus.R20.csproj" "bin\R2020\%CONFIGURATION%"
if errorlevel 1 goto FAIL
goto DONE

:BUILD_R25
call :BuildAndCopy "R25" "Source\LicorpExportPlus.R25\LicorpExportPlus.R25.csproj" "bin\R2025\%CONFIGURATION%"
if errorlevel 1 goto FAIL
goto DONE

:BUILD_R27
call :BuildAndCopy "R27" "Source\LicorpExportPlus.R27\LicorpExportPlus.R27.csproj" "bin\R2027\%CONFIGURATION%"
if errorlevel 1 goto FAIL
goto DONE

:BuildAndCopy
set "TAG=%~1"
set "CSPROJ=%~2"
set "OUT_REL=%~3"

echo.
echo --- Building %TAG% ---
dotnet build "%CSPROJ%" -c "%CONFIGURATION%" --nologo
if errorlevel 1 (
    echo ERROR: Build failed for %TAG%.
    exit /b 1
)

set "SRC=%SCRIPT_DIR%%OUT_REL%"
set "DST=%BUILD_TEST_DIR%\%TAG%\%CONFIGURATION%"

if not exist "%SRC%" (
    echo ERROR: Build output not found: %SRC%
    exit /b 1
)

if not exist "%DST%" mkdir "%DST%"

rem Prevent recursive publish nesting from previous runs.
if exist "%DST%\publish" (
    rmdir /s /q "\\?\%DST%\publish" 2>nul
    if exist "%DST%\publish" powershell -NoProfile -ExecutionPolicy Bypass -Command "Remove-Item -LiteralPath '\\?\%DST%\publish' -Recurse -Force -ErrorAction SilentlyContinue"
)

rem Copy build output but exclude publish/obj to avoid publish\...\publish loops.
robocopy "%SRC%" "%DST%" *.* /E /XD "publish" "obj" /NFL /NDL /NJH /NJS /NP >nul
set "RC=%ERRORLEVEL%"
if %RC% GEQ 8 (
    echo ERROR: Failed to copy output for %TAG%.
    exit /b 1
)

echo OK: %TAG% output copied to:
echo     %DST%
exit /b 0

:DONE
echo.
echo ============================================================
echo  SUCCESS
echo ============================================================
echo  Build test folder:
echo    %BUILD_TEST_DIR%
echo.
echo  Main DLL paths for AddIn Manager:
if /I "%TARGET%"=="R20" echo    %BUILD_TEST_DIR%\R20\%CONFIGURATION%\LicorpExportPlus.dll
if /I "%TARGET%"=="R25" echo    %BUILD_TEST_DIR%\R25\%CONFIGURATION%\LicorpExportPlus.dll
if /I "%TARGET%"=="R27" echo    %BUILD_TEST_DIR%\R27\%CONFIGURATION%\LicorpExportPlus.dll
if /I "%TARGET%"=="ALL" (
echo    %BUILD_TEST_DIR%\R20\%CONFIGURATION%\LicorpExportPlus.dll
echo    %BUILD_TEST_DIR%\R25\%CONFIGURATION%\LicorpExportPlus.dll
echo    %BUILD_TEST_DIR%\R27\%CONFIGURATION%\LicorpExportPlus.dll
)
echo.

popd
exit /b 0

:FAIL
echo.
echo FAILED: quick build test did not complete.
popd
exit /b 1
