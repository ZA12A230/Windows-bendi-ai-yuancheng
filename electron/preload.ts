import { contextBridge, ipcRenderer } from 'electron';
import type { AIModel, SystemConfig, TunnelConfig } from '../shared/types';

contextBridge.exposeInMainWorld('electronAPI', {
  ai: {
    getModels: () => ipcRenderer.invoke('ai:get-models'),
    downloadModel: (modelId: string) => ipcRenderer.invoke('ai:download-model', modelId),
    cancelDownload: (modelId: string) => ipcRenderer.invoke('ai:cancel-download', modelId),
    installLMStudio: () => ipcRenderer.invoke('ai:install-lmstudio'),
    checkLMStudio: () => ipcRenderer.invoke('ai:check-lmstudio'),
    openLMStudio: () => ipcRenderer.invoke('ai:open-lmstudio'),
  },
  tunnel: {
    start: (config: TunnelConfig) => ipcRenderer.invoke('tunnel:start', config),
    stop: () => ipcRenderer.invoke('tunnel:stop'),
    getStatus: () => ipcRenderer.invoke('tunnel:status'),
    testConnection: (config: TunnelConfig) => ipcRenderer.invoke('tunnel:test-connection', config),
  },
  performance: {
    getStatus: () => ipcRenderer.invoke('performance:get-status'),
    setThreshold: (threshold: number) => ipcRenderer.invoke('performance:set-threshold', threshold),
    startMonitoring: () => ipcRenderer.invoke('performance:start-monitoring'),
    stopMonitoring: () => ipcRenderer.invoke('performance:stop-monitoring'),
  },
  system: {
    setAutoStart: (enable: boolean) => ipcRenderer.invoke('system:set-auto-start', enable),
    getAutoStart: () => ipcRenderer.invoke('system:get-auto-start'),
    setShutdownToSleep: (enable: boolean) => ipcRenderer.invoke('system:set-shutdown-to-sleep', enable),
    getShutdownToSleep: () => ipcRenderer.invoke('system:get-shutdown-to-sleep'),
  },
  config: {
    get: () => ipcRenderer.invoke('config:get'),
    set: (config: SystemConfig) => ipcRenderer.invoke('config:set', config),
  },
});

declare global {
  interface Window {
    electronAPI: {
      ai: {
        getModels: () => Promise<AIModel[]>;
        downloadModel: (modelId: string) => Promise<boolean>;
        cancelDownload: (modelId: string) => Promise<boolean>;
        installLMStudio: () => Promise<boolean>;
        checkLMStudio: () => Promise<boolean>;
        openLMStudio: () => Promise<boolean>;
      };
      tunnel: {
        start: (config: TunnelConfig) => Promise<boolean>;
        stop: () => Promise<boolean>;
        getStatus: () => Promise<{ 
          connected: boolean; 
          url: string | null;
          config: TunnelConfig | null;
        }>;
        testConnection: (config: TunnelConfig) => Promise<{ 
          success: boolean; 
          message: string;
        }>;
      };
      performance: {
        getStatus: () => Promise<{
          cpuUsage: number;
          memoryUsage: number;
          gpuUsage: number;
          threshold: number;
        }>;
        setThreshold: (threshold: number) => Promise<void>;
        startMonitoring: () => Promise<void>;
        stopMonitoring: () => Promise<void>;
      };
      system: {
        setAutoStart: (enable: boolean) => Promise<boolean>;
        getAutoStart: () => Promise<boolean>;
        setShutdownToSleep: (enable: boolean) => Promise<boolean>;
        getShutdownToSleep: () => Promise<boolean>;
      };
      config: {
        get: () => Promise<SystemConfig>;
        set: (config: SystemConfig) => Promise<boolean>;
      };
    };
  }
}
