@echo off
cd /d "%~dp0"
if exist publish rmdir /s /q publish
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=embedded -o publish || exit /b 1
wsl -d Ubuntu /mnt/f/OneDrive/Guidelines/Certificates/codesign/disklens-codesign.sh
