@echo off
if exist "%~dp0src\ReLaitis.UI\bin\Release\net9.0-windows\ReLaitis.UI.exe" (
    start "" "%~dp0src\ReLaitis.UI\bin\Release\net9.0-windows\ReLaitis.UI.exe"
) else (
    start "" "%~dp0src\ReLaitis.UI\bin\Debug\net9.0-windows\ReLaitis.UI.exe"
)
