---
feature: public-document-upload
date: 2026-06-05
status: in-progress
base-branch: feat/replicate-old-app
related-issues: []
---

## Goal

Give a patient a no-login page (reached by a per-document verification-code link) to
upload a required document, and a tenant-aware helper that builds that link -- PR4 of the
document-type master initiative.

## Context

PR4 is independent of PR1-3. This is **parity**: the legacy app had the same flow (per-document
`VerificationCode`, an auth-bypassed `DocumentUploadController`, and an emailed "Click here to
upload" link). On the new stack most of it already exists and must NOT be changed:
- `PublicDocumentUploadController` -- `POST /api/public/appointment-documents/{id}/upload-by-code/{verificationCode}`,
  `[AllowAnonymous]`, delegating to `UploadByVerificationCodeAsync` (validates code-match +
  appointment Approved & not-past-due + PDF/JPG/PNG magic-byte + 10MB; flips Pending -> Uploaded).
- The per-code 5/hr rate-limit (`DocumentUploadByCodePathPrefix`).
- The `PublicDocumentUploadService` proxy (mis-types `IFormFile`, so call RestService with a
  browser `FormData`, like `AppointmentDocumentsComponent.upload()`).
- `VerificationCode` issuance (`AppointmentDocument.CreateQueued` via PackageDocumentQueueHandler).

What's missing: (a) the SPA public page, (b) a tenant-aware link helper. Email wiring stays
deferred (locked decision 6).

## Approach

- **Link helper on `AccountUrlBuilder`** -- the authoritative tenant-aware composer
  (BUG-029). Add `BuildPublicDocumentUploadUrlAsync` mirroring `BuildInviteUrlAsync` (which
  already resolves `PortalBaseUrl` + injects the tenant subdomain via `TenantUrlComposer`).
  In-process only -- no controller/proxy; the future email handler will call it. Test-after
  against the existing `AccountUrlBuilderTests`.
- **Guard-free SPA route** -- declare `public/document-upload/:id/:verificationCode` with NO
  `canMatch`/`canActivate` (first guard-free route; `postLoginRedirectGuard` is `canMatch` on
  `/` only, so it won't fire here). Reuse the proven `RestService` + `FormData` upload pattern.
- **Path params** for the link/route (decision 1) so the helper and the route agree on
  `/public/document-upload/{documentId}/{verificationCode}`.
- Rejected: a new anonymous GET to preview the document name (decision 2 -- out of scope;
  keep the page a simple file picker). Rejected: touching the existing endpoint/rate-limit
  (already correct).

## Tasks

- T1: Tenant-aware public-upload link helper.
  - approach: test-after
  - files-touched:
    - `src/HealthcareSupport.CaseEvaluation.Application/Notifications/IAccountUrlBuilder.cs` (add `Task<string> BuildPublicDocumentUploadUrlAsync(Guid tenantId, Guid documentId, Guid verificationCode)`)
    - `src/HealthcareSupport.CaseEvaluation.Application/Notifications/AccountUrlBuilder.cs` (impl mirroring `BuildInviteUrlAsync`: resolve `PortalBaseUrl` -> `AppendPath("/public/document-upload/{documentId}/{verificationCode}")`)
    - `test/HealthcareSupport.CaseEvaluation.Application.Tests/Notifications/AccountUrlBuilderTests.cs` (test mirroring existing: composes the tenant-subdomain portal URL + the right path; guard on Guid.Empty if the existing methods do)
  - acceptance: unit test passes -- the URL is `{tenant}.{portalhost}/public/document-upload/{documentId}/{verificationCode}`; backend builds clean.

- T2: Angular public upload page + guard-free route.
  - approach: code (UI; live-verified)
  - files-touched:
    - `angular/src/app/app.routes.ts` (add `{ path: 'public/document-upload/:id/:verificationCode', loadComponent: ... }` -- NO canMatch/canActivate)
    - `angular/src/app/public-document-upload/public-document-upload.component.ts` (new standalone; reads `id` + `verificationCode` from `ActivatedRoute`; file picker with client-side 10MB + PDF/JPG/PNG guard mirroring the authenticated form; uploads via `RestService` `FormData` POST to the public endpoint; states: idle / uploading / success / invalid-or-expired code / appointment-not-open / rate-limited(429))
    - `angular/src/app/public-document-upload/public-document-upload.component.html` (minimal standalone page -- no app-shell nav; clear result messaging)
  - acceptance: route reachable logged-out (no redirect to login); a valid code uploads (200) and shows success; a bad code shows a friendly "invalid or expired" message; prettier-clean; Angular build green.

- T3: Docs in the same change.
  - approach: code
  - files-touched:
    - `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/CLAUDE.md` (note the public upload page + the `BuildPublicDocumentUploadUrlAsync` link helper; the endpoint/rate-limit already existed)
    - `docs/plans/2026-06-03-document-type-master.md` (optional: leave as-is per the no-status-marker convention)
  - acceptance: docs reflect the shipped page + helper.

## Risk / Rollback

- Blast radius: one new Angular route/component + one backend helper method. No schema, no
  migration, no change to the existing endpoint or rate-limit.
- Rollback: revert the PR; the unused link helper and the route disappear with it.
- Anonymous write surface is unchanged -- security rests on the existing (proven) code-match,
  appointment-state, file-format/size, and 5/hr rate-limit gates. PR4 adds no new server gate.
- The link helper is unused until the future email handler lands -- plan-sanctioned "ready".

## Verification

1. `dotnet test` -- the new `AccountUrlBuilderTests` case passes (show output).
2. Backend build + Angular build (in-container) green.
3. Live on the Docker dev stack (logged OUT):
   - Find/seed a queued (Pending) AppointmentDocument with a VerificationCode (PackageDocumentQueueHandler
     creates them on appointment submission; seed one synthetic if absent). Record id + code.
   - Visit `http://{tenant}.localhost:4230/public/document-upload/{id}/{code}` with NO session ->
     the page loads (no redirect to login); uploading a small PDF returns success and the doc
     flips to Uploaded (DB check).
   - A wrong/expired code -> friendly "invalid or expired" message (not a raw error).
4. Self-review (code-simplifier + code-reviewer); PR into `feat/replicate-old-app`; STOP.
   Expect the SonarCloud Quality Gate to be the only failing check (accepted exception).
