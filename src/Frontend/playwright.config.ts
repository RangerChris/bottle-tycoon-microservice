import { defineConfig, devices } from '@playwright/test';
import react from '@vitejs/plugin-react';

// Ensure PLAYWRIGHT_TEST_BASE_URL is set (fallback) so component testing mount fixture can find the template URL
process.env.PLAYWRIGHT_TEST_BASE_URL = process.env.PLAYWRIGHT_TEST_BASE_URL || 'http://127.0.0.1:5173';

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  expect: { timeout: 5000 },
  reporter: [['list'], ['html', { outputFolder: 'playwright-report' }]],
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  use: {
    baseURL: 'http://localhost:3000',
    headless: true,
    viewport: { width: 1280, height: 720 },
    ignoreHTTPSErrors: true,
    actionTimeout: 5000,
  },
  // separate e2e and component tests by folder
  testMatch: /.*\.(spec|test)\.(ts|tsx|js)$/,
  // component testing settings
  ct: {
    // command to run Vite dev server for component testing
    devServer: {
      // use npx vite directly and bind to localhost
      command: 'npx vite --port 5173 --host 127.0.0.1',
      url: 'http://127.0.0.1:5173',
      reuseExistingServer: true,
    },
    // directory with template index.html for component tests
    indexHtml: 'tests/index.html',
    // provide vite config so Playwright can resolve and build React components
    viteConfig: {
      plugins: [react()],
      resolve: {
        alias: {}
      }
    }
  }
});