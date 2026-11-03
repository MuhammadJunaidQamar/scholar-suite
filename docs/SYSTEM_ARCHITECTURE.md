# System Architecture

> Living document. Describes the architecture as built; deviations are recorded here when made.

## 1. Overview

```
                ┌──────────────────────────── Browser ────────────────────────────┐
                │ Next.js app (React, TypeScript, Tailwind, TanStack Query/Table) │
                │ Admin · Teacher (/teach) · Family (/family) · Student (/learn)  │
                └───────────────┬─────────────────────────────────────────────────┘
                                │ same-origin  /api/*   (HttpOnly cookies + CSRF header)
                ┌───────────────▼───────────────┐
                │ Next.js server (rewrite proxy)│   future mobile apps ──► Bearer tokens
                └───────────────┬───────────────┘                          │
                                │                                          │
                ┌───────────────▼──────────────────────────────────────────▼──┐
                │ ASP.NET Core 10 Web API — modular monolith                  │
                │  Api ─► Application ─► Domain        Infrastructure (EF,    │
                │                                       Identity, files, PDF, │
                │  Background workers: outbox, notifications, scheduled jobs  │
                └───────────────┬─────────────────────────────────────────────┘
                                │ Npgsql / EF Core 10
                        ┌───────▼────────┐        ┌──────────────────────────┐
                        │ PostgreSQL 16  │        │ File storage (local disk │
                        │ (authoritative)│        │ now; S3-compatible later)│
                        └────────────────┘        └──────────────────────────┘
```

* **Modular monolith.** One deployable API, internally divided by business module. Modules share one
  database and one `DbContext` but own their tables, services and endpoints. Cross-module calls go
  through application services, never through another module's tables in ad-hoc SQL.
* **Backend-for-everything.** The same versioned HTTP API serves the web app and future mobile apps.
  The web app is a pure API client; it holds no business rules that the API does not also enforce.

## 2. Repository layout

```
backend/
  Scholar.slnx
  src/
    Scholar.Domain/          entities, value objects, enums, domain rules (no framework deps besides Identity stores' base types)
    Scholar.Application/     use-case services, DTOs, validators, interfaces (IAppDbContext, ICurrentUser, IClock, IFileStorage …)
    Scholar.Infrastructure/  EF Core (AppDbContext, configurations, migrations, interceptors), Identity, token service,
                             file storage, PDF rendering, notification providers, background workers, seeding
    Scholar.Api/             HTTP endpoints (minimal APIs grouped per module), auth wiring, middleware, OpenAPI
  tests/
    Scholar.UnitTests/        pure domain/application rules
    Scholar.IntegrationTests/ API tests via WebApplicationFactory against a real PostgreSQL test database
frontend/                     Next.js application
docs/                         product, architecture, database, workflows, plan, progress
scripts/                      developer setup (database bootstrap, seeding helpers)
```

### Module map

| Module | Domain / Application folder | API prefix |
|---|---|---|
| Platform & tenancy | `Platform` | `/api/platform` |
| Identity & access | `Identity`, `Access` | `/api/auth`, `/api/access` |
| School configuration | `Organization` | `/api/org/*` |
| Students & guardians | `Students` | `/api/students`, `/api/guardians` |
| Admissions | `Admissions` | `/api/admissions` |
| Academics | `Academics` | `/api/academics/*` |
| Attendance | `Attendance` | `/api/attendance` |
| Finance | `Finance` | `/api/finance/*` |
| Staff, HR, payroll | `Staff`, `Payroll` | `/api/staff`, `/api/hr`, `/api/payroll` |
| Examinations | `Exams` | `/api/exams` |
| Communication | `Communication` | `/api/comms` |
| Library, Transport, Inventory | `Library`, `Transport`, `Inventory` | `/api/library` … |
| Welfare | `Welfare` | `/api/welfare` |
| Events & facilities | `Events` | `/api/events`, `/api/facilities` |
| Documents | `Documents` | `/api/files`, `/api/documents` |
| Hostel, Cafeteria (optional) | `Hostel`, `Cafeteria` | `/api/hostel`, `/api/cafeteria` |
| Reporting | `Reporting` | `/api/reports`, `/api/dashboard` |
| Portals | `Portals` | `/api/portal/{teacher,family,student}` |

## 3. Layering rules

* `Domain` has no dependency on Application/Infrastructure/Api. Entities guard their own invariants
  (e.g. `Invoice.ApplyAllocation`, `AcademicYear.Close`); state transitions are methods, not setters.
* `Application` depends on `Domain`. It talks to persistence through `IAppDbContext` (exposes
  `DbSet<T>`s and transaction helpers). We deliberately do **not** wrap EF Core in generic
  repositories — `DbSet<T>` already is one. Dedicated query services exist where a query is complex or reused.
* `Infrastructure` implements Application interfaces. It is the only layer that knows Npgsql, file
  systems, QuestPDF or messaging vendors.
* `Api` composes everything, maps HTTP ⇄ application calls, and owns authentication middleware.

## 4. Multi-tenancy and data isolation

* **Tenant = School.** Every tenant-owned table carries `school_id` (FK, indexed, leading column of
  composite indexes and unique constraints).
* **Tenant context** comes from the authenticated session (`ICurrentUser.SchoolId`), never from a
  request parameter. Platform administrators enter a school explicitly; that is audit-logged.
* **Enforcement, defence in depth:**
  1. EF Core *named global query filters* (`"Tenant"`) on every `ITenantEntity` restrict reads to the current school.
  2. A `SaveChanges` interceptor stamps `SchoolId` on new tenant entities and **rejects** writes whose
     `SchoolId` differs from the current tenant.
  3. Application services resolve referenced IDs through tenant-filtered sets, so a foreign ID from
     another school behaves exactly like "not found".
  4. Integration tests assert that a user of school A cannot read or modify school B data through any endpoint family.
* **Campus scoping.** Role assignments may be limited to campuses. `ICurrentUser.CampusScope` is either
  *all campuses* or a set; campus-owned aggregates (students, sections, staff, registers, invoices, payments)
  are filtered by it in their services.
* **Ownership scoping** for portals: teacher endpoints resolve the teacher's sections/subjects;
  family endpoints resolve guardian ↔ student links with per-link grants (`can_view_fees`, …);
  student endpoints resolve the student linked to the user.

## 5. Authentication

* **Identity:** ASP.NET Core Identity Core (`AddIdentityCore<User>`) for password hashing, lockout,
  security stamps and data-protection reset tokens. Users are **global**; `school_memberships` link a user
  to the schools they belong to. Login identifier is email or username (students get generated usernames).
* **Sessions:** a login creates a `user_session` (device, IP, user agent, last seen, active school).
  Sessions are listed and revocable by the user and by administrators.
* **Tokens:**
  * Access token — JWT (HS256 with a configured key; RS256-ready), 15 minutes, claims: `sub`, `sid`, `school`.
    Permissions are **not** embedded: they are resolved server-side (cached briefly) so revocation is immediate.
  * Refresh token — 256-bit random, stored only as SHA-256 hash, 14-day sliding/30-day absolute lifetime,
    **rotated on every use**. Presenting an already-used refresh token revokes the whole session
    (stolen-token detection).
* **Browser transport:** tokens live in HttpOnly cookies — `ss_at` (Path=/api, SameSite=Lax) and
  `ss_rt` (Path=/api/auth, SameSite=Strict); `Secure` and the `__Host-`/`__Secure-` prefixes are used
  outside development. Nothing sensitive is stored in `localStorage`.
* **CSRF:** double-submit token. The API issues a non-HttpOnly `ss_csrf` cookie whose value is an HMAC
  of the session id; every unsafe request authenticated by cookie must echo it in `X-CSRF-Token`.
  Requests authenticated by `Authorization: Bearer` (mobile) are exempt because browsers cannot forge that header.
* **Mobile:** `/api/auth/token` and `/api/auth/refresh` accept/return tokens in the body when the client
  identifies as a native client; same rotation rules.
* Each request validates that the session is still active (memory-cached for 30 s) — revoking a session or
  changing a password ends access within that window.

## 6. Authorization

* **Permission catalogue** in code (`Permissions` static class): `module.resource.action` strings with
  metadata (module, label, description, sensitivity). Seeded into the UI for role editing.
* **Roles** are per school (`roles`, `role_permissions`); seeded system roles have a stable `key`.
  A role declares its **portal** (admin, teacher, family, student) which drives landing and navigation.
* **Assignments** `user_roles(user, school, role, campus?)`.
* **Policy enforcement:** endpoints declare `.RequirePermission(Permissions.Students.View)`; a policy
  provider turns permission names into requirements; the handler checks the user's effective permissions
  for the active school. Services additionally assert permissions for sensitive branches (e.g. returning
  payroll fields) and enforce campus/ownership scope.
* **Sensitive data** (payroll, bank details, medical, welfare) uses separate permissions and separate DTOs;
  list endpoints never include it.
* The frontend receives the user's effective permission list (`/api/auth/me`) only to shape the UI.

## 7. Persistence

* EF Core 10 + Npgsql, snake_case naming (`EFCore.NamingConventions`), migrations in `Scholar.Infrastructure`.
* Keys: UUID v7 (`Guid.CreateVersion7()`), time-ordered for index locality.
* Money: `numeric(18,2)`; quantities `numeric(12,3)`; percentages `numeric(7,4)`.
* Time: `timestamptz` (UTC `DateTimeOffset`/`DateTime` UTC) for instants; `date` (`DateOnly`) and `time` (`TimeOnly`)
  for calendar concepts. The school's IANA timezone converts "today" and display.
* Concurrency: PostgreSQL `xmin` as row version on editable aggregates; 409 Conflict surfaced to UI.
* Soft delete only where business-meaningful (documents, announcements, catalogue items) via a named
  `"SoftDelete"` filter. People and money are never deleted — they change status.
* Auditing: interceptor writes `audit_logs` (who, when, entity, changed columns old→new) for `IAuditable` entities.
* Extensions: `pg_trgm` (fuzzy name search), `btree_gist` (exclusion constraints for bookings), `citext` where useful.

## 8. Transactions, idempotency and the ledger

* Financial use cases run in an explicit transaction (`IAppDbContext.ExecuteInTransactionAsync`) at
  `ReadCommitted` with targeted row locks (`SELECT … FOR UPDATE`) on invoices/sequences they mutate.
* Document numbers (receipts, challans, admission numbers…) come from `number_sequences` via
  `UPDATE … RETURNING` inside the business transaction — gap-free and race-free.
* Write endpoints that create money movements require an `Idempotency-Key` header. The key is stored with
  a unique constraint on the resulting record; a replay returns the original result instead of a duplicate.
* The general ledger is double-entry: `journal_entries` + `journal_lines`. A deferred constraint trigger
  guarantees every entry balances at commit; another trigger makes posted lines immutable. Corrections are
  reversing entries. Student balances are the receivables sub-ledger (`journal_lines.student_id`);
  invoice/payments tables keep cached balances that are verified by the reconciliation report and tests.

## 9. Background processing

* `BackgroundService` workers poll PostgreSQL work queues with `FOR UPDATE SKIP LOCKED`
  (`outbound_messages`, `scheduled_jobs`). No external queue is required; workers are idempotent and safe to
  run on multiple instances.
* Jobs: dispatch notifications, scheduled announcements, daily late-fee application, overdue library
  checks, low-stock alerts, fee reminders.
* **Implemented (M5):** `outbound_messages` is written in the same transaction as the event that causes it (e.g. a
  register), then `MessageDispatcher` hands each message to the `IMessageChannelProvider` for its channel right after
  commit. `PendingMessageSweeper` (hosted service, every minute) re-dispatches anything left Pending — for example if
  the process stopped between commit and dispatch. With no provider registered a message is marked **NotSent** with
  the reason; `Queued`, `Sent` and `Delivered` are only ever set from what a provider reports.

## 10. Notifications

* Business code raises intents (`INotificationService.Enqueue(template, recipients, data)`); it never calls a vendor.
* Channels: in-app, email, SMS, WhatsApp. Each has a provider interface (`IEmailProvider`, `ISmsProvider`,
  `IWhatsAppProvider`) selected by configuration. Development providers write to the log and mark messages
  **Sent (unconfirmed)** — never Delivered.
* Status model (implemented): `Pending → NotSent` (no provider) or `Pending → Queued` (provider accepted) `→ Sent →
  Delivered`, with `Failed` and `Cancelled` (stopped before leaving the school). Only provider reports move a message
  past Queued; development uses no provider at all, so nothing is ever shown as sent.

## 11. Files and documents

* `IFileStorage` (local disk outside the web root now; S3-compatible later). `stored_files` holds metadata,
  checksum, owner (entity type + id), category and sensitivity.
* Downloads go through `GET /api/files/{id}` which authorizes by owner type (e.g. student documents need
  `students.documents.view` and campus scope; a guardian may fetch their child's report card). Same-origin
  cookies make `<img src="/api/files/…">` work without signed public URLs.
* PDFs (challans, receipts, report cards, payslips, certificates, ID cards) are rendered server-side with
  QuestPDF using the school's letterhead and templates.

## 12. API conventions

* JSON, camelCase, ISO-8601 dates; enums as strings.
* Lists: `?page=1&pageSize=25&sort=lastName&dir=asc&search=…&<filters>` → `{ items, page, pageSize, total }`.
* Errors: RFC 9457 Problem Details; validation errors include a field map; business-rule violations use
  `422` with a stable `code`; concurrency conflicts `409`.
* OpenAPI document at `/openapi/v1.json`, Swagger UI at `/swagger` (development and staging).
* Versioning: URL segment reserved (`/api/...` = v1); breaking changes go to `/api/v2`.

## 13. Logging and observability

* Serilog → console (JSON in production) with request logging, correlation id (`X-Request-Id`),
  user id and school id enrichment. Sensitive values (passwords, tokens, CNICs) are never logged.
* Health checks: `/health/live`, `/health/ready` (database).

## 14. Frontend architecture

* Next.js App Router, TypeScript strict. Route groups per portal: `(admin)`, `teach`, `family`, `learn`, `(auth)`.
* Server state via TanStack Query (query keys per module, invalidation on mutations); no global client store
  for server data. Forms via React Hook Form + Zod schemas mirrored from API validators.
* `lib/api` — a fetch client that adds the CSRF header, parses Problem Details, performs a single-flight
  token refresh on 401, and supports `Idempotency-Key`.
* `components/ui` — the design system (Radix primitives + Tailwind tokens). `components/data-table` — a
  TanStack Table wrapper with server-side pagination, sorting, filters, column visibility, selection,
  bulk actions and export.
* The Next.js `proxy` performs a cheap cookie-presence redirect to `/login`; the API remains the authority.
* Theming via CSS variables (light/dark) and a per-school accent colour applied at runtime.

## 15. Deployment (target)

* API and web app as two containers behind a reverse proxy (TLS, HSTS), PostgreSQL managed or self-hosted
  with PITR backups, file storage on a persistent volume or object store.
* Configuration through environment variables / secret store: `ConnectionStrings__Default`,
  `Auth__JwtSigningKey`, `Storage__Root`, provider credentials.
* Migrations applied by a release step (`dotnet ef database update` or the bundled migrator), not on app start in production.
