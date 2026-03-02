@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
color 0A
title Device Ecosystem Builder
cls

set "ROOT=%~dp0"
set "OUT=%ROOT%build\release"
set "GRADLE=%ROOT%gradle-8.7\bin\gradle.bat"
set "JDK17=%ROOT%jdk-17.0.13+11"

if not exist "%JDK17%\bin\java.exe" (
    echo ОШИБКА: JDK 17 не найден в %JDK17%
    pause & exit /b 1
)

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo ОШИБКА: .NET SDK не найден! Скачайте: https://dotnet.microsoft.com/download
    pause & exit /b 1
)

if not exist "%GRADLE%" (
    echo ОШИБКА: Gradle не найден в %GRADLE%
    pause & exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"
if not exist "%OUT%\windows" mkdir "%OUT%\windows"
if not exist "%OUT%\android" mkdir "%OUT%\android"

echo ==========================================
echo    DEVICE ECOSYSTEM BUILDER
echo ==========================================
echo.

:: ===== WINDOWS =====
echo [1/2] Сборка Windows приложения...
cd /d "%ROOT%EcosystemWindows"

dotnet restore --verbosity quiet
if %errorlevel% neq 0 goto :error

dotnet publish -c Release -r win-x64 --self-contained -o "%OUT%\windows" --verbosity quiet
if %errorlevel% neq 0 goto :error
echo       OK

:: ===== ANDROID =====
echo.
echo [2/2] Сборка Android приложения...
cd /d "%ROOT%EcosystemAndroid"

set "ANDROID_HOME=C:\Users\%USERNAME%\AppData\Local\Android\Sdk"
if not exist "%ANDROID_HOME%" (
    echo ПРЕДУПРЕЖДЕНИЕ: Android SDK не найден в %ANDROID_HOME%
    echo Пропускаем сборку Android.
    goto :report
)

set "JAVA_HOME=%JDK17%"
call "%GRADLE%" assembleDebug --quiet
if %errorlevel% neq 0 goto :error

copy /y "app\build\outputs\apk\debug\app-debug.apk" "%OUT%\android\DeviceEcosystem.apk" >nul
echo       OK

:: ===== ОТЧЁТ =====
:report
cd /d "%ROOT%"
echo.
echo ==========================================
echo    РЕЗУЛЬТАТ СБОРКИ
echo ==========================================

set "WIN_OK=0"
set "APK_OK=0"

if exist "%OUT%\windows\DeviceEcosystem.exe" (
    set "WIN_OK=1"
    echo   Windows EXE : %OUT%\windows\DeviceEcosystem.exe
)
if exist "%OUT%\android\DeviceEcosystem.apk" (
    set "APK_OK=1"
    echo   Android APK : %OUT%\android\DeviceEcosystem.apk
)

if "%WIN_OK%"=="0" if "%APK_OK%"=="0" (
    echo   Ничего не собралось. Смотрите ошибки выше.
)

echo.
echo Нажмите любую клавишу...
pause >nul
exit /b 0

:error
echo.
echo СБОРКА ПРЕРВАНА. Смотрите ошибки выше.
pause
exit /b 1
