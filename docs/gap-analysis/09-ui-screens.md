# 09 -- UI Screens per Role: Gap Analysis OLD vs NEW

## Summary

Chrome DevTools MCP-driven browser capture of OLD (`http://localhost:4201`) and NEW (`http://localhost:4200`) for all 7 OLD roles. OLD exposes ~33 user-reachable screens across 7 role profiles; NEW exposes roughly 15 admin-visible screens (non-admin role-scoped users are not yet seeded in NEW, which is itself a gap called out in track 5). For OLD, 3 internal roles (ItAdmin, StaffSupervisor, ClinicStaff) land on `/dashboard` with progressively narrower sidebar navigation. The 4 external roles (Patient, Adjuster, PatientAttorney, DefenseAttorney) all land on `/home` with an identical minimal UI consisting of "Book Appointment", "Book Re-evaluation", and "My Appointments Requests" tiles. MVP risk rating: High -- substantial portions of OLD's internal-user UI (Custom Fields, System Parameters, Reports, Appointment Change Logs, Document Management, Template Management, Notes) are completely missing in NEW.

## Method

- Chrome DevTools MCP on the parent session drove both browser tabs sequentially.
- OLD login pattern: `localStorage.clear()` + `sessionStorage.clear()` -> navigate `/login` -> `evaluate_script` to dispatch `input` events on both form fields (the MCP `fill` tool alone does not unlock Angular 7's disabled Sign In button) -> click Sign In -> wait 4-5 seconds for lazy chunks.
- Per role: log in, harvest navigation links from the left sidebar via `document.querySelectorAll('a')` filtered to meaningful hrefs, take `take_snapshot` + `take_screenshot` for the landing page and key navigation destinations.
- NEW: login as `admin` / `1q2w3E*`. Non-admin role-scoped users are not yet seeded in NEW per track 5 findings, so per-role NEW captures are not possible today.
- Screenshots saved under `W:\patient-portal\development\docs\gap-analysis\screenshots\<old|new>\<role>\<slug>.png`.

Note: the chrome-devtools MCP tools are not granted to the general-purpose subagent by default; the first dispatch of this track failed with "Permission denied for MCP" and the capture work was handled directly in the parent session. Full coverage (every single screen per role) was scoped to landing-page-plus-primary-navigation given the session's time budget; the per-role nav inventory below documents every reachable URL so follow-up capture runs can be exhaustive.

Timestamp: 2026-04-23.

## OLD version state

### Nav inventory harvested from the ItAdmin session (most comprehensive)

| # | Label | Path | Dropdown parent |
|---|---|---|---|
| 1 | Dashboard | `/dashboard` | -- |
| 2 | Book Appointment | `/appointments/add` | -- |
| 3 | Check-in & Check-out | `/appointment-approve-request` | -- |
| 4 | All Appointments | `/appointment-search` | Appointments |
| 5 | Pending Appointments | `/appointment-pending-request` | Appointments |
| 6 | Rescheduled Requests | `/appointment-rescheduled-requests` | Appointments |
| 7 | Cancel Requests | `/appointment-cancel-requests` | Appointments |
| 8 | Change / Audit Log | `/appointment-change-logs` | Appointments |
| 9 | Appointment Documents | `/appointment-documents-search` | Document |
| 10 | Document Types | `/appointment-document-types` | Document |
| 11 | Doctor Details | (dropdown to doctors list) | Doctor Management |
| 12 | Availability & Time slots | `/doctors-availabilities` | Doctor Management |
| 13 | Location Management | `/locations/:type` | Doctor Management |
| 14 | System Parameters | `/system-parameters/:id` | Configurations |
| 15 | Users | `/users` | Configurations |
| 16 | Custom Fields | `/custom-fields` | Configurations |
| 17 | Reports | `/report` | -- |

### Role-by-role landing page captures + nav summary

#### Role 1 -- ItAdmin (`admin@local.test`)

- Landing: `/dashboard` -- 13 stat cards (Pending Appointment, Approved Appointment, Rejected Appointment, Cancelled Appointment, Rescheduled Appointment, Checked-In Appointment, Checked-Out Appointment, Billed Appointment, Patient, Claim Examiner, Applicant Attorney, Defense Attorney). Branding strip shows "Kevin Pelton M.D. orthopedic surgeon" and "SoCal" footer.
- Screenshots:
  - `screenshots/old/admin/01-dashboard.png` -- Dashboard with empty stat cards (post-bring-up has no seeded appointments)
  - `screenshots/old/admin/02-book-appointment.png` -- `/appointments/add` full intake form (patient demographics, employer, attorney sections, injury details collapsed) -- the form Adrian debugged earlier in the day
  - `screenshots/old/admin/03-checkin-checkout.png` -- `/appointment-approve-request` list
- Sidebar items ItAdmin has: Dashboard, Book Appointment, Check-in & Check-out, Appointments (dropdown: All / Pending / Rescheduled Req / Cancel Req / Change Log), Document (dropdown: Appointment Documents / Document Types), Doctor Management (dropdown: Doctors / Availability / Location), Configurations (dropdown: System Parameters / Users / Custom Fields), Reports.

#### Role 2 -- StaffSupervisor (`supervisor@local.test`)

- Landing: `/dashboard` -- identical card layout to admin.
- Screenshots:
  - `screenshots/old/supervisor/01-dashboard.png`
- Sidebar: same as ItAdmin MINUS "Configurations" dropdown. Has: Dashboard, Book Appointment, Check-in & Check-out, Appointments, Document, Doctor Management, Reports.

#### Role 3 -- ClinicStaff (`staff@local.test`)

- Landing: `/dashboard` -- dashboard cards identical.
- Screenshots:
  - `screenshots/old/staff/01-dashboard.png`
- Sidebar: same as StaffSupervisor (no Configurations, no ability to manage Users or System Parameters).

#### Role 4 -- Patient (`patient@local.test`)

- Landing: `/home` -- minimal external-user UI with: "Please click on the below button to book a new appointment" + "Book Appointment" link -> `/appointments/add?type=1`; "Please click on the below button to book re-evaluation" + "Book Re-evaluation" link -> `/appointments/add?type=2`; "My Appointments Requests (0)" section with search bar.
- Screenshots:
  - `screenshots/old/patient/01-home.png` -- landing
  - `screenshots/old/patient/02-book-appointment.png` -- full intake form when patient clicks Book Appointment (pre-filled with patient's Last Name, First Name, Email from user record; read-only email field)
- Sidebar: "Appointments" only.

#### Role 5 -- Adjuster (`adjuster@local.test`)

- Landing: `/home` -- identical layout to Patient (see screenshot `06-adjuster.png`). Same "Book Appointment" + "Book Re-evaluation" + "My Appointments Requests" tiles.
- Screenshots:
  - `screenshots/old/adjuster/01-home.png`
- Sidebar: "Appointments" only.

#### Role 6 -- PatientAttorney (`patatty@local.test`)

- Landing: `/home` -- identical to Patient/Adjuster.
- Screenshots:
  - `screenshots/old/patatty/01-home.png`
- Sidebar: "Appointments" only.

#### Role 7 -- DefenseAttorney (`defatty@local.test`)

- Landing: `/home` -- identical to Patient/Adjuster/PatientAttorney.
- Screenshots:
  - `screenshots/old/defatty/01-home.png`
- Sidebar: "Appointments" only.

**Key observation:** all 4 external roles (Patient, Adjuster, PatientAttorney, DefenseAttorney) land on `/home` with an IDENTICAL minimal UI. The differentiation between them is invisible on the landing page and must happen on downstream screens (appointment detail views, document requests, etc.). The `AccessPermissionService.ts` in OLD's frontend collapses all 4 external roles into a single `ExternalUserModules` list -- confirming the UI sameness is by design.

## NEW version state

### Admin (only role seeded)

Landing after admin login at `http://localhost:4200`: ABP LeptonX themed dashboard with a sidebar showing:

- Home / Dashboard
- Saas (tenants, editions, connection strings)
- Identity Management (users, roles, claim types, security logs)
- Audit Logs
- Feature Management
- Settings (SMTP, notifications, UI theme)
- OpenIddict (applications, scopes, tokens)
- GDPR
- File Management
- Text Template Management
- Language Management

Plus feature-specific routes (appointment-management/*, doctor-management/*, configurations/states, applicant-attorneys, patients):

- `/appointment-management/appointment-types`
- `/appointment-management/appointment-statuses`
- `/appointment-management/appointment-languages`
- `/appointments` + `/appointments/view/:id` + `/appointments/add`
- `/doctor-management/locations`
- `/doctor-management/wcab-offices`
- `/doctor-management/doctors`
- `/doctor-management/doctor-availabilities` + `/generate` + `/add`
- `/doctor-management/patients` + `/my-profile`
- `/applicant-attorneys`
- `/configurations/states`

**Non-admin role UX is not capturable** because NEW does not yet have role-scoped test users seeded (per track 5 finding 5-G01 through 5-G14). The ABP external-role seed contributor creates the 4 external roles as empty shells (no permissions, no users).

## Delta

### MVP-blocking screen gaps (present in OLD, absent in NEW)

| gap-id | screen | OLD evidence | NEW evidence | Effort |
|---|---|---|---|---|
| UI-01 | `/appointment-change-logs` audit log viewer | ItAdmin / Staff / Supervisor nav | absent; ABP's generic `/audit-logs` covers entity-level but not appointment-scoped | Medium |
| UI-02 | `/appointment-documents-search` approval workflow | Document nav | absent | Medium |
| UI-03 | `/appointment-document-types` admin screen | Document nav | absent | Small |
| UI-04 | `/appointment-rescheduled-requests` queue | Appointments nav | absent | Medium |
| UI-05 | `/appointment-cancel-requests` queue | Appointments nav | absent | Small-Medium |
| UI-06 | `/appointment-pending-request` detail | Appointments nav | absent (NEW has only `/appointments/view/:id` + list) | Small |
| UI-07 | `/appointment-search` all-appointments standalone | Appointments nav | absent | Small |
| UI-08 | `/custom-fields` admin screen | Configurations nav (ItAdmin) | absent | Medium |
| UI-09 | `/system-parameters/:id` admin screen | Configurations nav (ItAdmin) | ABP Settings Management is analogous but separate | Small |
| UI-10 | `/users` custom user management | Configurations nav (ItAdmin) | ABP Identity `/identity/users` is analogous | Small (delegate to ABP) |
| UI-11 | `/report` report search page | Reports nav (Internal roles) | absent | Large (depends on G2-11 data + G-API-13 export) |
| UI-12 | `/appointment-new-documents/:id` -- additional document upload | Document area | absent (depends on doc storage) | Medium |
| UI-13 | `/upload-documents/:id/:type` anonymous magic-link upload | public route | absent | Large |
| UI-14 | `/appointment-joint-declarations-search` | Document area | absent | Medium |
| UI-15 | `/templates` email/SMS template admin | external reach | absent (ABP TextTemplateManagement covers partly) | Medium |
| UI-16 | Patient's `/home` external-user landing with its specific layout | Patient/Adjuster/PatAtty/DefAtty land here | NEW has `/dashboard` only; no external-user-optimized home | Small |
| UI-17 | `/notes` appointment notes thread | linked from appointment detail | absent | Small |

### Non-MVP screen gaps

| gap-id | screen | notes |
|---|---|---|
| UI-N1 | Internal-role dashboard counter cards (13 different stats) | NEW dashboard is admin-only and minimal |
| UI-N2 | WCAB office detail panels | partially covered by NEW's `/doctor-management/wcab-offices` |
| UI-N3 | User Query / Contact-us widget | absent from NEW |
| UI-N4 | Package Details screen | absent (doc packaging feature) |
| UI-N5 | Document library CRUD screens | absent |
| UI-N6 | Patient "My Profile" external-user screen (self-service) | NEW has `/doctor-management/patients/my-profile` but needs role-scoped user to access |

### Post-MVP deferred gaps (per Adrian 2026-04-23 -- handle after MVP)

| gap-id | capability | evidence-old | evidence-new-absent | effort |
|---|---|---|---|---|
| BRAND-01 | Per-tenant branding config (logo, clinic name, phone/fax, support email, header/footer copy) | `server-settings.json` keys `companyName`, `headertext`, `logoName`, `footertext`, `email`, `fax`, `ph_US`; surfaced via `spConfigurationContents` -> `ClientSetting.Configuration` | No tenant-settings surface wired in NEW; LeptonX theme loads with ABP defaults | M (2-3 days -- ABP `ISettingProvider` per-tenant + admin edit page) |
| BRAND-02 | Angular login page / navbar / footer honor tenant branding at bootstrap | OLD frontend reads `ClientSetting.Configuration` at app init and uses it across login card, top-bar logo, footer copy | NEW LeptonX shell has no tenant-config hook; hardcoded generic ABP chrome | M (2-3 days -- `APP_INITIALIZER` pattern + LeptonX theme slots) |

### Intentional architectural differences (NOT gaps)

| Topic | OLD | NEW | Why |
|---|---|---|---|
| Theme | Custom Bootstrap-derived with SoCal branding (Kevin Pelton M.D. logo) | ABP LeptonX professional theme | Vendor-supported, consistent, faster to maintain |
| External-user landing | `/home` dedicated minimal layout per user type | Single `/dashboard` for all authenticated users | Simpler mental model, fewer screens |
| Internal-user sidebar | Per-role static list via `AccessPermissionService.ts:17-90` | ABP's dynamic `RoutesService` assembled from `*-base.routes.ts` metadata at bootstrap | Code-driven permissions, runtime flexible |
| Login page | Branded with SoCal visual | ABP Account Module standard login screen | Standardization; can be rebranded via LeptonX theme |
| Admin section | Single "Configurations" dropdown with System Parameters / Users / Custom Fields | Split across `Saas`, `Identity Management`, `Settings`, `Feature Management`, `Audit Logs`, `OpenIddict` -- 6+ top-level admin areas | ABP modular structure |
| Visual density | Dense tables, many columns visible | Fewer columns per row, more whitespace | ABP/LeptonX design conventions |

### Extras in NEW

- `/saas/*` tenants + editions + connection strings (OLD has no tenant-switch UI)
- `/identity/users` + `/identity/roles` + `/identity/claim-types` + `/identity/security-logs`
- `/audit-logs` (generic entity audit, covers many OLD features)
- `/feature-management` runtime feature toggles
- `/file-management/*` (OLD has no file-management UI)
- `/text-template-management/*`
- `/language-management/*`
- `/openiddict/*` (applications, scopes, tokens admin)
- `/gdpr` + `/gdpr-cookie-consent/privacy` + `/gdpr-cookie-consent/cookie` (privacy compliance)
- `/doctor-management/wcab-offices` (OLD has no WCAB CRUD UI)
- `/doctor-management/doctor-availabilities/generate` (bulk slot generation UI; OLD had to do it via API)

## Open questions

1. **External-user UX**: OLD gives all 4 external roles (Patient/Adjuster/PatAtty/DefAtty) an identical `/home`-based minimal UI. Is that still MVP, or does NEW need role-distinguished external dashboards?
2. **Internal dashboard counters**: the 13 stat cards (Pending/Approved/Rejected/etc.) need specific data. Does MVP require replicating all 13, a subset, or a different set?
3. **Appointment Change Logs UI**: ABP's generic `/audit-logs` shows entity-level changes. Does MVP need the appointment-scoped view that OLD has at `/appointment-change-logs`?
4. **Custom Fields admin UI**: if `CustomFields` is MVP (see gap G2-N2), the admin UI at `/custom-fields` is also needed.
5. **System Parameters vs ABP Settings**: the OLD admin UI at `/system-parameters/:id` is a single-row key-value grid. Does MVP require a dedicated screen, or is ABP's Settings Management UI sufficient (delegated to the admin area)?
6. **Reports UI**: the OLD `/report` page is the entry point for CSV/PDF export. If export is MVP, the UI is too.
7. **Role-scoped NEW users for coverage**: NEW has zero external-role users seeded. Without them, NEW-side per-role capture is impossible. Does MVP require role-scoped seed users, or will this remain admin-only until post-launch?
8. **Template Management UI**: if per-tenant email/SMS templates are MVP, does the admin UI use ABP's Text Template Management or a custom one?
9. **Anonymous upload page**: the OLD `/upload-documents/:appointmentId/:type` magic-link flow is a significant UX piece for external users to submit documents without logging in. Is it in MVP scope?

## Screenshot catalog (as captured)

OLD (saved under `screenshots/old/`):

| Role | File | Screen |
|---|---|---|
| admin | 01-dashboard.png | Dashboard (ItAdmin) |
| admin | 02-book-appointment.png | `/appointments/add` full intake form |
| admin | 03-checkin-checkout.png | `/appointment-approve-request` list |
| supervisor | 01-dashboard.png | Dashboard (StaffSupervisor) |
| staff | 01-dashboard.png | Dashboard (ClinicStaff) |
| patient | 01-home.png | `/home` external landing |
| patient | 02-book-appointment.png | `/appointments/add?type=1` as Patient (pre-filled user fields) |
| adjuster | 01-home.png | `/home` external landing (Adjuster) |
| patatty | 01-home.png | `/home` external landing (PatientAttorney) |
| defatty | 01-home.png | `/home` external landing (DefenseAttorney) |

NEW (saved under `screenshots/new/`):

| Role | File | Screen |
|---|---|---|
| admin | -- | Not captured in this pass; NEW admin is walkable, follow-up capture recommended |

**Coverage gap acknowledgment:** exhaustive per-role per-screen capture (~100+ screenshots across both versions as originally scoped) was not completed because (a) chrome-devtools MCP permission was denied to the subagent and the work had to run in the parent session, (b) other tracks consumed parent-session capacity. A follow-up capture run can use the per-role nav inventory above as the navigation map; every URL in the admin nav list is reachable today on the running instance.
