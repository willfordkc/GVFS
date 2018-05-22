@ECHO OFF
SETLOCAL

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")
IF "%2"=="" (SET "GVFSVersion=0.2.173.2") ELSE (SET "GVFSVersion=%2")

SET msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\amd64\msbuild.exe"
SET nuget="%~dp0\..\..\.tools\nuget.exe"

IF NOT EXIST %nuget% (
  mkdir %nuget%\..
  powershell -ExecutionPolicy Bypass -Command "Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile %nuget%"
)

%msbuild% %~dp0\..\GVFS\GVFS.Build\GVFS.PreBuild.csproj /target:"GVFSPreBuild" /p:GVFSVersion=%GVFSVersion% /p:Configuration=%Configuration% /p:Platform=x64 || exit /b 1

%nuget% restore %~dp0\..\GVFS.sln || exit /b 1

dotnet restore %~dp0\..\GVFS\GVFS.CLI\GVFS.CLI.csproj || exit /b 1
dotnet restore %~dp0\..\GVFS\GVFS.Common\GVFS.Common.csproj || exit /b 1
dotnet restore %~dp0\..\GVFS\GVFS.Virtualization\GVFS.Virtualization.csproj || exit /b 1

%msbuild% %~dp0\..\GVFS\GVFS.Build\GVFS.PreBuild.csproj /target:"GenerateAll" /p:BuildOutputDir="%~dp0\..\..\BuildOutput" /p:PackagesDir="%~dp0\..\..\packages" /p:GVFSVersion=%GVFSVersion%  /p:Configuration=%Configuration% /p:Platform=x64 || exit /b 1
%msbuild% %~dp0\..\GVFS.sln /p:GVFSVersion=%GVFSVersion% /p:Configuration=%Configuration% /p:Platform=x64 || exit /b 1

ENDLOCAL