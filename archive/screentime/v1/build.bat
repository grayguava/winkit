@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo csc.exe not found at expected path: %CSC%
    echo Check your .NET Framework installation.
    exit /b 1
)

if not exist bin\ md bin

echo Building screentime...
"%CSC%" /nologo /optimize+ /target:winexe /out:bin\screentime.exe /recurse:src\*.cs
if %ERRORLEVEL% EQU 0 (
    echo Build succeeded: bin\screentime.exe
) else (
    echo Build failed.
    exit /b 1
)
