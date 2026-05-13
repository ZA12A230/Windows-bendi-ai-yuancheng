import { useState } from 'react'

export default function SetupOllama({ installed, running, onNext }) {
  const [installing, setInstalling] = useState(false)
  const [progress, setProgress] = useState(0)
  const [useMirror, setUseMirror] = useState(false)
  const [error, setError] = useState('')

  const handleInstall = async () => {
    setInstalling(true)
    setError('')
    
    try {
      const res = await fetch('/api/setup/ollama/install', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ use_mirror: useMirror })
      })
      const data = await res.json()
      
      const checkInterval = setInterval(async () => {
        const checkRes = await fetch('/api/setup/check')
        const checkData = await checkRes.json()
        
        if (checkData.ollama_installed && checkData.ollama_running) {
          clearInterval(checkInterval)
          setProgress(100)
          setTimeout(onNext, 1000)
        } else {
          setProgress(prev => Math.min(prev + 5, 90))
        }
      }, 3000)
    } catch (err) {
      setError('下载失败，请检查网络连接')
      setInstalling(false)
    }
  }

  return (
    <div className="setup-container">
      <div className="setup-card">
        <h2>📦 安装 Ollama</h2>
        
        {installed && running ? (
          <div className="status-success">
            <p>✅ Ollama 已安装并运行</p>
            <button className="btn-primary" onClick={onNext}>下一步</button>
          </div>
        ) : installed ? (
          <div className="status-warning">
            <p>⚠️ Ollama 已安装但未运行，正在启动...</p>
            <button className="btn-primary" onClick={onNext}>下一步</button>
          </div>
        ) : (
          <>
            <p className="description">
              检测到您的系统未安装 Ollama。请选择以下方式安装：
            </p>

            <div className="install-options">
              <div className="option-card">
                <h3>官方下载</h3>
                <p>从 Ollama 官网下载最新版本的安装包</p>
                <div className="link-box">
                  <input 
                    type="text" 
                    value="https://ollama.com/download/OllamaSetup.exe" 
                    readOnly 
                  />
                  <button 
                    className="btn-copy"
                    onClick={() => navigator.clipboard.writeText('https://ollama.com/download/OllamaSetup.exe')}
                  >
                    复制
                  </button>
                </div>
                <a 
                  href="https://ollama.com/download/OllamaSetup.exe" 
                  className="btn-link"
                  target="_blank"
                >
                  在浏览器中打开
                </a>
              </div>

              <div className="option-card">
                <h3>国内镜像下载</h3>
                <p>使用国内镜像源加速下载</p>
                <div className="link-box">
                  <input 
                    type="text" 
                    value="https://ghproxy.net/https://github.com/ollama/ollama/releases/latest/download/OllamaSetup.exe" 
                    readOnly 
                  />
                  <button 
                    className="btn-copy"
                    onClick={() => navigator.clipboard.writeText('https://ghproxy.net/https://github.com/ollama/ollama/releases/latest/download/OllamaSetup.exe')}
                  >
                    复制
                  </button>
                </div>
              </div>

              <div className="option-card highlight">
                <h3>🚀 一键自动安装</h3>
                <p>无人值守自动下载安装</p>
                <label className="checkbox-label">
                  <input 
                    type="checkbox" 
                    checked={useMirror}
                    onChange={(e) => setUseMirror(e.target.checked)}
                  />
                  使用国内镜像源
                </label>
                <button 
                  className="btn-primary" 
                  onClick={handleInstall}
                  disabled={installing}
                >
                  {installing ? `安装中... ${progress}%` : '开始安装'}
                </button>
              </div>
            </div>

            {error && <p className="error">{error}</p>}
          </>
        )}
      </div>
    </div>
  )
}
