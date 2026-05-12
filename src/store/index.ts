import { create } from 'zustand';
import { AIModel, SystemConfig, DEFAULT_CONFIG, AI_MODELS, TunnelConfig } from '../../shared/types';

interface AppState {
  models: AIModel[];
  config: SystemConfig;
  tunnelConfig: TunnelConfig;
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
  activeTab: 'home' | 'settings' | 'status';
  lmStudioInstalled: boolean;
  setModels: (models: AIModel[]) => void;
  updateModel: (modelId: string, updates: Partial<AIModel>) => void;
  setConfig: (config: SystemConfig) => void;
  setTunnelConfig: (config: TunnelConfig) => void;
  setPerformance: (perf: Partial<AppState['performance']>) => void;
  setTunnel: (tunnel: Partial<AppState['tunnel']>) => void;
  setActiveTab: (tab: 'home' | 'settings' | 'status') => void;
  setLmStudioInstalled: (installed: boolean) => void;
}

export const useAppStore = create<AppState>((set) => ({
  models: AI_MODELS,
  config: DEFAULT_CONFIG,
  tunnelConfig: DEFAULT_CONFIG.tunnelConfig,
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
  activeTab: 'home',
  lmStudioInstalled: false,

  setModels: (models) => set({ models }),
  
  updateModel: (modelId, updates) => set((state) => ({
    models: state.models.map((m) =>
      m.id === modelId ? { ...m, ...updates } : m
    ),
  })),
  
  setConfig: (config) => set({ config, tunnelConfig: config.tunnelConfig }),
  
  setTunnelConfig: (tunnelConfig) => set({ tunnelConfig }),
  
  setPerformance: (perf) => set((state) => ({
    performance: { ...state.performance, ...perf },
  })),
  
  setTunnel: (tunnel) => set((state) => ({
    tunnel: { ...state.tunnel, ...tunnel },
  })),
  
  setActiveTab: (tab) => set({ activeTab: tab }),
  
  setLmStudioInstalled: (installed) => set({ lmStudioInstalled: installed }),
}));
