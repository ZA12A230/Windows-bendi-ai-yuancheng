import React from 'react';
import { useAppStore } from '../store';
import { ModelCard } from '../components/ModelCard';

export const HomePage: React.FC = () => {
  const { models, tunnel, lmStudioInstalled } = useAppStore();
  const installedCount = models.filter(m => m.status === 'installed').length;

  const handleOpenLMStudio = async () => {
    if (window.electronAPI) {
      await window.electronAPI.ai.openLMStudio();
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold text-white mb-2">AI模型管理</h2>
          <p className="text-slate-400">
            {installedCount > 0 
              ? `已安装 ${installedCount}/${models.length} 个模型` 
              : '点击卡片即可下载模型到本地'}
          </p>
        </div>
        
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2 px-4 py-2 bg-slate-800/50 rounded-lg border border-slate-700">
            <div className={`w-2 h-2 rounded-full ${tunnel.connected ? 'bg-green-500 animate-pulse' : 'bg-slate-500'}`}></div>
            <span className="text-sm text-slate-300">
              {tunnel.connected ? '远程已连接' : '本地模式'}
            </span>
          </div>
          
          {installedCount > 0 && (
            <button
              onClick={handleOpenLMStudio}
              className="bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white px-5 py-2 rounded-lg font-medium transition-all duration-200 shadow-lg shadow-cyan-500/25 flex items-center gap-2"
            >
              <span>🚀</span>
              <span>打开LM Studio</span>
            </button>
          )}
        </div>
      </div>

      {!lmStudioInstalled && (
        <div className="bg-amber-500/10 border border-amber-500/30 rounded-xl p-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <span className="text-2xl">⚠️</span>
              <div>
                <p className="text-amber-400 font-medium">LM Studio 未安装</p>
                <p className="text-amber-300/70 text-sm">点击下方按钮下载安装LM Studio</p>
              </div>
            </div>
            <button
              onClick={async () => {
                if (window.electronAPI) {
                  await window.electronAPI.ai.installLMStudio();
                }
              }}
              className="bg-amber-600 hover:bg-amber-500 text-white px-4 py-2 rounded-lg font-medium transition-colors"
            >
              下载LM Studio
            </button>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-5">
        {models.map((model) => (
          <ModelCard key={model.id} model={model} />
        ))}
      </div>

      {installedCount > 0 && (
        <div className="bg-slate-800/50 backdrop-blur-sm rounded-xl p-6 border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
            <span>💡</span>
            使用提示
          </h3>
          <div className="space-y-2 text-slate-300 text-sm">
            <p>• 点击「打开LM Studio」启动AI服务</p>
            <p>• 在LM Studio中选择已下载的模型并加载</p>
            <p>• 点击左下角「Server」启用API服务</p>
            <p>• 启用内网穿透后，可通过外网地址远程访问</p>
          </div>
        </div>
      )}
    </div>
  );
};
