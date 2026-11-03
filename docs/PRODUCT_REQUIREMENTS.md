# Product Requirements

> Working name: **Scholar Suite** (configurable — see *Branding* below).
> Status: living document. Last revised 2026-11-03.

## 1. Vision

A complete, commercial-grade school management platform that runs the full operational
lifecycle of a school — inquiry → admission → enrollment → daily academic life → examinations →
promotion → graduation/alumni — together with the finance, HR, communication and logistics
operations that surround it.

It must feel like a carefully designed product, not a set of CRUD screens:

- **Fully functional.** Every screen is backed by real persisted data and real business rules.
- **Understandable** by non-technical administrators; workflows read like the school's own process.
- **Fast** with thousands of students and hundreds of thousands of attendance/ledger rows.
- **Secure and isolated.** Multiple schools (tenants) and campuses on one platform with enforced data isolation.
- **Pakistan first, international by design.** PKR, B-Form/CNIC, Mon–Sat weeks, matric/intermediate
  streams and Asia/Karachi out of the box — but currency, timezone, locale, identifiers, grading and
  calendars are all per-school configuration.

## 2. Users and portals

| Portal | Audience | Purpose |
|---|---|---|
| **Administration** (`/`) | Owner, administrator, principal, vice principal, registrar, admissions officer, accountant, HR manager, librarian, transport manager, nurse | The school's command centre. Navigation is filtered by permission. |
| **Teacher** (`/teach`) | Teachers, class teachers | My classes, timetable, attendance, assignments, marks, students, messages, leave. |
| **Family** (`/family`) | Parents & guardians | Child switcher, attendance, results, fees/challans/receipts, homework, notices, leave, messages. |
| **Student** (`/learn`) | Students | Timetable, assignments & submissions, attendance, results, materials, notices. |
| **Platform** (`/platform`) | Platform super administrators | Tenant (school) provisioning, suspension, platform health. |

A user may hold several roles (e.g. a teacher who is also a parent). The shell offers a portal
switcher when a user has access to more than one portal.

### Roles (seeded per school, editable)

Platform Super Administrator · School Owner · School Administrator · Principal · Vice Principal ·
Admissions Officer · Registrar · Accountant · HR Manager · Teacher · Class Teacher · Librarian ·
Transport Manager · Nurse/Medical Officer · Parent/Guardian · Student.

Roles are bundles of **permissions** (`module.action`, e.g. `students.view`, `fees.payments.reverse`).
Schools can create custom roles and change permissions of non-locked system roles. Role
assignments can be limited to specific campuses.

## 3. Functional scope

Each module below is specified in detail in [MODULES_AND_WORKFLOWS.md](MODULES_AND_WORKFLOWS.md)
and modelled in [DATABASE_DESIGN.md](DATABASE_DESIGN.md).

| # | Module | Highlights |
|---|---|---|
| A | School configuration | Profile & branding, campuses, academic years/terms, grades, sections, rooms, subjects & groups, departments, designations, working days, holidays, calendar, grading scales, numbering sequences, document templates, module toggles |
| B | Identity & access | Login, lockout, password reset, sessions & device list, refresh-token rotation, CSRF, roles, granular permissions, campus scoping, audit log |
| C | Admissions | Inquiries, online & office applications, documents, review, entrance tests, interviews, decisions, waiting list, offers, admission-fee challan, enrollment, configurable lifecycle |
| D | Students | Rich profile, enrollment history, guardians, emergency contacts, medical alerts, documents, timelines of attendance/fees/results/discipline/transport/library, search, filters, bulk import/export, status changes, transfers, withdrawals, alumni |
| E | Guardians | Profiles, multi-student relationships with per-link portal permissions, preferences, portal accounts |
| F | Staff & HR | Profiles, recruitment, employee IDs, qualifications, history, contracts, documents, assignments, staff attendance, leave, performance, salary structures, payroll, payslips, allowances/deductions, advances & loans |
| G | Academics | Curriculum, subject & class-teacher assignment, bell schedules, timetables with conflict detection, substitutions, lesson plans, homework/assignments & submissions, resources, syllabus progress, promotions, retention, graduation |
| H | Attendance | Daily & period-wise registers, configurable statuses, fast bulk entry, corrections with approval, student leave, reports, percentages, guardian absence alerts, import/export |
| I | Fees & finance | Fee heads, class & student fee structures, concessions/scholarships with approval & validity, billing runs, challans, late fees, installments, partial/advance payments, allocations, receipts, reversals, refunds, waivers, cash registers, bank accounts, double-entry ledger, daily collection, defaulters, statements, reconciliation |
| J | Examinations | Exam types, sessions, schedules, papers, max/pass marks, grading scales, weighted assessment schemes, bulk marks entry, validation, approval, publication, report cards, transcripts, positions, analysis, promotion decisions |
| K | Communication | Announcements/notices/circulars with audiences, in-app notifications, email/SMS/WhatsApp through provider abstraction, templates, scheduled messages, delivery logs with honest statuses, preferences, parent–teacher messaging |
| L | Library | Catalogue, ISBN, authors, publishers, categories, copies/accession numbers, issue/return/renew, reservations, fines, lost/damaged, histories, reports |
| M | Transport | Vehicles, drivers & attendants, routes & stops, student allocations feeding fees, maintenance & fuel, boarding attendance, reports |
| N | Inventory & assets | Categories, items, suppliers, purchase orders, receipts, issues, returns, stock ledger, low-stock alerts, asset register, allocation, maintenance |
| O | Welfare & discipline | Restricted incidents, actions, counselling, parent meetings, follow-ups, medical visits, health alerts |
| P | Events & facilities | School calendar, events, PTMs, room reservations without double-booking, facility maintenance requests, event notifications |
| Q | Documents & certificates | Secure file storage, templates, ID cards, bonafide/character/transfer/leaving/fee-clearance certificates, report cards, payslips, challans, receipts as PDF |
| R | Hostel & cafeteria | Optional per-school modules: hostels/rooms/beds/allocations/check-in-out/attendance/incidents; menus, meal plans, subscriptions, orders, payments |
| S | Reporting & analytics | Database-backed dashboards & reports across every module with date ranges, filters, comparisons and CSV/XLSX export |

## 4. Non-functional requirements

These consolidate the brief's explicit requirements and — because sections 7–14 of the original
brief were not received — the reasonable expectations of a commercial product of this kind.
They will be reconciled with the original text if it is supplied.

### 4.1 Security & privacy
- Authorization is enforced on the server for every request: permission **and** tenant **and** campus
  **and** ownership (teacher → own classes, guardian → linked children with granted access, student → self).
- Passwords hashed with ASP.NET Core Identity (PBKDF2), lockout after repeated failures, password
  policy, password reset via single-use time-limited token, forced reset for provisioned accounts.
- Tokens never reach JavaScript: browser sessions use HttpOnly cookies; refresh tokens rotate and
  token reuse revokes the session family. Double-submit CSRF token for cookie-authenticated writes.
- Sensitive data (payroll, bank details, medical, counselling, discipline) behind dedicated permissions,
  excluded from general DTOs, and audit-logged on access where appropriate.
- Files are private: served only through authenticated, authorized endpoints. No public object URLs.
- Rate limiting on authentication and public endpoints. Security headers. No secrets in source control.
- Audit trail for create/update/delete of business records and every financial event.

### 4.2 Performance & scale
- All lists are server-paginated, sorted and filtered; nothing loads "all rows" into the browser.
- Targets (single modest server): p95 < 300 ms for list/detail APIs at 10k students; attendance
  register for a 60-student class saves in one request; billing run for 5k students < 60 s.
- Indexes on every tenant-scoped access path; trigram search for names; aggregate queries in SQL.

### 4.3 Data integrity
- PostgreSQL is authoritative. FK constraints, unique constraints, check constraints, and
  DB-level guards (balanced, immutable journal; no double-booked rooms).
- Financial operations are atomic, idempotent (Idempotency-Key), and never destructive — reversals,
  credit notes and refunds are new records linked to the original.
- Timestamps stored in UTC (`timestamptz`); dates that are calendar concepts stored as `date`;
  presentation in the school's timezone.
- Optimistic concurrency on frequently edited aggregates.

### 4.4 Usability & accessibility
- WCAG 2.2 AA: keyboard reachable, visible focus, labelled controls, sufficient contrast in both themes.
- Forms: logical groups, inline validation, clear required markers, sensible defaults, autosave of
  drafts where long, destructive actions confirmed, success/failure feedback via toasts.
- Every list has designed empty, loading and error states. Mobile-friendly layouts for parent,
  student and teacher portals (attendance taking on a phone is a first-class scenario).

### 4.5 Localisation
- English (en-PK) first. Strings structured for translation; RTL-safe layout primitives so Urdu can follow.
- Currency, number and date formatting through the school's locale/currency (default PKR, `en-PK`, `DD MMM YYYY`).
- National identifiers configurable (B-Form/CNIC labels and format validation are Pakistan defaults).

### 4.6 Operability
- Structured logging (Serilog) with correlation IDs; health checks; OpenAPI documentation.
- Database migrations are versioned and applied explicitly; seed data only in development.
- Configuration via environment/secret stores; twelve-factor friendly; container-ready.

### 4.7 Quality
- Automated tests (xUnit): domain rules, application services, API integration tests against a real
  PostgreSQL database (never an in-memory substitute), including tenant-isolation and ledger tests.
- Type-checked frontend (`strict`), lint-clean, builds reproducibly.

## 5. Branding

The product name, logo and default accent are platform configuration
(`Platform:ProductName` in the API, `NEXT_PUBLIC_PRODUCT_NAME` in the web app). Each school
additionally configures its own name, crest/logo, accent colour and document letterheads; the
school's identity is what users see first inside the app. No school or product name is hardcoded
in components.

## 6. Assumptions

- One PostgreSQL database hosts all tenants (shared schema, `school_id` discriminator). Per-tenant
  databases can be introduced later behind the same abstractions if a customer requires it.
- A person has **one** login across the platform and a membership per school (e.g. a parent with
  children at two schools on the platform switches school context instead of holding two accounts).
- Online payment gateways (e.g. 1Link, JazzCash, Easypaisa, card acquirers) and SMS/WhatsApp vendors
  are integrated through provider interfaces; the reference implementation ships development
  providers that record intent honestly and never claim delivery or settlement.
- **Attendance (M5):** statuses are fixed (Present, Late, Absent, Leave) rather than school-defined codes, so
  percentages and exports mean the same everywhere; schools choose whether Leave counts as attended. The morning roll
  call is the official daily record even in period-wise schools. Guardian alerts go out for same-day registers only.
  The staff leave year is the academic year containing the leave (calendar year when no year covers it).
- PDF generation uses QuestPDF (free Community licence below USD 1M annual revenue; a commercial
  licence is required above that threshold — flagged for the business).
