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

rem WPF-сборки для рекламного баннера (лежат в GAC, ставятся вместе с .NET Framework)
set GAC=%WINDIR%\Microsoft.NET\assembly
for /f "delims=" %%p in ('dir /b /s "%GAC%\WindowsBase.dll" 2^>nul') do if not defined REF_WB set REF_WB=%%p
for /f "delims=" %%p in ('dir /b /s "%GAC%\PresentationCore.dll" 2^>nul') do if not defined REF_PC set REF_PC=%%p
for /f "delims=" %%p in ('dir /b /s "%GAC%\PresentationFramework.dll" 2^>nul') do if not defined REF_PF set REF_PF=%%p
for /f "delims=" %%p in ('dir /b /s "%GAC%\System.Xaml.dll" 2^>nul') do if not defined REF_XAML set REF_XAML=%%p
for /f "delims=" %%p in ('dir /b /s "%GAC%\WindowsFormsIntegration.dll" 2^>nul') do if not defined REF_WFI set REF_WFI=%%p
if not defined REF_WB goto :nowpf
if not defined REF_PC goto :nowpf
if not defined REF_PF goto :nowpf
if not defined REF_XAML goto :nowpf
if not defined REF_WFI goto :nowpf

if not exist banner.mp4 (
  echo Не найден banner.mp4 ^(рекламное видео^) рядом с build.cmd
  exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /win32manifest:app.manifest /win32icon:app.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll ^
  /r:"%REF_WB%" /r:"%REF_PC%" /r:"%REF_PF%" /r:"%REF_XAML%" /r:"%REF_WFI%" ^
  /resource:banner.mp4,banner.mp4 ^
  /out:build\ZapretChecker.exe Program.cs AdBanner.cs
if errorlevel 1 (
  echo Ошибка сборки ZapretChecker.exe
  exit /b 1
)

rem Setup.exe собирается с программой внутри: скачивать больше ничего не нужно
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /win32manifest:app.manifest /win32icon:app.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll ^
  /resource:build\ZapretChecker.exe,ZapretChecker.exe ^
  /out:build\Setup.exe SetupProgram.cs
if errorlevel 1 (
  echo Ошибка сборки Setup.exe
  exit /b 1
)

echo Готово: build\ZapretChecker.exe + build\Setup.exe
exit /b 0

:nowpf
echo Не найдены WPF-сборки в %GAC% — установка .NET Framework повреждена?
exit /b 1
