# AI 本地部署工具箱

一个功能强大的Windows桌面应用程序，用于自动下载、部署和管理本地AI模型，同时提供内网穿透和系统优化功能。

## ✨ 主要功能

### 🤖 AI模型管理
- 支持多种主流AI模型：
  - GPT 5.5
  - Claude Opus 4.7
  - Gemini 最新版
  - DeepSeek R1
  - 豆包 最新版
- 单个或批量下载模型
- 实时显示下载进度
- 自动部署到本地运行环境（基于Ollama）

### 🌐 内网穿透
- 自动配置内网穿透
- 提供公网访问地址
- 支持从非局域网远程访问
- 实时连接状态监控

### ⚡ 性能监控
- 实时监控CPU、内存、GPU使用率
- 可配置性能阈值
- 系统负载过高时自动降低AI资源占用
- 性能恢复后自动恢复

### 💻 Windows系统优化
- 开机自启动
- 后台运行（最小化到系统托盘）
- 修改开始菜单关机键为息屏
- 真正关机需要按电源键

## 🛠️ 技术栈

- **前端框架**: React 18 + TypeScript
- **桌面框架**: Electron
- **状态管理**: Zustand
- **样式方案**: Tailwind CSS
- **构建工具**: Vite
- **系统监控**: systeminformation
- **配置存储**: electron-store
- **自动启动**: auto-launch

## 📁 项目结构

```
/workspace
├── src/                      # 前端源码
│   ├── components/          # React组件
│   │   ├── Sidebar.tsx      # 侧边栏
│   │   └── ModelCard.tsx    # 模型卡片
│   ├── pages/              # 页面组件
│   │   ├── HomePage.tsx    # 首页/模型管理
│   │   ├── SettingsPage.tsx# 设置页面
│   │   └── StatusPage.tsx  # 系统状态
│   ├── store/              # Zustand状态管理
│   ├── App.tsx             # 主应用组件
│   ├── main.tsx            # 入口文件
│   └── index.css           # 全局样式
├── electron/               # Electron主进程
│   ├── main.ts            # 主进程入口
│   ├── preload.ts         # 预加载脚本
│   └── modules/           # 核心模块
│       ├── ai-manager.ts  # AI管理器
│       ├── tunnel.ts      # 内网穿透
│       ├── performance.ts # 性能监控
│       └── system.ts      # 系统管理
├── shared/                # 共享类型定义
│   └── types.ts
├── .trae/documents/       # 项目文档
│   ├── prd.md            # 产品需求文档
│   └── arch.md           # 技术架构文档
├── package.json
├── tsconfig.json
├── vite.config.ts
└── tailwind.config.js
```

## 🚀 快速开始

### 安装依赖

```bash
npm install
```

### 开发模式

```bash
npm run dev
```

### 构建应用

```bash
npm run build
npm run electron:build
```

### 仅启动Electron（开发模式）

```bash
npm run electron:dev
```

## 📖 使用说明

### 1. 模型管理
- 在"模型管理"页面浏览可用的AI模型
- 勾选需要的模型（支持多选）
- 点击"下载"按钮开始下载
- 等待下载和自动部署完成

### 2. 系统设置
- 进入"设置"页面
- 配置开机自启动、后台运行
- 启用内网穿透
- 调整性能阈值
- 配置关机键行为
- 点击"保存设置"

### 3. 系统监控
- 在"系统状态"页面查看实时资源使用
- 查看内网穿透连接状态和访问地址
- 查看已安装的模型列表

## 🔧 配置项

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| autoStart | 开机自启动 | false |
| runInBackground | 后台运行 | true |
| enableTunnel | 启用内网穿透 | true |
| performanceThreshold | 性能阈值 | 80% |
| shutdownToSleep | 关机键改息屏 | false |

## 📄 文档

- [产品需求文档 (PRD)](file:///workspace/.trae/documents/prd.md)
- [技术架构文档](file:///workspace/.trae/documents/arch.md)

## ⚠️ 注意事项

1. **AI模型下载**: 本项目使用Ollama作为本地AI运行时，需要先安装Ollama
2. **内网穿透**: 需要配置frp或其他内网穿透服务
3. **系统权限**: 修改关机键行为需要管理员权限
4. **资源占用**: AI模型占用较多系统资源，请确保硬件配置足够

## 🤝 贡献

欢迎提交Issue和Pull Request！

## 📝 许可证

MIT License
