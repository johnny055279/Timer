# publish.ps1 - build, package, and release a new Timer version via Velopack.
# Prereqs: git tag already created matching <Version> in Timer.csproj,
#          `dotnet tool install -g vpk` done at least once.
$ErrorActionPreference = "Stop"

$version = (dotnet msbuild Timer.csproj -getProperty:Version).Trim()
Write-Host "Publishing Timer v$version"

# 1. Publish framework-dependent build (VerifyVersionMatchesGitTag runs here).
dotnet publish Timer.csproj -c Release -r win-x64 --self-contained false -o .\publish

# 2. Package with Velopack: installer (TimerSetup.exe) + delta update packages.
vpk pack `
  --packId Timer `
  --packVersion $version `
  --packDir .\publish `
  --mainExe Timer.exe `
  --framework net10-x64-desktop

# 3. Upload to GitHub Releases (public repo, no --token needed).
vpk upload github `
  --repoUrl https://github.com/johnny055279/Timer `
  --publish `
  --tag $version
