import React, { useEffect, useState } from 'react';
import { useAppStore } from './store';
import { HomePage } from './pages/HomePage';
import { SettingsPage } from './pages/SettingsPage';
import { StatusPage } from './pages/StatusPage';
import { Sidebar } from './components/Sidebar';
import { AI_MODELS } from '../shared/types';

function App() {
  const {
    activeTab,
    setModels,
    setConfig,
    setPerformance,
    setTunnel,
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
        const [models, config, perf, tunnelStatus] = await Promise.all([
          window.electronAPI.ai.getModels(),
          window.electronAPI.config.get(),
          window.electronAPI.performance.getStatus(),
          window.electronAPI.tunnel.getStatus(),
        ]);
        
        setModels(models.length > 0 ? models : AI_MODELS);
        setConfig(config);
        setPerformance(perf);
        setTunnel(tunnelStatus);
      }
    } catch (error) {
      console.error('初始化应用失败:', error);
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
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-900 to-slate-800">
        <div className="text-center">
          <div className="w-16 h-16 border-4 border-cyan-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <p className="text-slate-300 text-lg">正在加载...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex">
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
