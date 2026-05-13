import { useState, useEffect } from 'react'

export default function SetupModel({ onNext }) {
  const [models, setModels] = useState([])
  const [modelName, setModelName] = useState('')
  const [pulling, setPulling] = useState(false)
  const [pullProgress, setPullProgress] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => {
    fetchModels()
  }, [])

  const fetchModels = async () => {
    try {
      const res = await fetch('/api/ollama/models')
      const data = await res.json()
      if (data.models) {
        setModels(data.models)
      }
    } catch (err) {
      console.error('Failed to fetch models')
    }
  }

  const handlePull = async () => {
    if (!modelName.trim()) {
      setError('请输入模型名称')
      return
    }

    setPulling(true)
    setError('')
    setPullProgress({ status: '准备下载...', completed: 0, total: 100 })

    try {
      const res = await fetch('/api/ollama/pull', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ model_name: modelName })
      })

      const pollInterval = setInterval(async () => {
        fetchModels()
      }, 5000)

      setTimeout(() => {
        clearInterval(pollInterval)
        setPulling(false)
        setPullProgress(null)
        onNext()
      }, 60000)
    } catch (err) {
      setError('下载失败，请检查网络')
      setPulling(false)
    }
  }

  const popularModels = [
    { name: 'llama3.2', desc: 'Meta 最新模型，轻量高效', size: '~2GB' },
    { name: 'qwen2.5:7b', desc: '阿里通义千问，中文能力强', size: '~4.7GB' },
    { name: 'mistral', desc: 'Mistral AI 开源模型', size: '~4.1GB' },
    { name: 'phi3', desc: '微软小模型，速度快', size: '~2.2GB' },
    { name: 'gemma:2b', desc: 'Google 轻量模型', size: '~1.6GB' },
    { name: 'deepseek-coder', desc: '深度求索编程专用', size: '~2GB' },
  ]

  return (
    <div className="setup-container">
      <div className="setup-card">
        <h2>🧠 下载 AI 模型</h2>

        {models.length > 0 && (
          <div className="installed-models">
            <h3>已安装的模型</h3>
            <div className="model-list">
              {models.map((model, idx) => (
                <div key={idx} className="model-item">
                  <span className="model-name">{model.name}</span>
                  <span className="model-size">{(model.size / 1024 / 1024 / 1024).toFixed(1)} GB</span>
                </div>
              ))}
            </div>
          </div>
        )}

        <div className="model-download">
          <h3>下载新模型</h3>
          
          <div className="input-group">
            <label>模型名称或下载链接</label>
            <input
              type="text"
              placeholder="例如: llama3.2 或 https://..."
              value={modelName}
              onChange={(e) => setModelName(e.target.value)}
              className="input-text"
            />
          </div>

          <div className="model-links">
            <h4>🔗 热门模型推荐</h4>
            <div className="model-grid">
              {popularModels.map((model, idx) => (
                <div 
                  key={idx} 
                  className="model-card"
                  onClick={() => setModelName(model.name)}
                >
                  <span className="model-card-name">{model.name}</span>
                  <span className="model-card-desc">{model.desc}</span>
                  <span className="model-card-size">{model.size}</span>
                </div>
              ))}
            </div>
            <p className="hint">
              💡 更多模型请访问 <a href="https://ollama.com/library" target="_blank">Ollama 模型库</a>，复制链接粘贴到上方输入框
            </p>
          </div>

          <button 
            className="btn-primary" 
            onClick={handlePull}
            disabled={pulling}
          >
            {pulling ? `下载中... ${pullProgress?.status || ''}` : '开始下载'}
          </button>

          {pullProgress && (
            <div className="progress-bar">
              <div 
                className="progress-fill" 
                style={{ width: `${pullProgress.completed / pullProgress.total * 100}%` }}
              />
              <span className="progress-text">
                {(pullProgress.completed / pullProgress.total * 100).toFixed(1)}%
              </span>
            </div>
          )}

          {error && <p className="error">{error}</p>}
        </div>

        <button className="btn-secondary" onClick={onNext}>
          跳过，稍后下载
        </button>
      </div>
    </div>
  )
}
