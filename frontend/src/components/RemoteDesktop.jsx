import { useEffect, useRef } from 'react'

export default function RemoteDesktop() {
  const iframeRef = useRef(null)

  useEffect(() => {
    if (iframeRef.current) {
      iframeRef.current.src = 'http://localhost:8081'
    }
  }, [])

  return (
    <div className="remote-container">
      <div className="remote-header">
        <h2>🖥️ 远程桌面控制</h2>
        <p className="hint">通过网页远程控制本机，支持鼠标键盘操作</p>
      </div>
      
      <div className="remote-frame">
        <iframe 
          ref={iframeRef}
          title="Remote Desktop"
          className="remote-iframe"
        />
      </div>

      <div className="remote-info">
        <h3>使用说明</h3>
        <ul>
          <li>在远程设备上打开浏览器，访问本机的 IPv4 地址或内网穿透地址</li>
          <li>输入设置时配置的用户名和密码进行认证</li>
          <li>支持鼠标移动、点击、滚轮操作</li>
          <li>支持键盘输入和组合键（Ctrl、Alt、Shift 等）</li>
          <li>Ctrl+Alt+Del 可调用安全选项</li>
        </ul>
      </div>
    </div>
  )
}
