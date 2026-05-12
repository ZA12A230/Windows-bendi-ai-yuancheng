import React, { useEffect, useState } from 'react';
import { useAppStore } from './store';
import { HomePage } from './pages/HomePage';
import { SettingsPage } from './pages/SettingsPage';
import { StatusPage } from './pages/StatusPage';
import { Sidebar } from './components/Sidebar';
import { AI_MODELS, DEFAULT_CONFIG } from '../shared/types';

function App() {
  const {
    activeTab,
    setModels,
    setConfig,
    setPerformance,
    setTunnel,
    setLmStudioInstalled,
  } = useAppStore();
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    initializeApp();
  }, []);

  useEffect(() => {
    const interval = setInterval(refreshData, 2000);
    return () => clearInterval(interval);
  }, []);

  const initializeApp = async () => {
    try {
      if (window.electronAPI) {
        const [config, perf, tunnelStatus, lmStudioInstalled] = await Promise.all([
          window.electronAPI.config.get(),
          window.electronAPI.performance.getStatus(),
          window.electronAPI.tunnel.getStatus(),
          window.electronAPI.ai.checkLMStudio(),
        ]);
        
        setConfig(config);
        setModels(AI_MODELS);
        setPerformance(perf);
        setTunnel(tunnelStatus);
        setLmStudioInstalled(lmStudioInstalled);
      }
    } catch (error) {
      console.error('初始化应用失败:', error);
      setConfig(DEFAULT_CONFIG);
      setModels(AI_MODELS);
    } finally {
      setIsLoading(false);
    }
  };

  const refreshData = async () => {
    try {
      if (window.electronAPI) {
        const [perf, tunnelStatus] = await Promise.all([
          window.electronAPI.performance.getStatus(),
          window.electronAPI.tunnel.getStatus(),
        ]);
        setPerformance(perf);
        setTunnel(tunnelStatus);
      }
    } catch (error) {
      console.error('刷新数据失败:', error);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900">
        <div className="text-center">
          <div className="w-20 h-20 border-4 border-cyan-500 border-t-transparent rounded-full animate-spin mx-auto mb-6"></div>
          <p className="text-slate-300 text-lg font-medium">正在加载 AI 本地部署工具箱...</p>
          <p className="text-slate-500 text-sm mt-2">初始化系统组件</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900">
      <Sidebar />
      <main className="flex-1 p-6 overflow-auto">
        {activeTab === 'home' && <HomePage />}
        {activeTab === 'settings' && <SettingsPage />}
        {activeTab === 'status' && <StatusPage />}
      </main>
    </div>
  );
}

export default App;
