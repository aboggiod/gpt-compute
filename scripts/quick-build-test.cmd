@echo off
REM Quick test to verify the solution builds

echo Testing if solution builds...
echo.

cd /d "%~dp0.."

echo Running: dotnet restore
dotnet restore --nologo
if %ERRORLEVEL% NEQ 0 (
    echo [FAILED] Restore failed
    pause
    exit /b 1
)

echo.
echo Running: dotnet build
dotnet build --nologo --configuration Release
if %ERRORLEVEL% NEQ 0 (
    echo [FAILED] Build failed
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Solution builds successfully!
echo.
pause
