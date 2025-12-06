import React from 'react'
import useGameStore, { GameState } from '../store/useGameStore'

export default function Header() {
  const credits = useGameStore((state: GameState) => state.credits)
  const timeLevel = useGameStore((state: GameState) => state.timeLevel)
  const setTimeLevel = useGameStore((state: GameState) => state.setTimeLevel)

  const multiplierMap: Record<number, number> = { 1: 0, 2: 1, 3: 2, 4: 4, 5: 5 }
  const multiplier = multiplierMap[timeLevel] ?? 1

  return (
    <header className="flex items-center justify-between mb-6">
      <div className="flex items-center gap-4">
        <h1 className="text-3xl font-extrabold text-emerald-400">🍾 Bottle Tycoon</h1>
      </div>

      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2 px-4 py-2 bg-gray-700 rounded-lg">
          <span className="text-sm text-gray-300">Credits</span>
          <span className="text-xl font-bold text-yellow-300">{credits.toLocaleString()}</span>
        </div>

        <div className="flex items-center gap-3">
          <button aria-label="slower" className="btn btn-secondary btn-sm time-btn no-outline-btn" onClick={() => setTimeLevel(Math.max(1, timeLevel - 1))}>⏪</button>
          <button aria-label="pause" className="btn btn-secondary btn-sm time-btn no-outline-btn" onClick={() => setTimeLevel(1)}>⏸</button>
          <button aria-label="faster" className="btn btn-secondary btn-sm time-btn no-outline-btn" onClick={() => setTimeLevel(Math.min(5, timeLevel + 1))}>⏩</button>
          <span className="ml-3 text-sm text-gray-300">Speed: {multiplier === 0 ? 'Paused' : `x${multiplier}`}</span>
          {/* Last tick removed per request */}
        </div>
      </div>
    </header>
  )
}