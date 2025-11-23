@echo off
REM MCP Memory Server - Build and Test Script (Windows)
REM Runs full build, test, and verification

echo ================================================
echo MCP Memory Server - Build ^& Test
echo ================================================
echo.

REM Check .NET version
echo ^>^>^> Checking .NET SDK version
where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK not found. Please install .NET 8 SDK.
    echo Download from: https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)

dotnet --version
echo [SUCCESS] .NET SDK found
echo.

REM Clean previous builds
echo ^>^>^> Cleaning previous builds
dotnet clean --nologo
for /d /r . %%d in (bin,obj) do @if exist "%%d" rd /s /q "%%d"
del /f /q *.db *.db-shm *.db-wal 2>nul
echo [SUCCESS] Clean complete
echo.

REM Restore dependencies
echo ^>^>^> Restoring NuGet packages
dotnet restore --nologo
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Restore failed
    exit /b 1
)
echo [SUCCESS] Dependencies restored
echo.

REM Build solution
echo ^>^>^> Building solution
dotnet build --nologo --configuration Release
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed
    exit /b 1
)
echo [SUCCESS] Build successful
echo.

REM Run unit tests
echo ^>^>^> Running unit tests
dotnet test --nologo --configuration Release --logger "console;verbosity=normal"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Tests failed
    exit /b 1
)
echo [SUCCESS] All tests passed
echo.

REM Build summary
echo ================================================
echo Build and Test Complete!
echo ================================================
echo.
echo Build artifacts:
echo   - Release build: src\McpMemoryServer\bin\Release\net8.0\
echo   - Test results: tests\McpMemoryServer.Tests\TestResults\
echo.
echo Next steps:
echo   1. Run the server: cd src\McpMemoryServer ^&^& dotnet run
echo   2. Test endpoints: Use Postman or curl
echo   3. Deploy to production
echo.
