# Prompt — Drive the Falkinstein demo end-to-end via Playwright MCP

Paste this verbatim into a Claude session that has the Playwright MCP server loaded (the `browser_navigate`, `browser_click`, `browser_type`, `browser_take_screenshot`, etc. tools must be available). The session does **not** need any other context — every URL, credential, selector hint, and acceptance criterion is in this prompt.

---

## Role + authority

You are a QA engineer driving an end-to-end demo smoke test on a local docker-compose stack. Your job is to follow the 7 demo flows below in order, capture a screenshot at every checkpoint, and produce a tight diagnostic report at the end. You may interrupt the flow ONLY to surface a blocker that prevents the next step (e.g. login fails, a 500 on the booking form). Otherwise complete every step. Do **not** modify code; this is a read-the-app, drive-the-app, write-a-report job.

## Outcome

A markdown report with one section per demo flow, each section containing:

1. **Status:** `pass` / `fail` / `partial` / `blocked`.
2. **Evidence:** screenshot file path(s) the MCP saved (under `.playwright-mcp/`).
3. **Observations:** what the user-visible UI did at each step, in 2-4 bullets.
4. **Failures (if any):** exact error message + the page URL it surfaced on.

End the report with a `## Open issues` section listing every defect encountered, each with: severity (blocker / major / minor), one-line description, and which flow surfaced it.

## Constraints (each carries a reason)

- Run all flows against the **Falkinstein** tenant. Reason: that's the only seeded tenant; any other tenant slug resolves to host context which won't have the demo data.
- Open every page via `http://falkinstein.localhost:<port>`, never via raw `http://localhost:<port>` or via IP. Reason: ABP's tenant resolver reads the subdomain (ADR-006). Bypassing it lands you in host context where Patient queries return empty.
- Do **not** try to register a new user with the same email twice across runs. Reason: ABP enforces email uniqueness; a stale row from a prior run causes a 400 with "this email is already in use" that masks unrelated bugs. Stamp the registration email with `Date.now()` to avoid collision.
- When the AuthServer login page appears, verify the **tenant box is NOT shown** before submitting credentials. Reason: T2 of ADR-006 hides the LeptonX tenant switcher; if it appears, T2 regressed and the rest of the flow is unsafe to interpret.
- Do **not** click `Logout` between flows unless a flow explicitly says so. Reason: ABP's distributed cookie cache occasionally races on quick login → logout → login cycles in dev and produces a "concurrent login" false-positive that wastes diagnosis time.

## Service URLs (local docker-compose)

| Service | URL |
|---|---|
| SPA | `http://falkinstein.localhost:4200` |
| AuthServer | `http://falkinstein.localhost:44368` |
| API | `http://falkinstein.localhost:44327` |
| Swagger | `http://falkinstein.localhost:44327/swagger` |

`*.localhost` resolves to `127.0.0.1` natively in Edge / Chrome / Firefox via RFC 6761 — no hosts file edit required.

## Credentials (Development-only seeds, password is `1q2w3E*r` for every account)

| Role | Email | Notes |
|---|---|---|
| Tenant admin | `admin@falkinstein.test` | Tenant-scoped, can do everything inside Falkinstein |
| Staff Supervisor | `supervisor@falkinstein.test` | Internal user that creates slots in OLD app |
| Clinic Staff | `staff@falkinstein.test` | Internal user with Appointments + Patients access |
| IT Admin | `it.admin@hcs.test` | Host-side; cross-tenant; **do NOT use for tenant flows** |
| Patient (pre-seeded) | `patient@falkinstein.test` | Use only if registration is broken |
| Adjuster / Claim Examiner | `adjuster@falkinstein.test` | Pre-seeded external user |
| Applicant Attorney | `applicant.attorney@falkinstein.test` | Pre-seeded external user |
| Defense Attorney | `defense.attorney@falkinstein.test` | Pre-seeded external user |

If any of these can't log in (e.g. "user not found"), the seed contributor didn't run cleanly — surface as a blocker and stop.

## Reasoning affordance

Before clicking through, take 30 seconds to think:

- The 7 flows are sequentially dependent (slot → booking → approval → packet). A failure in step N invalidates step N+1; mark N+1 as `blocked` if N didn't pass, don't waste time clicking through it.
- The packet PDF generation step (flow 7) is **expected to be unimplemented** as of 2026-05-05 — the parity audit doc at `docs/parity/packet-generation-audit.md` is in REVIEW READY status, not SHIPPED. If you hit "feature not implemented" or a missing route, mark as `blocked` with a "deferred per packet-generation-audit.md" note rather than as a regression.
- Email-confirmation gate: ABP redirects to `/Account/ConfirmUser` after login if `EmailConfirmed=false`. Seeded users have `EmailConfirmed=true` set by the `InternalUsersDataSeedContributor` so this gate should not fire for the admin/supervisor/staff seeds. If you DO see the ConfirmUser page, the most recent rebuild may not have picked up the seed change — surface as a blocker.

## The 7 flows (in order)

### Flow 1 — Subdomain landing + tenant box hide

1. Open `http://falkinstein.localhost:4200/`. Confirm the SPA loads (no redirect to admin.localhost; that only happens on the bare-host URL).
2. Click `Login` (top-right, in the LeptonX shell, or the home-page CTA).
3. Confirm the AuthServer login page appears at a URL containing `falkinstein.localhost:44368/Account/Login`.
4. **Verify the tenant box is NOT visible** (no "Tenant: Not selected / switch" anchor). Screenshot.
5. Stop here — no submit yet.

**Acceptance:** SPA loaded on the subdomain; AuthServer login URL is on the subdomain; no tenant-switcher UI rendered. If any fails: `fail` + halt the run.

### Flow 2 — Internal user login + slot generation

1. From the AuthServer login page (Flow 1 left you there), sign in as `supervisor@falkinstein.test` / `1q2w3E*r`.
2. After redirect, you should land on `/dashboard`. Screenshot.
3. Navigate to the slot-management page. The route is `/doctor-availabilities` or accessible via the sidebar under "Doctor Management" → "Slot Generation". The page has a `Generate` mode at `/doctor-availabilities/generate` that creates a batch of slots in one operation.
4. Use the slot-generation form to create slots for **tomorrow**, time range **9:00 AM to 5:00 PM**, location any one shown in the dropdown, appointment type any one shown.
5. Submit. Screenshot the success state (toast + table refresh showing the new slots).

**Acceptance:** at least one slot row visible on the list page after generation. If 0 slots: `fail`, capture the toast / error response from the network panel.

### Flow 3 — External user registration (new patient)

1. From the SPA root, click `Register` (or navigate directly to `http://falkinstein.localhost:4200/account/register`).
2. Fill the form with a fresh email like `e2e-patient-<unix-timestamp>@e2e.test`, the default password `1q2w3E*r`, first name `E2E`, last name `RunNNN` (where NNN is the timestamp's last 3 digits).
3. Submit. Screenshot the post-register state (either a "verify your email" notice, or a redirect into the booking flow if the system auto-confirms).
4. If a confirmation email is required, the demo seeds set `EmailConfirmed=true` only for INTERNAL users; new registrations may need to confirm via email. **If the page asks you to confirm**, mark Flow 3 as `partial` and use the pre-seeded `patient@falkinstein.test` account for Flow 4.

**Acceptance:** account created with no exception page. The role assigned should be `Patient`. Confirm by checking the user dropdown after login — if the role is not Patient, the registration form picked the wrong role. `fail` with the role observed.

### Flow 4 — External user login + booking

1. Log in with the freshly registered account (or fall back to `patient@falkinstein.test`).
2. From the home page, navigate to the booking entry. The route is the appointment-add component (often surfaced as "Book Appointment" or under the home page CTA). If you can't find a CTA, navigate directly to `/appointments` and look for the "New" or "Add" button.
3. Fill the booking form. The form has multiple sections: Appointment (type + slot picker), Patient (your fields, mostly prefilled from registration), Applicant Attorney (skip if optional), Injury Details (claim number, date of injury, body parts), Custom Fields (one or two fields the tenant has configured — accept defaults).
4. Pick the slot you generated in Flow 2 (it shows tomorrow's date and the time range you set).
5. Submit. Screenshot the post-submit state.

**Acceptance:** confirmation number is shown; redirect or visible state change confirms the appointment is in `Pending` (or whatever the OLD-status equivalent is — check the status pill on the resulting page). If form rejects with validation errors that block submit, `fail` and capture the error text + which field surfaced it.

### Flow 5 — Patient views their appointment

1. Navigate to the appointments view. From the patient's account, the route is typically `/appointments` or `/home`.
2. Confirm the appointment from Flow 4 is visible in the list, with the confirmation number, the slot details, and the status pill.
3. Click into it (route should be `/appointments/view/:id`). Screenshot the detail page.

**Acceptance:** the patient sees the appointment they just booked. Status is `Pending` or `Awaiting Approval`. If the list is empty, the IMultiTenant filter or the per-user filter is misconfigured — `fail`, capture the network response for `GET /api/app/appointments`.

### Flow 6 — Internal user reviews + approves/rejects

1. **Without logging out** (per the Constraints note above), open a new tab to `http://falkinstein.localhost:4200/` in the same browser context — actually scratch that, ABP's auth state is shared across tabs, so a clean second-user flow needs a logout. **Exception to the no-logout rule:** for this flow, log out of the patient account first, then log in as `supervisor@falkinstein.test`.
2. Navigate to the appointments list (`/appointments`). The supervisor should see the just-booked appointment in `Pending`.
3. Click into the appointment detail page. There should be `Approve` and `Reject` action buttons (recently shipped per commit `4b42575 feat(approval): replicate OLD approve+reject modals end-to-end (A1)`).
4. Click `Approve`. A modal should appear. Fill any required fields (comments or a confirmation note) and submit.
5. Screenshot the post-approval state. Status pill should change to `Approved` (or OLD-equivalent).
6. **Repeat with another fresh booking using a different patient and the `Reject` action** to confirm rejection wiring also fires.

**Acceptance:** approval mutates the status visibly; the appointments list reflects it on refresh. Capture the network response for the approve call (POST /api/app/appointments/.../approve or similar).

### Flow 7 — Email + PDF packet validation

1. **Email validation** — the demo wires emails to Azure Communication Services SMTP (`smtp.azurecomm.net`). Locally there is no SMTP catcher, so emails either fail-to-send (and log the failure) or send to a real Azure environment. Open the `replicate-old-app-api-1` container's stdout via the docker logs panel of your terminal (you can shell out via the MCP's terminal/process tool if available, otherwise note that you'd need a separate shell to run `docker logs replicate-old-app-api-1 --tail 500 | grep -iE "smtp|sendmail|notification"`).
2. After a successful registration, booking submit, and approval, you should see at least three sets of email-send attempts in the logs:
   - "User registered" notification to the new patient.
   - "Appointment submitted" notification to the patient + applicant attorney + defense attorney + claim examiner (if any).
   - "Appointment approved" notification to the same recipients PLUS attachments for the patient + doctor packets.
3. **Packet PDF download** — on the approved appointment's detail page, look for a "Patient Packet" + "Doctor Packet" download button. If present and clicking generates a PDF (browser opens or downloads a `.pdf` file), capture both.

**Acceptance:** email-send log lines for all three lifecycle events; both PDFs download and open without corruption. **Expected gap:** packet generation is per `docs/parity/packet-generation-audit.md` in REVIEW READY status, NOT SHIPPED. If the buttons are missing or generation 500s, mark as `blocked` and quote the audit-doc status.

## Output format

Produce the report as a single markdown document. Use H2 headings for each flow, in the order above. Each flow ends with the four-field block (`Status`, `Evidence`, `Observations`, `Failures`). After Flow 7, end with `## Open issues` (severity-ranked) and a `## Recommendations` section (next 1-3 things to fix to unblock the demo).

## Anti-patterns (do NOT do)

- Do not invent fixes or change source code. Your output is a report, not a PR.
- Do not retry the same login more than 3 times. ABP locks out accounts after a small number of failed attempts; if 3 retries fail, surface the lockout as a blocker.
- Do not use `it.admin@hcs.test` for any tenant-scoped flow. That account is host-side; using it produces misleading "I can see all tenants' data" results.
- Do not skip the "tenant box not visible" check in Flow 1. The whole demo's HIPAA story rests on that affordance being absent.
- Do not paraphrase error messages. Quote them verbatim — line numbers and exception types matter when triaging.
