# Integration Playbook — Prompts for Claude Code

Page-by-page integration plan. Copy this folder into the repo (suggested: `<repo>/design/`), then run these prompts in Claude Code **inside the repo**. One prompt ≈ one session ≈ one page; integrate, test live, then move on.

> **Assumptions baked into these prompts (correct me if wrong):**
> 1. The portal keeps the **codebase brand** (`#055495` blue / `#9dc13b` green / Roboto) from `design/styles/tokens.css` — the separate "Evaluators" design system is for the marketing site, not this app.
> 2. Pages are replaced **in-place** (same routes), old component files deleted once the new page passes live testing.
> 3. You run the **backend foundation items first** (Prompt 1) yourself or via Claude Code, before page integration.

---

## Phase 0 — Kickoff (run once, paste at the start of EVERY session)

```
You are integrating a complete UI redesign into this Angular 20 + ABP v10 codebase (angular/ = SPA, src/ = .NET backend). The design package lives in design/ at the repo root.

Read these first, in order:
1. design/README.md — what every design file is, brand rules, JSX→Angular mapping guidance.
2. design/BACKEND-CHANGES.md — backend deltas; assume items are done unless I say otherwise.
3. design/styles/tokens.css — the design tokens. Port these once into a global SCSS file (e.g. angular/src/styles/_tokens.scss) and reference via CSS custom properties; never hard-code values that exist as tokens.

Ground rules:
- The design/*.html files are REFERENCE PROTOTYPES (React-in-browser). Recreate them as Angular standalone components with reactive forms and the existing proxy/ services. Do NOT copy React code into the app; do NOT implement the prototypes' top chrome bar (role/status switchers — that's harness-only).
- Drop LeptonX/ABP UI components on redesigned pages: plain standalone components + SCSS, pixel-faithful to the prototype.
- Brand is fixed for all tenants (#055495 / #9dc13b / Roboto); tenant logo + clinic name are runtime slots from BrandingAppService.
- 6-status model: Pending, Info Requested, Approved, Rejected, Cancelled, Rescheduled. Decision deadline = 3 days from request creation (staff-facing only).
- Roles: IT Admin + Staff Supervisor are host-scoped (tenant switcher); Intake Staff is tenant-locked.
- Keep auth (AuthServer Razor pages) untouched.
- After each page: run the app, give me a manual test checklist, and wait for my live sign-off before deleting the old component.

Today we are integrating: [PAGE NAME — see playbook].
```

## Phase 1 — Backend foundation (before any page)

```
Work through design/BACKEND-CHANGES.md sections A and B (roles + status model + decision deadline), in the suggested sequencing. For each item: implement, add/adjust migrations, update the Angular proxy (abp generate-proxy), and list what you changed. Stop after A+B and give me a test plan (role logins, tenant switching, status migration spot-checks) before continuing to C/D.
```

Then later, before the pages that need them: C+D (booking/doc schema) before Prompts 5–7; E before Prompt 10; F/G before Prompts 13–15; **I before Prompt 16**.

## Phase 2 — Shared shells (order matters; everything else depends on these)

**Prompt 2 — tokens + external top navbar**
```
Port design/styles/tokens.css into global styles. Then recreate the external top navbar from design/External Role Home - Redesign.html (components/ext-after.jsx: ExtNav) as a standalone AppExternalNavbar: tenant logo slot, notifications dropdown, help (submit-query modal), account dropdown (My profile, My documents, Sign out). Wire to BrandingAppService + current user. Mount it on the existing external layout without changing pages yet.
```

**Prompt 3 — internal shell**
```
Recreate the internal staff shell from design/Internal Shell - Redesign.html (components/in-shell.jsx) as the layout for all internal routes: collapsible navy sidebar (grouped nav + pending/change-request badges via a counts endpoint), topbar (breadcrumb, tenant chip — switcher for IT Admin/Supervisor only, static for Intake — notifications, New appointment, account menu). Role-aware nav per the design's IN_NAV/IN_NAV_HOST tables. Replace the LeptonX layout for internal routes.
```

## Phase 3 — Pages, one per session (suggested order)

For each, use this template — swapping the bracketed parts:

```
Integrate [N]: recreate design/[FILE] as Angular standalone component(s) replacing [current route/component]. Match the prototype pixel-faithfully (open it in a browser side-by-side). Data comes from [proxy services]; the design's mock files (design/data.js, design/components/[mock]) show the expected shapes. When done: build clean, no console errors, and give me a manual test checklist covering [key behaviors].
```

| # | Page | Design file | Replaces / route | Key behaviors to test live |
|---|---|---|---|---|
| 4 | External Home | `External Role Home - Redesign.html` | `/home` (external) | 4 roles, 6-status chips w/ counts, cards↔table, search+filters, View/Documents actions, notifications + account dropdowns |
| 5 | Booking wizard | `Request an Appointment - Redesign.html` | appointment-add (external) | step gating, PQME panel rule, AA/DA/Ins toggle-off modals, claim modal, doc labels + strike-list rule, review, submit-confirm, draft autosave |
| 6 | External Appointment Detail | `Appointment Detail - Redesign.html` | appointment-view (external) | status banners, action gating, Document Manager (required tracker, upload, badges), reschedule/cancel request modals, authorized users |
| 7 | My Documents + My Profile | `My Documents - Redesign.html`, `My Profile - Redesign.html` | my-profile route + new route | per-section edit + confirm, no SSN, doc groups/filters |
| 8 | Public pages + state screens | `Public Pages - Redesign.html`, `State Screens - Redesign.html` | /public/* + global states | all states incl. expired/rate-limited; empty/error/404/timeout/offline wired app-wide |
| 9 | Internal Dashboard | `Internal Dashboard - Redesign.html` | /dashboard | role variants (Supervisor/Intake/IT Admin), decision-deadline alert, charts, clickable KPIs → filtered list |
| 10 | Internal Appointments list | `Internal Appointments - Redesign.html` | /appointments | chips, filters, bulk bar, kebab gating (incl. Pending + Info Requested), Decide-by column (Pending only), pagination, Intake no-delete |
| 11 | Internal Appointment Detail | `Internal Appointment Detail - Redesign.html` | appointment view/edit (internal) | full field parity editable per-section, office actions by status, claim/doc/auth-user management, staff panel |
| 12 | Internal Add | `Internal Add Appointment - Redesign.html` | appointment-add (internal) | staff booker copy, patient picker, footer alignment w/ sidebar |
| 13 | Workflow | `Internal Workflow - Redesign.html` | change-requests, change-logs, reports | unified inbox + consent warnings, timeline diffs + redaction, report columns/exports (no SSN) |
| 14 | Scheduling | `Internal Scheduling - Redesign.html` | doctor-availabilities, generate, locations, wcab | week grid↔table, slot/day delete, generate w/ both patterns + conflict gate, CRUD modals |
| 15 | Configuration + People | `Internal Configuration - Redesign.html`, `Internal People - Redesign.html` | lookups + patients/attorneys/examiners | field-config 3 toggles + default, locks/usage guards, patient detail view, portal chips |
| 16 | Users & Access + Admin | `Internal Users - Redesign.html`, `Internal Admin - Redesign.html` | invite, internal-users + admin pages | invites (firm name, pending list), tenants, template editor + variables + test send, parameters, permission matrix, audit |
| 17 | **Send back / Request info** | `Send Back Feature - Redesign.html` | NEW (touches 10, 11, 4, 6) | staff modal tree+hints+note, email, external fix-it (only flagged editable, progress, resubmit), diff review, Info Requested chips everywhere |

## Phase 4 — Final sweep
```
Cross-cutting QA: navigate every route as each of the 7 roles. Verify: no LeptonX remnants on redesigned pages; tokens used (no stray hex); 6-status consistency; decision-deadline only staff-facing; SSN never in lists/reports; Fax never in tables; tenant logo slots render from BrandingAppService; all toasts/modals/empty states present. Produce a defect list.
```

**Per-page sign-off checklist (use every time):** renders pixel-close to prototype · real data loads · every action hits a real endpoint (no dead buttons) · permission-gated per role · mobile/narrow width OK · no console errors · old component deleted + route cleaned.
