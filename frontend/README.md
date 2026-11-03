# Web app

Next.js 16 (App Router, Cache Components) client for the Scholar Suite API.

```bash
npm install
npm run dev          # http://localhost:3000 — proxies /api/* to API_ORIGIN (default http://localhost:5080)
npx tsc --noEmit     # strict type-check
npm run lint
```

- **Auth**: the browser never sees tokens. `/api/*` is rewritten to the API on the same origin, so HttpOnly session
  cookies flow automatically; `src/lib/api/client.ts` adds the CSRF header and handles token refresh.
- **Design system**: tokens in `src/app/globals.css`, primitives in `src/components/ui`. In development,
  `/design` renders every primitive for review in light and dark themes.
- **Branding**: `NEXT_PUBLIC_PRODUCT_NAME` sets the product name; each school's accent colour is applied at runtime.
