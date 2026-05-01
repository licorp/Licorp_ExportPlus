@echo off
setlocal

cd /d "%~dp0"

set "VERSION="
set "SKIPBUILD="

rem Usage:
rem   build-package.bat
rem   build-package.bat -SkipBuild
rem   build-package.bat -SkipBuild 1.2.3
rem   build-package.bat skip 1.2.3

if /I "%~1"=="-SkipBuild" set "SKIPBUILD=-SkipBuild"
if /I "%~1"=="skip" set "SKIPBUILD=-SkipBuild"
if not "%~2"=="" set "VERSION=%~2"

echo.
echo ======================================
echo LicorpExportPlus Package Script
echo ======================================
if defined VERSION (
  echo Version: %VERSION%
) else (
  echo Version: (from Directory.Build.props)
)
if defined SKIPBUILD (
    echo Mode: Skip build, package from existing outputs
) else (
    echo Mode: Build then package
)
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-package.ps1" %SKIPBUILD% %VERSION%
if errorlevel 1 (
    echo.
    echo ======================================
    echo PACKAGE FAILED
    echo ======================================
    echo.
    pause
    exit /b 1
)

echo.
echo ======================================
echo PACKAGE COMPLETED
echo ======================================
echo.
echo ZIP:
echo   %~dp0artifacts\LicorpExportPlus_Setup_*.zip
echo.
echo RELEASE FOLDER:
echo   %~dp0artifacts\release\
echo.
pause
exit /b 0

