@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
 echo Instale o SDK .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0
 pause
 exit /b 1
)
dotnet run --project EspacoLivre.csproj
if errorlevel 1 pause
