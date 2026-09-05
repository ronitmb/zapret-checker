@echo off
rem Сборка ZapretChecker.exe и Setup.exe в папку build\
rem Использует csc.exe из .NET Framework 4.x (уже есть в Windows, ничего ставить не нужно)
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Не найден csc.exe .NET Framework 4.x
  exit /b 1
)
if not exist build mkdir build

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /win32manifest:app.manifest /win32icon:app.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll ^
  /out:build\ZapretChecker.exe Program.cs
if errorlevel 1 (
  echo Ошибка сборки ZapretChecker.exe
  exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /win32manifest:app.manifest /win32icon:app.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll ^
  /out:build\Setup.exe SetupProgram.cs
if errorlevel 1 (
  echo Ошибка сборки Setup.exe
  exit /b 1
)

echo Готово: build\ZapretChecker.exe + build\Setup.exe
