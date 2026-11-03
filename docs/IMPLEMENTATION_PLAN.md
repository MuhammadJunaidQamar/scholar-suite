# Implementation Plan

Development is iterative: every milestone leaves the system building, migrating and tested, and every
logical unit is committed and pushed to `origin/main` (github.com/MuhammadJunaidQamar/scholar-suite, private).

## Initial assessment (2026-11-03)

| Item | Finding | Action |
|---|---|---|
| Repository | `~/projects/scholar-suite` existed but was empty; not a git repo; no remote | `git init -b main`; private GitHub repo created with the owner's approval |
| .NET | SDK 10.0.403 at `~/.dotnet` (runtime 10.0.14), linux-arm64 | Target `net10.0` |
| Node | v22.23.4, npm 10.9.9 | Next.js 16 |
| PostgreSQL | Not installed | Installed PostgreSQL 16 from Ubuntu 24.04 packages; local cluster on 5432 |
| Docker | Not available | Integration tests use a dedicated local PostgreSQL test database instead of Testcontainers |
| Library versions | Some current majors (TanStack Table 9, TypeScript 7, @hookform/resolvers 7) are newer than the team's verified API knowledge | Pin to latest well-understood majors: TanStack Table 8, TypeScript 5.9, resolvers 5; revisit later |
| Brief | Sections 7–14 missing from the received text | Requirements inferred and recorded in PRODUCT_REQUIREMENTS §4 |

## Milestones

Each bullet is roughly one commit (conventional commit message in brackets).

### M0 — Foundations
- Docs, repo hygiene (`docs: …`, `chore: …`).
- Backend solution & layering, Serilog, OpenAPI/Swagger, health checks, Problem Details (`chore(backend): initialize solution architecture`).
- PostgreSQL configuration, `AppDbContext`, conventions, interceptors (audit, tenant guard), first migration (`feat(db): …`).
- Integration test harness against real PostgreSQL (`test: …`).

### M1 — Platform, identity & access
- Schools, campuses, memberships; tenant context (`feat(platform): …`).
- Identity, sessions, cookie/JWT tokens, refresh rotation with reuse detection, CSRF, lockout, password reset (`feat(auth): …`).
- Permission catalogue, roles, assignments, policy enforcement, campus scope; seeded system roles (`feat(access): …`).
- Tests: auth flows, CSRF, tenant isolation, permission enforcement.

### M2 — Frontend foundation
- Next.js app, Tailwind tokens, typography, light/dark themes (`feat(ui): design tokens and theming`).
- Design-system components: buttons, inputs, selects, checkbox, dialogs, drawers, dropdowns, tabs, badges,
  toasts, tooltips, skeletons, empty/error states (`feat(ui): design system primitives`).
- Data table (server pagination/sort/filter/columns/selection/bulk/export) (`feat(ui): data table`).
- API client (CSRF, refresh single-flight, Problem Details) + auth pages (`feat(web): authentication`).
- Application shell: sidebar groups, collapse, top bar, command palette search, notifications, account menu (`feat(ui): application shell`).

### M3 — School configuration
- Academic years/terms, grades, sections, rooms, subjects, curriculum, departments, designations, calendar & holidays,
  grading scales, numbering, school profile/branding, modules (`feat(org): …`, `feat(web): settings …`).

### M4 — Students, guardians, admissions
- Students & guardians APIs + list/profile/forms, import/export (`feat(students): …`).
- Admissions inquiries/applications/assessments/decisions/enrollment, online application (`feat(admissions): …`).

### M5 — Attendance
- Status definitions, registers, fast entry UI, corrections, leave, reports, absence alerts (`feat(attendance): …`).

### M6 — Finance
- Chart of accounts, registers, bank accounts, ledger posting with DB guards (`feat(finance): ledger`).
- Fee heads, structures, student items, concessions with approvals (`feat(fees): configuration`).
- Billing runs, invoices/challans, late fees (`feat(fees): billing`).
- Collection, allocation, receipts, idempotency, reversals, refunds, waivers (`feat(fees): collection`).
- Reports: daily collection, defaulters, statements, trial balance, reconciliation (`feat(fees): reports`).
- Integration tests for every posting rule and concurrency/idempotency guard.

### M7 — Staff, HR, payroll
- Staff profiles, qualifications, contracts, documents, assignments, staff attendance, leave (`feat(staff): …`).
- Salary structures, payroll runs, payslips, loans (`feat(payroll): …`).

### M8 — Academics
- Bell schedules, timetable with conflict detection, substitutions (`feat(academics): timetable`).
- Assignments & submissions, lesson plans, resources, promotions (`feat(academics): …`).

### M9 — Examinations
- Exam types/sessions/papers, marks entry, computation, approval/publication, report cards (`feat(exams): …`).

### M10 — Communication
- Announcements, notifications, templates, outbox worker, providers, delivery log, messaging (`feat(comms): …`).

### M11 — Portals
- Teacher, family, student portals (`feat(portal): …`).

### M12 — Dashboards & reporting
- Role dashboards with real data, report centre with exports (`feat(reports): …`).

### M13 — Operations modules
- Library, transport, inventory & assets, welfare, events & facilities, documents & certificates (`feat(<module>): …`).

### M14 — Optional modules & hardening
- Hostel, cafeteria (module toggles) (`feat(hostel): …`, `feat(cafeteria): …`).
- Performance pass (indexes, query plans), accessibility pass, security review, deployment assets.

## Definition of done (per unit)
1. Backend builds with zero warnings treated as errors in new code; migrations generated and applied.
2. Relevant tests pass (`dotnet test`), including new tests for the unit's rules.
3. Frontend type-checks, lints and builds; key screens verified visually in light and dark themes.
4. No secrets or build outputs staged; `docs/PROGRESS.md` updated.
5. Commit with a conventional message; push to `origin/main`; push verified.
