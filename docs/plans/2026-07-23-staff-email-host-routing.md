---
feature: Staff appointment-notification emails link to the host (admin) portal
date: 2026-07-23
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Internal-staff-ONLY notification emails (queue digest, pending digest, the office "new request"
notice, and the clinical-staff cancellation notice) link staff to the host portal `admin.<base>`
instead of `{tenant}.<base>`.

## Context & decisions

Staff work in the host portal (`admin.<base>`), but the staff-facing notification emails build
their "PortalUrl" from the office's `TenantId` (`BuildPortalRootUrlAsync(eventData.TenantId)` ->
`{tenant}.<base>`), so the link drops staff onto the wrong portal. (Confirmed live: the flow is
the operational digests/alerts, not the account emails, which are already host-correct.)

Resolved decisions:
- **Decision: fix the staff-facing LINK to `admin.<base>` (host) and keep the tenant SEND scope**
  (SMTP/branding), because the recipient is a per-tenant value (`NotificationsPolicy.OfficeEmail`)
  rendered with per-tenant `ClinicName`, and host-scope SMTP is not configured (SMTP is per-office).
  This is exactly the fallback Adrian specified ("if not able to send from host, ensure the link
  is `admin.<base>`").
- **Decision: no re-architecture for mixed emails.** `NotificationDispatcher` renders a template
  ONCE per dispatch (all recipients share one `PortalUrl`), so per-recipient links are impossible
  in a single dispatch. `BookingSubmissionEmailHandler` ALREADY dispatches the office notice
  separately from the external-party notice, so we only repoint the office leg's link.
- **Decision (Adrian's scoping rule): include an email ONLY if it goes exclusively to internal
  staff; exclude any email with an external recipient (To OR CC).** A full audit of every
  portal-link-embedding dispatch classified them by discriminator: recipients built from a
  settings `OfficeEmail` / ETO `StaffEmail` (single `OfficeAdmin`) are staff-ONLY -> INCLUDE;
  recipients from `_recipientResolver.ResolveAsync(...)` are appointment PARTIES (external), with
  the office appended as an `OfficeAdmin` CC (`CcRecipientAppender`) -> "external To + staff CC"
  -> EXCLUDE.
  - INCLUDE (staff-only + tenant link): `InternalStaffQueueDigest`, `PendingDailyDigest`,
    `ClinicalStaffCancellation`, and `BookingSubmission`'s separate office notice.
  - EXCLUDE (party-resolver -> external To, office CC): `AppointmentReminder`, `AttyCEPacket`,
    all `ChangeRequest*`, all `Document*`, `IntakeChanged`, `JdfAutoCancelled`, `StatusChange`,
    `Accessor*`, `PatientPacket`. Also `UserQuerySubmitted` (staff recipient but NO portal link ->
    nothing to change).

## All needed context

- Host-link primitive (exists, tested): `IAccountUrlBuilder.BuildPortalRootUrlAsync(null)` ->
  `admin.<base>` via `TenantNaming.ReservedSlug`
  (`src/HealthcareSupport.CaseEvaluation.Application/Notifications/AccountUrlBuilder.cs:128`,
  `:188-195`). `BuildPortalRootUrlAsync(tenantId)` -> `{tenant}.<base>`.
- Dispatch model (render-once): `NotificationDispatcher.DispatchAsync` /
  `DispatchToWithCcAsync` render the template once, then fan out to recipients -- one `PortalUrl`
  per dispatch (`.../Notifications/NotificationDispatcher.cs:92`, `:127`).
- Handler anchors:
  - `.../Notifications/Handlers/InternalStaffQueueDigestEmailHandler.cs:59` -- staff-only digest;
    `PortalUrl` from `eventData.TenantId`.
  - `.../Notifications/Handlers/PendingDailyDigestEmailHandler.cs:84` -- office pending digest;
    `PortalUrl` from `eventData.TenantId`; recipient = per-tenant `OfficeEmail`.
  - `.../Notifications/Handlers/BookingSubmissionEmailHandler.cs:235` -- `portalBaseUrl` from
    `eventData.TenantId`; used ONLY by the separate office notice at `:318-333`
    (`BuildAppointmentRequestedVariables(..., portalBaseUrl, authServerBaseUrl)`, `:320-322`). The
    external notice (`:308 DispatchToWithCcAsync`) uses `authServerBaseUrl`-based `LoginUrl`, NOT
    `portalBaseUrl` -- so repointing the office link does not touch external recipients.
  - `.../Notifications/Handlers/ClinicalStaffCancellationEmailHandler.cs:113` -- staff-only
    (single `OfficeEmail` recipient, `:120-126`); passes `portalUrl: ctx.PortalBaseUrl` (tenant).
    Does NOT inject `IAccountUrlBuilder` today (fields `:38-43`) -- must add the injection to build
    a host URL. Do NOT mutate `ctx.PortalBaseUrl` (shared by external Document* handlers).
- Test harness: DI-resolved handler tests exist under
  `test/HealthcareSupport.CaseEvaluation.Application.Tests/Notifications/`; the dispatcher is
  injectable, so a fake `INotificationDispatcher` can capture the dispatched `variables["PortalUrl"]`.
- Gotcha: the office/tenant name still comes from per-tenant context; only the LINK changes. Do
  NOT change `CurrentTenant.Change(...)` scope (keeps tenant SMTP/branding + recipient resolution).

## Tasks (implementation blueprint)

### Task 1 -- staff queue digest link (tdd)
- **what:** MODIFY `InternalStaffQueueDigestEmailHandler.cs:59` -- change
  `BuildPortalRootUrlAsync(eventData.TenantId)` -> `BuildPortalRootUrlAsync(null)`.
- **pattern:** the null-tenant host call in `AccountUrlBuilder` (`BuildHostPasswordResetUrlAsync`).
- **approach:** tdd -- fake `INotificationDispatcher`, dispatch the ETO, assert the captured
  `variables["PortalUrl"]` is the host (`admin`) URL, not the `{tenant}` URL.
- **acceptance (EARS):** WHEN the staff queue digest is dispatched, THE SYSTEM SHALL set `PortalUrl`
  to the host portal URL (`admin.<base>`).

### Task 2 -- pending daily digest link (tdd)
- **what:** MODIFY `PendingDailyDigestEmailHandler.cs:84` -- `BuildPortalRootUrlAsync(eventData.TenantId)`
  -> `BuildPortalRootUrlAsync(null)`.
- **pattern:** Task 1.
- **approach:** tdd -- same captured-dispatcher assertion.
- **acceptance (EARS):** WHEN the pending daily digest is dispatched, THE SYSTEM SHALL set `PortalUrl`
  to `admin.<base>`.

### Task 3 -- booking office-notice link (tdd)
- **what:** MODIFY `BookingSubmissionEmailHandler.cs` -- add
  `var officePortalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(null);` and pass
  `officePortalUrl` (not `portalBaseUrl`) into `BuildAppointmentRequestedVariables(...)` for the
  office notice at `:320-322`. Leave `portalBaseUrl` / `authServerBaseUrl` and the external
  `DispatchToWithCcAsync` notice unchanged.
- **pattern:** the existing separate office dispatch at `:318-333`.
- **approach:** tdd -- assert the office notice (`AppointmentRequestedOffice`) dispatch carries a
  host `PortalUrl`, and the external notice (`AppointmentRequestedRegistered`) still carries the
  `{tenant}` login link.
- **acceptance (EARS):** WHEN an appointment-requested fan-out runs, THE SYSTEM SHALL give the office
  notice a host (`admin.<base>`) `PortalUrl` AND SHALL keep the external notice's `{tenant}` login link.

### Task 4 -- clinical-staff cancellation notice link (tdd)
- **what:** MODIFY `ClinicalStaffCancellationEmailHandler.cs` -- inject `IAccountUrlBuilder`
  (constructor param + `private readonly` field, mirror `PendingDailyDigestEmailHandler`), then at
  `:113` pass `portalUrl: await _accountUrlBuilder.BuildPortalRootUrlAsync(null)` instead of
  `ctx.PortalBaseUrl`. Leave `ctx.PortalBaseUrl` itself untouched (shared by external handlers).
- **pattern:** `PendingDailyDigestEmailHandler` DI of `IAccountUrlBuilder` + the host-null call.
- **approach:** tdd -- fake dispatcher, assert the cancellation notice's `PortalUrl` is the host URL.
- **acceptance (EARS):** WHEN the clinical-staff cancellation notice is dispatched, THE SYSTEM SHALL
  set `PortalUrl` to `admin.<base>`.

## Validation loop

From the P:-mapped root (or `C:/src/patient-portal/main`, short path):
1. `dotnet build` -- clean.
2. `dotnet test test/HealthcareSupport.CaseEvaluation.Application.Tests --filter "FullyQualifiedName~Notifications"` -- new + existing handler tests green.
3. `dotnet format --verify-no-changes` on the changed files -- format parity.

Done-bar: all three green (host link asserted for the three staff notices; external link unchanged).

## Risk / rollback

- Blast radius: the `PortalUrl` link text in three staff-facing emails. No schema, no send-scope
  change, no external-party link change, no Angular. Emails still SEND via the office SMTP/branding.
- Rollback: revert the branch; nothing persisted (link is computed at dispatch time).
