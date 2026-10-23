#requires -version 5
<#
    打发布包：
      1. 编译 dist\ClipPeek.exe
      2. 跑自测，任何一个 FAIL 就中止
      3. 便携包 dist\ClipPeek-<ver>-portable.zip
      4. 安装包 dist\ClipPeek-Setup-<ver>.exe（需要 Inno Setup 的 ISCC.exe）
      5. dist\SHA256SUMS.txt

    用法:
      powershell -ExecutionPolicy Bypass -File package.ps1
      powershell -ExecutionPolicy Bypass -File package.ps1 -SkipBuild
      powershell -ExecutionPolicy Bypass -File package.ps1 -AllowChocoInstall   # CI 用
#>

param(
    [switch]$SkipBuild,
    [switch]$SkipSelftest,
    [switch]$AllowChocoInstall
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$dist = Join-Path $root 'dist'

# --- 版本号 ---
$versionFile = Join-Path $root 'installer\version.iss'
$versionText = [System.IO.File]::ReadAllText($versionFile, [System.Text.Encoding]::UTF8)
if ($versionText -notmatch 'AppVersion\s+"([0-9]+\.[0-9]+\.[0-9]+)"') { throw 'version.iss 里读不到 AppVersion' }
$version = $Matches[1]
Write-Host "版本: $version"

# --- 编译 ---
if (-not $SkipBuild) {
    & (Join-Path $root 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw '编译失败' }
}

$appExe = Join-Path $dist 'ClipPeek.exe'
if (-not (Test-Path $appExe)) { throw "找不到 $appExe" }

# --- 自测 ---
if (-not $SkipSelftest) {
    Write-Host '跑自测…'
    $resultFile = Join-Path $dist 'selftest-result.txt'
    if (Test-Path $resultFile) { Remove-Item $resultFile -Force }

    # GUI 程序不能用 Start-Process -Wait 可靠地拿退出码，改看结果文件
    & $appExe --selftest
    $deadline = (Get-Date).AddSeconds(60)
    while (-not (Test-Path $resultFile) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }
    if (-not (Test-Path $resultFile)) { throw '自测没有产出结果文件（进程没跑起来？）' }

    $lines = Get-Content $resultFile -Encoding UTF8
    $lines | ForEach-Object { Write-Host "  $_" }
    if (-not ($lines -match '^RESULT OK')) { throw '自测失败（见上面的 FAIL 行）' }
    Write-Host '  -> 自测通过'
}

# --- 便携包 ---
Add-Type -AssemblyName System.IO.Compression.FileSystem
$portableDir = Join-Path $dist '_portable'
if (Test-Path $portableDir) { Remove-Item $portableDir -Recurse -Force }
New-Item -ItemType Directory -Path $portableDir | Out-Null

Copy-Item $appExe $portableDir
Copy-Item (Join-Path $root 'README.en.md') (Join-Path $portableDir 'README.md')
Copy-Item (Join-Path $root 'LICENSE') $portableDir
# portable.txt 让程序把 history.json / settings.json 写在自己旁边
Set-Content -Path (Join-Path $portableDir 'portable.txt') -Value "ClipPeek portable build $version - data files live next to ClipPeek.exe" -Encoding UTF8

$zipPath = Join-Path $dist "ClipPeek-$version-portable.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($portableDir, $zipPath)
Remove-Item $portableDir -Recurse -Force
Write-Host "便携包: $zipPath"

# --- 安装包 ---
$iscc = $null
$candidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 7\ISCC.exe'
)
foreach ($c in $candidates) { if (Test-Path $c) { $iscc = $c; break } }
if (-not $iscc) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}

if (-not $iscc -and $AllowChocoInstall) {
    Write-Host '本机没有 Inno Setup，尝试用 choco 安装…'
    & choco install innosetup -y --no-progress
    foreach ($c in $candidates) { if (Test-Path $c) { $iscc = $c; break } }
}

if ($iscc) {
    Write-Host "用 $iscc 编译安装包…"

    # 有些 Inno Setup 发行版不带简体中文语言文件；没有就临时生成一份去掉中文的脚本
    $issFile = Join-Path $root 'installer\clippeek.iss'
    $languagesDir = Join-Path (Split-Path $iscc) 'Languages'
    if (-not (Test-Path (Join-Path $languagesDir 'ChineseSimplified.isl'))) {
        Write-Warning '  Inno Setup 缺少 ChineseSimplified.isl，改用仅英文的临时脚本'
        $text = [System.IO.File]::ReadAllText($issFile, [System.Text.Encoding]::UTF8)
        $text = ($text -split "`r?`n" | Where-Object { $_ -notmatch '^\s*Name:\s*"zh"' -and $_ -notmatch '^\s*zh\.' }) -join "`r`n"
        $issFile = Join-Path $root 'installer\clippeek.build.iss'
        [System.IO.File]::WriteAllText($issFile, $text, (New-Object System.Text.UTF8Encoding($true)))
    }

    & $iscc $issFile
    $isccExit = $LASTEXITCODE
    if (Test-Path (Join-Path $root 'installer\clippeek.build.iss')) {
        Remove-Item (Join-Path $root 'installer\clippeek.build.iss') -Force
    }
    if ($isccExit -ne 0) { throw "ISCC 编译失败（exit: $isccExit）" }

    $setup = Join-Path $dist "ClipPeek-Setup-$version.exe"
    if (-not (Test-Path $setup)) { throw '安装包没有生成' }
    Write-Host "安装包: $setup"
} else {
    Write-Warning '没找到 ISCC.exe，跳过安装包（便携包已生成）'
}

# --- 校验和 ---
$sumFile = Join-Path $dist 'SHA256SUMS.txt'
if (Test-Path $sumFile) { Remove-Item $sumFile -Force }
$lines = @()
foreach ($f in @("ClipPeek-Setup-$version.exe", "ClipPeek-$version-portable.zip")) {
    $path = Join-Path $dist $f
    if (Test-Path $path) {
        $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLower()
        $lines += "$hash  $f"
    }
}
if ($lines.Count -gt 0) {
    Set-Content -Path $sumFile -Value $lines -Encoding ASCII
    Write-Host "校验和: $sumFile"
}

Write-Host ''
Write-Host '产物:'
Get-ChildItem $dist -File | Where-Object { $_.Name -notmatch '^_' } | ForEach-Object {
    Write-Host ("  {0}  ({1:N0} 字节)" -f $_.Name, $_.Length)
}
