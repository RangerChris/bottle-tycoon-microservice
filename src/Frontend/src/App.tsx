import { useEffect, useState, type ReactNode } from 'react'
import Header from './components/Header'
import GameCanvas from './components/world/GameCanvas'
import BuildToolbar from './components/world/BuildToolbar'
import EntityInspector from './components/world/EntityInspector'
import RecyclersSection from './components/RecyclersSection'
import TrucksSection from './components/TrucksSection'
import EarningsChart from './components/EarningsChart'
import ActivityLog from './components/ActivityLog'
import HelpModal from './components/HelpModal'
import useGameStore from './store/useGameStore'
import useGameLoop from './hooks/useGameLoop'
import { initWorldBridge } from './store/worldBridge'

// Collapsible HUD card anchored over the world canvas.
function HudCard({
  title,
  position,
  open,
  onToggle,
  children,
}: {
  title: string
  position: 'bottom-left' | 'bottom-right'
  open: boolean
  onToggle: () => void
  children: ReactNode
}) {
  return (
    <div className={`absolute ${position === 'bottom-left' ? 'bottom-4 left-4' : 'bottom-4 right-4'} z-10 w-80 max-w-[calc(100%-2rem)]`}>
      <div className="card bg-base-200/90 shadow-xl backdrop-blur">
        <div className="card-body gap-2 p-3">
          <button className="flex items-center justify-between text-left" onClick={onToggle}>
            <span className="card-title text-sm text-emerald-500">{title}</span>
            <span className="text-gray-400">{open ? '▾' : '▸'}</span>
          </button>
          {open ? <div className="max-h-[240px] overflow-y-auto">{children}</div> : null}
        </div>
      </div>
    </div>
  )
}

export default function App() {
  const [DebugPanel, setDebugPanel] = useState<any>(null)
  const init = useGameStore((s: any) => s.init)
  const [helpOpen, setHelpOpen] = useState(false)
  const [logOpen, setLogOpen] = useState(true)
  const [chartOpen, setChartOpen] = useState(false)

  useGameLoop()

  useEffect(() => {
    if ((import.meta as any).env?.MODE !== 'production') {
      import('./components/DebugPanel').then(m => setDebugPanel(() => m.default)).catch(() => { })
    }
  }, [])

  useEffect(() => {
    (async () => {
      await init()
    })()
  }, [init])

  // Economy → world visuals sync (placed recyclers appear on the map).
  useEffect(() => initWorldBridge(), [])

  return (
    <div className="min-h-screen bg-linear-to-br from-gray-900 to-gray-800 text-gray-100">
      <div className="max-w-7xl mx-auto p-6">
        <Header />

        <section className="relative mb-6 h-[min(68vh,660px)] min-h-[420px]">
          <GameCanvas />
          <BuildToolbar />
          <EntityInspector />
          <HudCard title="📜 Activity Log" position="bottom-left" open={logOpen} onToggle={() => setLogOpen(o => !o)}>
            <ActivityLog embedded />
          </HudCard>
          <HudCard title="📊 Bottles Processed" position="bottom-right" open={chartOpen} onToggle={() => setChartOpen(o => !o)}>
            <EarningsChart embedded />
          </HudCard>
        </section>

        {/* Classic panels: the original card grid, kept for power users. */}
        <details className="collapse collapse-arrow bg-base-200 shadow-xl">
          <summary className="collapse-title text-sm text-gray-300">🃏 Classic panels</summary>
          <div className="collapse-content">
            <main className="grid grid-cols-1 items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
              <section className="space-y-6 min-w-0">
                <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
                  <RecyclersSection />
                  <TrucksSection />
                </div>
                <EarningsChart />
              </section>

              <aside className="space-y-6 lg:sticky lg:top-6">
                <ActivityLog />

                <div className="card bg-base-200 shadow-xl">
                  <div className="card-body">
                    <h3 className="card-title text-emerald-500">ℹ️ Quick Info</h3>
                    <p className="text-sm text-gray-400">Bottle values, how-to-play and upgrades moved to Help.</p>
                    <div className="mt-4">
                      <button className="btn btn-outline btn-sm w-full" onClick={() => setHelpOpen(true)}>Open Help</button>
                    </div>
                  </div>
                </div>
              </aside>
            </main>
          </div>
        </details>

        <HelpModal open={helpOpen} onClose={() => setHelpOpen(false)} />
        {DebugPanel ? <DebugPanel /> : null}
      </div>
    </div>
  )
}