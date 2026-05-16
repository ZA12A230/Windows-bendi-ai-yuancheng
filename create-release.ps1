# LocalAIStudio 版本管理和发布脚本
param(
    [string]$Version = "",
    [switch]$CreateTag,
    [switch]$Push
)

$ErrorActionPreference = "Stop"

function Get-NextVersion {
    $tags = git tag --list "v*" | Sort-Object {[version]($_ -replace 'v', '')}
    if ($tags.Count -eq 0) {
        return "v1.0.0"
    }
    $lastTag = $tags[-1]
    $version = $lastTag -replace 'v', ''
    $parts = $version.Split('.')
    $parts[2] = [int]$parts[2] + 1
    return "v$($parts[0]).$($parts[1]).$($parts[2])"
}

if ($Version -eq "") {
    $Version = Get-NextVersion
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " LocalAIStudio 版本管理" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  新版本：$Version" -ForegroundColor White
Write-Host ""

if ($CreateTag) {
    Write-Host "[步骤] 创建 Git 标签..." -ForegroundColor Yellow
    git tag -a $Version -m "Release $Version"
    Write-Host "[成功] 标签已创建：$Version" -ForegroundColor Green
}

if ($Push) {
    Write-Host "[步骤] 推送到远程仓库..." -ForegroundColor Yellow
    git push origin $Version
    Write-Host "[成功] 标签已推送" -ForegroundColor Green
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " GitHub Actions 将自动构建并发布" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "请前往以下地址查看构建进度:" -ForegroundColor Gray
    Write-Host "https://github.com/ZA12A230/Windows-bendi-ai-yuancheng/actions" -ForegroundColor Cyan
}
