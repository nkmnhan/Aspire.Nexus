@echo off
echo ========================================
echo Aspire.Nexus - Local Debug Host
echo ========================================
echo.
echo Edit appsettings.json to choose which services to debug locally.
echo Use "dotnet user-secrets" to store sensitive values (cert passwords, etc.)
echo Dashboard: http://localhost:15178
echo.

cd /d %~dp0

dotnet run --launch-profile http
