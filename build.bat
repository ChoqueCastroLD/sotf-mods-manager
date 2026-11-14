@echo off
rem Builds SotfModsManager.exe with the C# compiler that ships with Windows (.NET Framework 4.8). No SDK needed.
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
"%FW%\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /out:SotfModsManager.exe /lib:"%FW%\WPF" ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Web.Extensions.dll /r:System.Data.dll /r:System.Xml.dll ^
  /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll /resource:assets\logo.png,logo.png src\*.cs
