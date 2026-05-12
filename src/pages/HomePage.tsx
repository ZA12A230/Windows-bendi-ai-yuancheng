import React from 'react';
import { useAppStore } from '../store';
import { ModelCard } from '../components/ModelCard';

export const HomePage: React.FC = () => {
  const { models, selectedModels, clearSelection } = useAppStore();

  const handleDownloadSelected = async () => {
    for (const modelId of selectedModels) {
      if (window.electronAPI) {
        await window.electronAPI.ai.downloadModel(modelId);
      }
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold text-white mb-2">AI模型管理</h2>
          <p className="text-slate-400">选择并下载需要的AI模型到本地</p>
        </div>
        {selectedModels.length > 0 && (
          <div className="flex items-center gap-3">
            <span className="text-slate-400">
              已选择 {selectedModels.length} 个模型
            </span>
            <button
              onClick={handleDownloadSelected}
              className="bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white px-6 py-2 rounded-lg font-medium transition-all duration-200 shadow-lg shadow-cyan-500/25"
            >
              全部下载
            </button>
            <button
              onClick={clearSelection}
              className="bg-slate-700 hover:bg-slate-600 text-white px-4 py-2 rounded-lg font-medium transition-all duration-200"
            >
              清除选择
            </button>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {models.map((model) => (
          <ModelCard key={model.id} model={model} />
        ))}
      </div>
    </div>
  );
};
