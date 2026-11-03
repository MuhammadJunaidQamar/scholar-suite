# Progress

_Last updated: 2026-11-03_

## Current stage
**M3 complete → starting M4 (students, guardians, admissions).** Foundations, identity, access control, the web
application shell and the full school-configuration area are in place and verified end to end (API + PostgreSQL +
Next.js, screenshots in light/dark).

## Completed

### Backend (ASP.NET Core 10, EF Core 10, PostgreSQL 16)
- Modular-monolith solution (Domain / Application / Infrastructure / Api), central package management,
  warnings-as-errors, Serilog, OpenAPI + Swagger UI, health checks (`/health/live`, `/health/ready`), RFC 9457 errors.
- Persistence: snake_case schema, UUID v7 keys, named `Tenant`/`SoftDelete` query filters, tenant write guard,
  audit interceptor (field-level history), xmin concurrency, restrict-by-default FKs, gap-free number sequences.
- Identity: global users + school memberships, ASP.NET Identity hashing/lockout/reset tokens, sessions,
  15-min JWT access tokens validated against live session state, hashed rotating refresh tokens with reuse
  detection (+ 20 s multi-tab grace), HttpOnly cookie transport, session-bound double-submit CSRF, bearer mode for
  mobile, password forgot/reset/change, session list/revoke, school switching, rate limiting, security headers.
- Access: 122-permission catalogue, 15 seeded system roles, campus-scoped assignments, cached effective access with
  immediate invalidation, escalation guards (cannot grant what you lack; owner role owner-only; last owner kept).
- Platform: school provisioning with pluggable module steps (roles, numbering, designations, departments, grading).
- Organization: overview, school profile/policies/modules/numbering, campuses, academic years & terms, grades,
  sections, subjects, streams, curriculum, rooms, calendar, grading scales, departments & designations, audit trail.
- Development demo seed: Greenfield Grammar School (Lahore), 2 campuses, 2025–26 and current 2026–27 years with
  terms, 13 grades, 31 sections, 40 rooms, 16 subjects with curriculum, 2026–27 calendar, 11 staff accounts.

### Frontend (Next.js 16, React 19, Tailwind 4)
- Design system "The Register": OKLCH tokens, school-derived accent clamped for legibility, light/dark themes,
  Inter + Source Serif 4 + JetBrains Mono, buttons, inputs, fields, form sections, selects, toggles, dialogs,
  sheets, menus, tooltips, tabs, badges, avatars, empty/error/loading states, toasts, command list.
- Data table: URL-synced server paging/sort/search/filters, facet filters, column visibility + density, selection with
  bulk-action bar, row actions, skeleton/empty/error states.
- API client: same-origin proxy, CSRF header, single-flight cross-tab refresh, problem-details errors.
- App shell: collapsible sidebar (permission/module-filtered navigation, school switcher), top bar with ⌘K command
  palette, account menu with theme switching, mobile drawer, skip link.
- Sign-in, forgot/reset password, invitation acceptance, forced password change; routing proxy.
- Overview dashboard (real data), Settings index, Users (invite, roles, suspend), Roles & permissions (matrix editor),
  Account and Password & sessions.
- School configuration: profile & branding (live accent preview), policies (working week, attendance, fees,
  admissions, results), campuses, modules, numbering (live preview), academic years & terms (timeline with today
  marker), grades & sections (structure view, copy between years), subjects, streams & per-grade curriculum editor,
  rooms, calendar (almanac view), grading scales (band editor), departments & designations, audit log (diffs).

## Tests
`dotnet test backend/Scholar.slnx` — 15 unit + 34 integration tests (real PostgreSQL), all passing.
Frontend: `npx tsc --noEmit` and `npm run lint` clean.

## Git
| | |
|---|---|
| Branch | `main` → `origin` https://github.com/MuhammadJunaidQamar/scholar-suite (private) |
| Commits so far | `2fe23d5` docs · `4513974` solution · `92e09dc` db · `106b822` auth · `6811202` access · `e2d3d8f` seed · `47e195b` ui tokens · `1254d9f` data table · `3a32b8b` api overview/audit · `7e0f586` web shell · `bd9cb01` key-generation fix · `f197f81` org APIs · _settings screens (this commit)_ |
| Latest pushed | every commit above is pushed; verify with `git log origin/main -1` |

## Known problems / decisions to revisit
- Sections 7–14 of the original brief were not received; assumptions in PRODUCT_REQUIREMENTS §4.
- Data-protection keys are stored in PostgreSQL unencrypted at rest (dev); production should add key encryption.
- Development account e-mails (reset/invitation links) are written to the API log until the communication module ships.
- Signed-in screens are client-rendered behind the session gate; Next.js instant-navigation validation is set to
  `manual-warning` accordingly.
- QuestPDF licensing must be reviewed for organisations above USD 1M revenue (when PDF generation lands).

## Next tasks
1. M4 — students (profiles, enrolment, guardians, documents, import/export) and admissions pipeline.
2. M5 — attendance; M6 — fees and ledger.
3. Logo/crest upload once secure file storage lands (M4 documents).
