import { create } from 'zustand';
import { AIModel, SystemConfig, DEFAULT_CONFIG, AI_MODELS } from '../../shared/types';

interface AppState {
  models: AIModel[];
  config: SystemConfig;
  performance: {
    cpuUsage: number;
    memoryUsage: number;
    gpuUsage: number;
    threshold: number;
  };
  tunnel: {
    connected: boolean;
    url: string | null;
  };
  selectedModels: string[];
  activeTab: 'home' | 'settings' | 'status';
  setModels: (models: AIModel[]) => void;
  updateModel: (modelId: string, updates: Partial<AIModel>) => void;
  setConfig: (config: SystemConfig) => void;
  setPerformance: (perf: Partial<AppState['performance']>) => void;
  setTunnel: (tunnel: Partial<AppState['tunnel']>) => void;
  toggleModelSelection: (modelId: string) => void;
  clearSelection: () => void;
  setActiveTab: (tab: 'home' | 'settings' | 'status') => void;
}

export const useAppStore = create<AppState>((set, get) => ({
  models: AI_MODELS,
  config: DEFAULT_CONFIG,
  performance: {
    cpuUsage: 0,
    memoryUsage: 0,
    gpuUsage: 0,
    threshold: 80,
  },
  tunnel: {
    connected: false,
    url: null,
  },
  selectedModels: [],
  activeTab: 'home',

  setModels: (models) => set({ models }),
  
  updateModel: (modelId, updates) => set((state) => ({
    models: state.models.map((m) =>
      m.id === modelId ? { ...m, ...updates } : m
    ),
  })),
  
  setConfig: (config) => set({ config }),
  
  setPerformance: (perf) => set((state) => ({
    performance: { ...state.performance, ...perf },
  })),
  
  setTunnel: (tunnel) => set((state) => ({
    tunnel: { ...state.tunnel, ...tunnel },
  })),
  
  toggleModelSelection: (modelId) => set((state) => ({
    selectedModels: state.selectedModels.includes(modelId)
      ? state.selectedModels.filter((id) => id !== modelId)
      : [...state.selectedModels, modelId],
  })),
  
  clearSelection: () => set({ selectedModels: [] }),
  
  setActiveTab: (tab) => set({ activeTab: tab }),
}));
