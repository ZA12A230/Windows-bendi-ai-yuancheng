import { useState, useEffect, useRef } from 'react'

export default function AIChat() {
  const [models, setModels] = useState([])
  const [selectedModel, setSelectedModel] = useState('')
  const [messages, setMessages] = useState([])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const messagesEndRef = useRef(null)

  useEffect(() => {
    fetch('/api/ollama/models')
      .then(r => r.json())
      .then(data => {
        if (data.models && data.models.length > 0) {
          setModels(data.models)
          setSelectedModel(data.models[0].name)
        }
      })
  }, [])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const handleSend = async () => {
    if (!input.trim() || !selectedModel) return

    const userMessage = { role: 'user', content: input }
    setMessages(prev => [...prev, userMessage])
    setInput('')
    setLoading(true)

    try {
      const res = await fetch('/api/ollama/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: selectedModel,
          messages: [...messages, userMessage]
        })
      })

      const data = await res.json()
      if (data.response) {
        setMessages(prev => [...prev, { role: 'assistant', content: data.response }])
      }
    } catch (err) {
      setMessages(prev => [...prev, { 
        role: 'system', 
        content: '❌ 请求失败，请检查 Ollama 是否正常运行' 
      }])
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="chat-container">
      <div className="chat-header">
        <h2>💬 AI 对话</h2>
        {models.length > 0 ? (
          <select 
            value={selectedModel} 
            onChange={(e) => setSelectedModel(e.target.value)}
            className="model-select"
          >
            {models.map(model => (
              <option key={model.name} value={model.name}>
                {model.name}
              </option>
            ))}
          </select>
        ) : (
          <span className="no-models">⚠️ 请先下载 AI 模型</span>
        )}
      </div>

      <div className="chat-messages">
        {messages.length === 0 && (
          <div className="chat-empty">
            <p>👋 你好！我是本地 AI 助手</p>
            <p>选择一个模型开始对话吧</p>
          </div>
        )}

        {messages.map((msg, idx) => (
          <div key={idx} className={`message ${msg.role}`}>
            <div className="message-content">
              {msg.content}
            </div>
          </div>
        ))}

        {loading && (
          <div className="message assistant">
            <div className="message-content thinking">
              <span className="dot">●</span>
              <span className="dot">●</span>
              <span className="dot">●</span>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      <div className="chat-input">
        <input
          type="text"
          placeholder="输入消息..."
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyPress={(e) => e.key === 'Enter' && handleSend()}
          disabled={loading || !selectedModel}
        />
        <button 
          onClick={handleSend}
          disabled={loading || !selectedModel || !input.trim()}
        >
          发送
        </button>
      </div>
    </div>
  )
}
