#Requires -Version 5.1
<#
.SYNOPSIS
    发布 WinTool 单文件 exe。

.DESCRIPTION
    产物：dist\WinTool-win-x64\WinTool.exe —— 自包含（用户机器不需要装 .NET），单文件。
    数据库/留底都在 exe 同级目录生成，整个文件夹可以直接拷走。

    D7（安装形态 / 数据根 / 卸载保留 / 开机自启）仍未确认，因此这里刻意**不做**安装包，
    只产出绿色单文件，避免把未确认的安装行为提前固定下来。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File build\publish.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File build\publish.ps1 -FrameworkDependent
#>
param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    # 框架依赖版体积小，但要求目标机装了 .NET 10 桌面运行时。
    [switch]$FrameworkDependent,
    [switch]$NoClean
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src\WinTool.App\WinTool.App.csproj'
$outDir = Join-Path $repo "dist\WinTool-$Runtime"

if (-not $NoClean -and (Test-Path $outDir)) {
    Remove-Item $outDir -Recurse -Force
}

$args = @(
    'publish', $project,
    '-c', $Configuration,
    '-r', $Runtime,
    '-o', $outDir,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=none',
    '-p:GenerateDocumentationFile=false'
)

if ($FrameworkDependent) {
    $args += @('--self-contained', 'false')
} else {
    $args += @('--self-contained', 'true')
}

Write-Host "dotnet $($args -join ' ')" -ForegroundColor DarkGray
& dotnet @args
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败，退出码 $LASTEXITCODE"
}

$exe = Join-Path $outDir 'WinTool.exe'
if (-not (Test-Path $exe)) {
    throw "没有生成 $exe"
}

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ''
Write-Host "产物: $exe ($size MB)" -ForegroundColor Green
Write-Host "首次运行会在 exe 同级创建 data\ 与 待办留底\（不可写则回退 %LOCALAPPDATA%\WinTool\）。" -ForegroundColor DarkGray
