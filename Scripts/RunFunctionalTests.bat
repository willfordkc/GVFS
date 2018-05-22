@ECHO OFF
IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")

SETLOCAL
SET PATH=C:\Program Files\GVFS;%PATH%

REM Force GVFS.FunctionalTests.exe to use the installed version of GVFS
del %~dp0\..\..\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\GitHooksLoader.exe
del %~dp0\..\..\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\GVFS.exe
del %~dp0\..\..\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\GVFS.Hooks.exe
del %~dp0\..\..\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\GVFS.ReadObjectHook.exe
del %~dp0\..\..\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\GVFS.VirtualFileSystemHook.exe
del %~dp0\..\..\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\GVFS.Mount.exe
del %~dp0\..\..\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\GVFS.Service.exe
del %~dp0\..\..\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\GVFS.Service.UI.exe

%~dp0\..\..\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\GVFS.FunctionalTests.exe --test-gvfs-on-path %2 %3
set error=%errorlevel%

call %~dp0\StopAllServices.bat

exit /b %error%