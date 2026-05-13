export type AppStage = 
  | 'checking_ollama' 
  | 'installing_ollama' 
  | 'checking_models' 
  | 'selecting_models' 
  | 'downloading_models'
  | 'disk_cleanup'
  | 'network_config'
  | 'ai_config'
  | 'shutdown_config'
  | 'dashboard';

export interface AIModel {
  id: string;
  name: string;
  version: string;
  description: string;
  sizeGB: number;
  downloadUrl: string;
  isInstalled: boolean;
  isDownloading: boolean;
  downloadProgress: number;
  isSelected: boolean;
  orderIndex: number;
}

export interface NetworkConfig {
  enableTunnel: boolean;
  tunnelServerAddress: string;
  tunnelPort: string;
  tunnelUsername: string;
  tunnelPassword: string;
  enableIPv4: boolean;
  ipv4Address: string;
  ipv4Username: string;
  ipv4Password: string;
  localPort: string;
}

export interface AIConfig {
  role: string;
  personality: string;
  temperature: number;
  maxTokens: number;
  contextLength: number;
}

export interface ShutdownConfig {
  enabled: boolean;
  showShutdownButton: boolean;
  requirePowerButton: boolean;
}

export interface SystemConfig {
  autoStart: boolean;
  lastStage: AppStage;
  lastState: any;
  performanceThreshold: number;
}

export interface DiskInfo {
  drive: string;
  totalGB: number;
  usedGB: number;
  freeGB: number;
  usedPercent: number;
}

export interface DiskFile {
  path: string;
  size: number;
  sizeText: string;
  isSystem: boolean;
  isUnused: boolean;
  isSelected: boolean;
}

export interface SystemStatus {
  cpuUsage: number;
  memoryUsage: number;
  gpuUsage: number;
  diskUsage: number;
  cpuTemp: number;
  gpuTemp: number;
  diskTemp: number;
  uptime: number;
  aiRunTime: number;
  aiCpuUsage: number;
  aiMemoryUsage: number;
  uploadSpeed: number;
  downloadSpeed: number;
}

export interface DashboardData {
  status: SystemStatus;
  history: {
    cpu: number[];
    memory: number[];
    aiCpu: number[];
    aiMemory: number[];
    timestamps: string[];
  };
  runningModels: AIModel[];
  tunnelUrl: string | null;
  ipv4Url: string | null;
}

export const AI_MODELS: AIModel[] = [
  { id: 'gpt-3.5', name: 'GPT-3.5', version: '3.5', description: 'OpenAI对话模型，响应快速', sizeGB: 4, downloadUrl: 'ollama:llama3', isInstalled: false, isDownloading: false, downloadProgress: 0, isSelected: false, orderIndex: 1 },
  { id: 'claude', name: 'Claude', version: '3', description: 'Anthropic Claude模型，推理能力强', sizeGB: 16, downloadUrl: 'ollama:claude', isInstalled: false, isDownloading: false, downloadProgress: 0, isSelected: false, orderIndex: 2 },
  { id: 'gemma', name: 'Gemini (Gemma)', version: '7B', description: 'Google Google模型，多模态能力', sizeGB: 8, downloadUrl: 'ollama:gemma', isInstalled: false, isDownloading: false, downloadProgress: 0, isSelected: false, orderIndex: 3 },
  { id: 'doubao', name: '豆包', version: '最新', description: '字节跳动豆包模型，中文优秀', sizeGB: 10, downloadUrl: 'ollama:qwen', isInstalled: false, isDownloading: false, downloadProgress: 0, isSelected: false, orderIndex: 4 },
  { id: 'xfyun', name: '科大讯飞', version: '星火', description: '科大讯飞模型，语音+文本', sizeGB: 12, downloadUrl: 'ollama:llama3:chinese', isInstalled: false, isDownloading: false, downloadProgress: 0, isSelected: false, orderIndex: 5 },
  { id: 'deepseek', name: 'DeepSeek', version: 'Coder', description: '深度求索代码模型，编程助手', sizeGB: 18, downloadUrl: 'ollama:deepseek-coder', isInstalled: false, isDownloading: false, downloadProgress: 0, isSelected: false, orderIndex: 6 },
  { id: 'qwen', name: '千问', version: 'Qwen 14B', description: '阿里通义千问，多能力通用', sizeGB: 14, downloadUrl: 'ollama:qwen:14b', isInstalled: false, isDownloading: false, downloadProgress: 0, isSelected: false, orderIndex: 7 },
  { id: 'glm', name: 'GLM', version: 'GLM-4', description: '智谱AI模型，中英文都好', sizeGB: 20, downloadUrl: 'ollama:glm4', isInstalled: false, isDownloading: false, downloadProgress: 0, isSelected: false, orderIndex: 8 },
];

export const DEFAULT_NETWORK_CONFIG: NetworkConfig = {
  enableTunnel: true,
  tunnelServerAddress: 'frp.example.com',
  tunnelPort: '7000',
  tunnelUsername: '',
  tunnelPassword: '',
  enableIPv4: true,
  ipv4Address: '',
  ipv4Username: '',
  ipv4Password: '',
  localPort: '11434',
};

export const DEFAULT_AI_CONFIG: AIConfig = {
  role: 'assistant',
  personality: 'friendly',
  temperature: 0.7,
  maxTokens: 2048,
  contextLength: 4096,
};

export const DEFAULT_SHUTDOWN_CONFIG: ShutdownConfig = {
  enabled: true,
  showShutdownButton: true,
  requirePowerButton: true,
};

export const DEFAULT_SYSTEM_CONFIG: SystemConfig = {
  autoStart: true,
  lastStage: 'dashboard',
  lastState: null,
  performanceThreshold: 85,
};
