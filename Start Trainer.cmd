@echo off
rem Copyright (C) 2026 Despical
rem SPDX-License-Identifier: GPL-3.0-or-later
setlocal
cd /d "%~dp0"
if exist "ETS2Trainer.exe" (
    start "" "ETS2Trainer.exe"
    exit /b
)
if exist "dist\ETS2Trainer.exe" (
    start "" "dist\ETS2Trainer.exe"
) else (
    echo Run build.ps1 first, or extract the complete release ZIP.
    pause
)
