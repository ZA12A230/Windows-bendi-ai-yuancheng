import React, { useEffect, useState } from 'react';
import { useAppStore } from '../store';

export const SettingsPage: React.FC = () => {
  const { config, setConfig, setPerformance } = useAppStore();
  const [localConfig, setLocalConfig] = useState(config);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setLocalConfig(config);
  }, [config]);

  const handleSave = async () => {
    setIsSaving(true);
    try {
      if (window.electronAPI) {
        await Promise.all([
          window.electronAPI.config.set(localConfig),
          window.electronAPI.system.setAutoStart(localConfig.autoStart),
          window.electronAPI.system.setShutdownToSleep(localConfig.shutdownToSleep),
          window.electronAPI.performance.setThreshold(localConfig.performanceThreshold),
          localConfig.enableTunnel
            ? window.electronAPI.tunnel.start()
            : window.electronAPI.tunnel.stop(),
        ]);
        setConfig(localConfig);
      }
    } catch (error) {
      console.error('保存设置失败:', error);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-bold text-white mb-2">设置</h2>
        <p className="text-slate-400">配置应用的运行参数</p>
      </div>

      <div className="grid gap-6">
        <div className="bg-slate-800/50 backdrop-blur-sm rounded-xl p-6 border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
            <span>🚀</span>
            常规设置
          </h3>
          
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-white font-medium">开机自启动</p>
                <p className="text-slate-400 text-sm">系统启动时自动运行应用</p>
              </div>
              <label className="relative inline-flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  checked={localConfig.autoStart}
                  onChange={(e) => setLocalConfig({ ...localConfig, autoStart: e.target.checked })}
                  className="sr-only peer"
                />
                <div className="w-14 h-7 bg-slate-700 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-cyan-500/25 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-0.5 after:left-[4px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-6 after:w-6 after:transition-all peer-checked:bg-cyan-600"></div>
              </label>
            </div>

            <div className="flex items-center justify-between">
              <div>
                <p className="text-white font-medium">后台运行</p>
                <p className="text-slate-400 text-sm">关闭窗口时最小化到系统托盘</p>
              </div>
              <label className="relative inline-flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  checked={localConfig.runInBackground}
                  onChange={(e) => setLocalConfig({ ...localConfig, runInBackground: e.target.checked })}
                  className="sr-only peer"
                />
                <div className="w-14 h-7 bg-slate-700 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-cyan-500/25 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-0.5 after:left-[4px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-6 after:w-6 after:transition-all peer-checked:bg-cyan-600"></div>
              </label>
            </div>

            <div className="flex items-center justify-between">
              <div>
                <p className="text-white font-medium">启用内网穿透</p>
                <p className="text-slate-400 text-sm">允许从外网访问本地AI服务</p>
              </div>
              <label className="relative inline-flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  checked={localConfig.enableTunnel}
                  onChange={(e) => setLocalConfig({ ...localConfig, enableTunnel: e.target.checked })}
                  className="sr-only peer"
                />
                <div className="w-14 h-7 bg-slate-700 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-cyan-500/25 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-0.5 after:left-[4px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-6 after:w-6 after:transition-all peer-checked:bg-cyan-600"></div>
              </label>
            </div>
          </div>
        </div>

        <div className="bg-slate-800/50 backdrop-blur-sm rounded-xl p-6 border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
            <span>⚡</span>
            性能设置
          </h3>
          
          <div className="space-y-6">
            <div>
              <div className="flex justify-between mb-2">
                <p className="text-white font-medium">性能阈值</p>
                <p className="text-cyan-400 font-semibold">{localConfig.performanceThreshold}%</p>
              </div>
              <p className="text-slate-400 text-sm mb-4">
                当系统资源使用率超过此阈值时，自动降低AI模型的资源占用
              </p>
              <input
                type="range"
                min="50"
                max="95"
                value={localConfig.performanceThreshold}
                onChange={(e) => setLocalConfig({ ...localConfig, performanceThreshold: parseInt(e.target.value) })}
                className="w-full h-2 bg-slate-700 rounded-lg appearance-none cursor-pointer accent-cyan-500"
              />
              <div className="flex justify-between text-xs text-slate-500 mt-2">
                <span>50%</span>
                <span>95%</span>
              </div>
            </div>
          </div>
        </div>

        <div className="bg-slate-800/50 backdrop-blur-sm rounded-xl p-6 border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
            <span>💤</span>
            电源设置
          </h3>
          
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-white font-medium">关机键改为息屏</p>
                <p className="text-slate-400 text-sm">点击开始菜单关机键时只息屏，真正关机需要按电源键</p>
              </div>
              <label className="relative inline-flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  checked={localConfig.shutdownToSleep}
                  onChange={(e) => setLocalConfig({ ...localConfig, shutdownToSleep: e.target.checked })}
                  className="sr-only peer"
                />
                <div className="w-14 h-7 bg-slate-700 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-cyan-500/25 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-0.5 after:left-[4px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-6 after:w-6 after:transition-all peer-checked:bg-cyan-600"></div>
              </label>
            </div>
          </div>
        </div>

        <div className="flex justify-end">
          <button
            onClick={handleSave}
            disabled={isSaving}
            className="bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 disabled:opacity-50 disabled:cursor-not-allowed text-white px-8 py-3 rounded-lg font-semibold transition-all duration-200 shadow-lg shadow-cyan-500/25"
          >
            {isSaving ? '保存中...' : '保存设置'}
          </button>
        </div>
      </div>
    </div>
  );
};
