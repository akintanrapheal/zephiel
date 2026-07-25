@echo off
echo Stopping any running instances...
taskkill /F /IM Zephiel.Web.exe /T 2>NUL
taskkill /F /IM dotnet.exe /T 2>NUL
timeout /t 2 /nobreak >NUL

echo Building latest code...
cd /d "%~dp0src\Zephiel.Web"
dotnet publish -c Release -o "C:\Temp\sterling-pub" 2>NUL
if errorlevel 1 (
    echo Build failed. Check for errors.
    pause
    exit /b 1
)

echo Starting Zephiel on http://localhost:5000 ...
set ASPNETCORE_ENVIRONMENT=Development
dotnet "C:\Temp\sterling-pub\Zephiel.Web.dll" --urls "http://localhost:5000"
