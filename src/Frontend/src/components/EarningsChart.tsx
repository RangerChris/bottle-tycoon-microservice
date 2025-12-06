import React, { useEffect, useRef } from 'react'
// Chart.js types may not be installed; import dynamically
import useGameStore, { GameState } from '../store/useGameStore'

export default function EarningsChart() {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const chartRef = useRef<any>(null)
  const points = useGameStore((s: GameState) => s.chartPoints)

  useEffect(() => {
    let Chart: any
    let mounted = true
    import('chart.js/auto').then((mod) => { if (!mounted) return; Chart = mod.default; const ctx = canvasRef.current; if (!ctx) return; chartRef.current = new Chart(ctx, { type: 'line', data: { labels: points.map((p: any) => new Date(p.time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })), datasets: [ { label: 'Glass', data: points.map((p: any) => p.bottles.glass), borderColor: 'rgb(34,197,94)', backgroundColor: 'rgba(34,197,94,0.1)', fill: true, tension: 0.4 }, { label: 'Metal', data: points.map((p: any) => p.bottles.metal), borderColor: 'rgb(148,163,184)', backgroundColor: 'rgba(148,163,184,0.1)', fill: true, tension: 0.4 }, { label: 'Plastic', data: points.map((p: any) => p.bottles.plastic), borderColor: 'rgb(59,130,246)', backgroundColor: 'rgba(59,130,246,0.1)', fill: true, tension: 0.4 } ] }, options: { responsive: true, maintainAspectRatio: false } }) })

    return () => { mounted = false; chartRef.current?.destroy?.() }
  }, [])

  useEffect(() => {
    if (!chartRef.current) return
    chartRef.current.data.labels = points.map((p: any) => new Date(p.time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }))
    chartRef.current.data.datasets[0].data = points.map((p: any) => p.bottles.glass)
    chartRef.current.data.datasets[1].data = points.map((p: any) => p.bottles.metal)
    chartRef.current.data.datasets[2].data = points.map((p: any) => p.bottles.plastic)
    chartRef.current.update()
  }, [points])

  return (
    <div className="card bg-base-200 shadow-xl">
      <div className="card-body">
        <div className="flex items-center justify-between">
          <h2 className="card-title text-emerald-500">📊 Bottles Processed</h2>
          <div className="text-right">
            <div className="text-sm text-gray-400">Total Earnings</div>
            <div className="font-bold text-gray-100">{useGameStore.getState().totalEarnings.toLocaleString()}</div>
          </div>
        </div>

        <div className="mt-4 bg-gray-800 rounded-lg p-6" style={{ height: 220 }}>
          <canvas ref={canvasRef}></canvas>
        </div>
      </div>
    </div>
  )
}