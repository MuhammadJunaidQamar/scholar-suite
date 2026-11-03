# Progress

_Last updated: 2026-11-03 (M5)_

## Current stage
**M5 complete (attendance) — awaiting review before M6.** Staff directory, student and staff attendance, corrections,
leave, guardian alerts through an honest outbox, reports and the teacher's day work end to end on PostgreSQL +
Next.js, verified by integration tests, browser end-to-end checks, and screenshots in light and dark themes and at
phone width. Deployed to the Tailscale preview.

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
- **Staff (M5):** directory (`staff`, EMP numbering, campus-scoped), status changes that release a leaver's classes,
  sign-in access with honest handover, class teacher per section and subject teachers (`section_subject_teachers`).
  Staff, staff-attendance and leave permissions are core (not gated by the optional HR module); new
  `staff.attendance.approve`.
- **Attendance (M5):** daily roll call and period registers (unique per section/day/period in the database),
  suggested marks from approved leave and suspensions, holiday/weekly-off/future/out-of-year rules with a deliberate
  make-up-day override, locking after the day (+ `lock_after_hours`), optimistic concurrency, corrections with
  approval (approvers' own apply at once; stale requests refused), student leave (approval excuses recorded
  absences, cancelling reverts), bell times per campus, staff register with check-in/out, staff corrections
  (never decided by the person themselves), leave types with yearly allowances and working-day counting, reports
  (today, trend, absentee call list, monthly register CSV/XLSX, a student's year incl. subject attendance, below
  minimum with export, staff month with export), teacher's day (`/api/teach/today`, own attendance and requests).
- **Outbox (M5):** `outbound_messages` with a dedup key per guardian/channel/student/day, dispatched after commit,
  re-dispatched by a one-minute sweeper; no provider is bundled, so alerts are recorded as **Not sent** with the
  reason — nothing is ever reported as sent or delivered. Attendance marks are audited on change, not on creation.
- Development demo seed: Greenfield Grammar School (Lahore), 2 campuses, 2025–26 / 2026–27 / 2027–28 (admissions)
  years, 13 grades, 31 sections, 40 rooms, 16 subjects with curriculum, calendar, 834 students in ~550 families,
  11 leavers, 39 applications at every stage, 14 inquiries; **M5:** 90 staff (every section has a class teacher;
  189 subject assignments), 60 teacher sign-ins (main demo teacher `teacher@greenfield.example`, class teacher of
  Class 5 – A), bell times for both campuses, 78 school days of registers since 3 Aug (64k marks, realistic habits
  incl. chronic absentees), 31 student leave applications, corrections in every state, the staff register (7k
  days), 27 staff leave requests and 8.4k guardian alerts recorded as not sent.

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
- **Attendance (M5):** `/attendance` (today: registers taken/outstanding, figures, needs-attention, absent today),
  `/attendance/register/[sectionId]` (fast keyboard entry, alert preview, lock state, correction requests and
  in-place approval, period selector in period-wise schools), `/attendance/absentees` (call list with each alert's
  real state), `/attendance/corrections`, `/attendance/leave`, `/attendance/reports` (monthly register grid +
  export, below minimum + export, daily trend chart with table view), `/attendance/staff` (daily staff register,
  month grid + export, corrections), `/attendance/staff-leave`; Attendance tab on student profiles.
- **Staff (M5):** `/staff` directory and `/staff/[id]` profiles (classes, sign-in access, attendance & leave);
  settings: Teaching assignments, Bell times, Leave types.
- **My day (M5):** `/teach` — the teacher's registers for today, own attendance this month, leave balances,
  leave requests and self-service corrections.

## Tests
`dotnet test backend/Scholar.slnx` — 15 unit + 66 integration tests (real PostgreSQL), all passing. M5 adds staff
directory (numbering, scoping, assignments, leavers, sign-in handover), student attendance (class-teacher rights,
completeness, honest alerts and dedup, cancellation on correction, concurrency, audit, holidays/future/make-up days,
database uniqueness, locking and corrections, stale corrections, leave approval/cancel, reports, period registers)
and staff attendance/leave (register, self corrections, no self-approval, allowances).
Browser end-to-end check (Playwright, 20 checks): teacher takes a register by keyboard, honest alert wording,
correction request/withdraw, office leave record/cancel, staff register edit, staff add/delete, monthly export,
API refusal of another class's register.
Frontend: `npx tsc --noEmit` clean, `npm run lint` 0 errors (9 pre-existing React-Compiler `watch()` warnings),
`npx next build` passes.

## Git
| | |
|---|---|
| Branch | `main` → `origin` https://github.com/MuhammadJunaidQamar/scholar-suite (private) |
| M0–M3 | `2fe23d5` … `8c0ed0f` |
| M4 | `1018a90` students/admissions API · `e1d1926` per-period sequences · `d5cfcb9` demo students · `2e0f9f8` stable paging · `08c6c57` bulk moves · `ad6538c` section headcounts · `1759374` re-admission · `51dde3f` request logging · `4961c2b` students UI · `b31eddb` honest account handover · `81bd5d5` guardian campus scoping · `00ad6f4` guardians UI · `b613fd1` document lists, public crest · `2bd955e` admissions UI · `9640661` crest upload · `e4e15bf` build & phone fixes |
| M5 | `4e28c00` staff directory · `9d897cf` attendance data model · `54f44d7` attendance API · `47e435f` demo staff & attendance · `f80712b` alert policy on registers · `ce617b1` staff UI & teaching settings · `842cbb7` attendance workspace · `db078f4` staff delete fix |
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
- **Attendance (M5):** no SMS/WhatsApp/email provider is connected, so guardian alerts are recorded as not sent (the
  call list says so); providers plug in through `IMessageChannelProvider`. Alerts go out immediately for same-day
  registers (no "send at 10:00" delay yet). Period-wise registers have no timetable yet (M8): the teacher picks the
  period and the subject comes from their assignment. Staff photos/documents and leave attachments (e.g. medical
  certificates) are modelled but not yet uploadable. Families cannot yet request leave or see attendance in a portal
  (M11). Staff are not in ⌘K search yet. Attendance percentages are computed on request (fine at this size; add
  summaries if a school grows far beyond it). Biometric/RFID staff check-in is not integrated.

## Next tasks
1. Review of M5 by the product owner (do not start M6 before).
2. M6 — fees and ledger (structures, challans, collections with idempotency, double-entry, reversals, refunds).
3. M10 — communication: providers for the outbox (SMS/WhatsApp/email), delivery webhooks, templates; real
   invitations and offer reminders.
