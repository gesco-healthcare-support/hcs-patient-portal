[Home](../../INDEX.md) > [Product Intent](../) > [Cross-cutting](./) > Tenant Provisioning

# Tenant Provisioning -- Intended Behavior

**Status:** draft -- Phase 2 T13, cross-cutting cluster
**Last updated:** 2026-04-27
**Primary stakeholders:** Host admin (the onboarding driver), supervisor admin (the assigned mid-tier owner of the tenant after onboarding), Practice Admin (the first user the tenant has)

> Cross-cutting intent for how a new tenant gets provisioned in the Patient Portal -- the operational sister of T9 (multi-tenancy) and T10 (auth-and-roles). Lifts T4 host-admin-manual onboarding (Q20), T8 initial-Locations seeding (Q-A3), T9 tenant-unit definition + decommission rule (Q-T9-4), T10 supervisor admin assignment + initial Practice Admin user creation (Q-T10-3 / Q-T10-6), and the magic-link auth pattern (Q-T10-4) into one canonical onboarding procedure. Every claim source-tagged.

## Purpose

Tenant provisioning is the on-ramp for every new medical examiner's practice. T13 documents what the host admin does, what data is collected at the form level, when the tenant is "live" enough to accept bookings, how the initial Practice Admin user gets credentials, and how the supervisor admin is assigned. It is the only path to creating a tenant at MVP -- there is no invite-email or self-service signup for doctors (Q20). [Source: Adrian-confirmed across T4 + T8 + T9 + T10 + T13 sessions]

## Personas and goals

Cross-reference `00-BUSINESS-CONTEXT.md` and `cross-cutting/auth-and-roles.md` for full persona definitions.

- **Host admin (Gesco-side, top tier).** Drives onboarding. Operates the admin screen that creates the tenant. Picks the supervisor admin from the supervisor-admin roster. Enters the initial setup data on the onboarding form. Audit-logged for every action (T9 Q-T9-5).
- **Supervisor admin (Gesco-side, mid-tier).** Assigned at onboarding (or later) to own the tenant from a support / portfolio-management standpoint. Same authority as host admin within the tenant; cannot create tenants (T10 Q-T10-5). Takes over routine support tasks once the tenant is live.
- **Initial Practice Admin (within the tenant).** The first user the tenant has. Receives a magic-link invite email at onboarding (consistent with T10 Q-T10-4 Patient pattern). Logs in once via the link, sets a password, and then either (a) sees a fully-configured tenant (full-data onboarding path) or (b) completes the setup themselves (bare-bones onboarding path).
- **Doctor (the medical examiner whose practice the tenant represents).** Does NOT log in at MVP unless they want their own Practice Admin account (T10 Q-T10-2). Their profile fields are captured at onboarding for case-record / regulatory purposes; the exact set of fields is queued via Q22 (manager).

## Intended workflow

### Pre-onboarding (out-of-portal)

Before any portal action, the host admin collects the information the onboarding form needs from the doctor. This happens off-portal -- email, intake form, phone call, or whatever Gesco's operational practice is. The host admin's portal-side action only starts once they have the data ready (or have decided to use the bare-bones path; see below). [Source: Adrian best-guess 2026-04-27 -- NEEDS CONFIRMATION; consistent with Q20 manual-onboarding pattern]

### Onboarding paths (host admin's choice)

Two onboarding modes are supported, both initiated from the host admin's "create new tenant" admin screen. Host admin picks the mode that fits the situation. [Source: Adrian-confirmed 2026-04-27, T13 Q1 -- "I want both options ... This gives robustness to everyone"]

#### Full-data path

Host admin enters every field on the form in one go:

- **Tenant name** -- a display name for the practice (e.g., "Dr. Smith's Practice", "Smith Orthopedic Group"). Used in admin surfaces, audit logs, and notification footers.
- **Doctor profile** -- name, email, gender at minimum (per current code); Q22 may add credentials / license number / specialty / etc. once the manager confirms required regulatory fields.
- **Initial Locations** -- one or more office addresses (street / city / state / zip / parking fee). At least one Location is required for the tenant to accept bookings.
- **Initial AppointmentType coverage** -- multi-select from the host's global AppointmentType list (per T8). At least one AppointmentType is required for the tenant to accept bookings.
- **Initial Practice Admin user** -- name + email. Receives a magic-link invite per Q-T10-4 pattern (sets a password on first login).
- **Supervisor admin assignment** -- a single pick from the supervisor-admin roster.

On save, the tenant is **live** and ready for bookers to register and request appointments. [Source: Adrian-confirmed 2026-04-27, T13 Q2 -- "All-required at onboarding (Recommended)" applies to the full-data path]

#### Bare-bones path

Host admin enters only the minimum required for a tenant to exist:

- **Tenant name.**
- **Doctor profile** -- name + email at minimum (so the magic link can route to the right contact).
- **Initial Practice Admin user** -- name + email.
- **Supervisor admin assignment.**

Host admin does NOT enter Locations or AppointmentType coverage in this mode. The system creates the tenant in a "setup-incomplete" state and sends the magic-link invite to the Practice Admin's email.

When the Practice Admin clicks the magic link and lands logged-in, they see a setup checklist driving them to:

1. Add at least one Location.
2. Pick the tenant's AppointmentType coverage from the host's global list.
3. (Optionally) flesh out the Doctor profile fields beyond the minimum captured at onboarding.

Once the tenant has at least one Location AND at least one AppointmentType coverage AND at least one Practice Admin user (Practice Admin already exists from onboarding), the tenant transitions to **live** and can accept bookings. The transition is implicit -- there is no separate "go live" toggle (T13 Q2 ruled out that pattern). [Source: Adrian-confirmed 2026-04-27, T13 Q1 + Q2 inferred reconciliation -- the bare-bones path requires the practice to add the missing data before the tenant can accept bookings; flagged for review]

### Common: supervisor admin assignment

Whichever path is used, the **supervisor admin is assigned at onboarding** (host admin picks from the supervisor-admin roster). The assignment is editable later by the host admin; supervisor admins can be reassigned, replaced, or removed at any time. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-6]

### Common: initial Practice Admin login

The initial Practice Admin receives the magic-link invite at the email captured on the onboarding form. Click the link, set a password, and they are in. From then on:

- Full-data path: they can immediately operate the practice (review queue, schedule management, etc.).
- Bare-bones path: they see the setup checklist and complete it before bookings can flow.

Additional Practice Admin users (post-onboarding) are added by the host admin OR the assigned supervisor admin (T10 Q-T10-3). The initial Practice Admin themselves cannot add additional Practice Admin users.

### Tenant decommission (Adrian-confirmed via T9)

When a doctor retires, leaves Gesco, or otherwise stops using the portal, the tenant is **archived in place**: marked inactive; data retained read-only; new bookings + new logins blocked; in-flight cases continue off-portal. No export-then-delete; no successor-doctor transfer at MVP. [Source: Adrian-confirmed 2026-04-24, T9 Q-T9-4]

Decommission is host-admin-only. The supervisor admin assigned to the tenant at decommission time may need to be informed but does not take action.

## Business rules and invariants

- **Host admin is the only role that can create a new tenant.** Supervisor admin cannot; Practice Admin cannot. [Source: Adrian-confirmed 2026-04-22 via T4 Q20 + 2026-04-24 via T10 Q-T10-5]
- **One Doctor per tenant.** [Source: Adrian-confirmed 2026-04-22 via T4]
- **Two onboarding paths (full-data + bare-bones).** Mode is the host admin's choice on the onboarding form. [Source: Adrian-confirmed 2026-04-27, T13 Q1]
- **Tenant-live criteria** (both paths converge on these):
  - At least one Location.
  - At least one AppointmentType in the tenant's coverage.
  - At least one Practice Admin user.
  - A supervisor admin is assigned.
  - The Doctor record exists.
  
  When all of these are present, the tenant accepts bookings. When any is missing (only possible in the bare-bones path before completion), the tenant is reachable but cannot complete a booking. [Source: Adrian-confirmed 2026-04-27, T13 Q2 -- "all-required" interpretation extended across both paths via post-login completion]
- **Magic-link invite for the initial Practice Admin.** Same pattern as Patient auto-create (T10 Q-T10-4). One-time link in invite email; clicking lands logged-in; user sets password during the first session. [Source: Adrian-confirmed 2026-04-24 via T10 Q-T10-4 -- pattern reused for Practice Admin onboarding]
- **Supervisor admin assignment is required at onboarding.** Both paths must record an assigned supervisor admin before the tenant exists. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-6]
- **Audit logging.** Every step of onboarding (the form submit, magic-link send, Practice Admin first login, post-login setup completion, supervisor assignment / reassignment, tenant decommission) is audit-logged with the tenant context per T9 Q-T9-5.
- **Tenant unit definition unchanged.** A tenant represents one medical examiner + their office (T0). [Source: Adrian-confirmed 2026-04-22]

## Integration points

- **T0 (`00-BUSINESS-CONTEXT.md`)** -- tenant unit definition; the practice = examiner + office concept onboarding fulfills.
- **T4 Doctors** -- the Doctor profile that gets created at onboarding; one-Doctor-per-tenant rule; Q22 manager-bound for full profile-field set.
- **T8 Locations + AppointmentTypes** -- the Locations seeded at onboarding (full-data) or post-login (bare-bones); the AppointmentType coverage picked at onboarding from the host's global list.
- **T9 Multi-tenancy** -- the tenant unit, audit logging, decommission (archive-in-place); Locations being tenant-specific (the onboarding form is where this manifests).
- **T10 Auth-and-roles** -- the Practice Admin role created at onboarding; the supervisor admin tier assigned at onboarding; magic-link invite pattern for the Practice Admin's first login.
- **T11 Appointment lifecycle + T12 Notifications** -- once the tenant is live, the lifecycle and notification model take over for every booking the tenant receives.
- **`docs/issues/INCOMPLETE-FEATURES.md` FEAT-09** -- Patient `IMultiTenant` gap. Not directly an onboarding concern, but the tenant-isolation guarantee that onboarding establishes depends on FEAT-09 being closed.
- **`src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs`** -- the existing provisioning surface in code. Per `project_tenant_semantics` memory, this is the canonical entry point for tenant + Doctor creation. T13 captures the intent the AppService should fulfill; the bare-bones path likely requires new behaviour beyond what is currently coded.

## Edge cases and error behaviors

- **Practice Admin never clicks the magic-link invite.** Account stays in invite-pending state; tenant stays in setup-incomplete state if the bare-bones path was used. Host admin OR assigned supervisor admin can re-issue the invite. [Source: Adrian best-guess 2026-04-27 -- NEEDS CONFIRMATION; consistent with T10 Q-T10-4 magic-link pattern + edge case for Patient]
- **Onboarding fails partway (validation error, duplicate tenant name, etc.).** No partial state -- the form rejects the submit and the host admin retries. The bare-bones path's reduced field set is a deliberate skip of certain fields, not a partial-error fallback. [Source: Adrian best-guess 2026-04-27 -- NEEDS CONFIRMATION; standard form-validation pattern]
- **Doctor leaves Gesco mid-onboarding.** Host admin abandons the form; no tenant is created. If a tenant has already been saved (bare-bones path, magic link sent but not yet clicked), host admin decommissions it (archive-in-place per T9 Q-T9-4).
- **Supervisor admin in the dropdown gets reassigned out of Gesco entirely.** The host admin re-assigns the tenant to a different supervisor admin. Audit log records the re-assignment.
- **Initial Practice Admin's email bounces.** Bounce handling per T12 (notifications). Host admin sees the bounce indicator on the tenant's onboarding screen and corrects the email.
- **Two host admins try to onboard the same doctor at the same time.** Last-save-wins; the system rejects a duplicate tenant name. [Source: Adrian best-guess 2026-04-27 -- NEEDS CONFIRMATION; standard concurrency handling]
- **Tenant decommissioned then doctor wants to come back.** Host admin onboards a NEW tenant; the old tenant stays archived. (Re-activation / data merge is not in MVP scope.) [Source: derived from T9 Q-T9-4 archive-in-place ruling]

## Success criteria

- Host admin can pick either onboarding path (full-data or bare-bones) on a single admin screen.
- Full-data path: tenant is live and accepting bookings on save.
- Bare-bones path: tenant is created with a setup-incomplete indicator; Practice Admin's magic-link email goes out; first login lands them on a setup checklist; once the checklist is complete, the tenant is implicitly live.
- The supervisor admin assigned at onboarding is recorded; can be reassigned later by host admin.
- Initial Practice Admin's first login uses the magic-link flow; sets their own password; no password ever in plain email.
- Every onboarding action is audit-logged with the tenant context.
- Tenant decommission produces a read-only tenant; data is preserved; new bookings and new logins are blocked.

## Known discrepancies with implementation

- `[observed, not authoritative]` **`DoctorTenantAppService` is the existing provisioning surface in code.** Whether the AppService accepts the full set of onboarding fields (full-data path) or supports the bare-bones path (minimum-required + magic link to Practice Admin) is a code-verification job for Phase 3. The intent here may diverge from what the AppService currently does.
- `[observed, not authoritative]` **Magic-link invite for Practice Admin is not in code.** Same gap as the Patient magic-link invite (T10 Known Discrepancy). Build item.
- `[observed, not authoritative]` **No "setup-incomplete" tenant state in code.** The bare-bones path needs a way for the system to know a tenant exists but cannot accept bookings; current code has no such state.
- `[observed, not authoritative]` **No setup-checklist UI on Practice Admin's first login.** Build item for the bare-bones path.
- `[observed, not authoritative]` **Supervisor admin role itself does not exist in code yet** (T10 Known Discrepancy). The supervisor-admin-assignment-at-onboarding intent depends on T10's Supervisor-Admin build item landing first.
- `[observed, not authoritative]` **No tenant-archive-in-place state in code** (T9 Known Discrepancy). Decommission is currently destructive (full delete) unless build adds the inactive-flag + signup-blocking + login-blocking gates.
- `[observed, not authoritative]` **Audit logging at the onboarding-step grain** is not currently instrumented. Standard ABP audit may capture some events; the tenant-context audit field per T9 Q-T9-5 is a separate build item.

## Outstanding questions

- **Tenant URL / access model at MVP** -- subdomain per tenant (`drsmith.app.gesco.com`), path-prefix (`app.gesco.com/t/drsmith`), single-login-page with tenant picker, or some hybrid. Adrian deferred to the next manager email round (the question needs to be reframed in non-technical business-shape language so the manager can answer it; per the `feedback_business_questions_not_architecture_questions` memory, architecture-flavoured questions get rewritten before going to the manager). T13 holds the technical version here; the email-craft step builds the manager-facing version. [UNKNOWN -- queued for manager via next email round]
- **Pre-onboarding data-collection process** -- whether the host admin uses an intake form, an email exchange, or a phone call to collect the doctor's setup data is operational (not strict intent). Inline-seeded as "out-of-portal, host-admin-driven"; confirm or correct.
- **Onboarding-form bounce handling** -- if Practice Admin's email bounces at magic-link time, what does the host admin see and do? Inline-seeded as "bounce indicator on the tenant's onboarding screen"; consistent with T12 notification bounce-handling but worth confirming.
- **Setup-incomplete tenant visibility** -- can bookers see a setup-incomplete tenant in the tenant-picker / signup flow? Inline-seeded as "yes, visible but cannot complete a booking until the tenant is live"; confirm.
- **Doctor's profile field set at onboarding** -- captured here as name + email + gender (current code minimum); Q22 in the manager queue may expand this. T13 documents the minimum; the full set is determined by Q22's resolution.
- **Re-activation of a decommissioned tenant** -- inline-seeded as "out of MVP" per T9 Q-T9-4 ruling; no re-activation flow exists. If a doctor returns, host admin onboards a new tenant. Confirm.
