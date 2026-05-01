[Home](../../INDEX.md) > [Product Intent](../) > [Cross-cutting](./) > Multi-tenancy

# Multi-tenancy -- Intended Behavior

**Status:** draft -- Phase 2 T9, cross-cutting cluster
**Last updated:** 2026-04-24
**Primary stakeholders:** Host admin (Gesco) + tenant admin (doctor's admin / practice staff) + external users (patient, applicant attorney, defense attorney, claim examiner)

> Cross-cutting intent for multi-tenancy in the Patient Portal. Pulls together the tenant-vs-host scoping decisions made across feature docs T0 - T8, plus the foundational decisions made in this T9 interview (confirmation-number scope, external-user-account model, host-admin authority, tenant decommission, audit logging). Every claim source-tagged.

## Purpose

The Patient Portal is doctor-per-tenant: each medical examiner and their office form one tenant. Multi-tenancy decides what data is shared, what is isolated, who has authority across tenant boundaries, and what happens to data over the tenant's lifetime. This file consolidates those decisions so per-feature docs can reference one canonical source rather than re-establishing the rules in each. [Source: Adrian-confirmed 2026-04-22 via 00-BUSINESS-CONTEXT.md and `project_tenant_semantics` memory]

## Personas and goals

Cross-reference `00-BUSINESS-CONTEXT.md` for full persona definitions.

- **Host admin (Gesco-side).** Full operational authority across every tenant. Provisions new tenants at onboarding (T4); seeds host-managed reference data (States, AppointmentTypes, AppointmentLanguages); runs the T7 universal post-submit "proper process" for form-data corrections; can perform any action any tenant admin can perform, in any tenant, at any time. Every action they take inside a tenant is audit-logged. [Source: Adrian-confirmed 2026-04-24, Q-T9-3 + Q-T9-5]
- **Supervisor admin (Gesco-side, mid-tier; resolves Q21 part 2).** Sits between host admin and Practice Admin in the hierarchy. Has the same authority as host admin BUT scoped to an explicitly assigned portfolio of tenants; cannot create new tenants (host-admin-only); cannot act on tenants outside their portfolio. Adds Practice Admin accounts within their portfolio; runs T7 corrections within their portfolio; all actions audit-logged. The full role catalogue and the host > supervisor > practice admin hierarchy live in T10 (`docs/product/cross-cutting/auth-and-roles.md`). [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-clarification + Q-T10-5 + Q-T10-6]
- **Tenant admin (doctor's admin / practice staff).** Full authority within their own tenant; sees and acts on only that tenant's data. Maintains tenant-specific data (Locations, the practice's own appointments and slots, attorney / employer / patient records). Can edit form data BEFORE submit (T4 / T7); cannot do post-submit corrections (host-side path only). [Source: Adrian-confirmed via T4 + T7]
- **External users (patient, applicant attorney, defense attorney, claim examiner).** Strict per-tenant isolation. One human, multiple practices = multiple separate logins. No cross-tenant identity, no auto-link by email. A patient with cases at Dr. Smith and Dr. Jones has two distinct portal accounts. [Source: Adrian-confirmed 2026-04-24, Q-T9-2]

## Tenant lifecycle

### Creation (onboarding)

Host admin manually creates the tenant when a new medical examiner joins Gesco. The provisioning step creates:

- The tenant record itself.
- The `Doctor` entity in that tenant (one Doctor per tenant -- T4).
- The initial doctor's-admin IdentityUser for the practice.
- The tenant's initial Locations (the examiner's office addresses; doctor's admin maintains the list from then on -- T8).

There is no invite-email or self-service signup for doctors at MVP (Q20 confirmed via T4). [Source: Adrian-confirmed 2026-04-22 via T4; Source: Adrian-confirmed 2026-04-24 via T8 Q-A3 for Locations]

### Active

The tenant operates with its own users, appointments, availability, attorney / patient / employer records. External users register through the per-tenant signup (the tenant-selection step required at signup). Tenant admin manages the practice; host admin steps in for support, T7 corrections, or audit / legal queries. [Source: Adrian-confirmed 2026-04-24]

### Decommission (doctor retires or leaves Gesco)

**Archive in place.** When a doctor retires or otherwise stops using the portal, the tenant is marked inactive. All historical data (appointments, patient records, attorney / employer records, audit log) stays in the portal as read-only. New bookings are blocked; new logins are blocked. Existing in-flight cases continue off-portal (with the case record still readable for legal purposes). [Source: Adrian-confirmed 2026-04-24, Q-T9-4]

The portal does NOT export-then-delete the tenant; it does NOT transfer the tenant to a successor doctor. The simplest data-retention story is preserved.

## Business rules and invariants

### Tenant unit and per-entity scoping

- **Tenant = one medical examiner + their office.** Not a carrier, not a TPA, not a law firm, not an employer. [Source: Adrian-confirmed 2026-04-22]
- **One Doctor per tenant.** [Source: Adrian-confirmed 2026-04-22 via T4]
- **Tenant-scoped (`IMultiTenant`) at intent:** Doctor, Appointment, DoctorAvailability, AppointmentAccessor, AppointmentApplicantAttorney, ApplicantAttorney, AppointmentEmployerDetail (these match current code) PLUS Locations (intent per T8; code currently has Location host-scoped -- tenant-scoping is a follow-up build item). [Source: Adrian-confirmed 2026-04-22 via root CLAUDE.md and T8]
- **Host-managed reference data with per-tenant hide/show on top:** States, AppointmentTypes, AppointmentLanguages. The base list lives at the host scope; only Gesco's host admin edits it. Each tenant can hide entries it does not use, but cannot add its own entries to the global list. The hide/show layer is intent-only at MVP (no per-tenant visibility flag exists in code yet). [Source: Adrian-confirmed 2026-04-24, T8 Q-A + Q-A2]
- **AppointmentStatus entity is dropped at MVP.** The `AppointmentStatusType` enum + `en.json` localization keys cover display labels; the entity has no consumer. Removal is a follow-up build item. [Source: Adrian-confirmed 2026-04-24, T8 Q-M]
- **WCAB Office scope: pending (Q26 manager).** The catalogue has a complete CRUD surface in code but zero consumers; the MVP role is queued for the manager. [Source: Adrian-confirmed 2026-04-24, T8]
- **Patient anomaly.** The `Patient` entity has a `TenantId` column but does NOT implement `IMultiTenant` -- ABP's automatic tenant filter does not apply. Intent at MVP: one Patient record per tenant; the same physical person at two practices has two separate Patient rows. The current code's lack of `IMultiTenant` lets cross-tenant Patient queries leak across practice boundaries; FEAT-09 captures the bug. The intent is the strict isolation, not the leak. [Source: Adrian-confirmed 2026-04-22 via T5; cross-reference FEAT-09]

### Confirmation number scope

- **Globally unique across the portal.** The `A` + 5-digit confirmation number (e.g., `A00042`) names exactly one appointment system-wide. Customer service and any human reading the number do not need to know which practice the appointment belongs to -- the number alone is sufficient identifier. [Source: Adrian-confirmed 2026-04-24, Q-T9-1; resolves Q3 from the original research list]
- **5-digit format gives ~100k capacity.** At Gesco's current operational scale this is non-binding; if the portfolio grows past 100k appointments the format extends without changing the global-uniqueness rule. [Source: industry-standard observation; format-extension is a future build concern, not an MVP blocker]

### External user accounts

- **One login per practice.** A patient, applicant attorney, defense attorney, or claim examiner who needs to interact with two doctors' practices creates two separate portal accounts (one per practice). Email uniqueness is enforced per-tenant. There is no cross-tenant identity, no auto-link by email, no shared password / 2FA. [Source: Adrian-confirmed 2026-04-24, Q-T9-2]
- **Tenant selection is required at signup.** External signup flow lists the available practices; the user picks one and registers in that tenant's user store. Subsequent registrations at other practices are independent. [Source: code observation aligned with Q-T9-2]

### Host admin authority

- **Full operational authority across all tenants.** Host admin can perform any action any tenant admin can perform, in any tenant, at any time. There are no read-only restrictions, no per-action approval gates, and no "break-glass with elevated permission" workflow. The host admin role has unrestricted writes. [Source: Adrian-confirmed 2026-04-24, Q-T9-3]
- **Host admin runs the T7 proper-process path.** Post-submit form-data corrections (patient, attorneys, employer, appointment details) go through host admin only -- tenant admin authority for these stops at request-submit. [Source: Adrian-confirmed 2026-04-24 via T7]
- **Host admin can also seed and edit host-managed reference data** (States, AppointmentTypes, AppointmentLanguages -- the base lists; tenant admins handle the per-tenant hide/show layer). [Source: Adrian-confirmed 2026-04-24 via T8]

### Audit logging

- **Full audit on every host-admin action inside any tenant.** Each action carries timestamp, the host-admin user, the action verb (view / edit / cancel / approve / reject / etc.), the target entity ID, and the tenant the action was performed under. This applies to ALL host-admin actions, not just T7 corrections. [Source: Adrian-confirmed 2026-04-24, Q-T9-5]
- **Tenant-side actions follow standard ABP audit logging.** No additional instrumentation required for actions taken by tenant admins or external users within their own tenant. [Source: inferred from Q-T9-5 framing -- the "full audit" requirement is host-admin-specific]

### Universal post-submit lock (T7)

- Any data captured on a submitted appointment form is locked at submit. Post-submit changes to ANY field on the appointment record (patient, attorneys, employer, location, appointment type, etc.) require the host-admin "proper process" path. Tenant-side admins cannot self-edit form data after submit. [Source: Adrian-confirmed 2026-04-24 via T7]

## Integration points

- **T10 Auth and roles** (forthcoming) -- carries the role catalogue (admin, Patient, ApplicantAttorney, DefenseAttorney, ClaimExaminer) and the permission set for each. The "host admin = full operational authority" rule from this doc translates into a specific role-permission mapping in T10.
- **T11 Appointment lifecycle** (forthcoming) -- consumes the tenant-vs-host split for appointment-related entities and re-scopes the lifecycle to the portal's pre-approval + reschedule / cancel boundary (T8 Q-M context).
- **T12 Notifications** (forthcoming) -- consumes the all-parties-notification-on-event rule plus the audit-log requirement; embeds the (tenant-aware) doctor name and location address in notifications.
- **T13 Tenant provisioning** (forthcoming) -- the operational sister of this doc. Defines the host-admin onboarding flow that creates the tenant + initial Doctor + initial Location list + initial admin user.
- **Per-feature intent docs** (T2 - T8) -- already reference tenant-vs-host scoping decisions in their Business Rules sections. T9 is the canonical source those docs cite.
- **Code reference** -- root `CLAUDE.md` (Multi-tenancy Rules section) carries the developer-facing rules; updated 2026-04-24 to match T8 + T9 intent.

## Edge cases and error behaviors

- **Same human at multiple practices.** Separate IdentityUsers, separate Patient rows, separate appointments per tenant. No cross-tenant join in the portal. The case-tracking product (downstream) is the place where cross-practice case views land. [Source: Adrian-confirmed 2026-04-24 via Q-T9-2 + T5]
- **Tenant-decommissioned mid-case.** Tenant marked inactive; existing case data accessible read-only; in-flight case continues off-portal (typically via the doctor's office staff who arranged the appointment, working with the case parties). New bookings against the tenant are rejected. [Source: Adrian-confirmed 2026-04-24, Q-T9-4]
- **Confirmation number collision.** The global counter prevents collisions by design. If the format extends past 5 digits in the future, existing A##### numbers remain valid -- only new numbers use the extended format. [Source: industry-standard pattern]
- **Host admin acting in a tenant.** A standard tenant-context switch happens in the audit log; the action is recorded with the tenant ID it operated under so support / compliance reviewers can reconstruct what happened. Host admin does not see "tenant boundaries" in their UI; they see all tenants and pick the one they need to operate in. [Source: Adrian-confirmed 2026-04-24, Q-T9-3 + Q-T9-5]
- **External user with same email at two practices.** Email uniqueness is per-tenant; the same email can register at multiple practices and produces two distinct IdentityUsers. The user manages both passwords / 2FA / login flows independently. [Source: code observation aligned with Q-T9-2 intent]

## Success criteria

- A patient logged in at Tenant A sees zero data from Tenant B (no Patient rows, no Appointments, no anything).
- A tenant admin (doctor's admin) at Tenant A sees zero data from Tenant B.
- A host admin can switch into Tenant A and perform every action a tenant admin could; the same host admin can switch into Tenant B and do the same.
- Every host-admin action inside any tenant produces an audit entry with timestamp, user, action, target, and the tenant context.
- Confirmation number A00042 names exactly one appointment system-wide.
- Tenant decommission marks the tenant inactive; data stays read-only; new bookings and new logins are blocked.
- The same physical person registering at two different practices ends up with two separate IdentityUsers and two separate Patient rows; no cross-tenant linkage is created.

## Known discrepancies with implementation

- `[observed, not authoritative]` `Location` is host-scoped in code (no `IMultiTenant`; under `IsHostDatabase()` guard); intent (T8 + this doc) is tenant-specific. Tenant-scoping refactor + data migration is a follow-up build item.
- `[observed, not authoritative]` Per-tenant hide/show flag for States, AppointmentTypes, AppointmentLanguages does not exist in code. Intent requires a `Tenant<Entity>Visibility` table or equivalent filter mechanism per entity.
- `[observed, not authoritative]` `Patient` has a `TenantId` column but does NOT implement `IMultiTenant`. Cross-tenant Patient queries can leak across practice boundaries (FEAT-09). Intent is strict isolation; the `IMultiTenant` gap is the bug.
- `[observed, not authoritative]` Confirmation number generator (`AppointmentManager.GenerateNextRequestConfirmationNumberAsync`) currently runs in tenant context; ABP's automatic tenant filter scopes the query that finds the next number, producing per-tenant counters in practice. Intent is a global counter; the generator needs to be re-anchored at the host scope (or use a non-tenant-filtered query path).
- `[observed, not authoritative]` `AppointmentStatus` entity has full CRUD AppService + Angular UI; intent is to drop. Removal is a follow-up build item.
- `[observed, not authoritative]` Standard ABP audit logging may not capture the "full audit on every host-admin action with tenant context" required by Q-T9-5. New audit instrumentation likely needed: when host admin acts inside a tenant, the audit entry must record the action verb, target, AND the tenant context the action ran under.
- `[observed, not authoritative]` No tenant-decommission flow exists in code: no inactive-tenant flag on the tenant entity, no signup-blocking for archived tenants, no login-blocking, no read-only-mode UI. The "archive in place" intent requires a build item to add these gates.
- `[observed, not authoritative]` `docs/business-domain/DOMAIN-OVERVIEW.md`'s "Global vs Tenant-Scoped" diagram is now out of date: it lists Patients and Locations as global, lists AppointmentTypes / Statuses / Languages as global without the per-tenant hide/show layer, and frames AppointmentStatus as a live lookup. The doc lives in `docs/business-domain/` (out of T9 scope to modify); the canonical scoping is this T9 file going forward, and `docs/business-domain/` should defer to it.

## Outstanding questions

- WCAB Office tenant scope and MVP role: rolled up to `OUTSTANDING-QUESTIONS.md` Q26 (manager).
- Tenant URL / discovery model (subdomain, path-prefix, header-based) for ABP's tenant resolution -- not yet decided at intent level. Treat as a separate UX / infrastructure question outside MVP scope; revisit when the deployment model is settled.
- Audit-log retention period and access mechanism (who can read host-admin audit entries, for how long, with what redaction) -- not pinned down here. Likely lands in T10 Auth-and-Roles or a separate compliance section once the manager confirms HIPAA classification (Q6, deferred post-MVP).
