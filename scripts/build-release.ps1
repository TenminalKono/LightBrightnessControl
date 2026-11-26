# 一键发布框架依赖单文件版（约 200KB，需 .NET 8 Desktop Runtime）
param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$csprojPath  = Join-Path $projectRoot "LightBrightnessControl.csproj"

[xml]$csproj = Get-Content $csprojPath -Encoding UTF8
$assemblyName = $csproj.Project.PropertyGroup[0].AssemblyName
$version      = $csproj.Project.PropertyGroup[0].Version

if ([string]::IsNullOrWhiteSpace($assemblyName)) {
    throw "Failed to read AssemblyName from LightBrightnessControl.csproj"
}

$distRoot   = Join-Path $projectRoot "dist"
$outputDir  = Join-Path $distRoot $assemblyName
$zipPath    = Join-Path $distRoot "$assemblyName.zip"

Write-Host "Publishing $assemblyName (v$version) [$Runtime]..." -ForegroundColor Cyan

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

dotnet publish $csprojPath `
    -c Release `
    -r $Runtime `
    --no-self-contained `
    -p:SelfContained=false `
    -p:PublishSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# 清理不需要随发布包分发的临时文件（如 pdb / settings.json）
Get-ChildItem -Path $outputDir -Filter "*.pdb" | Remove-Item -Force -ErrorAction SilentlyContinue
$settingsLeak = Join-Path $outputDir "settings.json"
if (Test-Path $settingsLeak) {
    Remove-Item $settingsLeak -Force
}

$exePath = Join-Path $outputDir "$assemblyName.exe"
if (-not (Test-Path $exePath)) {
    throw "Expected executable not found: $exePath"
}

Compress-Archive -Path (Join-Path $outputDir "*") -DestinationPath $zipPath -Force

$exeSizeKB = [Math]::Round((Get-Item $exePath).Length / 1KB, 1)
$zipSizeKB = [Math]::Round((Get-Item $zipPath).Length / 1KB, 1)

Write-Host ""
Write-Host "Build & Package succeeded!" -ForegroundColor Green
Write-Host "  EXE : $exePath ($exeSizeKB KB)"
Write-Host "  ZIP : $zipPath ($zipSizeKB KB)"
