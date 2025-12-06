# Playwright tests for Frontend

This project uses Playwright for both end-to-end (e2e) and component testing.

Commands (run from `src/Frontend`):

- Install dependencies: npm install
- Install Playwright browsers: npx playwright install --with-deps
- Run all e2e tests: npm run test:e2e
- Run component tests: npm run test:component

Notes:
- E2E tests assume the app is available at http://localhost:3000 (Playwright `baseURL`).
- Component tests use Playwright Experimental Component Test for React.