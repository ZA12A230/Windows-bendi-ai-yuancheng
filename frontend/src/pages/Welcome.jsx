export default function Welcome({ onNext }) {
  return (
    <div className="setup-container">
      <div className="setup-card">
        <div className="logo">🤖</div>
        <h1>本地 AI 助手</h1>
        <p className="subtitle">在您的电脑上运行专属 AI 模型</p>
        
        <div className="features">
          <div className="feature-item">
            <span className="icon">🔒</span>
            <span>数据本地存储，隐私安全</span>
          </div>
          <div className="feature-item">
            <span className="icon">⚡</span>
            <span>基于 Ollama，开箱即用</span>
          </div>
          <div className="feature-item">
            <span className="icon">🌐</span>
            <span>支持内网穿透，远程访问</span>
          </div>
          <div className="feature-item">
            <span className="icon">🖥️</span>
            <span>远程桌面控制</span>
          </div>
        </div>

        <button className="btn-primary" onClick={onNext}>
          开始设置
        </button>
      </div>
    </div>
  )
}
