# 发布指南

## 自动构建和发布

本项目已配置 GitHub Actions 工作流，支持自动打包并发布到 GitHub Releases。

### 发布步骤

#### 方法 1: 使用发布脚本（推荐）

在 Windows 系统上运行 PowerShell 脚本：

```powershell
# 创建新版本并推送
.\create-release.ps1 -CreateTag -Push
```

脚本会自动：
1. 生成下一个版本号（如 v1.0.1）
2. 创建 Git 标签
3. 推送到远程仓库
4. 触发 GitHub Actions 自动构建

#### 方法 2: 手动创建标签

```bash
# 创建版本标签
git tag -a v1.0.0 -m "Release v1.0.0"

# 推送标签
git push origin v1.0.0
```

#### 方法 3: 使用 GitHub Actions 手动触发

1. 访问：https://github.com/ZA12A230/Windows-bendi-ai-yuancheng/actions
2. 选择 "Build and Release" 工作流
3. 点击 "Run workflow"
4. 输入版本号（如 v1.0.0）
5. 点击运行按钮

### 构建产物

构建完成后，GitHub Releases 页面会自动包含以下文件：

- `LocalAIStudio-win-x64.zip` - 完整的发布包（包含所有依赖）
- `LocalAIStudio.exe` - 单个可执行文件

### 构建配置

GitHub Actions 使用以下配置进行构建：

- **平台**: Windows-latest (Windows Server 2022)
- **.NET 版本**: 8.0.x
- **发布模式**: 单文件、自包含、压缩
- **目标平台**: Windows 10/11 x64

### 手动本地构建

如果在本地 Windows 系统上构建，可以使用：

```powershell
# 清理构建
.\publish.ps1 -Clean

# 发布 Release 版本
.\publish.ps1

# 或运行 PowerShell 构建脚本
dotnet publish LocalAIStudio/LocalAIStudio.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -o ./publish
```

### 查看构建进度

访问 GitHub Actions 页面查看实时构建状态：
https://github.com/ZA12A230/Windows-bendi-ai-yuancheng/actions

### 版本命名规范

遵循语义化版本控制 (SemVer)：

- `v1.0.0` - 主版本号。重大更新，可能不兼容
- `v1.1.0` - 次版本号。新功能，向后兼容
- `v1.0.1` - 修订号。Bug 修复，向后兼容

## 下载和使用

用户可以从 GitHub Releases 页面下载最新版本：
https://github.com/ZA12A230/Windows-bendi-ai-yuancheng/releases

下载后无需安装，直接运行 `LocalAIStudio.exe` 即可使用。
