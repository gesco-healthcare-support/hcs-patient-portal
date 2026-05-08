# Manual demo test handoff — Falkinstein tenant, Phase 1A

This is the cheat-sheet for testing the demo flows yourself in a browser. Pair it with `2026-05-05-demo-e2e-playwright-mcp-prompt.md` if you also want a Claude session driving Playwright in parallel.

## Service URLs

`*.localhost` resolves to `127.0.0.1` natively on Edge / Chrome / Firefox via RFC 6761. **No hosts-file edit required.** (Safari is out of scope for the local dev loop.)

| Service | URL | What you do here |
|---|---|---|
| **SPA (Falkinstein)** | http://falkinstein.localhost:4200 | Tenant portal — login, slots, booking, approvals |
| **SPA (host context)** | http://admin.localhost:4200 | IT-Admin / cross-tenant management |
| **AuthServer** | http://falkinstein.localhost:44368 | OAuth login pages |
| **API** | http://falkinstein.localhost:44327 | REST endpoints |
| **Swagger** | http://falkinstein.localhost:44327/swagger | API explorer with the AuthServer integration baked in |

If `falkinstein.localhost` doesn't resolve in your browser, your DNS resolver is overriding RFC 6761 (some corporate VPN-DNS setups do). Workaround: add `127.0.0.1 falkinstein.localhost admin.localhost` to `C:\Windows\System32\drivers\etc\hosts` (admin elevation required).

## Credentials

All passwords are `1q2w3E*r` (8 chars, satisfies the dev policy). Internal-user accounts have `EmailConfirmed=true` set by the seed contributor — login works on first try, no email-confirm gate. External demo accounts (the 4 below) are NOT yet seeding because of an ordering bug between the role contributor and the user contributor; for now use the **Register** path to create a fresh patient.

### Internal users (Falkinstein tenant)

| Role | Email | Use it for |
|---|---|---|
| Tenant admin | `admin@falkinstein.test` | Full tenant access — appointments, slots, master data |
| Staff Supervisor | `supervisor@falkinstein.test` | **Slot creation in OLD app — start here for slot generation** |
| Clinic Staff | `staff@falkinstein.test` | Day-to-day appointment work |

### External users (Falkinstein tenant)

The demo external users (`patient@falkinstein.test`, `adjuster@`, `applicant.attorney@`, `defense.attorney@`) are **not yet seeded** as of 2026-05-05 because the role contributor runs after the demo-user contributor (issue #18 in the task list). **Use the Register path instead** — see Flow 3 below.

### Host-side user

| Role | Email | Notes |
|---|---|---|
| IT Admin | `it.admin@hcs.test` | Visit via `admin.localhost:4200`, NOT via `falkinstein.localhost`. Has cross-tenant authority. **Don't use for tenant-scoped flows** — it confuses the IMultiTenant filter story. |

## Demo flows in 2-line summaries

### 1. Subdomain landing (T1-T3 verification)

- Open http://falkinstein.localhost:4200/ → SPA loads (no redirect to admin.localhost).
- Click Login → AuthServer page at falkinstein.localhost:44368 loads with **no "Tenant: Not selected"** affordance (T2 verified).

### 2. Internal user → slot generation

- Login as `supervisor@falkinstein.test`. Lands on `/dashboard`.
- Sidebar → Doctor Management → Slot Generation (or navigate to `/doctor-availabilities/generate`).
- Pick tomorrow's date, 9 AM - 5 PM, any location, any appointment type. Submit.

### 3. External user → register + login

- Logout. Click Register on the home page (or visit `/account/register`).
- Email: anything fresh like `e2e-patient-<your-stamp>@e2e.test`. Password: `1q2w3E*r`. Fill name + DOB + phone.
- Submit. The `RequireConfirmedEmail` setting was flipped to `false` for Phase 1A (commit pending), so login works **without** clicking a verification link. The email-send still fires — if you used a real mailbox, you'll get the verification email and can confirm at your leisure. Dummy `*.test` addresses just sail through.

### 4. Booking flow

- Logged in as the new patient. Home page should have a "Book Appointment" CTA, otherwise navigate to `/appointments` → "New".
- Pick the slot you generated in flow 2. Fill the rest of the form.
- Submit. Confirmation number renders.

### 5. Patient views their appointment

- After submit, you should see the appointment in your list (`/appointments`).
- Status: `Pending` or `Awaiting Approval`.

### 6. Internal user → approve / reject

- Logout. Login as `supervisor@falkinstein.test`.
- `/appointments` → click the pending booking. Approve and Reject modals are wired (commit `4b42575`).
- Approve flow: click Approve, fill the modal, submit. Status changes.

### 7. Email + packet PDFs

- **Email**: SMTP points at `smtp.azurecomm.net` per `appsettings.json`. There's no local mail catcher in docker-compose. To inspect: `docker logs replicate-old-app-api-1 --tail 500 | grep -iE "smtp|sendmail|notification"`.
- **Packet PDFs**: per `docs/parity/packet-generation-audit.md`, packet generation is in **REVIEW READY** status, not SHIPPED yet. The Approve flow's email-attachment step + the Doctor Packet download button are expected to be missing or non-functional. Mark this as expected-gap, not a regression.

## SQL helpers (paste into Azure Data Studio or sqlcmd via docker exec)

Connection: `Server=127.0.0.1,1434;User Id=sa;Password=myPassw@rd;Database=CaseEvaluation;TrustServerCertificate=True`.

```sql
-- See seeded users
SELECT Email, EmailConfirmed, TenantId
FROM AbpUsers
WHERE Email LIKE '%falkinstein%' OR Email LIKE '%hcs.test%'
ORDER BY Email;

-- See Falkinstein tenant
SELECT Id, Name, NormalizedName FROM SaasTenants;

-- Force-confirm an email (workaround if registration's confirm gate blocks login)
UPDATE AbpUsers SET EmailConfirmed = 1 WHERE Email = 'paste-the-email-here';

-- See Patient rows by tenant (verifies FEAT-09 / IMultiTenant filter)
SELECT Id, FirstName, LastName, Email, TenantId
FROM AppEntity_Patients
ORDER BY TenantId, LastName;

-- See appointments by tenant + status
SELECT Id, RequestConfirmationNumber, AppointmentStatus, TenantId, AppointmentDate
FROM AppEntity_Appointments
ORDER BY CreationTime DESC;
```

## Known gaps

| # | Severity | Description | Blocker for demo? |
|---|---|---|---|
| 1 | Major | External demo users (`patient@`, `adjuster@`, `applicant.attorney@`, `defense.attorney@`) skipped at seed time — role contributor runs after user contributor | No — register a fresh patient instead |
| 2 | ~~Major~~ Resolved | ~~Public registration may force `/Account/ConfirmUser`~~ — `RequireConfirmedEmail` flipped to `false` for Phase 1A demo. Email still sends; login no longer blocked. | No |
| 3 | Major | Packet PDF generation (Patient + Doctor) not yet implemented | Yes for Flow 7; flows 1-6 work |
| 4 | Minor | Local SMTP has no catcher; emails go to Azure or fail silently | No — verify via logs |
| 5 | Minor | One pre-existing State repository test flake (unrelated to T4) | No |

## Restart / reset commands

```bash
# Hard reset (drops DB, re-seeds, takes ~5 minutes)
docker compose down -v
docker compose up -d --build

# Soft restart (keeps DB)
docker compose restart api authserver angular

# Tail logs
docker logs replicate-old-app-api-1 --tail 100 -f
docker logs replicate-old-app-authserver-1 --tail 100 -f
docker logs replicate-old-app-db-migrator-1   # exits cleanly after seed; tail to see what got seeded
```

## What to test first if you have only 5 minutes

1. Open http://falkinstein.localhost:4200/ — confirm SPA loads.
2. Click Login → confirm no tenant box on the AuthServer page (T2).
3. Sign in as `supervisor@falkinstein.test` / `1q2w3E*r` → confirm `/dashboard` lands.
4. That validates T1 + T2 + T3 + the EmailConfirmed seed fix all in one pass. The rest of the flow is incremental from there.
