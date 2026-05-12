import React from 'react';
import { AIModel } from '../../shared/types';
import { useAppStore } from '../store';

interface ModelCardProps {
  model: AIModel;
}

export const ModelCard: React.FC<ModelCardProps> = ({ model }) => {
  const { updateModel } = useAppStore();

  const getStatusColor = (status: AIModel['status']) => {
    switch (status) {
      case 'installed':
        return 'bg-green-500';
      case 'downloading':
        return 'bg-cyan-500';
      case 'error':
        return 'bg-red-500';
      default:
        return 'bg-slate-500';
    }
  };

  const getStatusText = (status: AIModel['status']) => {
    switch (status) {
      case 'installed':
        return '已安装';
      case 'downloading':
        return '下载中';
      case 'error':
        return '失败';
      default:
        return '待下载';
    }
  };

  const handleDownload = async () => {
    if (model.status === 'downloading') return;
    
    if (window.electronAPI) {
      updateModel(model.id, { status: 'downloading', progress: 0 });
      
      const success = await window.electronAPI.ai.downloadModel(model.id);
      
      if (!success) {
        updateModel(model.id, { status: 'error', progress: 0 });
      }
    }
  };

  const handleCancel = async () => {
    if (window.electronAPI) {
      await window.electronAPI.ai.cancelDownload(model.id);
      updateModel(model.id, { status: 'idle', progress: 0 });
    }
  };

  const getButtonContent = () => {
    switch (model.status) {
      case 'idle':
        return (
          <>
            <span className="text-xl mb-1">⬇️</span>
            <span>点击下载</span>
          </>
        );
      case 'downloading':
        return (
          <>
            <span className="text-xl mb-1 animate-bounce">⏸️</span>
            <span>下载中 {model.progress}%</span>
          </>
        );
      case 'installed':
        return (
          <>
            <span className="text-xl mb-1">✅</span>
            <span>已就绪</span>
          </>
        );
      case 'error':
        return (
          <>
            <span className="text-xl mb-1">❌</span>
            <span>重试</span>
          </>
        );
      default:
        return null;
    }
  };

  return (
    <div
      onClick={model.status === 'idle' || model.status === 'error' ? handleDownload : model.status === 'downloading' ? handleCancel : undefined}
      className={`relative bg-gradient-to-br from-slate-800 to-slate-900 rounded-xl p-5 border-2 transition-all duration-300 cursor-pointer ${
        model.status === 'installed' 
          ? 'border-green-500/50 hover:border-green-400' 
          : model.status === 'downloading'
          ? 'border-cyan-500 cursor-pointer'
          : 'border-slate-700 hover:border-cyan-400 hover:shadow-lg hover:shadow-cyan-500/10'
      }`}
    >
      {model.status === 'downloading' && (
        <div 
          className="absolute inset-0 bg-gradient-to-r from-cyan-500/10 to-blue-500/10 rounded-xl"
          style={{ width: `${model.progress}%` }}
        />
      )}

      <div className="relative z-10">
        <div className="flex items-start justify-between mb-3">
          <div className="flex-1">
            <h3 className="text-lg font-bold text-white mb-1">{model.name}</h3>
            <p className="text-xs text-slate-400">v{model.version}</p>
          </div>
          <div className={`w-3 h-3 rounded-full ${getStatusColor(model.status)} ${model.status === 'downloading' ? 'animate-pulse' : ''}`}></div>
        </div>

        <p className="text-slate-300 text-sm mb-4 line-clamp-2 leading-relaxed">
          {model.description}
        </p>

        <div className="flex items-center justify-between text-xs text-slate-400 mb-4">
          <span>📦 {model.size}</span>
          <span>{getStatusText(model.status)}</span>
        </div>

        {model.status === 'downloading' && (
          <div className="mb-4">
            <div className="h-2 bg-slate-700 rounded-full overflow-hidden">
              <div
                className="h-full bg-gradient-to-r from-cyan-500 to-blue-500 transition-all duration-300 ease-out"
                style={{ width: `${model.progress}%` }}
              />
            </div>
          </div>
        )}

        <div className={`flex items-center justify-center gap-2 py-3 rounded-lg font-medium transition-all ${
          model.status === 'installed'
            ? 'bg-green-500/20 text-green-400'
            : model.status === 'downloading'
            ? 'bg-slate-700/50 text-slate-300'
            : 'bg-gradient-to-r from-cyan-600 to-blue-600 text-white hover:from-cyan-500 hover:to-blue-500'
        }`}>
          {getButtonContent()}
        </div>
      </div>
    </div>
  );
};
