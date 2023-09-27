@echo off
SETLOCAL EnableDelayedExpansion
cd %~dp0
SET ROOT=%~dp0
set PUBLISH=%ROOT%\RoboBackups\bin\Release\app.publish

if EXIST "%PUBLISH%" rd /s /q "%PUBLISH%"

msbuild /target:rebuild src\RoboBackups.sln /p:Configuration=Release "/p:Platform=Any CPU"
if ERRORLEVEL 1 goto :nobits
msbuild /target:publish src\RoboBackups.sln /p:Configuration=Release "/p:Platform=Any CPU"
if ERRORLEVEL 1 goto :nobits
if not EXIST %PUBLISH%\XmlNotepad.application goto :nobits

move "%PUBLISH%" "%ROOT%\publish"

echo Uploading ClickOnce installer to XmlNotepad
call AzurePublishClickOnce.cmd publish downloads/RoboBackups "%LOVETTSOFTWARE_STORAGE_CONNECTION_STRING%"
if ERRORLEVEL 1 goto :uploadfailed
goto :eof


:uploadfailed
echo Upload to Azure failed.
exit /b 1

:nobits
echo '%PUBLISH%' folder not found, so the build failed, please manually run release build and publish first.
exit /b 1
