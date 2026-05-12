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

export interface SystemConfig {
  autoStart: boolean;
  runInBackground: boolean;
  enableTunnel: boolean;
  performanceThreshold: number;
  shutdownToSleep: boolean;
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
    id: 'gpt5.5',
    name: 'GPT 5.5',
    version: '5.5',
    description: 'OpenAI最新大语言模型，具备强大的推理和生成能力',
    downloadUrl: 'https://ollama.com/library/gpt5.5',
    size: '24GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'claude-opus4.7',
    name: 'Claude Opus 4.7',
    version: '4.7',
    description: 'Anthropic最高级模型，在复杂任务上表现出色',
    downloadUrl: 'https://ollama.com/library/claude-opus',
    size: '18GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'gemini-latest',
    name: 'Gemini 最新版',
    version: 'Latest',
    description: 'Google多模态AI模型，支持图像和文本理解',
    downloadUrl: 'https://ollama.com/library/gemini',
    size: '20GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'deepseek-r1',
    name: 'DeepSeek R1',
    version: 'R1',
    description: '深度求索推理模型，数学和代码能力强',
    downloadUrl: 'https://ollama.com/library/deepseek-r1',
    size: '16GB',
    status: 'idle',
    progress: 0,
  },
  {
    id: 'doubao-latest',
    name: '豆包 最新版',
    version: 'Latest',
    description: '字节跳动AI模型，中文理解和生成能力优秀',
    downloadUrl: 'https://ollama.com/library/doubao',
    size: '12GB',
    status: 'idle',
    progress: 0,
  },
];

export const DEFAULT_CONFIG: SystemConfig = {
  autoStart: false,
  runInBackground: true,
  enableTunnel: true,
  performanceThreshold: 80,
  shutdownToSleep: false,
};
