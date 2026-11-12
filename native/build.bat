@echo off
REM Windows x64 build with MinGW-w64 gcc (on PATH). PowerShell users: use build.ps1 instead.
cd /d "%~dp0"
if not exist bin mkdir bin
gcc -std=c99 -O2 -Wall -Wextra -shared -static-libgcc -o bin\loanrisk.dll loanrisk.c
if errorlevel 1 exit /b 1
echo native build OK
REM Alternative with MSVC (x64 Developer Command Prompt):
REM   cl /nologo /O2 /LD /Fo:bin\ /Fe:bin\loanrisk.dll loanrisk.c
