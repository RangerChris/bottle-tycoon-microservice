import { useEffect, useState } from 'react'
import Header from './components/Header'
import RecyclersSection from './components/RecyclersSection'
import TrucksSection from './components/TrucksSection'
import EarningsChart from './components/EarningsChart'
import ActivityLog from './components/ActivityLog'
import HelpModal from './components/HelpModal'
import useGameStore from './store/useGameStore'
import useGameLoop from './hooks/useGameLoop'

export default function App() {
  const [DebugPanel, setDebugPanel] = useState<any>(null)
  const init = useGameStore((s: any) => s.init)
  const [helpOpen, setHelpOpen] = useState(false)

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

  return (
    <div className="min-h-screen bg-linear-to-br from-gray-900 to-gray-800 text-gray-100">
      <div className="max-w-7xl mx-auto p-6">
        <Header />

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

        <HelpModal open={helpOpen} onClose={() => setHelpOpen(false)} />
        {DebugPanel ? <DebugPanel /> : null}
      </div>
    </div>
  )
}