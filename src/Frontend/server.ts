import express from 'express'
import { createProxyMiddleware } from 'http-proxy-middleware'
import path from 'path'
import { fileURLToPath } from 'url'
import client from 'prom-client'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const app = express()
const port = Number(process.env.PORT) || 3000
const apiBase = process.env.API_BASE_URL || process.env.VITE_API_BASE_URL || 'http://localhost:5000'

client.collectDefaultMetrics()
app.get('/metrics', async (_req, res) => {
  try {
    res.set('Content-Type', client.register.contentType)
    res.end(await client.register.metrics())
  } catch {
    res.status(500).end()
  }
})

app.use('/health', createProxyMiddleware({ target: apiBase, changeOrigin: true }))
app.use('/api/traces', createProxyMiddleware({ target: 'http://localhost:14268', changeOrigin: true }))
app.use(express.static(path.join(__dirname, 'dist')))
app.get('*', (_req, res) => {
  res.sendFile(path.join(__dirname, 'dist', 'index.html'))
})

app.listen(port, '0.0.0.0', () => {
  console.log(`Frontend server listening on ${port}`)
})