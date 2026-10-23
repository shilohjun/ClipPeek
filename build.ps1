#requires -version 5
<#
    编译 ClipPeek.exe 到 dist\。只依赖系统自带的 .NET Framework 编译器，不需要装 SDK。
    用法:  powershell -ExecutionPolicy Bypass -File build.ps1
#>

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw '找不到 csc.exe（.NET Framework 编译器）'
}

$sources = Get-ChildItem -Path (Join-Path $root 'src') -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw 'src 目录里没有 .cs 文件' }

$dist = Join-Path $root 'dist'
if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }
$out = Join-Path $dist 'ClipPeek.exe'
if (Test-Path $out) { Remove-Item $out -Force }

$arguments = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/codepage:65001'
    '/warn:4'
    "/out:$out"
    "/win32manifest:$root\app.manifest"
    '/r:System.dll'
    '/r:System.Core.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
    '/r:System.Web.Extensions.dll'
) + $sources

& $csc $arguments
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

$info = Get-Item $out
Write-Host ("编译成功: {0}  ({1:N0} 字节)" -f $info.FullName, $info.Length)
