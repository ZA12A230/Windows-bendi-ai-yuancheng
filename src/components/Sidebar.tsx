import React from 'react';
import { useAppStore } from '../store';

export const Sidebar: React.FC = () => {
  const { activeTab, setActiveTab, tunnel, performance } = useAppStore();

  const menuItems = [
    { id: 'home' as const, label: '模型管理', icon: '🤖' },
    { id: 'status' as const, label: '系统状态', icon: '📊' },
    { id: 'settings' as const, label: '设置', icon: '⚙️' },
  ];

  return (
    <aside className="w-64 bg-slate-800/50 backdrop-blur-sm border-r border-slate-700 flex flex-col">
      <div className="p-6 border-b border-slate-700">
        <h1 className="text-2xl font-bold bg-gradient-to-r from-cyan-400 to-blue-500 bg-clip-text text-transparent">
          AI工具箱
        </h1>
        <p className="text-slate-400 text-sm mt-1">本地部署助手</p>
      </div>

      <nav className="flex-1 p-4 space-y-2">
        {menuItems.map((item) => (
          <button
            key={item.id}
            onClick={() => setActiveTab(item.id)}
            className={`w-full flex items-center gap-3 px-4 py-3 rounded-lg transition-all duration-200 ${
              activeTab === item.id
                ? 'bg-gradient-to-r from-cyan-600 to-blue-600 text-white shadow-lg shadow-cyan-500/25'
                : 'text-slate-300 hover:bg-slate-700/50 hover:text-white'
            }`}
          >
            <span className="text-xl">{item.icon}</span>
            <span className="font-medium">{item.label}</span>
          </button>
        ))}
      </nav>

      <div className="p-4 border-t border-slate-700 space-y-3">
        <div className="flex items-center gap-3">
          <div className={`w-3 h-3 rounded-full ${tunnel.connected ? 'bg-green-500 animate-pulse' : 'bg-slate-500'}`}></div>
          <span className="text-sm text-slate-400">
            {tunnel.connected ? '内网穿透已连接' : '内网穿透未连接'}
          </span>
        </div>
        
        <div className="flex items-center gap-3">
          <div className={`w-3 h-3 rounded-full ${performance.cpuUsage > 80 ? 'bg-red-500' : 'bg-green-500'}`}></div>
          <span className="text-sm text-slate-400">
            系统负载: {performance.cpuUsage}%
          </span>
        </div>
      </div>
    </aside>
  );
};
