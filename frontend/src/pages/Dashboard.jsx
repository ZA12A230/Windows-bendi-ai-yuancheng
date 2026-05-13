import { useState, useEffect, useRef } from 'react'
import Chart from '../components/Chart'
import AIChat from '../components/AIChat'
import RemoteDesktop from '../components/RemoteDesktop'

export default function Dashboard() {
  const [activeTab, setActiveTab] = useState('dashboard')
  const [stats, setStats] = useState(null)
  const [statsHistory, setStatsHistory] = useState([])
  const [remoteUrl, setRemoteUrl] = useState({})
  const wsRef = useRef(null)

  useEffect(() => {
    wsRef.current = new WebSocket(`ws://${window.location.host}/ws/stats`)
    
    wsRef.current.onmessage = (e) => {
      const data = JSON.parse(e.data)
      setStats(data)
      setStatsHistory(prev => {
        const newHistory = [...prev, data]
        return newHistory.slice(-60)
      })
    }

    fetch('/api/remote/url')
      .then(r => r.json())
      .then(data => setRemoteUrl(data))

    return () => {
      if (wsRef.current) {
        wsRef.current.close()
      }
    }
  }, [])

  const formatBytes = (bytes) => {
    if (!bytes) return '0 B'
    const units = ['B', 'KB', 'MB', 'GB', 'TB']
    let i = 0
    while (bytes >= 1024 && i < units.length - 1) {
      bytes /= 1024
      i++
    }
    return `${bytes.toFixed(1)} ${units[i]}`
  }

  const formatTime = (seconds) => {
    if (!seconds) return '0s'
    const h = Math.floor(seconds / 3600)
    const m = Math.floor((seconds % 3600) / 60)
    const s = Math.floor(seconds % 60)
    if (h > 0) return `${h}h ${m}m`
    if (m > 0) return `${m}m ${s}s`
    return `${s}s`
  }

  return (
    <div className="dashboard">
      <nav className="sidebar">
        <div className="logo">🤖 本地AI</div>
        <div className="nav-items">
          <button 
            className={activeTab === 'dashboard' ? 'active' : ''}
            onClick={() => setActiveTab('dashboard')}
          >
            📊 监控面板
          </button>
          <button 
            className={activeTab === 'chat' ? 'active' : ''}
            onClick={() => setActiveTab('chat')}
          >
            💬 AI 对话
          </button>
          <button 
            className={activeTab === 'remote' ? 'active' : ''}
            onClick={() => setActiveTab('remote')}
          >
            🖥️ 远程桌面
          </button>
        </div>
      </nav>

      <main className="main-content">
        {activeTab === 'dashboard' && (
          <div className="dashboard-content">
            <div className="stats-grid">
              <div className="stat-card">
                <h3>CPU 使用率</h3>
                <div className="stat-value">{stats?.cpu || 0}%</div>
                <div className="stat-bar">
                  <div className="stat-fill" style={{ width: `${stats?.cpu || 0}%` }} />
                </div>
              </div>

              <div className="stat-card">
                <h3>内存使用</h3>
                <div className="stat-value">
                  {formatBytes(stats?.memory_used)} / {formatBytes(stats?.memory_total)}
                </div>
                <div className="stat-percent">{stats?.memory_percent || 0}%</div>
                <div className="stat-bar">
                  <div className="stat-fill" style={{ width: `${stats?.memory_percent || 0}%` }} />
                </div>
              </div>

              <div className="stat-card">
                <h3>磁盘使用</h3>
                <div className="stat-value">
                  {formatBytes(stats?.disk_used)} / {formatBytes(stats?.disk_total)}
                </div>
                <div className="stat-percent">{stats?.disk_percent || 0}%</div>
                <div className="stat-bar">
                  <div className="stat-fill" style={{ width: `${stats?.disk_percent || 0}%` }} />
                </div>
              </div>

              <div className="stat-card">
                <h3>GPU 使用率</h3>
                <div className="stat-value">{stats?.gpu_usage || 0}%</div>
                <div className="stat-bar">
                  <div className="stat-fill" style={{ width: `${stats?.gpu_usage || 0}%` }} />
                </div>
              </div>
            </div>

            <div className="chart-section">
              <h3>📈 系统资源曲线图</h3>
              <Chart data={statsHistory} />
            </div>

            <div className="network-section">
              <div className="network-card">
                <h3>🌐 内网穿透地址</h3>
                <p>{remoteUrl.remote_url || '未配置'}</p>
              </div>
              <div className="network-card">
                <h3>📡 IPv4 地址</h3>
                <p>{remoteUrl.ipv4 || '未获取'}</p>
              </div>
              <div className="network-card">
                <h3>👤 访问账号</h3>
                <p>用户名: {remoteUrl.username || '未设置'}</p>
                <p>密码: ••••••••</p>
              </div>
            </div>

            <div className="uptime-section">
              <div className="uptime-card">
                <h3>⏱️ 系统运行时间</h3>
                <p>{formatTime(stats?.uptime)}</p>
              </div>
              <div className="uptime-card">
                <h3>⏰ 软件运行时间</h3>
                <p>{formatTime(stats?.app_uptime)}</p>
              </div>
              <div className="uptime-card">
                <h3>📶 网络流量</h3>
                <p>↑ {formatBytes(stats?.net_sent)} / ↓ {formatBytes(stats?.net_recv)}</p>
              </div>
            </div>
          </div>
        )}

        {activeTab === 'chat' && <AIChat />}
        {activeTab === 'remote' && <RemoteDesktop />}
      </main>
    </div>
  )
}
