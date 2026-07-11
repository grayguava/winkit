@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo csc.exe not found at expected path: %CSC%
    echo Check your .NET Framework installation.
    exit /b 1
)

if not exist bin\ md bin

"%CSC%" /nologo /target:exe /out:bin\nature.exe src\nature.cs
if %ERRORLEVEL% NEQ 0 exit /b 1

"%CSC%" /nologo /target:exe /out:bin\tech.exe src\tech.cs
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Build succeeded: bin\nature.exe, bin\tech.exe
