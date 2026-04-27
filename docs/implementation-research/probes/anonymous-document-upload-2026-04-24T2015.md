# Probe log: anonymous-document-upload

**Timestamp (local):** 2026-04-24T20:15:00
**Purpose:** confirm (a) no anonymous document-upload endpoint exists in NEW (proves the capability is still a gap), (b) the full set of `[AllowAnonymous]` sites in NEW does not include any document-upload surface, and (c) re-ground DB state (zero appointments) so no end-to-end mutating probe is attempted.

## Probe 1 -- Swagger scan for anonymous upload / public document paths

### Command
```
curl -sk https://localhost:44327/swagger/v1/swagger.json -o /tmp/swagger-anon-probe.json
python -c "import json; d=json.load(open('/tmp/swagger-anon-probe.json')); paths=[p for p in d['paths'] if ('public' in p.lower() and 'upload' in p.lower()) or 'upload-documents' in p.lower() or 'anonymous' in p.lower() or 'magic' in p.lower() or 'upload-by-token' in p.lower()]; print('\n'.join(paths) if paths else '<no matches>'); print('Total all paths:', len(d['paths']))"
```

### Response
Status: 200 (swagger.json download). 317 total paths in the document. Zero matches for any of: `public+upload`, `upload-documents`, `anonymous`, `magic`, `upload-by-token`. Existing `api/public/**` paths (from `ExternalSignupController`): `/api/public/external-signup/register`, `/api/public/external-signup/tenant-options`, `/api/public/external-signup/external-user-lookup`. None are upload-shaped.

### Interpretation

Proves no anonymous document-upload endpoint is live on NEW `main` as of 2026-04-24. The gap is true on the running system. The only `api/public/**` cluster is the external-signup trio, which matches the envelope this capability's controller should mirror.

## Probe 2 -- grep for `[AllowAnonymous]` across NEW source

### Analysis
```
pattern: \[AllowAnonymous\]
path: W:/patient-portal/implementation-research/src
glob: *.cs
```

### Response (6 matches)
```
src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:43
src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:169
src/HealthcareSupport.CaseEvaluation.Application/WcabOffices/WcabOfficesAppService.cs:94
src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs:27
src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs:42
src/HealthcareSupport.CaseEvaluation.HttpApi/Domain/WcabOffices/CLAUDE.md:60  (documentation reference, flagged as security concern)
```

### Interpretation

Zero `[AllowAnonymous]` in any AppointmentDocuments, DocumentUpload, AnonymousUpload, or magic-link path. Confirms the capability has no partial implementation; a Phase 2 consumer can design the anonymous-upload surface from scratch without backward-compatibility concerns. The WcabOffices site is flagged in its own CLAUDE.md as a security issue (track-10 did not pick it up; worth a follow-up that is out-of-scope for this brief). The ExternalSignup sites provide the canonical envelope pattern for a new anonymous controller.

## Probe 3 -- grep for token / magic-link / protector primitives

### Analysis
```
pattern: ITimeLimitedDataProtector|IDataProtectionProvider\.CreateProtector|MagicLink|UploadToken|VerificationNonce
path: W:/patient-portal/implementation-research/src
glob: *.cs
```

### Response (0 matches)

### Interpretation

No existing token-issuance or magic-link scaffolding in NEW. The recommended solution introduces this primitive fresh; no conflict with existing code. `IDataProtectionProvider` itself is available via ABP DataProtection + ASP.NET Core (confirmed via module graph; root CLAUDE.md mentions "Data protection via Redis in non-dev"), just not consumed yet.

## Probe 4 -- grep for rate-limiter / anti-abuse scaffolding

### Analysis
```
pattern: AddRateLimiter|UseRateLimiter|TokenBucketLimiter|FixedWindowLimiter|SlidingWindowLimiter|ConcurrencyLimiter
path: W:/patient-portal/implementation-research/src
glob: *.cs
```

### Response (0 matches)

### Interpretation

No rate-limiter wired in NEW. The recommendation adds `UseRateLimiter` + `AddTokenBucketLimiter` in `CaseEvaluationHttpApiHostModule.cs:OnApplicationInitialization` + `ConfigureServices`, keyed partition on token + IP. This is a small additive change (roughly 15 lines of config). No NuGet required; `Microsoft.AspNetCore.RateLimiting` ships with .NET 7+.

## Probe 5 -- authenticated appointments GET (replay from service-status.md; not re-issued)

### Source

`docs/implementation-research/probes/service-status.md:24-30` -- `GET /api/app/appointments` with host-admin Bearer token returned `{"totalCount":0,"items":[]}` HTTP 200 on 2026-04-24 ~12:45 local.

### Interpretation

DB has zero appointments. End-to-end mutating test of the anonymous-upload flow requires a seeded appointment + AppointmentDocumentType row first. Per the protocol's MUST NOT list (no schema mutations, no persistent state), this brief does not execute such a test; the design is validated statically against the live API surface.

## Cleanup

No mutating probes. No state to revert. `/tmp/swagger-anon-probe.json` retained for Phase 3 cross-referencing; not persistent state.
