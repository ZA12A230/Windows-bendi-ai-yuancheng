import { useEffect, useRef } from 'react'

export default function Chart({ data }) {
  const canvasRef = useRef(null)

  useEffect(() => {
    if (!canvasRef.current || data.length < 2) return

    const canvas = canvasRef.current
    const ctx = canvas.getContext('2d')
    const width = canvas.width
    const height = canvas.height

    ctx.clearRect(0, 0, width, height)

    const padding = { top: 20, right: 20, bottom: 30, left: 50 }
    const chartWidth = width - padding.left - padding.right
    const chartHeight = height - padding.top - padding.bottom

    ctx.strokeStyle = '#4a4a6a'
    ctx.lineWidth = 1
    for (let i = 0; i <= 4; i++) {
      const y = padding.top + (chartHeight / 4) * i
      ctx.beginPath()
      ctx.moveTo(padding.left, y)
      ctx.lineTo(width - padding.right, y)
      ctx.stroke()

      ctx.fillStyle = '#8a8a9a'
      ctx.font = '12px sans-serif'
      ctx.textAlign = 'right'
      ctx.fillText(`${100 - i * 25}%`, padding.left - 10, y + 4)
    }

    const metrics = [
      { key: 'cpu', color: '#4a90d9', label: 'CPU' },
      { key: 'memory_percent', color: '#d94a4a', label: '内存' },
      { key: 'gpu_usage', color: '#4ad97a', label: 'GPU' },
      { key: 'disk_percent', color: '#d9a94a', label: '磁盘' },
    ]

    metrics.forEach(({ key, color, label }) => {
      ctx.strokeStyle = color
      ctx.lineWidth = 2
      ctx.beginPath()

      data.forEach((point, idx) => {
        const x = padding.left + (chartWidth / (data.length - 1)) * idx
        const y = padding.top + chartHeight - (chartHeight / 100) * (point[key] || 0)

        if (idx === 0) {
          ctx.moveTo(x, y)
        } else {
          ctx.lineTo(x, y)
        }
      })

      ctx.stroke()
    })

    ctx.fillStyle = '#8a8a9a'
    ctx.font = '12px sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText(`最近 ${data.length} 秒`, width / 2, height - 5)

    const legendX = padding.left + 20
    let legendY = padding.top + 15
    metrics.forEach(({ color, label }) => {
      ctx.fillStyle = color
      ctx.fillRect(legendX, legendY - 8, 12, 12)
      ctx.fillStyle = '#ccc'
      ctx.textAlign = 'left'
      ctx.fillText(label, legendX + 18, legendY)
      legendY += 18
    })
  }, [data])

  return (
    <canvas 
      ref={canvasRef} 
      width={800} 
      height={300} 
      className="chart-canvas"
    />
  )
}
