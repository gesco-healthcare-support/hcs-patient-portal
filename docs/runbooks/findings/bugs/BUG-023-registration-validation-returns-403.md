---
id: BUG-023
title: ConfirmPasswordMismatch + FirmNameRequiredForAttorney return HTTP 403 instead of 400
severity: medium
status: open
found: 2026-05-14 hardening R2.2 + R2.3
flow: registration-validation
component: src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs (AbpExceptionHttpStatusCodeOptions mapping)
---

# BUG-023 - Two registration validation errors return 403 (same pattern as BUG-003)

## Symptom

R2 hardening probed `/api/public/external-signup/register` with bad inputs and expected 400 for client-side validation failures. Observed:

| Input | Expected status | Actual status | Error code |
| --- | --- | --- | --- |
| Duplicate email | 400 (fixed by PR #197) | 400 | `CaseEvaluation:Registration.DuplicateEmail` |
| ConfirmPassword mismatch | 400 | **403** | `CaseEvaluation:Registration.ConfirmPasswordMismatch` |
| AA without FirmName | 400 | **403** | `CaseEvaluation:Registration.FirmNameRequiredForAttorney` |

## Root cause
PR #197 added the duplicate-email error code -> HTTP 400 mapping to `AbpExceptionHttpStatusCodeOptions`. The same mapping was not added for `ConfirmPasswordMismatch` or `FirmNameRequiredForAttorney`, so they fall back to ABP's default for `BusinessException` (HTTP 403).

## Fix
In `CaseEvaluationHttpApiHostModule.cs`, extend the `AbpExceptionHttpStatusCodeOptions` block that already maps `ExternalSignupDuplicateEmail`. Add:

```csharp
options.Map(CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch, HttpStatusCode.BadRequest);
options.Map(CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequiredForAttorney, HttpStatusCode.BadRequest);
```

(Verify the exact const names by grepping `CaseEvaluationDomainErrorCodes.cs` for `ConfirmPasswordMismatch` and `FirmNameRequiredForAttorney`.)

## Functional impact
- Frontend register form may surface 403 errors as auth/permission failures instead of validation errors. Confusing UX.
- HIPAA risk minor: error codes don't leak input data, just status code is wrong.
- Severity: medium (status-mapping completeness, not security or correctness).

## Verified during R2
- Rate limiter (PR #197 + plan extension) confirmed working: after 5 attempts in 1 hour, register endpoint returns **429** with no body.
- Duplicate-email response is HIPAA-safe: generic message, no echo of submitted email.

## Related
- [[BUG-003]] (fixed PR #197) - the original 403-vs-400 for duplicate email. Same pattern, same fix mechanism.
