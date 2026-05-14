# Windows 本地 AI 远程助手

一个免安装的 Windows 本地 AI 助手，集成 Ollama 实现离线 AI 对话、系统监控、远程桌面控制等功能。

## 功能特性

- 自动检测并安装 Ollama
- 一键下载本地 AI 模型
- 系统资源实时监控（CPU/GPU/内存/磁盘/网络）
- AI 对话界面
- 远程桌面控制
- 内网穿透支持
- 开机自启/静默启动/自适应模式
- 关机键替换为息屏功能
- 磁盘清理与软件强力卸载

## 快速开始

### 开发环境运行

```bash
# 前端
cd frontend
npm install
npm run dev

# 后端
cd ..
go run ./cmd
```

### 构建 Windows 程序

```bash
cd frontend && npm run build && cd ..
GOOS=windows go build -o build/local-ai-assistant.exe ./cmd
```

## 项目结构

```
local-ai-assistant/
├── cmd/                    # 主程序入口
│   ├── main.go            # HTTP 服务器和路由
│   └── dist/              # 前端构建文件
├── frontend/              # React 前端
│   ├── src/
│   │   ├── pages/         # 页面组件
│   │   └── components/    # 通用组件
│   └── vite.config.js
├── internal/              # 内部包
│   ├── config/           # 配置管理
│   ├── ollama/           # Ollama API 封装
│   ├── system/           # 系统操作
│   ├── network/          # 内网穿透
│   └── remote/           # 远程桌面
└── build/                # 编译输出
```

## 技术栈

- 后端：Go + net/http
- 前端：React + Vite
- 系统监控：gopsutil
- WebSocket：gorilla/websocket

## 许可证

MIT
