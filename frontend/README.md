# HomeApp frontend

Angular 21 and PrimeNG 21 provide the HomeApp interface. Node.js 24 is pinned
in the repository's `.nvmrc`. See the [project README](../README.md) for backend,
database and deployment setup.

```bash
npm ci
npm start
```

Open http://localhost:4200. During development, the browser sends API requests
to the same host on port 5000. Run the backend before using data-backed pages.

```bash
npm run build -- --configuration production
npm test -- --watch=false
npm run test:vessels
npm audit
```

Unit tests use Angular's Vitest runner. Vessel tests use Node's TypeScript
support. The production Docker image serves `dist/web-app/browser` through
ASP.NET Core; the separate Node SSR server is optional.

The browser smoke test uses synthetic intercepted data. Install Playwright
outside the app and set `PLAYWRIGHT_MODULE_PATH` to its `index.mjs`, then run:

```bash
COMMUTE_UI_ONLY=1 COMMUTE_BASE_URL=http://localhost:4200 \
  node e2e/commute-smoke.mjs
```

Private destination catalogues belong in the ignored
`src/assets/vessel-ports.private.json`; see [Commute Monitor](../docs/commute-monitor.md).
