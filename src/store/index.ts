import { create } from 'zustand';
import { 
  AppStage, 
  AIModel, 
  AI_MODELS, 
  SystemConfig, 
  DEFAULT_SYSTEM_CONFIG,
  NetworkConfig, 
  DEFAULT_NETWORK_CONFIG,
  AIConfig, 
  DEFAULT_AI_CONFIG,
  ShutdownConfig,
  DEFAULT_SHUTDOWN_CONFIG,
  SystemStatus,
  DiskInfo,
  DiskFile
} from '../../shared/types';

interface AppState {
  currentStage: AppStage;
  models: AIModel[];
  systemConfig: SystemConfig;
  networkConfig: NetworkConfig;
  aiConfig: AIConfig;
  shutdownConfig: ShutdownConfig;
  systemStatus: SystemStatus;
  diskInfo: DiskInfo[];
  diskFiles: DiskFile[];
  isEmergencyStopped: boolean;
  aiStartTime: number;
  setCurrentStage: (stage: AppStage) => void;
  setModels: (models: AIModel[]) => void;
  updateModel: (id: string, updates: Partial<AIModel>) => void;
  selectModel: (id: string, selected: boolean) => void;
  selectAllModels: (selected: boolean) => void;
  selectModelsByIndices: (indices: number[]) => void;
  setSystemConfig: (config: SystemConfig) => void;
  setNetworkConfig: (config: NetworkConfig) => void;
  setAIConfig: (config: AIConfig) => void;
  setShutdownConfig: (config: ShutdownConfig) => void;
  setSystemStatus: (status: SystemStatus) => void;
  setDiskInfo: (info: DiskInfo[]) => void;
  setDiskFiles: (files: DiskFile[]) => void;
  toggleDiskFile: (path: string) => void;
  selectAllDiskFiles: (selected: boolean) => void;
  emergencyStop: () => void;
  resetEmergency: () => void;
  setAIStartTime: (time: number) => void;
}

const initialStatus: SystemStatus = {
  cpuUsage: 0,
  memoryUsage: 0,
  gpuUsage: 0,
  diskUsage: 0,
  cpuTemp: 35,
  gpuTemp: 35,
  diskTemp: 30,
  uptime: 0,
  aiRunTime: 0,
  aiCpuUsage: 0,
  aiMemoryUsage: 0,
  uploadSpeed: 0,
  downloadSpeed: 0,
};

export const useAppStore = create<AppState>((set, get) => ({
  currentStage: 'checking_ollama',
  models: AI_MODELS,
  systemConfig: DEFAULT_SYSTEM_CONFIG,
  networkConfig: DEFAULT_NETWORK_CONFIG,
  aiConfig: DEFAULT_AI_CONFIG,
  shutdownConfig: DEFAULT_SHUTDOWN_CONFIG,
  systemStatus: initialStatus,
  diskInfo: [],
  diskFiles: [],
  isEmergencyStopped: false,
  aiStartTime: Date.now(),
  setCurrentStage: (stage: AppStage) => set({ currentStage: stage }),
  setModels: (models: AIModel[]) => set({ models }),
  updateModel: (id: string, updates: Partial<AIModel>) => 
    set(state => ({
      models: state.models.map(m => m.id === id ? { ...m, ...updates } : m)
    })),
  selectModel: (id: string, selected: boolean) => 
    set(state => ({
      models: state.models.map(m => m.id === id ? { ...m, isSelected: selected } : m)
    })),
  selectAllModels: (selected: boolean) => 
    set(state => ({
      models: state.models.map(m => ({ ...m, isSelected: selected }))
    })),
  selectModelsByIndices: (indices: number[]) => 
    set(state => ({
      models: state.models.map((m, i) => ({ 
        ...m, 
        isSelected: indices.includes(i + 1)
      }))
    })),
  setSystemConfig: (config) => set({ systemConfig: config }),
  setNetworkConfig: (config) => set({ networkConfig: config }),
  setAIConfig: (config) => set({ aiConfig: config }),
  setShutdownConfig: (config) => set({ shutdownConfig: config }),
  setSystemStatus: (status) => set({ systemStatus: status }),
  setDiskInfo: (info) => set({ diskInfo: info }),
  setDiskFiles: (files) => set({ diskFiles: files }),
  toggleDiskFile: (path) => 
    set(state => ({
      diskFiles: state.diskFiles.map(f => f.path === path ? { ...f, isSelected: !f.isSelected } : f)
    })),
  selectAllDiskFiles: (selected) => 
    set(state => ({
      diskFiles: state.diskFiles.map(f => ({ ...f, isSelected: selected }))
    })),
  emergencyStop: () => set({ isEmergencyStopped: true }),
  resetEmergency: () => set({ isEmergencyStopped: false }),
  setAIStartTime: (time) => set({ aiStartTime: time }),
}));
