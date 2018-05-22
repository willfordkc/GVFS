IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")

taskkill /F /T /FI "IMAGENAME eq git.exe"
taskkill /F /T /FI "IMAGENAME eq GVFS.exe"
taskkill /F /T /FI "IMAGENAME eq GVFS.Mount.exe"

call %~dp0\UninstallGVFS.bat

if not exist "c:\Program Files\Git" goto :noGit
for /F "delims=" %%g in ('dir "c:\Program Files\Git\unins*.exe" /B /S /O:-D') do %%g /VERYSILENT /SUPPRESSMSGBOXES /NORESTART & goto :deleteGit

:deleteGit
rmdir /q/s "c:\Program Files\Git"

:noGit
REM This is a hacky way to sleep for 2 seconds in a non-interactive window.  The timeout command does not work if it can't redirect stdin.
ping 1.1.1.1 -n 1 -w 2000 >NUL

call %~dp0\StopService.bat gvflt
call %~dp0\StopService.bat prjflt

if not exist c:\Windows\System32\drivers\gvflt.sys goto :removePrjFlt
del c:\Windows\System32\drivers\gvflt.sys

:removePrjFlt
REM If PrjFlt is not inbox we should delete it
REM 17121 -> Min RS4 version with inbox PrjFlt
REM 17600 -> First RS5 version
REM 17626 -> Min RS5 version with inbox PrjFlt
powershell -NoProfile -Command "&{$var=[System.Environment]::OSVersion.Version.Build; if($var -lt 17121 -or ($var -ge 17600 -and $var -lt 17626)){exit 1}else{exit 2}}"

REM ERRORLEVEL == 2 means PrjFlt is inbox
if %ERRORLEVEL% == 2 goto :runInstallers
if not exist c:\Windows\System32\drivers\PrjFlt.sys goto :runInstallers
del c:\Windows\System32\drivers\PrjFlt.sys

:runInstallers
call %~dp0\..\..\BuildOutput\GVFS.Build\InstallG4W.bat
call %~dp0\..\..\BuildOutput\GVFS.Build\InstallGVFS.bat
