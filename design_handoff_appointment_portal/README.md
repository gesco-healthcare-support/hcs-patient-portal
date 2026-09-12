# Handoff: Appointment Portal Redesign (CaseEvaluation)

## Overview
Complete UI redesign of the **CaseEvaluation** workers'-comp QME/AME appointment portal — every screen an **external user** (Patient, Applicant Attorney, Defense Attorney, Claim Examiner) and an **internal user** (IT Admin, Staff Supervisor, Intake Staff) can reach, plus two anonymous public pages and shared state screens.

The target codebase is an **Angular 20 standalone-component SPA** on **ABP Commercial v10** (`angular/` in the repo). The redesign intentionally **drops the LeptonX theme and ABP UI components** for the authenticated surface: every page here is meant to be implemented as plain Angular standalone components + SCSS. Only the **auth pages (AuthServer Razor)** and the **ABP auth backend** remain untouched.

## About the Design Files
The files in this bundle are **design references created in HTML/React (Babel-in-browser prototypes)** — they show intended look and behavior, **not production code to copy directly**. The task is to **recreate these designs in the Angular codebase** using its established patterns (standalone components, reactive forms, the `proxy/` services, permission guards). The React/JSX here maps 1:1 to Angular templates: components → standalone components, `useState` → component fields/signals, conditional JSX → `@if`, list maps → `@for`.

`BACKEND-CHANGES.md` in this folder is the companion list of **backend changes required** for these designs to integrate — implement those alongside or before the frontend work.

## Fidelity
**High-fidelity.** Colors, typography, spacing, radii, shadows, copy, and interaction states are final. Recreate pixel-perfectly. All values live in `styles/tokens.css` (single source of truth).

## Brand & Multi-tenancy (critical)
- Colors and fonts are **fixed for all tenants**: primary blue `#055495`, accent green `#9dc13b`, sidebar navy gradient (`#07304f → #053a66`), Roboto everywhere.
- The **tenant logo and clinic name are runtime slots** (`assets/header-logo.png` + "Falkinstein Orthopedics" are placeholders) — fill from `BrandingAppService`. Never hard-code tenant identity.
- Tenancy is subdomain-based. **IT Admin and Staff Supervisor are host-scoped (can switch tenants); Intake Staff is tenant-locked** (its tenant chip is non-interactive).

## Screens / Views (file → what it is)

### External (chrome-less; top navbar only)
| File | Screen |
|---|---|
| `External Role Home - Redesign.html` | Home for all 4 external roles: navbar (logo slot, notifications dropdown, help, account dropdown), welcome band, two 3D action cards (Request an Appointment / Re-evaluation — full content width, overlapping the band), appointment list (cards ↔ table toggle, 5-status filter chips with counts, quick search + collapsible filters, View + Documents per row). Role differences are config-driven (`components/ext-data.js`): booking roles, patient column, default view. No SSN, no Fax in lists. Claim # and ADJ # are separate fields. |
| `Request an Appointment - Redesign.html` | Booking wizard (all 4 external booker roles + Re-evaluation flow): steps Schedule → Patient → Applicant atty → Defense atty → Insurance → Examiner → Claim → Docs → Review. Panel # only for Panel QME; AA/DA/Insurance default-on with confirm-on-toggle-off modals ("self-represented?" etc.); claim-info modal (cumulative range, claim #, WCAB, ADJ #, body-part rows); document labels incl. Panel Strike List + Other (strike list mandatory when PQME + "I have it" checked); full review; submit-confirm modal (externals can't edit after submit); draft autosave (hydrate synchronously on load — see `ra-after.jsx`). Stepper never scrolls; labels shed responsively. |
| `Appointment Detail - Redesign.html` | External read-only detail: status banner (green Approved / red Rejected+Cancelled / amber Pending / blue Rescheduled), sticky section nav with right-aligned actions (Reschedule/Cancel ghost + Upload primary; gated by status), ledger sections (label/value rows, ALL booking-form fields incl. Fax), Document Manager (required-docs tracker, upload modal w/ labels, status badges + rejection reason), packet download, authorized users. No lifecycle timeline (5-status model). |
| `My Profile - Redesign.html` | Patient: avatar header + 4 read-only sections w/ per-section Edit → save-confirm modal (no SSN). Attorneys/examiners: enriched read-only card. Account & security card (password via AuthServer, email managed-by-support). No notification preferences (legal notices always delivered). |
| `My Documents - Redesign.html` | Cross-appointment document list: outstanding-required banner, search + status filter, collapsible per-appointment groups, download, upload jump. |
| `Public Pages - Redesign.html` | Anonymous: Document Upload (`/public/document-upload/:id/:code`) and Change-Request Consent (`/public/change-request-consent/:token`) — branded centered card, all states (ready/submitting/success/declined/expired/invalid/rate-limited). |
| `State Screens - Redesign.html` | Shared loading/skeleton, empty, error, 404, session-timeout, offline states. |

### Internal (navy sidebar shell — `components/in-shell.jsx`)
| File | Screen |
|---|---|
| `Internal Shell - Redesign.html` | The shell itself: collapsible grouped sidebar (badges for pending counts), topbar (breadcrumb, tenant chip/switcher, notifications, New appointment, account menu). Role-aware nav; IT Admin gets platform/SaaS nav. Sidebar labels: Workspace / Scheduling / Configuration / People / Administration (Users & Access · Users & Roles · Notification Templates · System Parameters · Audit Logs). |
| `Internal Dashboard - Redesign.html` | Hero KPIs (Pending / Pending change requests / Approved / Rejected) + timeframe toggle; **decision-deadline alert** ("decide within 3 days of submission"); trend bars; 5-status donut; today's schedule; activity. Intake = lighter variant; IT Admin = host totals + per-tenant table. |
| `Internal Appointments - Redesign.html` | Dense list: search, 5-status chips w/ counts, collapsible filters, bulk select/bar, row→detail, kebab (Review/Reschedule/Cancel on any non-terminal status incl. Pending/Delete), **"Decide by"** column (Pending rows only, 3-day urgency), pagination. Intake: no delete. |
| `Internal Appointment Detail - Redesign.html` | Staff detail: status banner + office actions (Pending → Approve/Reject/Reschedule/Cancel), **every booking-form field editable** per-section, claim-info add/edit/delete, document accept/manage, authorized users, staff-only internal panel (booker, doctor, decision-due, audit). |
| `Internal Add Appointment - Redesign.html` | The booking wizard embedded in the shell as `staff` booker: patient picker, staff copy ("can edit afterwards"), success → appointments list. |
| `Internal Workflow - Redesign.html` | Change Requests inbox (unified queue, aging badges, consent status, expandable slot diff, one-click approve w/ consent warning, reject w/ reason) · Change Logs (global grouped timeline → field-level old→new diffs, redaction) · Reports (stat strip, column chooser, CSV/PDF, **no SSN**). |
| `Internal Scheduling - Redesign.html` | Availabilities (week grid w/ color slots + utilization bars ↔ table toggle, bulk-delete day) · Generate slots (pattern toggle: date-range+weekdays OR pick-days calendar; time ranges; capacity default 3; conflict-gated preview) · Locations · WCAB Offices. **No Doctors page** (tenant = the practice). |
| `Internal Configuration - Redesign.html` | Config hub: Types / Statuses / Document types (Required flag) / Languages / States. System-row locks, usage counts + delete protection. **Per-type Field Configuration**: per booking-form field Visible/Hidden · Editable/Read-only · **Required/Optional** + pre-fill default (never parsed). |
| `Internal People - Redesign.html` | People hub: Patients (curated columns + chooser, full detail view w/ profile header + ledger + their appointments, write-only SSN), AA/DA/CE directories (no Fax/appointments column rules: appointments count only for AA/DA/CE). Portal-status chips + Invite to portal. |
| `Internal Users - Redesign.html` | Users & Access hub: Invite external (optional firm name for attorneys, 7-day tokenized link + copy fallback), Pending invites (resend/revoke/expiry), Internal users (list + create w/ tenant picker, temp-password email states), Tenants (IT Admin). |
| `Internal Admin - Redesign.html` | Admin hub: Notification Templates (split editor, variable chips, preview, send test, per-tenant override badge), System Parameters (grouped w/ units; decision deadline = 3 days), Users & Roles (permission matrix; IT Admin locked), Audit Logs (method/status chips, expandable). |
| `Send Back Feature - Redesign.html` | **Send back / Request more information** (new feature, both sides, 3-step flow switcher): staff “Request info” modal (checkbox tree of booking-form fields grouped by section, optional per-field hints, required note) → appointment becomes **Info Requested** + email with note & deep link → external **fix-it page** (staff note card, ONLY flagged fields editable with red highlight + hints, flagged document replace row, progress bar, Resubmit confirm) → staff review (Pending + green Resubmitted badge, old→new diff of flagged items, request-history timeline). Unlimited rounds; decision clock keeps running; ALL internal roles can send back. |

## Interactions & Behavior (cross-cutting rules)
- **6-status model**: Pending · **Info Requested** (purple — sent back for more information) · Approved · Rejected · Cancelled · Rescheduled. (Checked-in/out, Billed, No-show are deprecated everywhere.) Info Requested has its own chip on every list and keeps staff reschedule/cancel available.
- **Decision deadline**: staff must decide a request within **3 days of the request date** (configurable; legal limit 5). Surfaced on dashboard alert, list "Decide by" column (Pending only), detail panel. Never shown to external users.
- Status banners: Approved green, Rejected/Cancelled red, Pending amber, Rescheduled blue.
- Modals confirm destructive/legal actions (reject/cancel require a reason; toggle-off of AA/DA/Insurance confirms; submit confirms immutability for externals).
- Toasts confirm every action; entrance animations are transform-only (never opacity-from-0 with fill — breaks capture/print).
- External users request changes (stay Pending until staff approve); staff act directly, including before approval.
- Tables: row-click opens detail; kebab/inline icons for actions; delete blocked + dimmed when in-use/system.

## State Management (Angular guidance)
- List pages: ABP `ListService` + filters as today; status chips = client-side facet counts from the page result or a counts endpoint.
- Wizard: one reactive `FormGroup` per step, autosaved to `localStorage` (hydrate synchronously before first render), per-step validation gate.
- Shell nav/badges: route data + a small counts endpoint (pending appointments, pending change requests).

## Design Tokens
See `styles/tokens.css` — exact palette (blues `#055495` family, green `#9dc13b` family, neutrals, status tones), Roboto stack, radius scale (9–16px), shadow scale, spacing. All component CSS lives in `styles/*.css`, organized per area (`ext-*`, `ra-*`, `ad-*`, `mp-*`, `in-*`, `pp-*`, `b-*`).

## Assets
- `assets/header-logo.png`, `assets/fav-logo.png` — **tenant placeholders** (runtime slots).
- `components/icons.js` — inline SVG line-icon set (`window.Ico(name, size)`); port as an Angular icon component or swap to a line-icon library of equal weight.
- All other imagery is CSS-only; no stock photos.

## Files
- `*.html` — one harness per page (top chrome bar = prototype-only role/status switchers; **do not implement the chrome bar**).
- `components/*.jsx` / `components/*.js` — page logic + mock data (`data.js`, `ext-data.js`, `ra-data.js`, `ad-data.js` show the expected data shapes).
- `styles/*.css` — tokens + per-area styles.
- `BACKEND-CHANGES.md` — required backend work (read first).
