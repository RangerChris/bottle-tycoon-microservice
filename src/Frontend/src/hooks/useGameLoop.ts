import { useEffect, useRef } from 'react'
import useGameStore from '../store/useGameStore'

// Map timeLevel to multiplier: 1=paused(0),2=normal(1),3=2x,4=4x,5=5x
const timeMultipliers: Record<number, number> = { 1: 0, 2: 1, 3: 2, 4: 4, 5: 5 }

export default function useGameLoop() {
  const timeLevel = useGameStore(state => state.timeLevel)

  const depositIntervalRef = useRef<number | null>(null)
  const dispatchIntervalRef = useRef<number | null>(null)

  useEffect(() => {
    // expose store for quick debugging in browser console
    ;(window as any).gameStore = useGameStore
  }, [])

  useEffect(() => {
    // clear previous intervals
    if (depositIntervalRef.current) {
      window.clearInterval(depositIntervalRef.current)
      depositIntervalRef.current = null
    }
    if (dispatchIntervalRef.current) {
      window.clearInterval(dispatchIntervalRef.current)
      dispatchIntervalRef.current = null
    }

    const mult = timeMultipliers[timeLevel] ?? 1

    // run an immediate tick so time starts advancing without waiting for the first interval
    const store = (useGameStore as any).getState()
    if (mult > 0) {
      store.depositTick(mult)
      store.attemptSmartDispatch()
    } else {
      // when paused, still run a quick dispatch check
      store.attemptSmartDispatch()
    }

    // only create deposit loop when not paused
    if (mult > 0) {
      // Deposit loop runs every 1s real time; depositTick will handle multiplier (number of bottles per second)
      depositIntervalRef.current = window.setInterval(() => {
        const s = (useGameStore as any).getState()
        s.depositTick(mult)
      }, 1000)

      // Smart dispatch loop: run every 2s divided by multiplier (faster speeds check more often)
      const dispatchInterval = Math.max(200, Math.floor(2000 / mult))
      dispatchIntervalRef.current = window.setInterval(() => {
        const s = (useGameStore as any).getState()
        s.attemptSmartDispatch()
      }, dispatchInterval)
    } else {
      // paused: still allow dispatch checks at a low rate so manual interactions can trigger behavior
      dispatchIntervalRef.current = window.setInterval(() => {
        const s = (useGameStore as any).getState()
        s.attemptSmartDispatch()
      }, 2000)
    }


    return () => {
      if (depositIntervalRef.current) { window.clearInterval(depositIntervalRef.current); depositIntervalRef.current = null }
      if (dispatchIntervalRef.current) { window.clearInterval(dispatchIntervalRef.current); dispatchIntervalRef.current = null }
    }
  }, [timeLevel])
}