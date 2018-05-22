if not exist "c:\Program Files\GVFS" goto :end

call %~dp0\StopAllServices.bat

REM Find the latest uninstaller file by date and run it. Goto the next step after a single execution.
for /F "delims=" %%f in ('dir "c:\Program Files\GVFS\unins*.exe" /B /S /O:-D') do %%f /VERYSILENT /SUPPRESSMSGBOXES /NORESTART & goto :deleteGVFS

:deleteGVFS
rmdir /q/s "c:\Program Files\GVFS"

:end
