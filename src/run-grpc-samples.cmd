@echo off
REM Script to run both gRPC sample applications
REM This starts the server and client in the correct order

echo === Simplic.OxS gRPC Sample Applications ===
echo.

REM Build both projects first
echo Building projects...
dotnet build Simplic.OxS.GrpcSample.Server\Simplic.OxS.GrpcSample.Server.csproj
if %ERRORLEVEL% neq 0 (
    echo Build failed for server. Please fix compilation errors.
    exit /b 1
)

dotnet build Simplic.OxS.GrpcSample.Client\Simplic.OxS.GrpcSample.Client.csproj
if %ERRORLEVEL% neq 0 (
    echo Build failed for client. Please fix compilation errors.
    exit /b 1
)

echo.
echo Starting gRPC server...
echo Open a new command prompt and run: cd Simplic.OxS.GrpcSample.Server ^&^& dotnet run
echo Then press any key to continue with the client...
pause

echo.
echo Starting client application...
cd Simplic.OxS.GrpcSample.Client
dotnet run

echo.
echo Don't forget to stop the server in the other window!
echo Done!