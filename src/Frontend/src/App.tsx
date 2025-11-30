import { useEffect, useState } from 'react'

export default function App() {
  const [health, setHealth] = useState<string>('')
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string>('')

  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      setLoading(true)
      setError('')
      try {
        const base = (import.meta as ImportMeta).env.VITE_API_BASE_URL || ''
        const url = base ? `${base}/health` : '/health'
        const res = await fetch(url, { signal: controller.signal })
        const text = await res.text()
        setHealth(text)
      } catch (e: any) {
        setError(e?.message ?? 'Failed to load health')
      } finally {
        setLoading(false)
      }
    }
    load()
    return () => controller.abort()
  }, [])

  return (
    <div className="min-h-screen bg-base-200 text-base-content">
      <div className="navbar bg-base-100 shadow">
        <div className="container mx-auto">
          <a className="btn btn-ghost text-xl">Bottle Tycoon</a>
        </div>
      </div>
      <div className="container mx-auto p-6">
        <div className="card bg-base-100 shadow">
          <div className="card-body">
            <div className="mt-6">
              <h3 className="font-semibold">API Gateway Health</h3>
              {loading && <div className="text-sm">Loading...</div>}
              {error && <div className="text-sm text-error">{error}</div>}
              {!loading && !error && <pre className="text-sm whitespace-pre-wrap break-all">{health}</pre>}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}