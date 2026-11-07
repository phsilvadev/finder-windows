@echo off
cd /d "%~dp0"
dotnet publish EspacoLivre.csproj -c Release -r win-x64 --self-contained true -o dist
if errorlevel 1 (pause & exit /b 1)
echo Aplicativo gerado em dist\EspacoLivre.exe
pause
