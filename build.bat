@echo off
rem Builds SotfModsManager.exe with the C# compiler that ships with Windows (.NET Framework 4.8). No SDK needed.
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /out:SotfModsManager.exe /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll SotfModsManager.cs
