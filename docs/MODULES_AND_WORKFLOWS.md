# Modules and Workflows

How each module behaves: actors, states, transitions, rules and side effects. Permissions are written
as `module.action` (see `Scholar.Domain/Access/Permissions.cs` for the authoritative catalogue).

---

## A. School configuration

**Setup sequence for a new school** (guided by the *Setup* checklist on the dashboard):
1. School profile & branding (name, crest, accent, contact, timezone, currency).
2. Campuses (at least the main campus is created with the school).
3. Academic year (+ terms) → mark as current.
4. Grades (levels) with promotion order (`next_grade`).
5. Sections per grade per campus for the current year; rooms.
6. Subjects → curriculum (`grade_subjects`).
7. Staff, departments, designations → class teachers & subject teachers.
8. Fee heads & fee structures; bank accounts & cash registers.
9. Grading scale; exam types; assessment scheme.

**Rules**
- Exactly one *current* academic year; closing a year requires all its sections' promotions decided.
- A grade's sections are per academic year, so history is preserved when the next year is created.
- "Copy structure from previous year" clones sections, curriculum and fee structures into a new year.
- Holidays (`calendar_events.is_holiday`) and non-working weekdays are excluded from attendance %.
- Numbering sequences per document type: pattern + reset policy (e.g. `ADM-{yyyy}-{seq:4}` yearly).

## B. Identity & access

**Login:** identifier + password → lockout after 5 failures for 15 min → session + tokens (cookies).
If `must_change_password`, the session is limited to `/auth/change-password` until changed.
**Refresh:** rotating; reuse of a consumed token revokes the session and logs a security event.
**Password reset:** request (always responds 202 to avoid account enumeration) → emailed single-use
token (1 h) → set new password → all sessions revoked.
**Provisioning:** creating a staff/guardian/student portal account issues an invitation (temporary
password or reset link through the notification system) and assigns the matching system role.
**Roles:** administrators edit custom roles and non-locked system roles in a permission matrix
(module rows × actions). `School Owner` is locked with full access; at least one owner must remain.
**Campus scope:** role assignment can be limited to campuses; data from other campuses is invisible.

## C. Admissions

```
Inquiry: New → Contacted → ApplicationIssued → Converted | Closed

Application:
 Draft → Submitted → UnderReview ─┬→ AssessmentScheduled → Assessed ─┐
                                  └──────────────────────────────────┤
                                          ┌──────────────────────────┘
                                          ▼
                        Offered ─→ Accepted ─→ Enrolled
                          │  └→ Expired (offer lapsed)
                Waitlisted ──→ Offered        Rejected        Withdrawn (any open state)
```
- **Lifecycle configuration** (school settings): entrance test required?, interview required?,
  admission fee required before enrollment?, offer validity days, documents checklist per grade.
- **Online application:** public form per school (`/apply/{schoolCode}`), rate limited; creates a
  `Submitted` application and acknowledges to the guardian.
- **Offer:** optionally issues an *admission fee invoice* (fee heads with category Admission) for the student-to-be.
- **Enrollment** (`admissions.enroll`) — one transaction: creates `student` (admission number from
  sequence), guardians (de-duplicated by phone/CNIC), `student_guardians` links, the `enrollment` in the
  chosen section (capacity checked), moves documents to the student, re-links the admission invoice, and
  records history. The application becomes read-only.
- **Waiting list:** ordered by rank; offering a seat to the next applicant is one action.

## D. Students

- **Profile** sections: overview · personal · guardians & contacts · enrollment history · attendance ·
  fees · results · documents · discipline (restricted) · medical (restricted) · transport · library · activity log.
- **Status changes:** Active ⇄ Suspended/Inactive; → Withdrawn / TransferredOut (exit date, reason,
  destination; ends enrollment; optional TC & fee clearance) → Alumni after graduation.
- **Section transfer** within a year: closes the active enrollment, opens a new one (history kept).
- **Bulk import:** CSV with a downloadable template; dry-run validation report (row errors) before commit;
  commit is all-or-nothing.
- **Export:** current filter → CSV/XLSX.
- **Siblings** are inferred from shared guardians (used by sibling concessions and family portal).

## E. Guardians

- One guardian can be linked to many students; each link carries relationship and **grants**
  (portal access, view attendance / results / fees, receives notifications, fee payer, pickup).
- Portal account creation for a guardian assigns the *Parent/Guardian* role; the family portal shows
  only students whose link has `portal_access` and only the granted areas.

## F. Staff & HR, payroll

- **Employee lifecycle:** Recruitment (opening → applicant pipeline → Hired creates a staff record) →
  probation → confirmation → active → leave → exit (resigned/terminated/retired; portal access revoked).
- **Leave:** request → approval by HR/Principal → balance deducted; approved leave shows in staff attendance.
- **Payroll run** for a month:
  1. *Draft:* select period; system snapshots each active staff member's salary structure.
  2. *Process:* compute earnings (basic + allowances), deductions (fixed/percent, loan installments,
     leave-without-pay = basic/working-days × LWP days, one-off adjustments) → payslips.
  3. *Approve* (different user than processor when configured) → payslips locked.
  4. *Mark paid* (bank/cash) → loan outstanding reduced; payslips available to staff as PDF.
- Payroll data is visible only with `payroll.view`; staff see their own payslips in the teacher portal.

## G. Academics

- **Timetable:** per section, grid of weekdays × bell periods. Placing a lesson validates:
  section slot free; teacher not teaching elsewhere in an overlapping period (including other campuses'
  bell schedules by clock time); teacher not marked unavailable; room not occupied; weekly periods vs.
  curriculum (`periods_per_week`) shown as allocation progress. Conflicts are explained, never silently overwritten.
- **Substitutions:** for an absent teacher on a date, suggest free teachers (no lesson in that period,
  not on leave), record the substitution; it appears in the substitute's daily schedule.
- **Assignments:** teacher drafts → publishes (students and guardians notified) → submissions (on time /
  late) → grading with feedback → returned. Missing submissions marked after due date.
- **Lesson plans:** Draft → Submitted → Approved/Returned (coordinator/principal).
- **Promotion** at year end: for each section, proposed decision from results (pass → promote to
  `next_grade`; fail → retain; final grade → graduate); staff override per student; applying the batch
  creates next-year enrollments in target sections in one transaction.

## H. Attendance

- **Daily register** per section per date (or per period for period-wise schools). Opening a register
  pre-fills everyone as *Present*, students on approved leave as *Excused*; the teacher only touches exceptions.
  Keyboard: ↑/↓ to move, P/A/L/E to set; "mark all" actions; one save submits the register.
- **Rules:** cannot take attendance on holidays/non-working days (override with permission); future dates blocked;
  submitted registers are editable by the taker until locked (end of day + configurable grace); after lock,
  changes go through **corrections** requiring `attendance.approve`.
- **Notifications:** on submit, guardians of absent (and optionally late) students are notified once per day.
- **Percentages:** present-equivalent ÷ countable days; Excused can be configured to count or be excluded.
- **Leave requests:** guardian or office submits → class teacher/admin approves → future registers auto-mark Excused.

## I. Finance

**Configuration:** fee heads → fee structures per year/grade (items with frequency and billable months) →
student fee items for specific charges (transport, hostel, custom) → concession types → student concessions
(Pending → Approved/Rejected → Revoked; validity window).

**Billing run** (`fees.invoices.generate`): choose period (e.g. *November 2025*), scope (campus / grade / section /
student), issue & due dates. For each active student:
1. Collect billable items for the period from the structure + student items (frequency & months).
2. Skip items already billed for that student+head+period (DB unique guard).
3. Apply approved, in-validity concessions per line (percentage or fixed, capped at the line amount).
4. Create invoice + lines; post journal entry. Preview (dry run) shows counts and totals before committing.

**Collection** (`fees.payments.collect`):
- Search student → outstanding invoices (oldest first) → amount, method, reference → allocation
  auto-distributes oldest-first (editable) → confirm. Requires `Idempotency-Key`.
- One transaction: lock invoices `FOR UPDATE`, create payment (receipt number), allocations, update invoice
  balances/status, post journal entry, enqueue receipt notification.
- Overpayment becomes **student credit** (unallocated on the payment) and is auto-applied to the next invoice.
- Cheques may be recorded as *PendingClearance*; clearance posts normally, bounce = reversal.

**Corrections — nothing is deleted:**
- *Payment reversal* (`fees.payments.reverse`, reason required): payment → Reversed, allocations reversed,
  invoice balances restored, reversing journal entry linked to the original.
- *Invoice cancellation*: only when no active allocations; mirror journal entry.
- *Waiver/write-off*: request → approval (`fees.adjustments.approve`) → posts to waiver expense.
- *Refund*: from student credit; request → approval → paid (cash/bank) → journal entry.

**Late fees:** daily job (or manual run) adds a `LateFee` line to overdue invoices according to school
rules (fixed once / per day after grace, cap), each posted to late-fee revenue; waivable.

**Reports:** daily collection (by register/method/user), cash register closing, outstanding & defaulters
(aging buckets), student statement (sub-ledger), revenue by head/month, concessions & scholarships,
trial balance, reconciliation (invoice cache vs ledger; payments vs bank).

## J. Examinations

```
ExamSession: Planned → Scheduled (papers & timetable) → MarksEntry → Review → Approved → Published
```
- **Papers** per grade × subject with date/time/room, max & pass marks.
- **Marks entry** grid per section × paper (or per student across papers); validation: 0 ≤ marks ≤ max,
  absent/exempt flags; teachers enter only their subjects; locks after Review starts.
- **Computation:** per student: subject %, grade by scale, total, percentage, GPA; outcome (pass all subjects
  ∧ overall ≥ threshold, configurable); positions per section and grade (dense ranking on percentage, ties share).
  Final/annual results combine sessions with the assessment scheme weights.
- **Approval** (principal) → **Publication** (guardians/students notified; results visible in portals;
  report card PDFs available).
- **Analysis:** subject averages, pass rates, distribution, top performers, teacher-wise performance.

## K. Communication

- Announcements: Draft → Scheduled/Published → Archived; audience by role group, campus, grade, section;
  channels in-app/email/SMS/WhatsApp; optional acknowledgement tracking.
- Automated messages via templates: absence alert, fee reminder, receipt, exam published, assignment due,
  leave decision, password reset/invitation.
- Delivery log per message with provider status and failures; retries with backoff; *Delivered* only on
  provider confirmation.
- Preferences: users mute categories per channel (critical security messages cannot be muted).
- Messaging: guardian ↔ teacher conversations about a specific student; teachers see only their students' guardians.

## L. Library
Catalogue titles → copies (accession numbers) → **issue** (borrower limits, copy available) → **renew**
(max renewals, no waiting reservation) → **return** (fine = days late × rate, cap) → fines paid/waived or
billed to the fee account; lost/damaged marks copy and charges replacement. Reservations queue per title; a
returned copy with a waiting reservation becomes *Ready* for that borrower.

## M. Transport
Vehicles (documents expiry alerts) → routes with ordered stops, vehicle, driver, attendant → student
allocation (route+stop, capacity checked) creates a student fee item for the transport fee head →
boarding attendance per trip → maintenance & fuel logs → route reports (occupancy, cost per student).

## N. Inventory & assets
Items with reorder levels → purchase orders (Draft → Approved → Ordered → Received/PartiallyReceived) →
receipts create stock movements → issues to staff/departments → returns → adjustments (reason) →
low-stock alerts. Assets: register with tag, location, assignee history, maintenance, disposal.

## O. Welfare & discipline (restricted)
Incident reported → under review → action(s) decided (warning, detention, suspension, counselling, parent
meeting) → follow-ups → closed. Counselling notes are visible only to `welfare.counseling.view`. Medical
visits by the nurse; teachers see only the student's public medical alert text.

## P. Events & facilities
Calendar of events/holidays/exams/PTMs; room booking with server-side overlap prevention (DB exclusion
constraint); maintenance requests with priority and assignee; PTM slots bookable by guardians; notifications
on publish.

## Q. Documents & certificates
Upload to entities with categories and sensitivity; certificates generated from templates with snapshot data,
sequential number, verification code; revocation keeps the record. Report cards, challans (3-copy: bank,
school, student), receipts, payslips and ID cards rendered to PDF on demand.

## R. Optional modules
`schools.enabled_modules` toggles Hostel, Cafeteria, Transport, Library, Inventory. Disabled modules disappear
from navigation and their endpoints return 404 for that school. Hostel and cafeteria charges flow into fees as
student fee items or invoice lines.

## S. Reporting
Dashboard per role (owner/principal, accountant, admissions, teacher). Every number links to the report behind
it. Reports accept date range, campus, grade, section filters and export CSV/XLSX.
