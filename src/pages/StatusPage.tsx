import React from 'react';
import { useAppStore } from '../store';

export const StatusPage: React.FC = () => {
  const { performance, tunnel, models } = useAppStore();

  const getUsageColor = (usage: number) => {
    if (usage > 80) return 'text-red-400';
    if (usage > 60) return 'text-yellow-400';
    return 'text-green-400';
  };

  const getProgressColor = (usage: number) => {
    if (usage > 80) return 'from-red-500 to-red-600';
    if (usage > 60) return 'from-yellow-500 to-yellow-600';
    return 'from-green-500 to-green-600';
  };

  const installedModels = models.filter((m) => m.status === 'installed');
  const downloadingModels = models.filter((m) => m.status === 'downloading');

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-bold text-white mb-2">系统状态</h2>
        <p className="text-slate-400">实时监控系统资源和服务状态</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="bg-slate-800/50 backdrop-blur-sm rounded-xl p-6 border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-6 flex items-center gap-2">
            <span>💻</span>
            系统资源
          </h3>
          
          <div className="space-y-6">
            <div>
              <div className="flex justify-between mb-2">
                <p className="text-slate-300 font-medium">CPU 使用率</p>
                <p className={`font-semibold ${getUsageColor(performance.cpuUsage)}`}>
                  {performance.cpuUsage}%
                </p>
              </div>
              <div className="h-3 bg-slate-700 rounded-full overflow-hidden">
                <div
                  className={`h-full bg-gradient-to-r ${getProgressColor(performance.cpuUsage)} transition-all duration-500`}
                  style={{ width: `${performance.cpuUsage}%` }}
                ></div>
              </div>
            </div>

            <div>
              <div className="flex justify-between mb-2">
                <p className="text-slate-300 font-medium">内存使用率</p>
                <p className={`font-semibold ${getUsageColor(performance.memoryUsage)}`}>
                  {performance.memoryUsage}%
                </p>
              </div>
              <div className="h-3 bg-slate-700 rounded-full overflow-hidden">
                <div
                  className={`h-full bg-gradient-to-r ${getProgressColor(performance.memoryUsage)} transition-all duration-500`}
                  style={{ width: `${performance.memoryUsage}%` }}
                ></div>
              </div>
            </div>

            <div>
              <div className="flex justify-between mb-2">
                <p className="text-slate-300 font-medium">GPU 使用率</p>
                <p className={`font-semibold ${getUsageColor(performance.gpuUsage)}`}>
                  {performance.gpuUsage}%
                </p>
              </div>
              <div className="h-3 bg-slate-700 rounded-full overflow-hidden">
                <div
                  className={`h-full bg-gradient-to-r ${getProgressColor(performance.gpuUsage)} transition-all duration-500`}
                  style={{ width: `${performance.gpuUsage}%` }}
                ></div>
              </div>
            </div>

            <div className="pt-4 border-t border-slate-700">
              <div className="flex items-center justify-between">
                <p className="text-slate-300 font-medium">性能阈值</p>
                <p className="text-cyan-400 font-semibold">{performance.threshold}%</p>
              </div>
              <p className="text-slate-500 text-sm mt-1">
                超过此值将自动降低AI资源占用
              </p>
            </div>
          </div>
        </div>

        <div className="bg-slate-800/50 backdrop-blur-sm rounded-xl p-6 border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-6 flex items-center gap-2">
            <span>🌐</span>
            内网穿透
          </h3>
          
          <div className="space-y-4">
            <div className="flex items-center gap-3 p-4 bg-slate-700/50 rounded-lg">
              <div className={`w-4 h-4 rounded-full ${tunnel.connected ? 'bg-green-500 animate-pulse' : 'bg-slate-500'}`}></div>
              <div>
                <p className="text-white font-medium">
                  {tunnel.connected ? '连接成功' : '未连接'}
                </p>
                <p className="text-slate-400 text-sm">
                  {tunnel.connected ? '外网访问已启用' : '等待连接...'}
                </p>
              </div>
            </div>

            {tunnel.connected && tunnel.url && (
              <div className="p-4 bg-slate-700/50 rounded-lg">
                <p className="text-slate-400 text-sm mb-2">访问地址</p>
                <div className="flex items-center gap-2">
                  <code className="flex-1 bg-slate-800 text-cyan-400 px-3 py-2 rounded text-sm font-mono break-all">
                    {tunnel.url}
                  </code>
                  <button
                    onClick={() => navigator.clipboard.writeText(tunnel.url!)}
                    className="bg-cyan-600 hover:bg-cyan-500 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors"
                  >
                    复制
                  </button>
                </div>
              </div>
            )}

            {!tunnel.connected && (
              <div className="p-4 bg-slate-700/30 rounded-lg border border-dashed border-slate-600">
                <p className="text-slate-400 text-sm text-center">
                  在设置页面启用内网穿透功能
                </p>
              </div>
            )}
          </div>
        </div>

        <div className="bg-slate-800/50 backdrop-blur-sm rounded-xl p-6 border border-slate-700 lg:col-span-2">
          <h3 className="text-lg font-semibold text-white mb-6 flex items-center gap-2">
            <span>📦</span>
            模型状态
          </h3>
          
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="bg-slate-700/50 rounded-lg p-4 text-center">
              <p className="text-3xl font-bold text-green-400 mb-1">{installedModels.length}</p>
              <p className="text-slate-400">已安装</p>
            </div>
            <div className="bg-slate-700/50 rounded-lg p-4 text-center">
              <p className="text-3xl font-bold text-cyan-400 mb-1">{downloadingModels.length}</p>
              <p className="text-slate-400">下载中</p>
            </div>
            <div className="bg-slate-700/50 rounded-lg p-4 text-center">
              <p className="text-3xl font-bold text-slate-400 mb-1">{models.length}</p>
              <p className="text-slate-400">总模型</p>
            </div>
          </div>

          {installedModels.length > 0 && (
            <div className="mt-6 pt-6 border-t border-slate-700">
              <p className="text-slate-300 font-medium mb-3">已安装模型</p>
              <div className="flex flex-wrap gap-2">
                {installedModels.map((model) => (
                  <span
                    key={model.id}
                    className="bg-green-600/20 text-green-400 px-3 py-1 rounded-full text-sm font-medium"
                  >
                    {model.name}
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
