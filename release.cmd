@echo off
rem release.cmd - atalho para o release.ps1
rem Uso (na pasta do projeto):
rem     release 2.4.1
rem     release 2.4.1 -Notes "corrige o audio"
rem     release 2.4.1 -NoUpload
setlocal
cd /d "%~dp0"

if "%~1"=="" (
    echo Uso: release ^<versao^> [-Notes "texto"] [-NoPublish] [-NoUpload]
    echo Exemplo: release 2.4.1
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0release.ps1" %*
exit /b %ERRORLEVEL%
