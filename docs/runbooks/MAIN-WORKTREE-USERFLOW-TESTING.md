# Main Worktree Userflow Testing — Comprehensive Hand-off

**Audience:** the Claude Code session running in `W:\patient-portal\main`
whose job is to systematically test every userflow in the NEW Patient
Portal against the OLD reference implementation, surface gaps as
structured bug tickets, and write parity audit docs for slices that
don't yet have them.

**Source of truth for this hand-off:** the planning session in
`W:\patient-portal\replicate-old-app` on 2026-05-13 that closed Phase A
(security blockers, PR #186 merged) and Phase B (auth + session polish,
PR #187 awaiting merge). This doc supersedes the brief 2026-05-13
runbook that lived at the same path.

---

## Part 1: What you are testing

### The pipeline

Two apps run side-by-side on this machine:

| App | Tech | Purpose for testing |
| --- | --- | --- |
| **OLD** | Angular 8 + ASP.NET Framework + MySQL | The behavior we are replicating. Authoritative reference for every flow. |
| **NEW** | Angular 20 standalone + .NET 10 + ABP Commercial 10.0.2 + SQL Server + OpenIddict | The replication target. What you are testing. |

OLD is **read-only** -- never edit anything in `P:\PatientPortalOld`. It
exists so you can answer "what does the OLD app do here?" for any
behavior question that comes up mid-test.

### The mission

**Primary:** walk every userflow per role on NEW. For each one, verify
behavior matches OLD parity AND meets the user-friendliness /
idiot-proofing / adversarial-input bars. Report findings as structured
tickets back to `W:\patient-portal\replicate-old-app`.

**Secondary:** while walking flows, write parity audit docs for the
~12 internal-user slices that don't have one yet. Existing audit docs
live in `docs/parity/wave-1-parity/`. Use them as templates.

**NOT primary:** fixing bugs. Bugs go back to the fix worktree as
tickets. If you find an obvious one-line typo, fix it; if it's a real
issue, ticket it.

### The role landscape (locked 2026-05-01)

**External users (4 -- can register themselves):**

- **Patient** — files a case, uploads documents, views own appointments
- **Applicant Attorney** — represents the patient, books appointments,
  uploads JDFs (AME-type appointments only)
- **Defense Attorney** — represents the employer/insurer, views own
  appointments, uploads ad-hoc docs
- **Claim Examiner** — represents the insurance carrier (OLD called
  this role "Adjuster"). Receives emails, views the appointments they
  are linked to via `AppointmentClaimExaminer.Email`

**Internal users (3 -- only IT Admin can create):**

- **Clinic Staff** — front-line receptionist; approves appointments,
  reviews documents, checks patients in/out
- **Staff Supervisor** — Clinic Staff plus approval-cascade and
  change-request approval rights, plus signature upload
- **admin** — full tenant admin; OLD called this the "Office Manager"

**Host scope (1):**

- **IT Admin** — manages tenants, user accounts, master data, system
  parameters, notification templates. Not present in any tenant; works
  in the host scope.

**Doctor** is a non-user **entity** -- it's a row in the Doctor table,
not a login. Doctor availabilities are seeded by Admin / Clinic Staff
on behalf of the doctor. Don't try to log in "as a doctor."

**"Claim Examiner" vs OLD's "Adjuster":** the role was renamed during
the rewrite. They are the same role. If a parity doc says "Adjuster,"
it means Claim Examiner. The DB column is still `Adjuster*` in some
joined views per OLD.

### What "parity" means here

You are checking that for the same input on OLD vs NEW:

1. **The same data shape** is captured (same fields, same labels,
   same validation rules).
2. **The same business rules** fire (status transitions, role gates,
   email triggers, document workflows).
3. **The same user-visible outcomes** happen (success/error messages,
   redirects, emails, file generation).
4. **The same role-based access** is enforced (who can see what).

What is **NOT** in scope for parity:

- Visual identical pixel-for-pixel UI -- NEW uses LeptonX components;
  OLD uses bootstrap. Colors + fonts mirror per
  `docs/design/_design-tokens.md` but layout differs.
- Library choices, framework versions, hosting.
- DOCX vs PDF for reports -- NEW intentionally generates PDFs because
  they are immutable.

When in doubt about "is this a parity bug or an acceptable
modernization?" surface it as a ticket and let the fix worktree decide.

---

## Part 2: Pre-flight checklist

Run through this before you start any flow tests. Skipping any of
these will burn cycles on known-fixed bugs.

### Required state

- [ ] Worktree is `W:\patient-portal\main` (not the replicate-old-app
      one -- if you are there, switch).
- [ ] On branch tracking `main` (or a fresh branch off main if you are
      writing audit docs you want to PR).
- [ ] PR #186 (Phase A security blockers) merged into
      `feat/replicate-old-app`. Verify:
      `git log --oneline origin/feat/replicate-old-app | grep 4d1e5bf`
      should return the merge commit.
- [ ] PR #187 (Phase B auth + session polish) merged. Verify the same
      way against `origin/feat/replicate-old-app`. If not merged yet,
      either wait or ask the fix-worktree session for status.
- [ ] Both PRs promoted to `main` (separate PR; not auto). Verify your
      branch has the commits from both.
- [ ] Both stacks (OLD and NEW) are NOT running before you start --
      `docker compose ps` should show no `replicate-old-app-*` or
      `main-*` containers running. They will fight over ports.

### Phase A + B fixes already landed (do not re-test)

These are closed. Do not file tickets that re-describe them.

| Issue | What it fixed |
| --- | --- |
| #114 | AppointmentDocuments — added 7-pathway access gate. External parties cannot list/upload/download docs on appointments they are not party to. |
| #115 | Past-date appointment bookings rejected at the domain layer (CreateAsync unconditionally, UpdateAsync only on date change). |
| #116 | Confirmation-number generation race -- unique index + 5-attempt retry. (Was actually closed 2026-05-04; doc was stale.) |
| #119 | The 4 @gesco.com Gmail inbox users (SoftwareThree/Four/Five/Six) seeded into Falkinstein on every fresh DB. |
| #105 | SPA `/account/register` -- 404 for anon, redirect to `/` for authed. |
| #120 | Stale `console.log` breadcrumb comment cleaned up. |
| #107 | Silent-refresh wiring ripped. Token renewal via refresh_token rotation (transparent). `SessionIdentityWatcherService` degraded to passive `sub`-change listener. |
| #106a | Logout clears `__tenant` + `XSRF-TOKEN` cookies on both AuthServer and SPA; SPA scrubs OAuth localStorage scraps. |
| #106b | Register-success fires fire-and-forget `/Account/Logout` so a brand-new registration cannot auto-sign-in as a prior user. |
| #117 | Doc-upload proxy methods now send `FormData` (multipart), not JSON. |
| #106c | `docs/security/SESSION-AND-TOKENS.md` -- threat model + invalidation flow. |

If a flow test surfaces something that looks like one of these,
double-check you are on the right branch first.

---

## Part 3: Boot procedure

### One stack at a time

OLD and NEW use overlapping ports for some services. Start one, finish
your test pass, stop it, start the other.

### Boot NEW

From `W:\patient-portal\main`:

```powershell
# 1. Ensure NUthing is running
docker compose ps

# 2. Wipe volumes for a clean DB + blob state
docker compose down -v

# 3. CRITICAL: rebuild db-migrator first so the seed contributors that
#    landed after the cached image's build (Clinic Staff role grants,
#    InboxedExternalUsers) actually run.
docker compose build db-migrator

# 4. Boot the whole stack
docker compose up -d --build

# 5. Wait for healthchecks to pass (about 3 to 5 minutes cold)
docker compose ps
# all rows should be "healthy" except db-migrator which exits 0
```

Healthcheck order: sql-server → redis → minio → gotenberg → db-migrator
runs and exits → authserver → api → angular.

### Boot OLD

OLD lives at `P:\PatientPortalOld` and was historically started via a
batch script that runs the OLD MySQL container plus the OLD Web app.
Don't reverse-engineer that here; use whatever scripted entry point
already exists in `P:\PatientPortalOld\` (typically a `start*.bat` or
`docker-compose.yml`). When in doubt, ask the user for the exact
command -- this is the kind of thing that varies per machine.

OLD runs at `http://localhost:4202` per project memory. Verify with a
quick curl before doing anything else: `curl -s -o NUL -w "%{http_code}"
http://localhost:4202/`.

### Smoke test NEW after boot

Before any flow tests, hit these three URLs to confirm the stack is
actually serving:

```powershell
curl -s -o NUL -w "API:        %{http_code}`n" http://localhost:44327/health-status
curl -s -o NUL -w "AuthServer: %{http_code}`n" http://localhost:44368/.well-known/openid-configuration
curl -s -o NUL -w "Angular:    %{http_code}`n" http://localhost:4200/
```

All three should return 200. If AuthServer returns 5xx, give it
another 30 seconds -- `dotnet watch` is slow on first boot.

---

## Part 4: Test accounts

Every account is seeded into `Falkinstein` tenant unless noted as host
scope. Default password: `1q2w3E*r` (8 chars; digit + non-alphanumeric).
Host admin uses `1q2w3E*` (no trailing `r`).

| Role | Email | Subdomain to sign in on |
| --- | --- | --- |
| **Host admin** | admin@abp.io | `localhost:44368` (no subdomain) |
| **IT Admin** | it.admin@hcs.test | `localhost:44368` |
| **Tenant admin** | admin@falkinstein.test | `falkinstein.localhost:44368` |
| **Tenant admin (extra)** | SoftwareOne@evaluators.com | `falkinstein.localhost:44368` |
| **Tenant admin (extra)** | SoftwareTwo@evaluators.com | `falkinstein.localhost:44368` |
| **Staff Supervisor** | supervisor@falkinstein.test | `falkinstein.localhost:44368` |
| **Clinic Staff** | staff@falkinstein.test | `falkinstein.localhost:44368` |
| **Patient (synthetic)** | patient@falkinstein.test | `falkinstein.localhost:44368` |
| **Patient (Gmail inbox)** | SoftwareThree@gesco.com | `falkinstein.localhost:44368` |
| **Applicant Attorney (synth)** | applicant.attorney@falkinstein.test | `falkinstein.localhost:44368` |
| **Applicant Attorney (Gmail)** | SoftwareFour@gesco.com | `falkinstein.localhost:44368` |
| **Defense Attorney (synth)** | defense.attorney@falkinstein.test | `falkinstein.localhost:44368` |
| **Defense Attorney (Gmail)** | SoftwareFive@gesco.com | `falkinstein.localhost:44368` |
| **Claim Examiner (synth)** | adjuster@falkinstein.test | `falkinstein.localhost:44368` |
| **Claim Examiner (Gmail)** | SoftwareSix@gesco.com | `falkinstein.localhost:44368` |

### When to use which

- **Synthetic users** (`@falkinstein.test`) for fast flow tests where
  you don't need to check email content. Their mail goes to MailKit's
  pickup folder, not a real inbox.
- **Gmail inbox users** (`@gesco.com`) when the test needs to verify
  an email landed in a real inbox. Adrian has access to those
  mailboxes; if you need to verify an email, ask Adrian to spot-check
  the inbox.
- **Tenant admin extras** (`SoftwareOne/Two@evaluators.com`) for
  multi-tester scenarios where two human testers need parallel admin
  accounts in the same tenant.

### Mailbox-routing gotcha

`SoftwareFour@gesco.com` routes some incoming mail to Junk via a
mailbox-level rule (not a code issue; documented at
`docs/demo-readiness/2026-05-11-pre-demo.md` item B). When testing an
AA flow that expects an email, check Junk if the inbox is empty.

---

## Part 5: URLs and ports

| What | URL | Notes |
| --- | --- | --- |
| OLD app | http://localhost:4202 | Read-only reference |
| AuthServer (NEW) | http://localhost:44368 | Razor pages for login / register / verify-email / password reset |
| API (NEW) | http://localhost:44327 | REST endpoints, Hangfire dashboard at `/hangfire` |
| Angular SPA (NEW) | http://localhost:4200 | Standalone components |
| Tenant subdomain (NEW) | http://falkinstein.localhost:{port} | The `falkinstein.` prefix triggers the subdomain tenant resolver |
| SQL Server (NEW) | localhost:1434 | sa / `MSSQL_SA_PASSWORD` from `docker/.env` |
| MinIO (NEW) | http://localhost:9000 (API), :9001 (console) | minioadmin / minioadmin |
| Redis (NEW) | localhost:6379 | No password |
| Gotenberg (NEW) | http://localhost:3000 | DOCX → PDF |
| Hangfire dashboard | http://localhost:44327/hangfire | Background job state |
| Health check | http://localhost:44327/health-status | API health composite |

### Tenant subdomain wildcard DNS

`*.localhost` resolves to 127.0.0.1 on Windows, macOS, and recent
Linux distros. So `falkinstein.localhost:4200` works out of the box;
no hosts-file edit needed. If a browser refuses to resolve it, add
`127.0.0.1 falkinstein.localhost` to `C:\Windows\System32\drivers\etc\hosts`.

---

## Part 6: OLD app reference

### Where things live

| What | Path |
| --- | --- |
| OLD source code | `P:\PatientPortalOld\` |
| OLD entity models | `P:\PatientPortalOld\PatientAppointment.DbEntities\` |
| OLD business logic | `P:\PatientPortalOld\PatientAppointment.Domain\` |
| OLD controllers | `P:\PatientPortalOld\PatientAppointment.Api\` |
| OLD Angular | `P:\PatientPortalOld\patientappointment-portal\` |
| OLD documentation | `P:\PatientPortalOld\Documents_and_Diagrams\` |
| OLD workflows + diagrams | `P:\PatientPortalOld\Documents_and_Diagrams\Workflow\` |
| OLD packet templates | `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\` |

### Parity audit docs already written

44 docs at `docs/parity/wave-1-parity/`. The priority subset (18) are
the external-user + internal-user-dependency flows -- those are the
ones already implemented in NEW. The remaining ~12 are internal-user
slices that need audit docs (your secondary deliverable).

Naming convention:
- `external-user-*.md` — Patient / AA / DA / CE flows
- `clinic-staff-*.md` — Clinic Staff flows
- `internal-user-*.md` — admin / Staff Supervisor flows
- `it-admin-*.md` — IT Admin host-scope flows
- `_*.md` — cross-cutting notes (parity flags, branding, etc.)
- `2026-*.md` — dated bug-research files

### Cross-reference rule

Before reporting a bug, **always** read the OLD code path AND the
corresponding parity audit doc (if one exists). Bugs are easier to
diagnose when you can cite "OLD does X at `AppointmentDomain.cs:221`,
NEW does Y at `AppointmentsAppService.cs:648`."

---

## Part 7: The Playwright MCP workflow

You have access to Playwright MCP. Use it for every flow test. The
patterns below are the ones that have worked during prior sessions in
this repo.

### Why Playwright MCP, not manual click-testing

- Reproducible -- the exact same actions can be replayed by a future
  session.
- Captures evidence -- snapshots, screenshots, console messages,
  network requests, all attached to the test record.
- Drives the test against a real browser (Chromium) so JS state +
  cookies + localStorage behave the same as in a real user session.

### Available MCP tools (commonly needed)

| Tool | Use for |
| --- | --- |
| `browser_navigate` | Go to a URL |
| `browser_snapshot` | Accessibility tree dump (more compact than HTML; better for reading) |
| `browser_take_screenshot` | Visual evidence; save under `.github/pr-media/` if attaching to a PR |
| `browser_fill_form` | Bulk-fill a form (faster than per-field `browser_type`) |
| `browser_click` | Click by accessibility-snapshot ref |
| `browser_select_option` | Pick a dropdown value |
| `browser_evaluate` | Run JS in the page context (read DOM, set values, check state) |
| `browser_console_messages` | Pull console errors / warnings (useful after every flow) |
| `browser_network_requests` | List every request fired during the flow |
| `browser_network_request` | Inspect a specific request's headers + body + response |

### Login sequence (worked example)

The SPA's login flow bounces through the AuthServer. Always start at
the tenant subdomain so the OAuth redirect carries the tenant context.

```text
1. browser_navigate -> http://falkinstein.localhost:4200/
2. SPA redirects to /account/login on the SPA
3. SPA detects no token, kicks off OAuth -> /connect/authorize
4. AuthServer redirects to /Account/Login (Razor)
5. browser_fill_form -> username + password
6. browser_click -> Sign In
7. AuthServer redirects back with code
8. SPA exchanges code for tokens, redirects to /home or /dashboard
   per the post-login-redirect.guard.
```

For a fully automated flow, drive each step explicitly. Don't rely on
`browser_navigate` to handle redirects -- inspect the URL after each
step with `browser_snapshot`.

### Snapshot vs screenshot

- `browser_snapshot` is your primary tool. The accessibility tree dump
  is short, readable, and shows interactive element refs you can use
  for `browser_click`.
- `browser_take_screenshot` only when you need pixel evidence for a
  bug ticket. Save under `tests/screenshots/{role}/{flow}/{step}.png`
  so the fix worktree can include them in PRs.

### Network capture pattern

Before any user action that should fire a network request:

```text
1. browser_navigate (or whatever action)
2. browser_network_requests  -- list all requests so far
3. Identify the request URL + method + status
4. browser_network_request -- inspect headers, body, response of the
   specific request
```

Verify both that the request went out AND that the response was 200
with the expected body shape. A 200 with empty body is still a bug.

### Console-clean assertion

After every user-facing flow, call `browser_console_messages` at level
"error". Any uncaught JS error during a clean flow is a bug. File it.

A few known noisy warnings are acceptable:
- `aspnetcore-browser-refresh.js: WebSocket failed` (dotnet watch
  reload channel, not relevant to user)
- DOM autocomplete suggestions
- LeptonX deprecation warnings on the LPX_THEME mismatch (harmless)

Anything beyond that → file.

### Multi-tab pattern

If a flow involves two users (e.g., Patient files a request, Clinic
Staff approves it), open two browser contexts:

```text
1. browser_navigate -> Patient signs in on tab 1
2. Submit appointment request
3. browser_take_screenshot for evidence (request confirmation #)
4. browser_close -- closes tab 1
5. browser_navigate (new context) -> Staff signs in on a fresh tab
6. Find the request in the pending queue
7. Approve
8. Verify the Patient sees the approval (either re-open tab 1 or check
   the patient view as Staff using accessor access)
```

Don't try to keep both tabs alive in the same browser context -- they
share localStorage, which means second-tab login can clobber first-tab
state.

---

## Part 8: Per-role flow inventory

This is the test matrix. Each row is a flow you must walk per role.
**Existing parity audit doc cited** where one exists. Where no audit
doc exists, write one (secondary deliverable) using existing docs as
templates.

### Patient (external)

| Flow | OLD ref | NEW path | Parity doc |
| --- | --- | --- | --- |
| Register | `AccountController.cs` Register | AuthServer Razor `/Account/Register` | `external-user-registration.md` |
| Verify email (link from inbox) | `AccountController.ConfirmEmail` | AuthServer `/Account/ConfirmUser` + SPA `/account/email-confirmation` | `external-user-registration.md` |
| Login | `AccountController.Login` | AuthServer Razor `/Account/Login` | `external-user-login.md` |
| Forgot password | `AccountController.ForgotPassword` | AuthServer `/Account/ForgotPassword` | `external-user-forgot-password.md` |
| Submit appointment request | `AppointmentDomain.Add` | SPA `/appointments/add` | `external-user-appointment-request.md` |
| View own appointments | `AppointmentController.GetAll` filtered | SPA `/home` (Patient view) | `external-user-view-appointment.md` |
| View appointment detail | `AppointmentController.Get` | SPA `/appointments/view/:id` | `external-user-view-appointment.md` |
| Upload ad-hoc document | `AppointmentDocumentDomain.Add` | SPA upload via doc-upload route | `external-user-appointment-ad-hoc-documents.md` |
| Upload package document (via verification code link in email) | `AppointmentDocumentDomain.GetValidation` | SPA `/upload/:id/:code` (anonymous) | `external-user-appointment-package-documents.md` |
| Request reschedule | `AppointmentRescheduleRequestDomain` | SPA appointment view | `external-user-appointment-rescheduling.md` |
| Request cancellation | `AppointmentCancelRequestDomain` | SPA appointment view | `external-user-appointment-cancellation.md` |
| Submit query | `UserQueryDomain.Add` | NOT YET BUILT in NEW | `external-user-submit-query.md` |

### Applicant Attorney (external)

All Patient flows above, plus:

| Flow | OLD ref | NEW path | Parity doc |
| --- | --- | --- | --- |
| Book appointment FOR a patient (full booking form) | `AppointmentDomain.Add` external-creator path | SPA `/appointments/add` | `external-user-appointment-request.md` |
| Upload AME Joint Declaration Form | `AppointmentJointDeclarationDomain` | SPA appointment view | `external-user-appointment-joint-declaration.md` |
| View attorney-scoped appointment list | `AppointmentController.GetAll` w/ accessor filter | SPA `/appointments` filtered | `external-user-view-appointment.md` |

### Defense Attorney (external)

| Flow | NEW path | Parity doc |
| --- | --- | --- |
| Login | AuthServer | `external-user-login.md` |
| View DA-scoped appointment list | SPA `/appointments` | `external-user-view-appointment.md` |
| View appointment detail | SPA appointment view | `external-user-view-appointment.md` |
| Upload ad-hoc document | SPA doc upload | `external-user-appointment-ad-hoc-documents.md` |

### Claim Examiner (external)

| Flow | NEW path | Parity doc |
| --- | --- | --- |
| Login | AuthServer | `external-user-login.md` |
| View CE-scoped appointment list (linked via injury detail email) | SPA `/appointments` | `external-user-view-appointment.md` |
| View appointment detail | SPA appointment view | `external-user-view-appointment.md` |

### Clinic Staff (internal)

| Flow | OLD ref | NEW path | Parity doc |
| --- | --- | --- | --- |
| Login | AuthServer | -- |
| View appointment queue (Pending + others) | OLD's dashboard | SPA `/appointments` | `clinic-staff-appointment-approval.md` |
| Approve / reject pending appointment | `AppointmentDomain.Approve` | SPA appointment view | `clinic-staff-appointment-approval.md` |
| Review uploaded documents | `AppointmentDocumentDomain.Approve` | SPA doc review | `clinic-staff-document-review.md` |
| Check patient in | `AppointmentDomain.CheckIn` | SPA appointment view | `clinic-staff-check-in-check-out.md` |
| Check patient out | `AppointmentDomain.CheckOut` | SPA appointment view | `clinic-staff-check-in-check-out.md` |
| Upload signature for packets | OLD's signature flow | SPA settings page | NO AUDIT DOC YET -- write `clinic-staff-signature-upload.md` |

### Staff Supervisor (internal)

All Clinic Staff flows plus:

| Flow | OLD ref | NEW path | Parity doc |
| --- | --- | --- | --- |
| Approve appointment change request | `AppointmentChangeRequestDomain.Approve` | SPA appointment view | NO AUDIT DOC YET -- write `staff-supervisor-change-request.md` |
| Reject appointment change request | `AppointmentChangeRequestDomain.Reject` | SPA appointment view | NO AUDIT DOC YET -- same as above |
| Approve cancellation request | `AppointmentCancelRequestDomain.Approve` | SPA | `external-user-appointment-cancellation.md` covers the request side; supervisor side needs its own |
| Bill / mark Billed | `AppointmentDomain.MarkBilled` | SPA appointment view | NO AUDIT DOC YET |

### admin (internal, tenant scope)

All Staff Supervisor flows plus:

| Flow | NEW path | Parity doc |
| --- | --- | --- |
| Master data CRUD (Doctors, Locations, AppointmentTypes, Languages, Statuses, States, WcabOffices, Patients, ApplicantAttorneys, DefenseAttorneys) | SPA various `/configurations/*` and `/doctor-management/*` | `master-data-crud.md` |
| User invite (admin invites external users) | SPA `/users/invite` | NO AUDIT DOC YET |
| View all appointments | SPA `/appointments` unfiltered | `internal-user-view-all-appointments.md` |
| Dashboard | SPA `/dashboard` | `internal-user-dashboard.md` |
| Reports | SPA `/reports/*` | `internal-user-reports.md` |

### IT Admin (host scope)

| Flow | NEW path | Parity doc |
| --- | --- | --- |
| Tenant CRUD | SPA `/saas/tenants` | NO AUDIT DOC YET |
| User management (host scope) | SPA `/identity/users` | `it-admin-user-management.md` |
| Custom fields catalog | SPA `/it-admin/custom-fields` | `it-admin-custom-fields.md` |
| Package details config | SPA `/it-admin/package-details` | `it-admin-package-details.md` |
| Notification template content | SPA `/text-template-management` | `it-admin-notification-templates.md` |
| System parameters | SPA `/it-admin/system-parameters` | `it-admin-system-parameters.md` |
| Application configurations | SPA | `application-configurations.md` |
| Audit logs | SPA `/audit-logs` | NO AUDIT DOC YET -- write `it-admin-audit-logs.md` |
| Record locking | (background concern) | `record-locking.md` |

### Per-flow variation matrix

For each flow above, walk these **variations** -- not just the happy
path:

1. **Happy path** -- valid input, expected outcome.
2. **Empty input** -- every required field blank, one at a time.
3. **Invalid input** -- malformed email, past dates, SSN with letters,
   over-length strings, special characters.
4. **Permission boundary** -- wrong role trying to access. E.g.,
   Defense Attorney trying to upload a JDF (should fail; only AA can
   upload JDFs on AME appointments).
5. **Cross-tenant boundary** -- log in as a user in tenant X, try to
   read tenant Y's data by manipulating the URL or hitting the API
   directly.
6. **Adversarial input** -- SQL-style chars in text fields, embedded
   `<script>` in name fields, attempting to fetch other appointments
   by Guid enumeration.
7. **Concurrent state** -- two users editing the same record (test the
   ConcurrencyStamp guard).
8. **Status-machine boundary** -- attempt a transition that's not
   allowed (e.g., approve an already-Rejected appointment).

The fix worktree's #114 case-separation gate, #115 past-date guard,
and #116 race-protection should already cover the most painful gaps
in (4), (5), (6), and (7). If they don't, file.

---

## Part 9: Database access

### Two DbContexts, one DB in Phase 1A

NEW uses dual DbContext pattern (`CaseEvaluationDbContext` for host +
tenant, `CaseEvaluationTenantDbContext` for tenant-only) but in Phase
1A everything lives in the single `CaseEvaluation` database.
`docs/architecture/MULTI-TENANCY.md` has the details if you need them.

### Connecting

```powershell
# From host (Windows)
sqlcmd -S localhost,1434 -U sa -P "$env:MSSQL_SA_PASSWORD" -d CaseEvaluation -Q "SELECT TOP 5 * FROM AbpUsers"
```

Or use Azure Data Studio / SSMS pointing at `localhost,1434`. Password
is in `docker/.env`.

### Key tables for testing

| Schema.Table | Why you care |
| --- | --- |
| `dbo.AbpTenants` | Tenant id + name. Falkinstein is here. |
| `dbo.AbpUsers` | All seeded users + any newly-registered users. `EmailConfirmed` column tells you if they passed verify-email. |
| `dbo.AbpUserRoles` + `dbo.AbpRoles` | Role assignment. Use to verify a user holds the expected role. |
| `dbo.AppEntity_Appointments` | Appointment rows. `AppointmentStatus` is the lifecycle enum (1=Pending ... 13=CancellationRequested). |
| `dbo.AppEntity_AppointmentDocuments` | Document rows. `Status` is the doc-status enum. `BlobName` is the MinIO key. |
| `dbo.AppEntity_AppointmentAccessors` | View / Edit grants for additional users on an appointment. |
| `dbo.AppEntity_AppointmentApplicantAttorneys` | AA-appointment join. `IdentityUserId` is null when attorney is named but not registered. |
| `dbo.AppEntity_AppointmentDefenseAttorneys` | Same shape as above for DA. |
| `dbo.AppEntity_AppointmentInjuryDetails` + `dbo.AppEntity_AppointmentClaimExaminers` | CE linkage via two-hop join (Injury → CE). |
| `dbo.OpenIddictAuthorizations` + `dbo.OpenIddictTokens` | Active OAuth grants. Useful for "is the user actually signed in?" queries. |
| `HangFire.Job` | Background-job state for emails (look up the SendAppointmentEmailJob entries). |
| `HangFire.State` | Per-job state history -- success/failure timestamps + error messages for failed email sends. |

### Useful queries

```sql
-- Verify a user has the expected role
SELECT u.Email, u.EmailConfirmed, r.Name as Role
FROM AbpUsers u
JOIN AbpUserRoles ur ON ur.UserId = u.Id
JOIN AbpRoles r ON r.Id = ur.RoleId
WHERE u.Email = 'SoftwareThree@gesco.com';

-- Find appointments in Pending status
SELECT a.RequestConfirmationNumber, a.AppointmentDate, a.AppointmentStatus, p.LastName as Patient
FROM AppEntity_Appointments a
LEFT JOIN AppEntity_Patients p ON p.Id = a.PatientId
WHERE a.AppointmentStatus = 1
ORDER BY a.CreationTime DESC;

-- Recent failed email jobs (Hangfire)
SELECT TOP 20 Id, CreatedAt, StateName, StateReason, InvocationData
FROM HangFire.Job
WHERE StateName = 'Failed'
ORDER BY CreatedAt DESC;
```

### Don't mutate via SQL during a test pass

Treat the DB as read-only during testing. Any direct UPDATE/INSERT is
state that won't survive a `down -v` and can mask bugs (e.g., #119
where Clinic Staff role grants were applied via direct SQL, masking
the fact that the migrator image was stale).

---

## Part 10: Parity audit methodology

For each flow you test, follow this loop:

1. **Read OLD source for the flow.** Start at the OLD controller, then
   trace through the domain. Note: the entity, the validation rules,
   the email triggers, the role gate.
2. **Read NEW source for the same flow.** Start at the NEW AppService
   (or AuthServer page model), trace through the manager.
3. **Compare:**
   - Same fields collected? Same labels? Same validation messages?
   - Same business rules? Same status transitions? Same conditions?
   - Same role gate? Same access scope?
   - Same emails fired? Same recipients? Same template variables?
   - Same DB writes? Same audit log entries?
4. **Walk the flow** on both apps with Playwright MCP.
5. **Write the parity audit doc** if one doesn't exist. Template
   structure (from existing docs):
   - Section 1: Goal
   - Section 2: Gap table (OLD vs NEW, per field/rule)
   - Section 3: OLD code map (file:line references)
   - Section 4: UI field inventory (every input, with type + validation)
   - Section 5: Business rules (numbered, with OLD source refs)
   - Section 6: Role matrix (which roles can do what)
   - Section 7: Edge cases observed
   - Section 8: Recommendations / open questions
6. **File a ticket for every gap** that isn't already documented as
   intentional (per `_parity-flags.md`).

### When OLD has a bug

If OLD does something obviously wrong (per project memory:
hardcoded `UserId=1`, `+91` country code for US app, etc.), don't
replicate it. Per the project CLAUDE.md "Bug and deviation policy":

- **Clear bug** -- fix in NEW silently (no flag needed).
- **Ambiguous** -- replicate verbatim AND add a `PARITY-FLAG` comment
  + a row in `docs/parity/_parity-flags.md` so a human can decide
  later.

---

## Part 11: Bug reporting format

Every finding goes back to `W:\patient-portal\replicate-old-app` as a
structured ticket. Don't free-form-paste descriptions; the fix
worktree will have to re-format them anyway.

### Ticket template

```markdown
[BUG-{NNN}] {short title <= 70 chars>}

Severity: blocker | high | medium | low
Role: Patient | Applicant Attorney | Defense Attorney | Claim Examiner |
      Clinic Staff | Staff Supervisor | admin | IT Admin
Flow: {feature name from docs/parity/wave-1-parity/}
Component: {NEW source file path that contains the bug, best guess}

Steps to reproduce:
  1. As {role}, navigate to {URL}
  2. {Action}
  3. {Observation}

Expected (per OLD parity):
  {What OLD does in the same situation, with OLD source ref}

Actual (current NEW behaviour):
  {What NEW does, with NEW source ref if known}

Evidence:
  - Screenshot: {path to PNG saved under tests/screenshots/}
  - Network request: {URL + method + status + relevant headers/body}
  - Console error: {verbatim error message + line}
  - DB state: {SQL query + result, if relevant}

OLD source: P:\PatientPortalOld\{path}:{line}
NEW source: src\{path}:{line}
Parity doc: docs/parity/wave-1-parity/{slug}.md (or NONE)

Suggested fix scope:
  {Best guess at what needs to change. Optional. Fix worktree decides.}
```

### Severity bands

- **blocker** — flow is unusable for a role; data corruption; security
  breach (cross-tenant read, missing access gate, etc.).
- **high** — flow works but produces wrong data or skips a required
  step; obvious deviation from OLD that breaks user expectations.
- **medium** — minor data shape difference, misleading error message,
  visual glitch.
- **low** — typo, missing comment, suboptimal copy.

### Where to put tickets

Append to a single file per session at:
`docs/runbooks/findings/{YYYY-MM-DD}-userflow-findings.md`

The fix worktree picks this up and triages.

### When to STOP testing

Stop and surface to Adrian when:

- A blocker is found on a foundational flow (login/register). No point
  walking downstream flows on a broken auth gate.
- The same bug surfaces in three+ different flows. Means the
  underlying primitive is broken; fix the primitive first.
- You hit a flow that has no audit doc AND no obvious OLD reference.
  Don't guess parity; ask.

---

## Part 12: Known limitations (do not file as bugs)

These are documented + accepted. Filing them again wastes everyone's
cycles.

### Architecture

- **Phase 1A is single-tenant.** Only Falkinstein. Don't test "create
  a second tenant and verify isolation" -- that's Phase 2.
- **Doctor is not a user role.** Doctor rows in the Doctor table are
  associated with appointments via `DoctorAvailability`. Don't try to
  log in as a doctor.
- **DOCX vs PDF.** OLD generated DOCX. NEW generates PDF. Intentional.
- **OLD theme colors + fonts.** NEW uses LeptonX components with OLD's
  color tokens (see `docs/design/_design-tokens.md`). Visual layout
  differs by design.

### Behaviour gated by upstream features (Category 8 emails)

These email categories will not fire because the upstream feature
hasn't shipped in NEW yet -- by design:

- `AddInternalUser` -- ABP Suite handles internal-user creation.
- `UserQuery` -- SubmitQuery feature not built.
- `AppointmentChangeLogs` -- audit-log writer not built.
- `AppointmentRescheduleRequestByAdmin` -- admin-direct-edit path
  doesn't exist (admin reschedules go through the change-request flow).

If your flow expects one of these emails, it won't get one. That's
documented in
`docs/parity/wave-1-parity/email-handlers-demo-critical.md`.

### Packet pagination

There's still ~40% residual overflow on Doctor packet p9, Patient
packet p13, Patient packet p16. These need per-template DOCX edits
(`KeepWithNext`, section-break cleanup). Tracked as Issue #104 in the
fix worktree. Don't file individual page-break tickets.

### Test data residual issues

- The Falkinstein tenant Id drifts on every fresh DB. Don't cache the
  GUID; look it up at session start (see Part 5).
- 64 Doctor availability slots and sample documents were wiped
  2026-05-12. You will need to re-create them as the first thing in a
  test session.

### SPA `/account/*` minus register

Today only `/account/register` is gated (404 for anon, redirect for
authed) per Issue #105. Other `/account/*` routes still pass through
ABP's stock module. If you find one of those routes renders something
unexpected, file it but mark it `medium` severity -- it's known.

---

## Part 13: Workflow recipes

### Recipe: Patient register + verify + login

```text
1. browser_navigate http://falkinstein.localhost:44368/Account/Register
2. browser_fill_form
   first-name: Eve
   last-name: Patient
   email: testpatient1@example.test
   password: 1q2w3E*r
   confirm-password: 1q2w3E*r
3. browser_click "Sign Up"
4. Verify success banner appears with "Verify Email" + "Sign In" buttons
5. browser_click "Verify Email" (opens ResendVerification with autosend)
6. Verify "We've sent a verification email" message
7. browser_console_messages -- expect no errors
8. browser_network_requests -- verify
   - POST /api/public/external-signup/register returned 200
   - GET /Account/Logout was fired (Issue #106b fire-and-forget)
   - GET /Account/ResendVerification with autosend=1 returned 200
9. SQL: SELECT EmailConfirmed FROM AbpUsers WHERE Email = ...
   -- should be 0 (not confirmed yet)
10. (Adrian's inbox check) verification email arrives at SoftwareThree@gesco.com
11. Click verify link from inbox -> SPA /account/email-confirmation
12. browser_navigate the link
13. Verify success message + always-visible Resend button (Issue #1.4)
14. SQL: EmailConfirmed = 1
15. Sign In -- standard OAuth flow lands on /home for Patient role
```

### Recipe: Cross-tenant document access attempt

```text
1. Sign in as patient@falkinstein.test
2. browser_evaluate () => { /* read access_token */ }
3. Find an appointment id the patient OWNS (their own)
4. Find an appointment id the patient does NOT own (via SQL, pick a
   row in same tenant but different patient)
5. browser_evaluate make fetch call to
   /api/app/appointments/{not-owned-id}/documents with Bearer token
6. Verify: 403 (per Issue #114 -- AppointmentReadAccessGuard)
7. Try the same against /documents/{doc-id}/download (use a doc id
   belonging to a different patient's appointment)
8. Verify: 403
9. browser_network_request -- inspect the 403 response body. Should
   contain the localized "Appointment:AccessDenied" message.
10. If ANY 200 comes back: BLOCKER ticket, regression of #114
```

### Recipe: Past-date booking attempt

```text
1. Sign in as Clinic Staff (or admin)
2. Open the appointment-update form for an existing appointment
3. browser_evaluate to override the AppointmentDate input value to a
   date 7 days in the past
4. Submit
5. Verify: server-side BusinessException with code
   `AppointmentBookingDateInsideLeadTime`, leadTimeDays=0
   (per Issue #115)
6. Verify: the database row's AppointmentDate is UNCHANGED
7. As a follow-up: edit the same appointment, change PanelNumber but
   NOT AppointmentDate. Submit. Should succeed (the past-date guard
   only fires when the date is changing).
```

### Recipe: Multi-tab session swap

```text
1. Browser context 1: sign in as patient@falkinstein.test
2. browser_take_screenshot -- record the patient's home page state
3. browser_evaluate -- read currentUser.id from ConfigStateService
   for the record
4. New browser context 2: open AuthServer Razor /Account/Login (port
   44368)
5. Sign in as adjuster@falkinstein.test (different sub claim)
6. Return to context 1 (the SPA still has patient's tokens in localStorage)
7. Force a refresh-token rotation (or wait for it to fire on its own
   timer)
8. Verify (Issue #107): if the new tokens have a different sub claim,
   SessionIdentityWatcherService should fire window.location.reload.
   In practice with refresh-token rotation, the new tokens belong to
   the SAME user as before (the refresh_token is owned by patient, so
   refresh returns patient's tokens regardless of the cookie state).
   So the reload does NOT fire. This is the accepted limitation
   documented in the service's XML comment.
9. The bug-case (cookie swap actually changing the SPA's tokens)
   requires a manual sign-in flow re-trigger. Hard to automate.
   Document as observed behaviour, not a finding.
```

---

## Part 14: Reporting cadence

### End-of-session bundle

After each test session, push a single commit (or PR) to
`feat/replicate-old-app` containing:

1. Updated `docs/runbooks/findings/{YYYY-MM-DD}-userflow-findings.md`
2. Any new parity audit docs you wrote under
   `docs/parity/wave-1-parity/`
3. Updated `docs/parity/_parity-flags.md` if you added new flags

Don't commit screenshot binaries unless they're attached to a specific
bug ticket -- they bloat the repo. Put them under
`tests/screenshots/{YYYY-MM-DD}/` (gitignored if a `.gitignore` rule
is missing -- add one).

### Daily summary for Adrian

At the end of the day, generate a brief summary (use the `/eod-report`
skill if available):

- Flows walked today
- Bugs filed (count by severity)
- Audit docs written
- Stuck / blocked items needing decisions

---

## Part 15: Quick-reference appendix

### Stock OLD vs NEW URL map

| OLD URL | NEW URL |
| --- | --- |
| `http://localhost:4202/login` | `http://falkinstein.localhost:44368/Account/Login` |
| `http://localhost:4202/register` | `http://falkinstein.localhost:44368/Account/Register` |
| `http://localhost:4202/forgot-password` | `http://falkinstein.localhost:44368/Account/ForgotPassword` |
| `http://localhost:4202/verify-email/{userId}?query={uuid}` | `http://falkinstein.localhost:4200/account/email-confirmation?userId={u}&confirmationToken={t}` (NEW SPA redirects OLD URL automatically) |
| `http://localhost:4202/dashboard` | `http://falkinstein.localhost:4200/dashboard` (internal) or `/home` (external) |
| `http://localhost:4202/appointment-request` | `http://falkinstein.localhost:4200/appointments/add` |
| `http://localhost:4202/appointments` | `http://falkinstein.localhost:4200/appointments` |

### Key project files

| File | Read when... |
| --- | --- |
| `CLAUDE.md` (root) | First read of every session. Critical constraints + bug policy. |
| `docs/parity/wave-1-parity/_parity-flags.md` | Need to know if a specific behaviour is intentional or a flag. |
| `docs/parity/wave-1-parity/_branding.md` | Question about colors / fonts / spacing. |
| `docs/parity/wave-1-parity/_old-docs-index.md` | Need to find an OLD-side doc reference. |
| `docs/security/SESSION-AND-TOKENS.md` | Auth or session-related test. |
| `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md` | Appointment-flow test. Contains entity shape + state diagram + known gotchas. |
| `.claude/rules/hipaa-data.md` + `.claude/rules/test-data.md` | Generating test data. Always synthetic. |

### Common command cheatsheet

```powershell
# Stop everything, fresh start
docker compose down -v
docker compose build db-migrator
docker compose up -d --build

# Tail logs for a specific service
docker compose logs -f api --tail 50

# Run a SQL query
sqlcmd -S localhost,1434 -U sa -P "$env:MSSQL_SA_PASSWORD" -d CaseEvaluation -Q "SELECT COUNT(*) FROM AbpUsers"

# Capture every email pickup file (MailKit dev mode)
ls docker/mail-pickup/ | head

# Resolve a tenant id from a slug
curl -s http://localhost:44327/api/public/external-signup/resolve-tenant?name=falkinstein

# Trigger background jobs immediately for a fresh seeded DB
curl -X POST http://localhost:44327/api/abp/application-configuration

# Test that the access gate is firing (Issue #114)
# (run after capturing a Bearer token from a patient signed in)
curl -H "Authorization: Bearer $TOKEN" http://localhost:44327/api/app/appointments/{not-owned-id}/documents
# Expect 403
```

---

## Part 16: Decision points and escalation

### Decisions you do NOT make

- Should we replicate this OLD bug? → ASK Adrian.
- Is this flag intentional? → READ `_parity-flags.md`, then if still
  unclear, ASK.
- Should a missing-in-NEW feature be flagged as a gap or skipped? →
  ASK Adrian.
- Whether to merge a PR → never. The fix worktree handles merges.

### Decisions you DO make

- Test order within a role.
- Test data values (within `.claude/rules/test-data.md`).
- Screenshot vs snapshot choice.
- Bug severity (using the bands in Part 11).
- Parity audit doc structure (within the existing template).

### How to escalate

When you need a decision:

1. Save your test progress (commit WIP).
2. Write a focused question with:
   - What you observed
   - What you think the options are
   - What you'd lean toward if you had to decide
3. Ask Adrian directly.
4. While waiting, move to a different flow.

---

## Part 17: Final checklist before you start

- [ ] Read this entire doc.
- [ ] Read `CLAUDE.md` at repo root.
- [ ] Boot NEW per Part 3.
- [ ] Smoke-test the three URLs in Part 3.
- [ ] Confirm Falkinstein tenant id with the resolve-tenant curl.
- [ ] Verify the 16 seeded users via SQL (Part 9 query).
- [ ] Open OLD at http://localhost:4202 in one browser context.
- [ ] Open NEW at http://falkinstein.localhost:4200 in another.
- [ ] Create `docs/runbooks/findings/{TODAY}-userflow-findings.md`
      with an empty bug list.
- [ ] Pick your first flow (recommend: Patient register + login, since
      everything downstream depends on it working).
- [ ] Start walking the flow with Playwright MCP per Part 7.

---

## Related

- `MAIN-WORKTREE-USERFLOW-TESTING.md` (this file) -- comprehensive
  hand-off
- `DEMO-LOGINS.md` -- legacy creds doc (mostly superseded by Part 4)
- `DOCKER-DEV.md` -- docker compose patterns
- `LOCAL-DEV.md` -- host-side dev (non-docker)
- `docs/parity/wave-1-parity/*.md` -- 44 parity audit docs
- `docs/security/SESSION-AND-TOKENS.md` -- threat model
- `docs/demo-readiness/2026-05-11-pre-demo.md` -- prior-demo bug list
  with mailbox-side gotchas
- `CLAUDE.md` at repo root -- branch-scoped guidance, bug policy
