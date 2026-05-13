import { useState } from 'react'
import DiskCleaner from '../components/DiskCleaner'

export default function SetupConfigure({ onComplete }) {
  const [config, setConfig] = useState({
    autoStart: false,
    sleepOnShutdown: false,
    silentStart: false,
    adaptiveMode: false,
    intranetPenetrate: false,
    penetrateAddr: '',
    penetrateUser: '',
    penetratePass: '',
    ipv4: '',
    username: '',
    password: '',
  })

  const handleCheckboxChange = (key) => {
    setConfig(prev => ({ ...prev, [key]: !prev[key] }))
  }

  const handleInputChange = (key, value) => {
    setConfig(prev => ({ ...prev, [key]: value }))
  }

  const handleSubmit = async () => {
    if (!config.ipv4 || !config.username || !config.password) {
      alert('请填写 IPv4 地址、用户名和密码')
      return
    }

    try {
      const params = new URLSearchParams()
      Object.entries(config).forEach(([key, value]) => {
        if (typeof value === 'boolean') {
          params.append(key, value ? 'true' : 'false')
        } else {
          params.append(key, value)
        }
      })

      await fetch('/api/setup/configure', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(config)
      })

      onComplete()
    } catch (err) {
      alert('保存配置失败')
    }
  }

  return (
    <div className="setup-container">
      <div className="setup-card large">
        <h2>⚙️ 系统配置</h2>

        <div className="config-section">
          <h3>🚀 启动选项</h3>
          
          <label className="toggle-item">
            <div className="toggle-info">
              <span className="toggle-title">开机自启动</span>
              <span className="toggle-desc">Windows 启动时自动运行本软件</span>
            </div>
            <input 
              type="checkbox" 
              checked={config.autoStart}
              onChange={() => handleCheckboxChange('autoStart')}
            />
          </label>

          <label className="toggle-item">
            <div className="toggle-info">
              <span className="toggle-title">关机键替换为息屏</span>
              <span className="toggle-desc">点击开始菜单关机键时执行息屏而非关机，需点击主机电源键唤醒</span>
            </div>
            <input 
              type="checkbox" 
              checked={config.sleepOnShutdown}
              onChange={() => handleCheckboxChange('sleepOnShutdown')}
            />
          </label>

          <label className="toggle-item">
            <div className="toggle-info">
              <span className="toggle-title">静默启动模式</span>
              <span className="toggle-desc">启动时不显示窗口，最小化到系统托盘</span>
            </div>
            <input 
              type="checkbox" 
              checked={config.silentStart}
              onChange={() => handleCheckboxChange('silentStart')}
            />
          </label>

          <label className="toggle-item">
            <div className="toggle-info">
              <span className="toggle-title">自适应模式</span>
              <span className="toggle-desc">检测到系统资源占用过高时自动降低 AI 占用率</span>
            </div>
            <input 
              type="checkbox" 
              checked={config.adaptiveMode}
              onChange={() => handleCheckboxChange('adaptiveMode')}
            />
          </label>
        </div>

        <div className="config-section">
          <h3>🌐 网络配置</h3>
          
          <label className="toggle-item">
            <div className="toggle-info">
              <span className="toggle-title">开启内网穿透</span>
              <span className="toggle-desc">允许外部网络访问本机 AI 服务</span>
            </div>
            <input 
              type="checkbox" 
              checked={config.intranetPenetrate}
              onChange={() => handleCheckboxChange('intranetPenetrate')}
            />
          </label>

          {config.intranetPenetrate && (
            <div className="config-sub">
              <div className="input-group">
                <label>穿透服务器地址</label>
                <input
                  type="text"
                  placeholder="例如: frp.example.com"
                  value={config.penetrateAddr}
                  onChange={(e) => handleInputChange('penetrateAddr', e.target.value)}
                  className="input-text"
                />
              </div>
              <div className="input-group">
                <label>穿透账号</label>
                <input
                  type="text"
                  placeholder="穿透服务账号"
                  value={config.penetrateUser}
                  onChange={(e) => handleInputChange('penetrateUser', e.target.value)}
                  className="input-text"
                />
              </div>
              <div className="input-group">
                <label>穿透密码</label>
                <input
                  type="password"
                  placeholder="穿透服务密码"
                  value={config.penetratePass}
                  onChange={(e) => handleInputChange('penetratePass', e.target.value)}
                  className="input-text"
                />
              </div>
            </div>
          )}

          <div className="input-group">
            <label>IPv4 地址</label>
            <input
              type="text"
              placeholder="本机 IPv4 地址"
              value={config.ipv4}
              onChange={(e) => handleInputChange('ipv4', e.target.value)}
              className="input-text"
            />
          </div>
          <div className="input-group">
            <label>访问用户名</label>
            <input
              type="text"
              placeholder="远程访问用户名"
              value={config.username}
              onChange={(e) => handleInputChange('username', e.target.value)}
              className="input-text"
            />
          </div>
          <div className="input-group">
            <label>访问密码</label>
            <input
              type="password"
              placeholder="远程访问密码（与穿透密码相同）"
              value={config.password}
              onChange={(e) => handleInputChange('password', e.target.value)}
              className="input-text"
            />
          </div>
        </div>

        <button className="btn-primary" onClick={handleSubmit}>
          完成设置
        </button>
      </div>

      <DiskCleaner />
    </div>
  )
}
