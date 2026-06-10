---
feature: change-request-two-party-consent
date: 2026-06-09
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Require the **opposing side's consent** (anonymous tokenized Yes/No email link) on an appointment cancel/reschedule change request before the existing Staff Supervisor approve/reject step runs.

## Context

- **Why now:** the Group D email-template review (2026-06-09) surfaced a product requirement that a reschedule/cancellation can only proceed if *both sides* agree. Today the flow is single-party: an external user submits a change request (`RequestStatus=Pending`) and a **Staff Supervisor** approves/rejects it (`AppointmentChangeRequestsApprovalAppService`). No opposing-party step exists; prior-work search confirmed this is net-new (OLD app was also single-approver).
- **Two "sides":** Side A = Patient + Applicant Attorney (AA); Side B = Defense Attorney (DA) + Claim Examiner (CE). In practice Patient and CE always exist, so each side always has at least one representative.
- **Constraints:** .NET 10 / ABP Commercial 10.0.2, single-tenant Phase 1, Docker-only dev (no `dotnet run` / `ng serve`; rebuild via `docker compose up -d --build`). EF Core code-first migration required. ABP conventions: `[RemoteService(IsEnabled=false)]`, manual controllers, Mapperly, nested permission consts. The new public approve/reject surface is `[AllowAnonymous]` and state-changing -> security-sensitive (token + rate-limit + replay protection + no PHI in logs).
- **Reuse (from research):** token crypto = `Domain/Invitations/InvitationManager.cs` (256-bit random raw token, SHA256 hash stored, 7-day TTL, single-use via concurrency stamp). Anonymous surface = `PublicDocumentUploadController` (`api/public/...`, `[AllowAnonymous]`+`[IgnoreAntiforgeryToken]`) + Angular public route `public/document-upload/:id/:code` (`eLayoutType.empty`, no guard) + `IAccountUrlBuilder.BuildPublicDocumentUploadUrlAsync` (tenant-prefixed SPA URL). Party resolution = `AppointmentRecipientResolver` (AA/DA join tables, CE via `Appointment.ClaimExaminerEmail`, Patient via `IdentityUserId`/`PatientId`, Office via setting). Gate insertion = the two approve methods after `ChangeRequestApprovalValidator.EnsurePending`.

## Approach

**Chosen: consent-first-then-staff, hashed single-use token, GET-landing + POST-action.**

1. On submit, issue a consent token for the **opposing side**; persist `ConsentStatus=Pending` + the submitting side on the change request.
2. Email the opposing side's single representative an actionable Yes/No link (+ all parties get a plain confirmation addressed to the requester, rest CC'd).
3. The link opens a **public GET confirmation landing page** (no state change) -> **POST** records the decision via a hashed, single-use, 7-day token.
4. The Staff Supervisor approve methods are **gated**: they may only finalize when `ConsentStatus=Approved`. On **No** or **Expired** (-> defaults to No), the request is flagged for **staff mediation** (not auto-rejected); staff reject if mediation fails.
5. Consent-gating is **feature-flagged** so it can be toggled off (fallback to today's pure-staff flow).

**Alternatives rejected:**
- *Raw-GUID code (doc-upload style, no hash/expiry):* rejected -- approve/reject is higher-risk than a document upload; use the hashed + expiring InvitationManager pattern.
- *Single `bool OpposingConsentGranted`:* rejected -- cannot distinguish *awaiting* vs *declined* vs *expired* in the supervisor queue. Use a `ConsentStatus` enum (adjustable).
- *State-changing GET link:* rejected -- email scanners/prefetchers auto-follow GET links and would auto-approve. GET landing page + POST action instead. (AWS Builders' Library; Stripe; Zuplo idempotency guidance.)
- *Consent replaces staff approval:* rejected per product decision (consent-first-then-staff).
- *Actionable email to both sides:* **ASSUMPTION to confirm** -- only the **opposing** side gets the Yes/No email (the requester already consented by submitting). If both sides should get an actionable email, T3/T7 change.

## Tasks

- **T1: Domain -- consent state + `ChangeRequestConsentManager`.**
  - approach: tdd
  - files-touched: `Domain.Shared/AppointmentChangeRequests/ConsentStatus.cs` (new enum NotRequired/Pending/Approved/Rejected/Expired), `Domain.Shared/AppointmentChangeRequests/RequestingSide.cs` (new enum SideA/SideB), `Domain/AppointmentChangeRequests/AppointmentChangeRequest.cs` (add `ConsentStatus`, `ConsentTokenHash`, `ConsentExpiresAt`, `ConsentRespondedAt`, `ConsentRespondedByEmail`, `RequestingSide`, persisted `SubmittedByUserId`), `Domain/AppointmentChangeRequests/ChangeRequestConsentManager.cs` (new), `Domain.Shared/AppointmentChangeRequests/AppointmentChangeRequestConsts.cs`.
  - detail: `ChangeRequestConsentManager` mirrors `InvitationManager` -- `IssueConsentAsync` (generate 256-bit raw token, store SHA256 hash, set `ConsentStatus=Pending`, `ConsentExpiresAt = now + 7d`, return raw token once); `RecordDecisionAsync(rawToken, approved)` (hash + lookup, reject if already responded / expired, set `Approved|Rejected` + `ConsentRespondedAt`/`Email`, single-use via concurrency stamp); `DefaultToNoIfExpired` (Expired -> treated as Rejected for gating + flagged for staff).
  - acceptance: unit tests pass for issue, valid record, expired->No, already-responded (double-submit), unknown/forged token.

- **T2: EF Core migration for the new columns.**
  - approach: code
  - files-touched: `EntityFrameworkCore/Migrations/*` (generated), `EntityFrameworkCore/.../CaseEvaluationDbContext` config if needed (max-lengths, index on `ConsentTokenHash`).
  - acceptance: `dotnet ef migrations add` succeeds; DbMigrator applies cleanly in the dev container; `ConsentTokenHash` has a filtered unique index.

- **T3: Side-resolution helper.**
  - approach: tdd
  - files-touched: `Application/AppointmentChangeRequests/ChangeRequestSideResolver.cs` (new) reusing `AppointmentRecipientResolver` party data.
  - detail: given appointment + submitter (`CurrentUser`), return `RequestingSide` and the **opposing side's actionable representative** (Side A rep = AA else Patient; Side B rep = DA else CE). Defensive: if the opposing side somehow has no party, return null -> caller routes to staff.
  - acceptance: unit tests for submitter = Patient/AA -> opposing rep DA-else-CE; submitter = DA/CE -> opposing rep AA-else-Patient; defensive no-party -> staff route.

- **T4: Gate the supervisor approve methods + wire submit to issue consent.**
  - approach: tdd
  - files-touched: `Application/AppointmentChangeRequests/ChangeRequestApprovalValidator.cs` or new `OpposingConsentValidator.cs`, `AppointmentChangeRequestsAppService.Approval.cs` (`ApproveCancellationAsync`/`ApproveRescheduleAsync` gate after `EnsurePending`), `AppointmentChangeRequestManager.cs` + `AppointmentChangeRequestsAppService.cs` (on submit: resolve side, call `IssueConsentAsync`, persist `RequestingSide`/`SubmittedByUserId`), feature-flag check.
  - detail: gate throws `BusinessException(ConsentNotGranted)` unless `ConsentStatus=Approved` (or feature flag off). `Rejected`/`Expired` -> the request is **flagged for mediation** (surfaced in T8), staff may then reject.
  - acceptance: unit/integration tests -- approve blocked while Pending; allowed when Approved; No/Expired routes to mediation, not auto-reject.

- **T5: Public consent endpoint (GET info + POST decision) + URL builder + rate-limit + audit.**
  - approach: test-after
  - files-touched: `Application.Contracts/.../IPublicChangeRequestConsentAppService.cs` + DTOs, `Application/.../PublicChangeRequestConsentAppService.cs` (`GetConsentInfoAsync(token)` returns conf#, reschedule date/time, reason for the landing page; `SubmitDecisionAsync(token, approved)` records via `ChangeRequestConsentManager`), `HttpApi/Controllers/.../PublicChangeRequestConsentController.cs` (`api/public/change-request-consent/...`, `[IgnoreAntiforgeryToken]`, `[AllowAnonymous]`), `Application/Notifications/AccountUrlBuilder.cs` (+`IAccountUrlBuilder`) add `BuildChangeRequestConsentUrlAsync`, `HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` (rate-limit partition for this route).
  - detail: GET is read-only (safe for prefetch); POST mutates. Audit-log decision (no PHI). Replay/double-click -> idempotent "already responded" result.
  - acceptance: GET returns info for a valid token; POST records once; replay returns "already responded"; expired token surfaces "expired (defaulted to No)".

- **T6: Angular public landing route.**
  - approach: test-after
  - files-touched: `angular/src/app/public/change-request-consent/*` (standalone component + route `public/change-request-consent/:token`, `eLayoutType.empty`, no auth guard), routing module.
  - detail: shows conf#, requested new date/time (reschedule), reason; Yes/No buttons POST the decision; renders confirmed / already-responded / expired states. Do NOT edit `angular/src/app/proxy/` by hand -- regenerate via `abp generate-proxy` after T5.
  - acceptance: page loads from the emailed link, submits a decision, shows the result state; second visit shows "already responded".

- **T7: Email handlers + templates (D1-D6) + addressing/grammar (D7-D9).**
  - approach: test-after
  - files-touched: `Application/Notifications/Handlers/ChangeRequestSubmittedEmailHandler.cs` (split: confirmation-to-all addressed To requester + rest CC'd; **separate actionable Yes/No email** to the opposing rep with the consent link + reschedule date + reason), `ChangeRequestApprovedEmailHandler.cs`, `ChangeRequestRejectedEmailHandler.cs`, new template(s) for the actionable consent email + `NotificationTemplateConsts.Codes` + `Codes.All` + `EmailSubjects.cs` + `EmailBodies/*.html`, edits to D1-D6 bodies/subjects, language/grammar/addressing-only for D7-D9 (`ClinicalStaffCancellation`, `AppointmentChangeLogs`, `AppointmentRescheduleRequestByAdmin`).
  - detail: confirmation + actionable emails BOTH include the requested new appointment date/time (reschedule) and the cancel/reschedule reason. D7-D9 get no consent buttons.
  - acceptance: submit a reschedule -> confirmation to all (To requester) + Yes/No to opposing rep, both showing new date + reason; outcome emails fire on staff finalize.

- **T8: Supervisor pending-queue consent buckets.**
  - approach: test-after
  - files-touched: `AppointmentChangeRequestsAppService.Approval.cs` (`GetPendingChangeRequestsAsync` + DTO add `ConsentStatus`/derived bucket), Angular pending list component.
  - detail: three buckets -- *Awaiting opposing-side consent* (RequestStatus=Pending + ConsentStatus=Pending), *Ready to finalize* (ConsentStatus=Approved), *Declined/Expired -> mediate* (Rejected/Expired).
  - acceptance: queue labels/filters reflect the three buckets.

- **T9: Tests consolidation + regression.**
  - approach: tdd (logic) / test-after (endpoint+handlers)
  - files-touched: `test/HealthcareSupport.CaseEvaluation.Domain.Tests/...`, `test/HealthcareSupport.CaseEvaluation.Application.Tests/...`.
  - acceptance: `dotnet test` green; consent manager / validator / side-resolver covered; endpoint + handler happy/expiry/replay paths covered.

- **T10: Security review checklist for the anonymous endpoint.**
  - approach: code
  - files-touched: `docs/security/change-request-consent-endpoint-review.md` (new).
  - detail: token entropy (256-bit) + SHA256 storage; single-use via concurrency stamp; 7-day expiry -> default No; per-token/IP rate-limit; GET-safe / POST-mutate; replay/idempotency; no PHI in logs or token; `[AllowAnonymous]`+`[IgnoreAntiforgeryToken]` justification. Run `/security-review` before merge.
  - acceptance: checklist complete; reviewer sign-off recorded.

- **T11: Feature flag + rollout note.**
  - approach: code
  - files-touched: settings/feature definition for `ChangeRequestConsent.Enabled` (default on for the demo tenant), this plan's rollout note.
  - detail: when off, the gate is skipped (today's pure-staff flow). Needs `docker compose up -d --build` to take effect; migration runs via DbMigrator on start.
  - acceptance: toggling the flag off restores the legacy flow without code changes.

## Risk / Rollback

- **Blast radius:** medium-high -- touches the change-request submit + approval flow (recently UI-fixed 2026-06-08), 3 email handlers + templates, adds a public `[AllowAnonymous]` endpoint, an Angular public route, and a schema migration. Mitigated by the feature flag (T11) and consent-gating being additive.
- **Security:** the anonymous state-changing endpoint is the primary risk surface; T10 + `/security-review` gate the merge.
- **Rollback:** feature-flag off restores legacy behavior immediately. The migration is additive (new nullable columns) -> forward-only; no destructive change, safe to leave in place if the flag is off. Revert the code via PR revert; the columns remain unused.

## Verification

After all tasks + `docker compose up -d --build`:
1. As an external Side-A user (patient/AA), submit a **reschedule** on an Approved appointment with a new slot + reason.
2. Confirm: all parties receive the confirmation email (To = requester, others CC'd) showing the new date/time + reason; the **opposing** rep (DA else CE) receives a separate Yes/No email with the consent link, also showing new date/time + reason.
3. Open the link -> public landing page shows conf# + new date/time + reason; click **Yes** -> "recorded" page; re-click -> "already responded".
4. As Staff Supervisor: the request now shows **Ready to finalize**; approve -> reschedule applies + outcome emails fire. Confirm approve was **blocked** before consent (Awaiting bucket).
5. Repeat, click **No** -> request shows **Declined -> mediate**; supervisor cannot auto-finalize; staff reject path works.
6. Let a token expire (or simulate) -> defaults to **No**, staff notified "token expired".
7. Toggle the feature flag **off** -> submit proceeds straight to the staff queue (legacy flow), no consent email.
8. `dotnet test` green; `/security-review` clean on the diff.
