[Home](../INDEX.md) > [Issues](./) > Technical Open Questions

# Technical Open Questions

These questions emerged from a comprehensive codebase audit and E2E testing (258 automated tests + 11 exploratory scenarios run on 2026-04-02). **Every question below represents something we searched the entire codebase for and could not find an answer to.** No assumptions have been made -- these are genuine unknowns that block further development.

> **Note**: These questions are code-level ambiguities that can, in principle, be resolved by examining the codebase, making a judgement call, or researching California workers' compensation regulations. They do not require access to the previous developer. For questions that represent genuinely non-recoverable knowledge, see [Questions for Previous Developer](QUESTIONS-FOR-PREVIOUS-DEVELOPER.md).

---

## How to Use This Document

Each question includes:
- **Why we need to know**: The development decision that's blocked
- **What we found**: Evidence from code search showing this isn't documented
- **What happens if unanswered**: The default assumption we'd make (which may be wrong)

Priority: Questions are ordered by impact on development. The first 5 block active feature work.

---

## Q1: What is the intended appointment status workflow?

**Research**: see [research/Q-01.md](research/Q-01.md) for state-machine library options (Stateless vs ABP Elsa), audit-log strategy, and likely default answers to the 5 sub-questions.

**Why we need to know**: We need to implement FEAT-01 (status transitions). The 13 statuses are defined but there's no state machine, no transition rules, and no way to change status after creation.

**What we found**:
- `AppointmentStatusType` enum defines 13 values (Pending through CancellationRequested)
- `AppointmentUpdateDto` does NOT have an `AppointmentStatus` field -- status is frozen at creation
- `AppointmentManager.UpdateAsync` does NOT accept a status parameter
- Zero comments, TODOs, or documentation describing who can transition to which status
- Angular UI has no status change buttons or dropdowns
- Test B11.1.1 confirms: PUT cannot change status

**Specific sub-questions**:
1. Who can approve an appointment? (Admin only? Doctor? Automatic?)
2. Who triggers check-in / check-out? (Front desk? Patient self-service?)
3. What's the difference between CancellationRequested and CancelledNoBill/CancelledLate?
4. Can Billed appointments ever be re-opened?
5. Is RescheduleRequested a patient-initiated or admin-initiated action?

**What happens if unanswered**: We'd implement a permissive state machine where any role can transition to any status, which is likely wrong for a healthcare system.

---

## Q2: Is the `AppointmentStatus` lookup table intentional or a design mistake?

**Research**: see [research/Q-02.md](research/Q-02.md) for enum-vs-lookup tradeoffs in ABP, ABP's `Enum:` localization convention, and keep-vs-delete recommendation.

**Why we need to know**: There are TWO disconnected representations of appointment status, and we need to know which one is the source of truth before building the workflow.

**What we found**:
- Representation 1: `AppointmentStatusType` enum (hardcoded 1-13) used by `Appointment.AppointmentStatus` property
- Representation 2: `AppointmentStatus` entity with a `Name` field, managed via `AppointmentStatusesAppService` CRUD
- The enum and the table are NOT linked -- the table IDs (GUIDs) have no relationship to the enum integers
- `Appointment.cs` line 40 uses the enum integer, not a FK to the table
- The lookup table has full CRUD (create, update, delete) which would break the enum if used

**What happens if unanswered**: We'd remove the lookup table and keep only the enum, which may delete something the previous developer intended to use.

---

## Q3: Should confirmation numbers be globally unique or unique per tenant?

**Research**: see [research/Q-03.md](research/Q-03.md) for filtered-unique-index pattern, concurrency strategy (unique-index + retry loop), and A99999 overflow mitigation.

**Why we need to know**: The current implementation queries globally but the system is multi-tenant. Tenants currently get independent A00001 sequences (confirmed in testing), but the code has no explicit tenant filter in the query.

**What we found**:
- `GenerateNextRequestConfirmationNumberAsync` in `AppointmentsAppService.cs` (lines 254-282) queries `_appointmentRepository.GetQueryableAsync()` with NO tenant filter
- ABP's automatic tenant filter means this actually queries per-tenant (since appointments are `IMultiTenant`)
- Result: T1 gets A00001, T2 also gets A00001 -- numbers are NOT globally unique
- No unique database constraint exists on `RequestConfirmationNumber`
- No comment explains whether this is intentional

**What happens if unanswered**: We'd add a unique constraint per tenant (which matches current behavior) but this could conflict with cross-tenant reporting requirements.

---

## Q4: What is the Claim Examiner role supposed to do?

**Research**: see [research/Q-04.md](research/Q-04.md) for CA workers'-comp claim-examiner definition, likely answers to the 4 sub-questions, and keep-flag-deferred recommendation.

**Why we need to know**: We registered it as a role, it appears in the signup flow, but it has zero functionality. We need to know whether to build it out or remove it.

**What we found**:
- `ExternalUserType.ClaimExaminer = 2` exists in the enum
- Role "Claim Examiner" is seeded in `ExternalUserRoleDataSeedContributor`
- Users CAN register as Claim Examiner via external signup
- But: `GetExternalUserLookupAsync` explicitly FILTERS OUT Claim Examiner from results (line 64-69 of `ExternalSignupAppService.cs`)
- No CE-specific permissions, no CE-specific UI, no CE-specific endpoints
- Test B11.6.1 confirms: CE role exists but does nothing

**Specific sub-questions**:
1. Is Claim Examiner a future role that was stubbed out?
2. Should CE users see a subset of appointments (e.g., their assigned cases)?
3. Why is CE excluded from the external user lookup?
4. Does CE need its own dashboard?

**What happens if unanswered**: We'd treat CE as a viewer role with the same permissions as Patient, which may undermine insurance workflow needs.

---

## Q5: What are the `InternalUserComments` and `IsPatientAlreadyExist` fields on Appointment?

**Research**: see [research/Q-05.md](research/Q-05.md) for official docs, community findings, gotchas, and recommended approach.

**Why we need to know**: These fields exist in the database and entity but are never set, never updated, and never exposed in any update DTO. They take up space and create confusion.

**What we found**:
- `Appointment.InternalUserComments` (string, max 250): Exists in entity (line 36), mapped in DbContext, exposed in read DTO, but NOT in `AppointmentUpdateDto`. Can never be written via API. Always null.
- `Appointment.IsPatientAlreadyExist` (bool): Exists in entity (line 28), mapped in DbContext, exposed in read DTO, but NOT in `AppointmentUpdateDto`. Always false. No code ever sets this.
- Zero comments, TODOs, or documentation explaining either field
- Test B11.4.1 confirms: InternalUserComments is always null

**What happens if unanswered**: We'd add these fields to `AppointmentUpdateDto` so they can actually be used, but we don't know what business rule they serve.

---

## Q6: Was there supposed to be a minimum advance booking window (e.g., 3 days)?

**Research**: see [research/Q-06.md](research/Q-06.md) for 8 CCR § 31.3 / § 35 analysis, 7-day minimum + 20-day soft-warning recommendation, and ABP Setting design.

**Why we need to know**: Our testing showed that appointments can be created for past dates (BUG-09). We need to know whether any time-based restriction was intended.

**What we found**:
- Zero code enforcing any advance booking window
- No "3 day", "advance", or "booking window" references anywhere in the codebase
- `AppointmentsAppService.CreateAsync` validates slot availability, location match, type match, date match, and time range -- but NOT whether the date is in the past or too close to today
- Past-date appointments are accepted (confirmed in exploratory test E1)
- The Angular availability generation UI has no documented minimum date

**What happens if unanswered**: We'd add a past-date validation (reject appointments before today) but wouldn't add an advance window without business requirements.

---

## Q7: Why is `DoctorConsts.EmailMaxLength = 49`?

**Research**: see [research/Q-07.md](research/Q-07.md) for RFC 5321/5322 analysis, ASP.NET Core Identity comparison, and migration guidance.

**Why we need to know**: This is an unusual number (not a power of 2, not a standard like 50 or 100). If it's intentional (e.g., matching a legacy system constraint), we need to preserve it. If it's a typo, we should fix it.

**What we found**:
- `DoctorConsts.cs` line 14: `public const int EmailMaxLength = 49`
- No comment explaining the value
- All other email fields in the system use 50 or higher
- Standard email max is 254 (RFC 5321) or 256 (RFC 5322)
- Patient email has no explicit max length constraint
- The value was present in the initial scaffold and never modified

**What happens if unanswered**: We'd change it to 50 (matching other entities), which could break if there's a downstream system with a 49-char limit.

---

## Q8: What is the intended deployment target?

**Research**: see [research/Q-08.md](research/Q-08.md) for Azure vs AWS comparison, ACA vs App Service tradeoff, HIPAA-eligible service shortlist, and Key Vault + Managed Identity pattern.

**Why we need to know**: We need to set up CI/CD, secrets management, and infrastructure. The codebase has zero deployment configuration beyond Docker Compose (which uses hardcoded passwords).

**What we found**:
- `appsettings.json` contains only localhost URLs and LocalDB connection strings
- No Azure, AWS, or GCP SDK packages
- No Terraform, Pulumi, or ARM templates
- Docker Compose exists with SA password via `${SA_PASSWORD}` env var
- Helm charts exist but are basic scaffolds
- No environment-specific configuration files (no `appsettings.Production.json`)
- No CI/CD pipeline definition (no GitHub Actions, Azure DevOps, Jenkins)

**What happens if unanswered**: We'd target Azure (most common for .NET/ABP) and set up Azure Key Vault for secrets.

---

## Q9: What social OAuth providers were configured?

**Research**: see [research/Q-09.md](research/Q-09.md) for HIPAA implications, BAA availability per provider, and removal-vs-keep recommendation.

**Why we need to know**: The AuthServer supports dynamic external login providers (Google, Microsoft, Twitter) but the configuration is in `appsettings.secrets.json` which is not in the repository.

**What we found**:
- `CaseEvaluationAuthServerModule.cs` registers dynamic external providers
- `appsettings.json` has no OAuth section
- `appsettings.secrets.json` is not in version control (but IS referenced in the code)
- No Google, Microsoft, or Twitter client IDs anywhere in the codebase
- Angular has ABP social login support wired up but no actual providers configured

**What happens if unanswered**: We'd skip social login entirely and use only password-based authentication.

---

## Q10: Is the File Management (blob storage) module actually used?

**Research**: see [research/Q-10.md](research/Q-10.md) for ABP module-removal checklist, migration strategy, and keep-vs-remove decision criteria.

**Why we need to know**: `Volo.FileManagement` is installed and wired into multiple module dependencies, but we found zero usage. Removing it would simplify the dependency graph.

**What we found**:
- `FileManagementApplicationModule` included in 4+ module `[DependsOn]` chains
- Database tables registered in EF Core migrations
- Zero controllers, zero service calls, zero Angular UI referencing file upload
- No blob container configuration in appsettings
- Test B11.5.1 probed for file upload on appointments -- returned 400 (no endpoint)

**What happens if unanswered**: We'd leave it installed but dormant, adding unnecessary package bloat and migration tables.

---

## Q11: Who owns the ABP Commercial license?

**Research**: see [research/Q-11.md](research/Q-11.md) for ABP org-ownership audit procedure, live-key-rotation plan (the key in `NuGet.Config` is committed), and licence-transfer process via `license@abp.io`.

**Why we need to know**: The NuGet API key in `NuGet.Config` and the npm tokens authenticate against Volo's commercial package feeds. If the license is under the previous developer's personal account, it will expire or be revoked.

**What we found**:
- NuGet API key: now templated in `NuGet.Config.template` (uses `${ABP_NUGET_API_KEY}`)
- `abp login-info` shows a logged-in user (but we can't determine whose account)
- No license file in the repository
- No documentation about license ownership or transfer
- Angular `package.json` references `@volo/abp.ng.*` packages which require commercial access

**What happens if unanswered**: If the license expires, `dotnet restore` and `npm install` will fail, blocking all development.

---

## Q12: Is the default password for all users intentional for production?

**Research**: see [research/Q-12.md](research/Q-12.md) for NIST 800-63B Rev 4 guidance, short-term (random + force-change) and long-term (invite-token) fixes, and ABP Identity integration points.

**Why we need to know**: `GetOrCreatePatientForAppointmentBookingAsync` in `PatientsAppService.cs` creates new identity users with a hardcoded default password. In production, every patient would have the same guessable password.

**What we found**:
- `PatientsAppService.cs` line 122: Hardcoded default password for new patient accounts
- No force-password-change mechanism on first login
- No email verification flow
- No temporary password notification
- Combined with SEC-05 (relaxed password policy), this means all auto-created patients share one password

**What happens if unanswered**: We'd implement email-based password setup for new patients, but that requires the email system (FEAT-05) to be working first.

---

## Summary

| # | Question | Blocks |
|---|----------|--------|
| Q1 | Status workflow rules | FEAT-01 implementation |
| Q2 | Lookup table vs enum | Database cleanup |
| Q3 | Confirmation number scope | DAT-07 unique constraint |
| Q4 | Claim Examiner purpose | FEAT-02 implementation |
| Q5 | Orphaned fields purpose | AppointmentUpdateDto redesign |
| Q6 | Booking window requirement | BUG-09 fix scope |
| Q7 | Email max 49 | DoctorConsts cleanup |
| Q8 | Deployment target | CI/CD setup (FEAT-06) |
| Q9 | Social OAuth config | AuthServer configuration |
| Q10 | File Management usage | Dependency cleanup |
| Q11 | ABP license ownership | Package restore continuity |
| Q12 | Default password intent | SEC-05 / FEAT-05 fix scope |

> **Note**: Most of these questions can be resolved without the previous developer by making a judgment call based on standard workers' compensation IME industry practice, California DWC regulations, or ABP Framework defaults. See [Questions for Previous Developer](QUESTIONS-FOR-PREVIOUS-DEVELOPER.md) for questions that cannot be resolved from any artifact.
