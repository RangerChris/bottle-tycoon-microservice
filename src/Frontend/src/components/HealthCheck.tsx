import { useEffect, useState } from 'react'

export default function HealthCheck() {
  const [health, setHealth] = useState<string>('')
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string>('')

  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      setLoading(true)
      setError('')
      try {
        const env = (import.meta as any).env || {}
        const envBase = env?.VITE_API_BASE_URL
        const defaultBase = (typeof window !== 'undefined' && window.location.hostname === 'localhost')
          ? 'http://localhost:5001'
          : 'http://apigateway:5000'
        const base = envBase || defaultBase
        const url = `${base.replace(/\/$/, '')}/health`
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
    <div className="mt-6">
      <h3 className="font-semibold">API Gateway Health</h3>
      {loading && <div className="text-sm">Loading...</div>}
      {error && <div className="text-sm text-error">{error}</div>}
      {!loading && !error && <pre className="text-sm whitespace-pre-wrap break-all">{health}</pre>}
    </div>
  )
}