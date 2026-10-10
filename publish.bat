@echo off
cd /d "%~dp0"
rem SysLens writes SysLens.settings.json next to SysLens.exe; it is kept while publish is rebuilt.
if exist publish\SysLens.settings.json copy /y publish\SysLens.settings.json "%TEMP%\SysLens.settings.json" >nul
if exist publish rmdir /s /q publish
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=embedded -o publish || exit /b 1
if exist "%TEMP%\SysLens.settings.json" move /y "%TEMP%\SysLens.settings.json" publish\ >nul
wsl -d Ubuntu /mnt/f/OneDrive/Guidelines/Certificates/codesign/syslens-codesign.sh
