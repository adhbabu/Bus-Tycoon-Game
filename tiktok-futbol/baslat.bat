@echo off
chcp 65001 >nul
title TikTok Canli Futbol
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo Node.js bulunamadi. https://nodejs.org adresinden LTS surumunu kurup tekrar dene.
  start "" https://nodejs.org
  pause
  exit /b
)
if not exist node_modules (
  echo Ilk calistirma: paketler kuruluyor...
  call npm install
)
start "" powershell -NoProfile -WindowStyle Hidden -Command "Start-Sleep 3; Start-Process 'http://localhost:3000/admin'; Start-Process 'http://localhost:3000/'"
node server.js
pause
