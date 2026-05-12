export interface AIModel {
  id: string;
  name: string;
  version: string;
  description: string;
  downloadUrl: string;
  size: string;
  status: 'idle' | 'downloading' | 'installed' | 'error';
  progress: number;
}

export interface TunnelConfig {
  serverAddress: string;
  serverPort: string;
  authUsername: string;
  authPassword: string;
  localPort: string;
}

export interface SystemConfig {
  autoStart: boolean;
  runInBackground: boolean;
  enableTunnel: boolean;
  performanceThreshold: number;
  shutdownToSleep: boolean;
  tunnelConfig: TunnelConfig;
}

export interface SystemStatus {
  cpuUsage: number;
  memoryUsage: number;
  gpuUsage: number;
  tunnelUrl: string | null;
  tunnelConnected: boolean;
}

export const AI_MODELS: AIModel[] = [
  {
    id: 'gpt-3.5-turbo',
    name: 'GPT-3.5-Turbo',
    version: '3.5',
    description: 'OpenAI快速响应模型，适合日常对话和简单任务',
    downloadUrl: 'lm-studio://gpt-3.5-turbo',
    size: '4GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'llama-3.1-8b',
    name: 'Llama 3.1 8B',
    version: '3.1',
    description: 'Meta开源大模型，性能优秀，支持中文',
    downloadUrl: 'lm-studio://llama-3.1-8b',
    size: '4.7GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'qwen-2.5-7b',
    name: 'Qwen 2.5 7B',
    version: '2.5',
    description: '阿里通义千问，中文理解能力强',
    downloadUrl: 'lm-studio://qwen-2.5-7b',
    size: '4.4GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'phi-3.5-mini',
    name: 'Phi-3.5 Mini',
    version: '3.5',
    description: '微软小模型，体积小但能力不错',
    downloadUrl: 'lm-studio://phi-3.5-mini',
    size: '2.2GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'mistral-7b',
    name: 'Mistral 7B',
    version: '7B',
    description: '欧洲顶级开源模型，性能卓越',
    downloadUrl: 'lm-studio://mistral-7b',
    size: '4.1GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'codellama-7b',
    name: 'CodeLlama 7B',
    version: '7B',
    description: '代码专用模型，编程辅助利器',
    downloadUrl: 'lm-studio://codellama-7b',
    size: '3.8GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'deepseek-coder-6.7b',
    name: 'DeepSeek Coder 6.7B',
    version: '6.7B',
    description: '深度求索代码模型，代码能力强',
    downloadUrl: 'lm-studio://deepseek-coder-6.7b',
    size: '3.6GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'yi-1.5-6b',
    name: 'Yi-1.5 6B',
    version: '1.5',
    description: '零一万物模型，中英双语优秀',
    downloadUrl: 'lm-studio://yi-1.5-6b',
    size: '3.5GB',
    status: 'idle',
    progress: 0,
  },
];

export const DEFAULT_TUNNEL_CONFIG: TunnelConfig = {
  serverAddress: 'frp.example.com',
  serverPort: '7000',
  authUsername: '',
  authPassword: '',
  localPort: '1234',
};

export const DEFAULT_CONFIG: SystemConfig = {
  autoStart: false,
  runInBackground: true,
  enableTunnel: false,
  performanceThreshold: 80,
  shutdownToSleep: false,
  tunnelConfig: DEFAULT_TUNNEL_CONFIG,
};
