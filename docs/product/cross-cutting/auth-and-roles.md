[Home](../../INDEX.md) > [Product Intent](../) > [Cross-cutting](./) > Auth and Roles

# Authentication and Roles -- Intended Behavior

**Status:** draft -- Phase 2 T10, cross-cutting cluster
**Last updated:** 2026-04-24
**Primary stakeholders:** Host admin (Gesco), Supervisor admin (Gesco-side mid-tier), Practice admin (within tenant), and the four external user roles (Patient, Applicant Attorney, Defense Attorney, Claim Examiner)

> Cross-cutting intent for authentication and roles in the Patient Portal. Defines the seven-role catalogue (three admin tiers + four external user roles), the authority hierarchy across tenants, the auth methods supported at MVP, the role-creation / assignment flows, and the access-control rules for ad-hoc grants (none at MVP). Resolves Q21 (doctor login + Gesco-side "doctor's manager" role) and Q12 (default password for auto-created patients) via the T10 interview. Every claim source-tagged.

## Purpose

This file is the canonical source for who can sign in to the Patient Portal, what role they hold, what authority that role carries, and how role assignments are managed. It complements T9 (multi-tenancy), which carries the tenant-vs-host scoping rules; T10 specifies the role hierarchy and auth flows that operate inside that scoping. [Source: Adrian-confirmed 2026-04-24 across the T10 interview]

## Personas and goals

The portal has a **seven-role catalogue**: three admin tiers on the operational side, plus four external user roles for the bookers and case parties.

### Three admin tiers (Gesco-side hierarchy)

#### 1. Host admin (Gesco-side, top)

- Top of the hierarchy.
- Full operational authority across ALL tenants. [Source: Adrian-confirmed 2026-04-24 via T9 Q-T9-3]
- Provisions NEW tenants (Q20 confirmed via T4 -- manual onboarding at MVP).
- Assigns supervisor admins to specific tenants. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-6]
- Adds Practice Admin accounts within any tenant. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-3]
- Runs the T7 universal post-submit "proper process" for form-data corrections in any tenant.
- Every action audit-logged with timestamp, user, action, target, and tenant context (T9 Q-T9-5).

#### 2. Supervisor admin (Gesco-side, mid-tier)

- Resolves Q21 part 2: the "doctor's manager at our side" role IS a separate role from host admin. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-clarification]
- Sits between host admin and Practice Admin in the authority hierarchy.
- Has the same authority as host admin BUT scoped to an explicitly assigned portfolio of practices. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-5]
- **Cannot create new tenants** -- tenant provisioning is host-admin-only.
- **Cannot act on practices outside their portfolio** -- their authority does not extend beyond what host admin has explicitly assigned.
- Adds Practice Admin accounts within their portfolio. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-3]
- Runs T7 proper-process post-submit corrections within their portfolio.
- All actions audit-logged with tenant context (per T9 Q-T9-5; same instrumentation as host admin).
- Assigned by host admin; the assignment can be edited at any time. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-6]

#### 3. Practice admin (within tenant; aka "Doctor's Admin" or "Staff Admin")

- One role inside each tenant; held by the practice's admin staff. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-1 -- "one role per practice"]
- Full within-tenant operational authority: review bookings (approve / reject / send-back-for-info per T2), manage availability (T3), maintain Locations (T8), edit form data PRE-submit (T2 / T4), handle cancellations and reschedules.
- Sees and acts on only that tenant's data; strictly tenant-isolated.
- **Cannot add new Practice Admin accounts** -- account creation is host-admin-or-supervisor-admin only. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-3]
- **Cannot do post-submit form-data corrections** -- those go through the T7 proper-process path (Gesco-side only).
- **Doctor login is a Practice Admin account.** Doctors who want their own portal login get an additional Practice Admin account at their own tenant; same authority and same data access as their staff. There is no separate "Doctor" role at MVP. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-2]

### Four external user roles (booker types and case parties)

#### Patient (injured worker)

- Self-service booking + own-profile editing. [Source: T5]
- View own appointments and own profile.
- Auto-created when the booker is not the patient (e.g., applicant attorney books on behalf of the patient); receives a magic-link invite per Q-T10-4.
- Subject to T7 universal post-submit lock rule on form-captured data.

#### Applicant Attorney

- Books appointments on behalf of the injured worker. [Source: T6]
- Saved firm profile (T6); per-attorney record under `ApplicantAttorney` entity.
- One applicant attorney per appointment at MVP (T6 ruling -- no co-counsel).
- View linked appointments + own profile.

#### Defense Attorney

- Symmetric to applicant attorney (T6 ruling -- T6 confirmed defense attorneys behave essentially the same as applicant attorneys, with saved firm profile + same booking flow). The current code's asymmetry between applicant and defense attorneys is an intent gap, not a design choice.
- Books for the employer / insurer side.
- View linked appointments.

#### Claim Examiner (CE)

- Minimally wired at MVP. [Source: T6]
- Books rarely (only for self-represented patients per T2).
- Receives all-parties notifications (per appointments.md notification design).
- Sees appointments where they are a legal party.
- No dedicated dashboard or CE-specific actions at MVP.

## Intended workflow

### Tenant onboarding (host admin manual at MVP)

Host admin manually provisions a new tenant when a medical examiner joins Gesco. The provisioning step (per T4 / T13 forthcoming):

1. Creates the tenant record.
2. Creates the `Doctor` entity (one Doctor per tenant).
3. Seeds the tenant's initial Locations (the examiner's office addresses; doctor's admin maintains thereafter per T8 Q-A3).
4. Creates the initial Practice Admin user account.
5. Assigns a supervisor admin to the tenant (host admin picks from the supervisor admin roster).

[Source: Adrian-confirmed 2026-04-22 via T4 and 2026-04-24 via T8 Q-A3 + T10 Q-T10-6]

### Adding Practice Admin accounts post-onboarding

After a tenant is running, additional Practice Admin accounts can be added by:

- Host admin (acts on any tenant), OR
- The supervisor admin assigned to that tenant.

Practice Admin staff cannot add other Practice Admins themselves. New staff or a doctor wanting their own login require Gesco involvement (host or assigned supervisor). [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-3]

### Adding supervisor admins

New Supervisor Admin accounts are created by host admin only. Each supervisor admin's portfolio (the set of tenants they cover) is host-admin-managed. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-6]

### External user signup

External users (patients, attorneys, claim examiners) self-register via the existing tenant-selection signup flow:

- Tenant selection is required at signup (per current code).
- Email uniqueness is enforced per-tenant (per T9 -- one login per practice).
- Password-based authentication only at MVP. No social login, no OAuth providers from third parties. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-7; resolves research Q9]

### Patient auto-creation when booker is not the patient

When an applicant attorney, defense attorney, or claim examiner books on behalf of a patient (or the practice admin books for a phone-and-email caller), the system auto-creates the Patient's IdentityUser:

- Account is created at booking-time (or at approval-time -- subject to Q23 invite-fire timing, still queued for manager).
- Patient receives a magic-link invite email (one-time login link).
- Clicking the link signs the patient in for that one session.
- Patient sets their own password during that first session.
- No password ever appears in plain email. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-4; resolves research Q12]

### AppointmentAccessor at MVP

Per T6, there are no ad-hoc access grants at MVP. Access to an appointment is implicit from legal-party membership; the existing parties on the case (patient, applicant attorney, defense attorney, claim examiner, employer-when-a-party) see the appointment because they are parties, not because someone explicitly granted them access. Staff who help a party use that party's registered credentials -- no proxy access pattern. [Source: Adrian-confirmed 2026-04-22 via T6]

The `AppointmentAccessor` entity exists in code with View / Edit access types and a UI surface; the intent at MVP is either (a) auto-populate from case-party membership without exposing the grant flow, or (b) leave the entity but hide the admin UI. The exact build approach is a follow-up implementation question; the intent rule is "no ad-hoc grants".

## Business rules and invariants

### Authority hierarchy table

| Role | Tier | Scope | Create tenants | Add Practice Admins | T7 fixes | Audit |
| --- | --- | --- | --- | --- | --- | --- |
| Host admin | Top, Gesco-side | All tenants | Yes | Yes (any tenant) | Yes (any tenant) | Full per Q-T9-5 |
| Supervisor admin | Mid, Gesco-side | Assigned portfolio | No | Yes (within portfolio) | Yes (within portfolio) | Full per Q-T9-5 |
| Practice admin | Within tenant | Own tenant | No | No | No | Standard ABP |
| Patient | External | Own profile + own appts | No | No | No | Standard ABP |
| Applicant Attorney | External | Own profile + linked appts | No | No | No | Standard ABP |
| Defense Attorney | External | Own profile + linked appts | No | No | No | Standard ABP |
| Claim Examiner | External | Linked appts + rare booker | No | No | No | Standard ABP |

### Authority rule for an action on tenant T

The portal authorizes an action on tenant T if any of:

- Actor is host admin.
- Actor is a supervisor admin AND assigned to T.
- Actor is a Practice Admin AND in tenant T (subject to within-practice scope rules).
- Actor is an external user AND has a legal-party link to the specific record (subject to per-action role rules from T2, T5, T6).

[Source: Adrian-confirmed 2026-04-24 across the T10 interview + prior session rulings]

### Authentication

- **Password-based at MVP only.** No social / OAuth / SSO providers wired into the public-facing auth flow at MVP. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-7]
- **Magic link for auto-created Patient accounts.** Booker-on-behalf-of-patient flow uses magic-link invites; patient sets their own password during the first session. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-4]
- **Standard password reset** for all roles via the existing ABP password-reset email flow. [Source: code observation -- no intent change required]
- **2FA / MFA: not specified at MVP.** Given host admin and supervisor admin have cross-tenant authority, 2FA is a serious post-MVP candidate; flagged in Outstanding Questions. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION]

### Universal post-submit lock (T7)

- Form-captured data on a submitted appointment is locked at request-submit.
- Post-submit corrections require the host-admin OR assigned supervisor-admin "proper process" path. Practice Admin staff cannot self-edit form data after submit.

[Source: Adrian-confirmed 2026-04-22 via T7 + 2026-04-24 via T10 Q-T10-5 expanding the authorized actors to include supervisor admin]

### Access control specifics

- **No ad-hoc access grants at MVP** (T6). Access to an appointment is implicit from legal-party membership.
- **Defense attorneys mirror applicant attorneys** at the role / capability level (T6). The code's current asymmetry is an intent gap.
- **One applicant attorney per appointment at MVP** (T6). No co-counsel modelled in code.
- **Doctor portal login is a Practice Admin account**, not a separate role (Q-T10-2). Tenant onboarding can include creating a doctor's own Practice Admin login if requested.

## Integration points

- **T9 Multi-tenancy** -- T9 carries the tenant-vs-host scoping rules and the host-admin-tier specifics; T10 is the canonical source for the full role hierarchy and adds the Supervisor Admin tier (which T9 originally framed as host-admin-only). T9 has been amended (small cross-reference) to point to T10 for the full hierarchy.
- **T11 Appointment lifecycle** (forthcoming) -- consumes role authority for state transitions (e.g., who can approve, reject, send-back-for-info; who can request reschedule / cancellation).
- **T12 Notifications** (forthcoming) -- recipient list per event derives from the per-role / per-party rules captured here.
- **T13 Tenant provisioning** (forthcoming) -- the operational sister of this file; details the host-admin onboarding flow that creates the tenant + initial Practice Admin + supervisor-admin assignment.
- **Per-feature intent docs (T2 - T8)** -- already reference role authority in their Business Rules sections; T10 is the canonical source those docs should cite.
- **Code references:** `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs` seeds the four external roles per tenant. The Supervisor Admin role does NOT exist in current code -- captured in Known Discrepancies. `docs/business-domain/USER-ROLES-AND-ACTORS.md` is OBSERVATION-ONLY per the Phase 1 README classification; it does not include the Supervisor Admin tier (intent gap).

## Edge cases and error behaviors

- **Supervisor admin's portfolio is empty.** Legitimate state -- the supervisor exists but has no assigned tenants yet. They can sign in but have no actions to take.
- **Supervisor admin attempts to act on a tenant outside their portfolio.** Blocked by the authority rule. Audit log records the attempted action and the denial.
- **Practice Admin staff turnover.** Removing or rotating Practice Admin accounts requires Gesco involvement (host admin OR the assigned supervisor admin). The practice cannot self-manage user list. [Source: corollary of Q-T10-3]
- **Patient declines to set password after magic-link invite.** Account remains in invite-pending state; a new magic-link invite can be reissued. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION; consistent with industry-standard magic-link UX]
- **Doctor wants to log in.** Host admin or assigned supervisor admin creates an additional Practice Admin account for them; same authority as the rest of the practice's staff.
- **Same person needs to act at two practices.** Two separate logins (per T9 Q-T9-2). For external users, that means separate self-registrations. For Gesco-side staff (supervisor admin), one supervisor account can have a portfolio that spans multiple tenants -- they don't need separate accounts per tenant.
- **Host admin actions inside a tenant.** Standard tenant-context switch; the audit log records the action with the tenant ID it operated under (T9 Q-T9-5).

## Success criteria

- All seven roles seeded / supported (three admin + four external).
- Permission check correctly authorizes / denies cross-tenant actions per the authority hierarchy table.
- Host admin can create and assign supervisor admins; a supervisor's portfolio is editable.
- Supervisor admin's actions are scoped to portfolio; out-of-portfolio attempts are denied and audit-logged.
- Practice Admin staff cannot add other Practice Admin users (host or supervisor only).
- Doctor login works via a regular Practice Admin account (no separate Doctor role).
- Magic-link Patient invite works end-to-end (booker creates -> Patient receives -> clicks link -> sets password).
- Password-only auth flow on signup and login; no social-login buttons.
- AppointmentAccessor at MVP: no ad-hoc grant UI exposed to bookers / staff; access is implicit from legal-party membership.

## Known discrepancies with implementation

- `[observed, not authoritative]` **Supervisor Admin role does NOT exist in code.** The seeder (`ExternalUserRoleDataSeedContributor`) creates the four external roles + the standard ABP `admin` role. Adding the Supervisor Admin role is a follow-up build item.
- `[observed, not authoritative]` **Portfolio assignment table does not exist.** A new mapping (e.g., `SupervisorAdminTenants` -- supervisor admin user ID + tenant ID + assignment timestamp) is needed to record which tenants each supervisor admin covers.
- `[observed, not authoritative]` **Permission checks do not account for the Supervisor Admin tier.** Authorization handlers need a custom permission provider or middleware that adds the "is this supervisor admin assigned to this tenant" rule to the standard role-based check.
- `[observed, not authoritative]` **Magic-link Patient invite flow does not exist in code.** Current ABP behaviour for auto-created users likely relies on default-password or random-password emailing; the magic-link flow is intent, build item.
- `[observed, not authoritative]` **`AppointmentAccessor` admin UI is in code.** The entity has full View / Edit / List screens. Intent at MVP per T6 is no ad-hoc grants. Either remove the admin UI or auto-populate the table from case-party membership without exposing the grant action; build decision pending.
- `[observed, not authoritative]` **Defense attorney symmetry gap (T6).** Code has only the `ApplicantAttorney` domain entity; defense attorneys at signup get an `IdentityUser` + role but no separate firm-profile entity. Intent (T6) is symmetric treatment with a saved firm profile. Build item.
- `[observed, not authoritative]` **One-applicant-attorney-per-appointment is not enforced** (T6 confirmed intent). Current code allows multiple `AppointmentApplicantAttorney` rows per appointment. Build item to add the constraint.
- `[observed, not authoritative]` **`docs/business-domain/USER-ROLES-AND-ACTORS.md` is now incomplete.** It does not include Supervisor Admin and frames the role catalogue as 4 external + 1 host. The Phase 1 README already classifies that file as OBSERVATION-ONLY (whole), so no update is needed; T10 is the canonical source going forward.
- `[observed, not authoritative]` **Audit-log review UI does not exist.** T9 + T10 require full audit logging for host and supervisor admin actions; the audit produces events but no review surface is built. Build item.

## Outstanding questions

- 2FA / MFA at MVP for admin tiers ([UNKNOWN -- queued for Adrian]). Host and supervisor admins have cross-tenant authority; password-only auth is a security concern. Likely worth confirming as MVP or post-MVP.
- Audit log review UI ([UNKNOWN -- queued for Adrian]). The full-audit instrumentation from T9 + T10 produces audit events; the question is who reads them and how. Probably bundled with general compliance / HIPAA classification (Q6, deferred post-MVP).
- Q23 invite-fire timing (still open in `OUTSTANDING-QUESTIONS.md`). Determines when the magic-link invite is actually sent (on submit, on approval, or both).
- Practice Admin removal flow ([UNKNOWN -- queued for Adrian]). Adding accounts is host / supervisor only; removal / rotation flow is implied to be the same path but not explicitly confirmed.
