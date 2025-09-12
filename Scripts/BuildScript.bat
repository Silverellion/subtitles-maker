@echo off
setlocal

echo ========================================
echo    Subtitles Maker - Release Build
echo ========================================
echo.

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%.."

REM Check if we're in the correct directory
if not exist "subtitles-maker.csproj" (
    echo ERROR: Could not find subtitles-maker.csproj file
    echo Make sure this script is in the Scripts folder of your project
    pause
    exit /b 1
)

echo Current directory: %CD%
echo.

REM Clean previous builds
echo Cleaning previous builds...
dotnet clean --configuration Release --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to clean project
    pause
    exit /b 1
)

echo.

REM Build the project in Release mode
echo Building project in Release mode...
dotnet build --configuration Release --verbosity minimal --no-restore
if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.

REM Publish the application
echo Publishing application...
dotnet publish --configuration Release --output ".\publish" --verbosity minimal --no-build
if %ERRORLEVEL% neq 0 (
    echo ERROR: Publish failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo         BUILD COMPLETED SUCCESSFULLY
echo ========================================
echo.
echo Build output: .\bin\Release\net8.0\
echo Published output: .\publish\
echo.

REM Ask if user wants to open the output folder
set /p "OPEN_FOLDER=Open output folder? (y/n): "
if /i "%OPEN_FOLDER%"=="y" (
    start explorer "%CD%\bin\Release\net8.0"
)

echo.
echo Press any key to exit...
pause >nul