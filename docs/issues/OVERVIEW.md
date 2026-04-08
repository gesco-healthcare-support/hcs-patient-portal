[Home](../INDEX.md) > [Issues](./) > Overview

# Known Issues & Technical Debt

This section catalogues all known bugs, architectural concerns, security vulnerabilities, incomplete features, and open questions identified during a codebase audit **and confirmed via automated E2E testing on 2026-04-02**. Issues are grouped by category and assigned a severity level to aid prioritisation.

> **Test Evidence**: 258 automated tests across 16 phases + 18 exploratory tests. See [TEST-EVIDENCE.md](TEST-EVIDENCE.md) for full results. 246 tests passed, 0 unexpected failures, 5 known gaps confirmed.

---

## Severity Definitions

| Severity | Meaning |
|---|---|
| **Critical** | Data loss, security breach, or silent data corruption possible in production |
| **High** | Incorrect behaviour visible to users, or significant security risk |
| **Medium** | Missing feature, incorrect logic that has a workaround, or code smell with real consequences |
| **Low** | Polish, consistency, or maintainability concern with no functional impact |

---

## Issue Index

### Security

| ID | Title | Severity | File |
|---|---|---|---|
| [SEC-01](SECURITY.md#sec-01-secrets-committed-to-source-control) | Secrets committed to source control | Critical | `appsettings.json`, `docker-compose.yml` |
| [SEC-02](SECURITY.md#sec-02-pii-logging-enabled-by-default) | PII logging enabled by default | High | `CaseEvaluationHttpApiHostModule.cs` |
| [SEC-03](SECURITY.md#sec-03-external-user-lookup-endpoint-unauthenticated-and-unprotected) | External user lookup endpoint unauthenticated and unprotected | High | `ExternalSignupAppService.cs` |
| [SEC-04](SECURITY.md#sec-04-cors-policy-is-wide-open) | CORS policy is wide open | Medium | `CaseEvaluationHttpApiHostModule.cs` |
| [SEC-05](SECURITY.md#sec-05-password-policy-fully-relaxed) | Password policy fully relaxed -- no complexity requirements | High | `IdentityOptions` configuration |

### Data Integrity

| ID | Title | Severity | File |
|---|---|---|---|
| [DAT-01](DATA-INTEGRITY.md#dat-01-race-condition-on-slot-booking) | Race condition on slot booking -- double booking possible | Critical | `AppointmentsAppService.cs` |
| [DAT-02](DATA-INTEGRITY.md#dat-02-duplicate-confirmation-numbers-possible) | Duplicate confirmation numbers possible | Critical | `AppointmentsAppService.cs` |
| [DAT-03](DATA-INTEGRITY.md#dat-03-reschedule-does-not-release-the-old-slot) | Reschedule does not release the old slot | High | `AppointmentsAppService.cs`, `AppointmentManager.cs` |
| [DAT-04](DATA-INTEGRITY.md#dat-04-non-transactional-tenant-creation) | Non-transactional tenant creation leaves orphaned tenants | High | `DoctorTenantAppService.cs` |
| [DAT-05](DATA-INTEGRITY.md#dat-05-disconnected-status-representations) | `AppointmentStatus` lookup table disconnected from status enum | High | `CaseEvaluationDbContext.cs` |
| [DAT-06](DATA-INTEGRITY.md#dat-06-missing-database-indexes-on-fk-columns) | Missing database indexes on frequently-queried FK columns | Medium | `CaseEvaluationDbContext.cs` |
| [DAT-07](DATA-INTEGRITY.md#dat-07-missing-unique-constraints) | Missing unique constraints on `RequestConfirmationNumber` and `Patient.Email` | Medium | `CaseEvaluationDbContext.cs` |

### Confirmed Bugs

| ID | Title | Severity | File |
|---|---|---|---|
| [BUG-01](BUGS.md#bug-01-slot-conflict-detection-logic-is-inverted) | Slot conflict detection logic is inverted | High | `DoctorAvailabilitiesAppService.cs` |
| [BUG-02](BUGS.md#bug-02-appointment-status-changes-are-never-persisted) | Appointment status changes are never persisted | High | `AppointmentUpdateDto.cs`, `AppointmentViewComponent` |
| [BUG-03](BUGS.md#bug-03-getdoctoravailabilitylookupasync-filter-condition-is-always-false) | `GetDoctorAvailabilityLookupAsync` filter is always false | Medium | `AppointmentsAppService.cs` |
| [BUG-04](BUGS.md#bug-04-slot-preview-uses-only-the-first-inputs-location-label) | Slot preview uses only the first input's location label | Medium | `DoctorAvailabilitiesAppService.cs` |
| [BUG-05](BUGS.md#bug-05-slot-save-fires-n1-individual-http-posts) | Slot save fires N+1 individual HTTP POSTs | Medium | `doctor-availability-generate.component.ts` |
| [BUG-06](BUGS.md#bug-06-goback-always-navigates-to-root) | `goBack()` always navigates to root regardless of origin | Low | `appointment-view.component.ts`, `appointment-add.component.ts` |
| [BUG-07](BUGS.md#bug-07-onerror-in-save-is-silently-swallowed) | `onSubmit()` error in `save()` is silently swallowed | Low | `appointment-add.component.ts` |
| [BUG-08](BUGS.md#bug-08-quick-startmd-instructs-ng-serve-which-silently-breaks-the-app) | `QUICK-START.md` instructs `ng serve` which silently breaks the app | High | `docs/QUICK-START.md` |
| [BUG-09](BUGS.md#bug-09-past-date-appointments-accepted-without-validation) | Past-date appointments accepted without validation | Medium | `AppointmentsAppService.cs` |
| [BUG-10](BUGS.md#bug-10-fromtime--totime-accepted-on-slot-creation) | `fromTime > toTime` accepted on slot creation | Medium | `DoctorAvailabilitiesAppService.cs` |

### Incomplete Features

| ID | Title | Severity | File |
|---|---|---|---|
| [FEAT-01](INCOMPLETE-FEATURES.md#feat-01-appointment-status-workflow-has-no-implementation) | Appointment status workflow has no implementation | High | `AppointmentStatusType.cs` |
| [FEAT-02](INCOMPLETE-FEATURES.md#feat-02-claim-examiner-has-no-ui-or-workflow) | Claim Examiner role has no UI or workflow | High | `ExternalUserType.cs` |
| [FEAT-03](INCOMPLETE-FEATURES.md#feat-03-tenant-dashboard-is-a-placeholder) | Tenant dashboard is a placeholder | Medium | `tenant-dashboard.component.ts` |
| [FEAT-04](INCOMPLETE-FEATURES.md#feat-04-appointmentemployerdetail-and-appointmentaccessor-have-no-angular-modules) | `AppointmentEmployerDetail` and `AppointmentAccessor` have no Angular modules | Medium | `angular/src/app/` |
| [FEAT-05](INCOMPLETE-FEATURES.md#feat-05-email-system-is-not-wired-up) | Email system is not wired up | Medium | `CaseEvaluationDomainModule.cs` |
| [FEAT-06](INCOMPLETE-FEATURES.md#feat-06-no-cicd-pipeline) | No CI/CD pipeline | Medium | `etc/` |
| [FEAT-07](INCOMPLETE-FEATURES.md#feat-07-near-zero-test-coverage) | Near-zero test coverage | Medium | `test/` |

### Architecture & Code Quality

| ID | Title | Severity | File |
|---|---|---|---|
| [ARC-01](ARCHITECTURE.md#arc-01-vestigial-books-entity-from-abp-scaffold) | Vestigial `Books` entity from ABP scaffold | Medium | Multiple |
| [ARC-02](ARCHITECTURE.md#arc-02-business-logic-in-the-application-service-layer) | Business logic in the Application Service layer, not Domain | Medium | `AppointmentsAppService.cs` |
| [ARC-03](ARCHITECTURE.md#arc-03-hardcoded-placeholder-values-for-gender-and-date-of-birth) | Hardcoded placeholder values for Gender and Date of Birth | High | `ExternalSignupAppService.cs`, `DoctorTenantAppService.cs` |
| [ARC-04](ARCHITECTURE.md#arc-04-role-name-strings-duplicated-with-no-shared-constant) | Role name strings duplicated across 8+ files with no shared constant | Medium | Multiple |
| [ARC-05](ARCHITECTURE.md#arc-05-appointmentaddcomponent-is-eagerly-loaded) | `AppointmentAddComponent` is eagerly loaded -- breaks lazy loading | Low | `app.routes.ts` |
| [ARC-06](ARCHITECTURE.md#arc-06-dto-validation-attributes-missing) | DTO validation attributes missing on availability input DTOs | Low | `DoctorAvailabilityGenerateInputDto.cs` |
| [ARC-07](ARCHITECTURE.md#arc-07-hardcoded-english-strings-in-user-visible-messages) | Hardcoded English strings in user-visible messages bypass localisation | Low | `appointment-add.component.ts`, `DoctorAvailabilitiesAppService.cs` |

---

## Open Questions

Questions are split into two tiers based on recoverability:

### Tier 1 — Irreversible (requires the previous developer)

These 10 questions represent knowledge that exists only in the previous developer's memory or private communications and cannot be reconstructed from any artifact. **Full details: [Questions for Previous Developer](QUESTIONS-FOR-PREVIOUS-DEVELOPER.md).**

| # | Question | Risk if Lost |
|---|---|---|
| P1 | Is there an active contract or Statement of Work? | Legal: breach of contract, IP ownership dispute |
| P2 | Was real patient data ever loaded into any environment? | Legal: HIPAA breach notification obligations |
| P3 | Were HIPAA compliance decisions made with legal counsel? | Legal: civil/criminal exposure |
| P4 | Who is the actual end client and their contact information? | Operational: no stakeholder to align with |
| P5 | Are there third-party service accounts needing ownership transfer? | Operational: services expire or fail silently |
| P6 | Were any features or behaviours verbally promised to the client? | Product: building the wrong thing |
| P7 | Why was the project handed over, and on what terms? | Context: misreading what is done vs abandoned |
| P8 | Were real end users involved in design or testing? | Product: undoing deliberate design decisions |
| P9 | Were external system integrations (DWC, carriers) discussed? | Product: self-contained vs connected architecture |
| P10 | Are there known security vulnerabilities or prior incidents? | Legal + Security: inheriting a compromised system |

### Tier 2 — Technical (resolvable from codebase or industry research)

These 12 questions are code-level ambiguities. Most can be resolved by examining the codebase, making a judgment call, or researching California workers' compensation standards. **Full details: [Technical Open Questions](TECHNICAL-OPEN-QUESTIONS.md).**

| # | Question | Why It Matters |
|---|---|---|
| Q1 | What is the intended appointment status workflow? | Blocks FEAT-01 (state machine). Tested: status frozen at creation (B11.1.1). |
| Q2 | Is the `AppointmentStatus` lookup table intentional? | DAT-05 -- enum and table are disconnected. |
| Q3 | Should confirmation numbers be globally unique or per-tenant? | Currently per-tenant (ABP filter). No unique constraint. |
| Q4 | What is the Claim Examiner role supposed to do? | FEAT-02 -- role exists, zero functionality. Excluded from user lookup. |
| Q5 | What are `InternalUserComments` and `IsPatientAlreadyExist` for? | Fields exist but can never be set via API. Always null/false. |
| Q6 | Was there a minimum advance booking window (e.g., 3 days)? | Past-date appointments accepted (BUG-09). No code enforces any window. |
| Q7 | Why is `DoctorConsts.EmailMaxLength = 49`? | Unusual number. Typo or legacy constraint? |
| Q8 | What is the intended deployment target? | No CI/CD, no cloud config, no environment files. |
| Q9 | What social OAuth providers were configured? | `appsettings.secrets.json` not in repo. |
| Q10 | Is File Management module actually used? | Wired up in 4+ modules, zero usage found. |
| Q11 | Who owns the ABP Commercial license? | NuGet key in repo. If it expires, builds break. |
| Q12 | Is the default password for all patients intentional? | Every auto-created patient shares one password. No force-change. |

---

## Related Documentation

- [Test Evidence](TEST-EVIDENCE.md) -- **E2E test results from 2026-04-02 (258 tests + 18 exploratory)**
- [Security Issues](SECURITY.md) -- Secrets, PII logging, authorization gaps
- [Data Integrity Issues](DATA-INTEGRITY.md) -- Race conditions, missing indexes, orphaned records
- [Confirmed Bugs](BUGS.md) -- Logic errors and broken behaviour
- [Incomplete Features](INCOMPLETE-FEATURES.md) -- Missing workflows, placeholder UI, no CI/CD
- [Architecture & Code Quality](ARCHITECTURE.md) -- Dead code, misplaced logic, structural concerns
- [Appointment Lifecycle](../business-domain/APPOINTMENT-LIFECYCLE.md) -- Current intended status model
- [Testing Strategy](../devops/TESTING-STRATEGY.md) -- Existing test infrastructure
