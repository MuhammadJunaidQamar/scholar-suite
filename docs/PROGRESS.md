# Progress

_Last updated: 2026-11-03_

## Current stage
**M4 complete → next: M5 (attendance).** Students, guardians, admissions (office and online) and private documents
work end to end on PostgreSQL + Next.js, verified with screenshots in light and dark themes and at phone width.
`next build` passes (all routes static or partially prerendered).

## Completed

### Backend (ASP.NET Core 10, EF Core 10, PostgreSQL 16)
- Modular-monolith solution (Domain / Application / Infrastructure / Api), central package management,
  warnings-as-errors, Serilog, OpenAPI + Swagger UI, health checks (`/health/live`, `/health/ready`), RFC 9457 errors.
- Persistence: snake_case schema, UUID v7 keys, named `Tenant`/`SoftDelete` query filters, tenant write guard,
  audit interceptor (field-level history), xmin concurrency, restrict-by-default FKs.
- Number sequences: format per school (`number_sequences`) and **one counter per period**
  (`number_sequence_counters`), so back-dated documents never collide; gap-free under rollback, serialised under
  concurrency; admins may move a counter forward (never back); restarting patterns must print their period.
- Lists: server paging with a stable tie-break on the time-ordered key (no repeated/missing rows across pages).
- Identity: global users + school memberships, ASP.NET Identity hashing/lockout/reset tokens, sessions,
  15-min JWT access tokens validated against live session state, hashed rotating refresh tokens with reuse
  detection (+ 20 s multi-tab grace), HttpOnly cookie transport, session-bound double-submit CSRF, bearer mode for
  mobile, password forgot/reset/change, session list/revoke, school switching, rate limiting, security headers.
- **Honest account handover:** notifiers return `NotSent / Queued / Sent`; with no e-mail provider (today), new staff,
  guardian and owner accounts get a one-time password shown once, never a claimed invitation. Administrators can
  issue one-time passwords (sessions ended, audited; refused for own account, platform admins, accounts shared with
  another school, or more privileged roles). Sign-in pages learn system-wide whether reset links can be e-mailed.
- Access: 122-permission catalogue, 15 seeded system roles, campus-scoped assignments, cached effective access with
  immediate invalidation, escalation guards (cannot grant what you lack; owner role owner-only; last owner kept).
- Platform: school provisioning with pluggable module steps (roles, numbering, designations, departments, grading).
- Organization: overview, school profile/policies/modules/numbering, crest (public crest endpoint), campuses,
  academic years & terms, grades, sections (with enrolled counts), subjects, streams, curriculum, rooms, calendar,
  grading scales, departments & designations, audit trail.
- **Students:** records with B-Form/CNIC normalisation, placements per academic year, section capacity (row-locked),
  section moves (single and bulk, all-or-nothing), status changes (suspend, withdraw, transfer out, graduate, alumni,
  re-admit into a section), guardians (deduplicated by mobile + name, roles and portal permissions), emergency
  contacts, restricted medical profile, private documents & photos, CSV/XLSX export (view or selection), validated
  all-or-nothing CSV import, siblings, campus scoping throughout.
- **Guardians:** list/detail/edit/delete with campus scoping through their children, mobile lookup across the school
  that never names children the caller may not see, family-portal access and portal password reset.
- **Admissions:** inquiries (follow-ups, close/reopen, convert), applications with an enforced lifecycle, assessments,
  offer/waiting list/rejection decisions, acceptance, enrolment (student + guardian + placement + documents in one
  transaction), pipeline counts, public online form with duplicate detection.
- Search (⌘K) across students, guardians and applications, campus-scoped.
- Development demo seed: Greenfield Grammar School (Lahore), 2 campuses, 2025–26 / 2026–27 / 2027–28 (admissions)
  years, 13 grades, 31 sections, 40 rooms, 16 subjects with curriculum, calendar, 11 staff accounts, 832 students in
  ~550 families (922 guardians), 11 leavers, 39 applications at every stage, 14 inquiries.

### Frontend (Next.js 16, React 19, Tailwind 4)
- Design system "The Register": OKLCH tokens, school-derived accent clamped for legibility, light/dark themes,
  Inter + Source Serif 4 + JetBrains Mono, full control set, dialogs/sheets/menus/tooltips/tabs, states, toasts.
- Data table: URL-synced server paging/sort/search/filters, facet filters, column visibility + density, selection with
  bulk-action bar, row actions, skeleton/empty/error states.
- API client: same-origin proxy, CSRF header, single-flight cross-tab refresh, problem-details errors, downloads and
  private-file viewing through the session; private photos recover from an expired session.
- App shell, sign-in flows, overview, settings area (as in M3), Users (honest invitations, one-time password reset).
- **Students:** list (scopes, filters, export, import, bulk move/export), profile (overview, family, history, medical,
  documents, photo upload, status/section dialogs, concurrent-edit detection), new-student form with guardian lookup.
- **Guardians:** list and profile with family-portal access.
- **Admissions:** pipeline strip, applications list, application page with a guided "Next step" panel (and who acts
  when the viewer can't), office application form (also converts inquiries), inquiries list.
- **Public** `/apply/[schoolCode]`: branded online application form with receipt.

## Tests
`dotnet test backend/Scholar.slnx` — 15 unit + 55 integration tests (real PostgreSQL), all passing.
Frontend: `npx tsc --noEmit` clean, `npm run lint` 0 errors (9 React-Compiler `watch()` warnings in older settings
pages), `npx next build` passes.

## Git
| | |
|---|---|
| Branch | `main` → `origin` https://github.com/MuhammadJunaidQamar/scholar-suite (private) |
| M0–M3 | `2fe23d5` … `8c0ed0f` |
| M4 | `1018a90` students/admissions API · `e1d1926` per-period sequences · `d5cfcb9` demo students · `2e0f9f8` stable paging · `08c6c57` bulk moves · `ad6538c` section headcounts · `1759374` re-admission · `51dde3f` request logging · `4961c2b` students UI · `b31eddb` honest account handover · `81bd5d5` guardian campus scoping · `00ad6f4` guardians UI · `b613fd1` document lists, public crest · `2bd955e` admissions UI · `9640661` crest upload · `e4e15bf` build & phone fixes |
| Latest pushed | every commit above is pushed; verify with `git log origin/main -1` |

## Known problems / decisions to revisit
- Sections 7–14 of the original brief were not received; assumptions in PRODUCT_REQUIREMENTS §4.
- No e-mail/SMS provider yet (M10): invitations and resets are handed over as one-time passwords; reset links are
  only logged in development (`Notifications:WriteLinksToLog`). Invitation links will need a longer lifetime than the
  current 1-hour reset token once real delivery exists.
- Offers past their reply date are not yet expired automatically (needs the scheduled-job runner, M10).
- The Users page lists family-portal accounts alongside staff once guardians have logins; separate them in M11.
- Data-protection keys are stored in PostgreSQL unencrypted at rest (dev); production should add key encryption.
- Signed-in screens are client-rendered behind the session gate; the (app) layout streams under Suspense.
- QuestPDF licensing must be reviewed for organisations above USD 1M revenue (when PDF generation lands).

## Next tasks
1. M5 — attendance (daily/period registers, locking, leave, absence alerts through the honest outbox later).
2. M6 — fees and ledger (structures, challans, collections with idempotency, double-entry, reversals, refunds).
3. M10 — communication: outbox with provider-confirmed delivery states; then real invitations and offer reminders.
