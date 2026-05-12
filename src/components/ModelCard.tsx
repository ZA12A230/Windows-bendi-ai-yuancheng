import React from 'react';
import { AIModel } from '../../shared/types';
import { useAppStore } from '../store';

interface ModelCardProps {
  model: AIModel;
}

export const ModelCard: React.FC<ModelCardProps> = ({ model }) => {
  const { selectedModels, toggleModelSelection, updateModel } = useAppStore();
  const isSelected = selectedModels.includes(model.id);

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
        return '错误';
      default:
        return '待下载';
    }
  };

  const handleDownload = async () => {
    if (window.electronAPI) {
      updateModel(model.id, { status: 'downloading', progress: 0 });
      const success = await window.electronAPI.ai.downloadModel(model.id);
      if (success) {
        updateModel(model.id, { status: 'installed', progress: 100 });
      } else {
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

  return (
    <div
      className={`relative bg-slate-800/50 backdrop-blur-sm rounded-xl p-6 border-2 transition-all duration-300 ${
        isSelected ? 'border-cyan-500 shadow-lg shadow-cyan-500/20' : 'border-slate-700 hover:border-slate-600'
      }`}
    >
      <div className="flex items-start justify-between mb-4">
        <div>
          <h3 className="text-lg font-semibold text-white mb-1">{model.name}</h3>
          <p className="text-sm text-slate-400">v{model.version}</p>
        </div>
        <div className="flex items-center gap-2">
          <div className={`w-3 h-3 rounded-full ${getStatusColor(model.status)}`}></div>
          <span className="text-xs text-slate-400">{getStatusText(model.status)}</span>
        </div>
      </div>

      <p className="text-slate-300 text-sm mb-4 leading-relaxed">{model.description}</p>

      <div className="flex items-center justify-between mb-4">
        <span className="text-slate-400 text-sm">大小: {model.size}</span>
        <input
          type="checkbox"
          checked={isSelected}
          onChange={() => toggleModelSelection(model.id)}
          className="w-5 h-5 rounded border-slate-600 bg-slate-700 text-cyan-500 focus:ring-cyan-500 focus:ring-offset-slate-800"
        />
      </div>

      {model.status === 'downloading' && (
        <div className="mb-4">
          <div className="flex justify-between text-sm text-slate-400 mb-2">
            <span>下载进度</span>
            <span>{model.progress}%</span>
          </div>
          <div className="h-2 bg-slate-700 rounded-full overflow-hidden">
            <div
              className="h-full bg-gradient-to-r from-cyan-500 to-blue-500 transition-all duration-300"
              style={{ width: `${model.progress}%` }}
            ></div>
          </div>
        </div>
      )}

      <div className="flex gap-2">
        {model.status === 'idle' && (
          <button
            onClick={handleDownload}
            className="flex-1 bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white py-2 px-4 rounded-lg font-medium transition-all duration-200"
          >
            下载
          </button>
        )}
        {model.status === 'downloading' && (
          <button
            onClick={handleCancel}
            className="flex-1 bg-slate-600 hover:bg-slate-500 text-white py-2 px-4 rounded-lg font-medium transition-all duration-200"
          >
            取消
          </button>
        )}
        {model.status === 'installed' && (
          <button
            className="flex-1 bg-green-600/20 text-green-400 py-2 px-4 rounded-lg font-medium cursor-default"
            disabled
          >
            已安装
          </button>
        )}
        {model.status === 'error' && (
          <button
            onClick={handleDownload}
            className="flex-1 bg-red-600/20 hover:bg-red-600/30 text-red-400 py-2 px-4 rounded-lg font-medium transition-all duration-200"
          >
            重试
          </button>
        )}
      </div>
    </div>
  );
};
