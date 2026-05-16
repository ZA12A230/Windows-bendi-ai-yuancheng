# LocalAIStudio 发布脚本
# 用于编译为单个exe（支持Win10/11 x64）

param(
    [switch]$Release,
    [switch]$Clean,
    [switch]$Watch
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ProjectName = "LocalAIStudio"
$ProjectPath = $PSScriptRoot
$OutputPath = Join-Path $ProjectPath "publish"
$Configuration = if ($Release) { "Release" } else { "Debug" }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " LocalAIStudio 构建脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

function Write-Step($message) {
    Write-Host "[步骤] $message" -ForegroundColor Yellow
}

function Write-Success($message) {
    Write-Host "[成功] $message" -ForegroundColor Green
}

function Write-Error($message) {
    Write-Host "[错误] $message" -ForegroundColor Red
}

function Test-DotNetSDK {
    Write-Step "检查 .NET SDK..."
    try {
        $sdkVersion = dotnet --version
        Write-Host "  .NET SDK 版本: $sdkVersion" -ForegroundColor Gray
        
        if ($sdkVersion -match "^8\.") {
            Write-Success ".NET 8 SDK 已安装"
            return $true
        } else {
            Write-Error "需要 .NET 8 SDK，请从 https://dotnet.microsoft.com/download 下载安装"
            return $false
        }
    } catch {
        Write-Error "未找到 .NET SDK，请先安装 .NET 8 SDK"
        return $false
    }
}

function Clear-Build {
    Write-Step "清理构建目录..."
    $binPath = Join-Path $ProjectPath "bin"
    $objPath = Join-Path $ProjectPath "obj"
    
    if (Test-Path $binPath) {
        Remove-Item -Path $binPath -Recurse -Force
        Write-Host "  已删除 bin 目录" -ForegroundColor Gray
    }
    
    if (Test-Path $objPath) {
        Remove-Item -Path $objPath -Recurse -Force
        Write-Host "  已删除 obj 目录" -ForegroundColor Gray
    }
    
    if (Test-Path $OutputPath) {
        Remove-Item -Path $OutputPath -Recurse -Force
        Write-Host "  已删除 publish 目录" -ForegroundColor Gray
    }
    
    Write-Success "清理完成"
}

function Restore-Packages {
    Write-Step "还原 NuGet 包..."
    Push-Location $ProjectPath
    try {
        $projectFile = Join-Path $ProjectPath "LocalAIStudio\LocalAIStudio.csproj"
        dotnet restore $projectFile --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore 失败"
        }
        Write-Success "包还原成功"
    } finally {
        Pop-Location
    }
}

function Build-Project {
    param([string]$Config)

    Write-Step "编译项目 ($Config 配置)..."
    Push-Location $ProjectPath
    try {
        $projectFile = Join-Path $ProjectPath "LocalAIStudio\LocalAIStudio.csproj"
        dotnet build $projectFile -c $Config --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "编译失败"
        }
        Write-Success "编译成功"
    } finally {
        Pop-Location
    }
}

function Publish-SingleFile {
    Write-Step "发布单文件 exe..."
    
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    
    Push-Location $ProjectPath
    try {
        $projectFile = Join-Path $ProjectPath "LocalAIStudio\LocalAIStudio.csproj"
        $publishArgs = @(
            "publish",
            $projectFile,
            "-c", "Release",
            "-r", "win-x64",
            "--self-contained", "true",
            "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:EnableCompressionInSingleFile=true",
            "-p:DebugType=None",
            "-p:DebugSymbols=false",
            "-o", $OutputPath
        )
        
        dotnet @publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "发布失败"
        }
        
        Write-Success "发布成功"
    } finally {
        Pop-Location
    }
}

function Get-FileSize($path) {
    $size = (Get-Item $path).Length
    if ($size -gt 1GB) {
        return "{0:N2} GB" -f ($size / 1GB)
    } elseif ($size -gt 1MB) {
        return "{0:N2} MB" -f ($size / 1MB)
    } else {
        return "{0:N2} KB" -f ($size / 1KB)
    }
}

function Show-Result {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " 发布完成" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    $exePath = Join-Path $OutputPath "LocalAIStudio.exe"
    
    if (Test-Path $exePath) {
        $fileSize = Get-FileSize $exePath
        Write-Host "  输出文件: $exePath" -ForegroundColor White
        Write-Host "  文件大小: $fileSize" -ForegroundColor White
        Write-Host "  目标平台: Windows 10/11 x64" -ForegroundColor White
        Write-Host ""
        Write-Host "  特性:" -ForegroundColor Gray
        Write-Host "    • 单文件部署，无需安装 .NET 运行时" -ForegroundColor Gray
        Write-Host "    • 自包含发布，所有依赖已打包" -ForegroundColor Gray
        Write-Host "    • 压缩发布，体积优化" -ForegroundColor Gray
        Write-Host ""
        
        $response = Read-Host "是否立即运行程序？(Y/N)"
        if ($response -eq "Y" -or $response -eq "y") {
            Start-Process $exePath
        }
    } else {
        Write-Error "未找到生成的文件"
    }
}

function Watch-FileChanges {
    Write-Step "启用文件监视模式..."
    Write-Host "  按 Ctrl+C 退出监视模式" -ForegroundColor Gray
    Write-Host ""
    
    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path = $ProjectPath
    $watcher.Filter = "*.cs"
    $watcher.IncludeSubdirectories = $true
    $watcher.EnableRaisingEvents = $true
    
    $action = {
        Write-Host "[检测到更改] 重新编译..." -ForegroundColor Yellow
        Build-Project -Config "Debug"
    }
    
    Register-ObjectEvent $watcher "Changed" -Action $action | Out-Null
    Register-ObjectEvent $watcher "Created" -Action $action | Out-Null
    Register-ObjectEvent $watcher "Renamed" -Action $action | Out-Null
    
    try {
        while ($true) {
            Start-Sleep -Seconds 1
        }
    } finally {
        $watcher.EnableRaisingEvents = $false
    }
}

# 主流程
try {
    if (-not (Test-DotNetSDK)) {
        exit 1
    }
    
    if ($Clean) {
        Clear-Build
        if ($Watch) {
            Write-Host ""
        }
    }
    
    Restore-Packages
    
    if ($Watch) {
        Build-Project -Config "Debug"
        Watch-FileChanges
    } else {
        Publish-SingleFile
        Show-Result
    }
    
    exit 0
} catch {
    Write-Error $_.Exception.Message
    Write-Host ""
    Write-Host "构建失败，请检查错误信息" -ForegroundColor Red
    exit 1
}
