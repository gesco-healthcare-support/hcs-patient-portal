# Implementation plan: replicate OLD Patient Portal Registration + Login + Appointment Lifecycle on the NEW ABP/Angular stack

## Your role

You are creating a detailed, comprehensive **implementation plan** (not implementation) that takes the existing parity audit set in `W:\patient-portal\replicate-old-app\docs\parity\` and turns it into an executable plan that, when followed, will make the NEW application
replicate the OLD application's Registration, Login, and Appointment Lifecycle workflow exactly.

You have NO prior context from previous sessions; you must read the audit docs and verify everything yourself.

## The goal in one sentence

Produce `W:\patient-portal\replicate-old-app\docs\plans\2026-05-01-old-app-parity-implementation.md` -- a single master plan that addresses every gap in every parity audit doc, plus any additional gaps you detect, organized so the implementer can execute sequentially without
having to re-derive context.

## Strict-parity directive (NON-NEGOTIABLE)

The NEW application must replicate the OLD application's logic, UI, entity names, field names, role names, status values, validation messages, business rules, notification triggers, and visible behavior verbatim.

The ONLY allowed deviations are framework/library swaps -- because the OLD application's packages are outdated, in-house, unmaintained, or no longer supported. Specifically allowed:

- ABP Commercial 10.0.2 modules replace OLD's custom RBAC, custom JWT, custom audit, custom i18n, custom email infrastructure
- OpenIddict replaces OLD's custom JWT issuance
- Riok.Mapperly replaces OLD's in-house mapper (NOT AutoMapper -- per branch CLAUDE.md)
- ABP `IBlobStorage` replaces OLD's local `wwwroot/Documents/...` and abandoned AWS code
- Angular 20 standalone components replace Angular 7 module-based
- ASP.NET Core Rate Limiting replaces the absence of rate limiting in OLD
- Hangfire replaces OLD's custom scheduler
- ABP `IDistributedEventBus` for in-process domain events
- ABP `ISmsSender` replaces direct Twilio SDK calls

Why this matters: OLD's behavior is the stakeholder-validated business contract. NEW features beyond OLD spec (Doctor user role, AppointmentSendBackInfo) are tracked for removal as cleanup tasks, not preserved.

When you find a behavior that COULD be improved, document the improvement as a follow-up after parity is achieved -- do NOT include it in the parity plan. The user's words: "We don't want to change any behavior of the OLD application. After this process is complete, we will
manually test, demo and then can work on more changes."

The exception: when OLD has a documented bug (e.g., the JDF status-flow bug noted in `external-user-appointment-joint-declaration.md`), fix it in NEW and document the fix as "OLD bug, fixed for correctness".

## Required reading (in this order, no skipping)

You will not understand the scope without reading all of these. Skipping any of them will produce a plan that misses gaps the audits already identified.

### Step 1 -- Project context

1. `W:\patient-portal\replicate-old-app\CLAUDE.md` -- branch CLAUDE.md, the parity contract, OLD-to-NEW translation map, ABP conventions, things never to do.
2. `W:\patient-portal\replicate-old-app\.claude\rules\*.md` -- branch-scoped rules (dotnet, dotnet-env, hipaa-data, test-data, angular if present).
3. `C:\Users\RajeevG\.claude\projects\W--patient-portal-replicate-old-app\memory\MEMORY.md` and the 4 memory files it indexes -- captures locked decisions including the strict-parity directive and the role model.
4. `C:\Users\RajeevG\.claude\rules\code-standards.md`, `commit-format.md`, `pr-format.md`, `hipaa.md`, `prompt-writing.md` -- user-level standards.

### Step 2 -- Master parity index

5. `W:\patient-portal\replicate-old-app\docs\parity\_old-docs-index.md` -- pointer to every OLD source doc, naming overrides table, multi-tenant plan quotes, things-not-to-port list, things-to-port list.

### Step 3 -- All 18 parity audit docs (read each in full)

External user (10):

6. `external-user-registration.md`
7. `external-user-login.md`
8. `external-user-forgot-password.md`
9. `external-user-appointment-request.md`
10. `external-user-appointment-package-documents.md`
11. `external-user-appointment-ad-hoc-documents.md`
12. `external-user-appointment-joint-declaration.md`
13. `external-user-view-appointment.md`
14. `external-user-appointment-cancellation.md`
15. `external-user-appointment-rescheduling.md`

Internal-user dependencies (8):

16. `it-admin-system-parameters.md`
17. `staff-supervisor-doctor-management.md`
18. `clinic-staff-appointment-approval.md`
19. `clinic-staff-document-review.md`
20. `it-admin-package-details.md`
21. `it-admin-custom-fields.md`
22. `it-admin-notification-templates.md`
23. `staff-supervisor-change-request-approval.md`

Each audit doc has: Purpose, OLD behavior (binding), OLD code map, NEW current state, Gap analysis (severity-coded table -- B/I/C), Internal dependencies surfaced, Branding/theming touchpoints, Replication notes, and a verification test plan. Treat these as the ground truth for
what the implementation plan must address.

### Step 4 -- OLD source code (the entirety of the workflow)

You must read OLD code, not rely on the audits alone. The audits captured the salient parts but the implementation plan needs file-level specificity.

OLD root: `P:\PatientPortalOld\` (READ ONLY -- never edit).

26. OLD source files referenced in each audit doc's "OLD code map" section. At minimum: - `P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs` - `P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs` - `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\*.cs` (AppointmentDomain, AppointmentInjuryDetailDomain, AppointmentAccessorDomain, AppointmentDocumentDomain, AppointmentNewDocumentDomain, AppointmentJointDeclarationDomain,
    AppointmentChangeRequestDomain) - `P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\*.cs` - `P:\PatientPortalOld\PatientAppointment.Domain\SystemParameterModule\SystemParameterDomain.cs` - `P:\PatientPortalOld\PatientAppointment.Domain\TemplateManagementModule\TemplateDomain.cs` - `P:\PatientPortalOld\PatientAppointment.Domain\DocumentManagementModule\PackageDetailDomain.cs` - `P:\PatientPortalOld\PatientAppointment.Domain\CustomFieldModule\CustomFieldDomain.cs` - All matching API controllers under `P:\PatientPortalOld\PatientAppointment.Api\Controllers\` - The corresponding Angular components under `P:\PatientPortalOld\patientappointment-portal\src\app\components\`

For files that exceed the Read tool's 25K-token limit, chunk by offset/limit; do not skip them. The audits already note when a file is large.

27. OLD documentation extracted to `W:\patient-portal\replicate-old-app\.claude\old-extracted\pandoc\*.md` and `xlsx\*.csv`. The `socal-project-overview.md` is the master business spec; verify it against the audits when in doubt.

### Step 5 -- NEW source code state (verify the audits' "NEW current state" sections)

NEW root: `W:\patient-portal\replicate-old-app\`.

28. The NEW project structure: glob `src\HealthcareSupport.CaseEvaluation.*\` and `angular\src\app\` to confirm the layer layout matches branch CLAUDE.md's translation map.
29. Existing entity files under `src\HealthcareSupport.CaseEvaluation.Domain\<Feature>\` -- especially `Appointments\`, `Patients\`, `DoctorAvailabilities\`, `AppointmentTypes\`, `Locations\`, `ApplicantAttorneys\`, `DefenseAttorneys\`, plus all the AppointmentX sub-entities.
30. Per-feature `CLAUDE.md` files where present (e.g., `src\HealthcareSupport.CaseEvaluation.Domain\Appointments\CLAUDE.md` is rich and authoritative for current NEW state).
31. AppService impls under `src\HealthcareSupport.CaseEvaluation.Application\<Feature>\`.
32. Manual controllers under `src\HealthcareSupport.CaseEvaluation.HttpApi\Controllers\<Feature>\`.
33. EF Core configuration in both `CaseEvaluationDbContext.cs` and `CaseEvaluationTenantDbContext.cs` (host vs tenant guard pattern).
34. Mappers in `src\HealthcareSupport.CaseEvaluation.Application\CaseEvaluationApplicationMappers.cs`.
35. Permissions in `src\HealthcareSupport.CaseEvaluation.Application.Contracts\Permissions\CaseEvaluationPermissions.cs`.
36. Localization in `src\HealthcareSupport.CaseEvaluation.Domain.Shared\Localization\CaseEvaluation\en.json`.
37. Identity customization in `src\HealthcareSupport.CaseEvaluation.Domain\Identity\` (InternalUsersDataSeedContributor, ChangeIdentityPasswordPolicySettingDefinitionProvider).
38. Angular feature folders under `angular\src\app\<feature>\` -- especially `appointments\`, `applicant-attorneys\`, `defense-attorneys\`, `appointment-documents\`, etc.
39. Existing tests in `test\HealthcareSupport.CaseEvaluation.Application.Tests\` (the audit calls out which tests are skipped with "KNOWN GAP" -- those are gaps to close).

The goal of this step is to verify whether each audit's "NEW current state" section is still accurate. Code may have moved or evolved since the audit was written (2026-05-01). Where you find divergence, note it and update your plan accordingly.

### Step 6 -- Framework + library documentation (consult while planning)

Modern conventions matter; the OLD code uses 2017-era patterns the NEW stack should NOT adopt.

40. ABP Commercial 10.0.2 docs:
    - https://docs.abp.io/en/abp/latest/Modules/Identity (password policy, email confirmation settings)
    - https://docs.abp.io/en/abp/latest/Modules/Account (registration, password reset, email verification)
    - https://docs.abp.io/en/abp/latest/Object-Extensions (extending IdentityUser with FirmName, IsExternalUser)
    - https://docs.abp.io/en/abp/latest/Distributed-Event-Bus (in-process domain events)
    - https://docs.abp.io/en/abp/latest/Localization
    - https://docs.abp.io/en/abp/latest/Authorization (permissions)
    - https://docs.abp.io/en/abp/latest/Background-Jobs (Hangfire integration for reminder jobs)
    - https://docs.abp.io/en/abp/latest/Blob-Storing (file storage abstraction)
    - https://docs.abp.io/en/abp/latest/Emailing
41. OpenIddict docs:
    - https://documentation.openiddict.com/configuration/ (token issuance + flows)
42. ASP.NET Core docs:
    - https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit (for password-reset rate limiting)
    - https://learn.microsoft.com/en-us/ef/core/ (for query patterns)
43. Angular 20 docs:
    - https://angular.dev/guide/standalone-components
    - https://angular.dev/guide/forms (Reactive forms / FormBuilder pattern -- NEW uses FormBuilder for the Add page; do NOT use ngModel for new forms even though the View page does)
44. Riok.Mapperly docs:
    - https://mapperly.riok.app/docs/intro/ (source-generated mapping; partial class pattern)

When uncertain about a framework feature's current API surface, consult the docs rather than guess. Cite the doc URL in the plan when you make a non-obvious choice.

### Step 7 -- Cross-cutting cleanup tasks (already created)

These are tracked tasks created during the audit phase. The plan must include them:

45. Task #7: Remove Doctor user role + login from NEW (strict OLD parity). Affects `InternalUsersDataSeedContributor.SeedTenantUsersAsync`, `LinkDoctorEntityAsync`, `InternalUserRoleDataSeedContributor.DoctorRoleName`, `Doctor.IdentityUserId` field, planned W-DOC-1 filter.
46. Task #8: Remove `AppointmentSendBackInfo` from NEW (NEW extension, not in OLD). Affects `AppointmentSendBackInfo` entity, related DTOs, AppointmentManager.SendBack methods, Angular send-back-appointment-modal component.

## Process

After reading is complete, follow this order:

### A. Catalog every gap

Build an internal index of every gap-table row across all 18 audit docs. There are dozens. Group them by:

- Type: entity-schema, business-logic, validation, UI, notification, permission, infrastructure, cleanup
- Feature: which audit doc it came from
- Severity: B (blocker), I (important), C (cosmetic)
- Cross-feature dependencies (e.g., `AppointmentChangeRequest` entity is referenced by both cancellation and rescheduling audits -- single plan item)

### B. Detect additional gaps the audits did not capture

Audits were written from OLD code. Re-check NEW code in detail and flag anything you find that breaks parity but isn't in any audit. Common categories:

- Stale Angular code referencing removed entities or fields
- ABP defaults that bleed through (e.g., default permission labels, default email subjects)
- Missing localization keys referenced by future code paths
- Missing CSS variables for branding tokens flagged in audit docs
- Test gaps (skipped tests that mark "KNOWN GAP" need to be unskipped after the gap closes)
- The dotnet rules file noted "Adrian is new to ASP.NET/C# -- explain by comparing to Node.js/Express equivalents" -- when the plan introduces an ABP pattern that has no clear Node analogue, add a one-line explanation; this is a documentation requirement, not optional.

### C. Identify foundational order

Some gaps block others. Determine the dependency order. A correct order is approximately:

1. Schema foundations: missing entities (`SystemParameter`, `AppointmentChangeRequest`, `Document`, `DocumentPackage`/`AppointmentPacketDocument`, `NotificationTemplate`, `NotificationTemplateType`), missing fields on existing entities (`OriginalAppointmentId`,
   `ReScheduleReason`, `ReScheduledById`, `CancellationReason`, `CancelledById`, `RejectionNotes`, `RejectedById`, `PrimaryResponsibleUserId`, `IsAdHoc` on AppointmentDocument, `IsJointDeclaration` flag, `IsBeyondLimit`, `AdminReScheduleReason`, `VerificationCode` on
   AppointmentDocument, IdentityUser extensions for `IsExternalUser`/`FirmName`/`FirmEmail`/`IsAccessor`).
2. Enum extensions: `BookingStatus.Reserved`, AppointmentTypes including OTHER, DocumentStatuses including Uploaded if missing, full AppointmentStatusType.
3. Data seeders: SystemParameter singleton seed (with strict-parity defaults from OLD), AppointmentTypes (5 entries), Roles (4 external + 3 internal, NO Doctor), AccessTypes (View=23, Edit=24), DocumentStatuses, NotificationTemplate seeds for every TemplateCode used.
4. Cleanup tasks (#7 Doctor role, #8 SendBackInfo) -- do these BEFORE building features, so subsequent code does not depend on artifacts that will be removed.
5. Identity setup: extension properties on IdentityUser, password policy reconciliation in `ChangeIdentityPasswordPolicySettingDefinitionProvider` (RequireDigit=true, RequireNonAlphanumeric=true, RequiredLength=8, others false), `IsEmailConfirmationRequiredForLogin=true`,
   override of `LookupNormalizer` if needed for lowercase email semantics.
6. Permission keys: register all required permission keys in `CaseEvaluationPermissionDefinitionProvider`.
7. Foundational features: System Parameters, Doctor management (incl Reserved transition).
8. Registration + Login + Email verification + Forgot password.
9. Booking form + AppointmentManager.CreateAsync (with patient dedup + lead-time/max-time gates + slot Reserved transition + accessor account creation).
10. Approval flow (clinic staff).
11. Document flows (package, ad-hoc, JDF) with VerificationCode-link upload.
12. Change requests (cancel + reschedule) with cascade-copy.
13. Notifications + reminders (email + SMS handlers, Hangfire jobs).
14. UI work: Angular forms, AuthServer Razor pages with LeptonX customization, branding tokens.

State the order in your plan with a one-line justification per phase ("Phase X depends on Y because ...").

### D. Detect any gap you spot in the audit docs themselves

If you find an audit doc that contradicts another, or a behavior in OLD code that none of the audits captured, add an "Additional gaps not in audits" section at the end of your plan. Do NOT silently fix the audit docs in this session -- record the discrepancies for Adrian to
review.

## Output

Single file: `W:\patient-portal\replicate-old-app\docs\plans\2026-05-01-old-app-parity-implementation.md`.

Frontmatter:

```yaml
---
plan: old-app-parity-implementation
created: 2026-05-01
scope: Registration + Login + Forgot Password + Appointment Lifecycle (request, view, package docs, ad-hoc docs, JDF, cancellation, rescheduling) + required internal-user dependencies
strict-parity: true
audit-docs-covered: 18
status: ready-to-execute
---
```

Required structure:

1. Executive summary -- one paragraph explaining what the plan delivers and the strict-parity directive.
2. Reading order for the implementer -- mirror the reading order in this prompt so anyone executing has the same starting point.
3. Cross-cutting decisions -- the locked decisions from the audit phase (role model, naming overrides, email-verification approach, cleanup tasks, branding parameterization). Each as a one-line statement with audit-doc reference.
4. Phase 0 -- Cleanup -- remove Doctor role + login, remove AppointmentSendBackInfo. Each item with file paths to delete/modify.
5. Phase 1 -- Schema + enum + seed foundations -- entity additions and modifications, enum extensions, seed contributors. One subsection per entity. Each subsection lists: entity name, file path, fields to add/modify (with EF Core conventions), audit-doc reference, and a
   verification step.
6. Phase 2 -- Identity + permissions -- IdentityUser extensions, password policy, email-confirmation setting, LookupNormalizer override if needed, all permission keys.
7. Phase 3+ -- Per-feature implementation -- one phase per audit doc, in the dependency order from section C. Each phase contains:

- Audit doc reference (path + the gap rows it addresses)
- Domain layer: entity changes, domain service methods, domain events
- Application layer: AppService interface + impl, DTOs, mappers, permission attributes
- Infrastructure: repository methods, EF configuration, blob storage container, Hangfire jobs
- HttpApi: manual controller routes
- Angular: components (standalone), services, routes, guards, localization keys
- Branding/theming touchpoints to wire up (CSS variables, brand-token references)
- Tests: xUnit + Shouldly file paths, one method per business rule, synthetic data per .claude/rules/{hipaa-data,test-data}.md
- Acceptance criteria: a checklist mirroring the audit's verification-test-plan section

8. Phase N -- Branding -- aggregate docs/parity/\_branding.md from per-feature touchpoints (this is also task #6, currently pending). Wire CSS variables + config-driven branding object.
9. Phase N+1 -- Cross-feature integration tests -- end-to-end scenarios spanning multiple features (book -> approve -> upload package docs -> reschedule -> cancel).
10. Localization key checklist -- every key referenced in the plan, with English value to add to en.json. Strict parity = match OLD's validation messages and email subjects verbatim.
11. Deferred items -- the 12 remaining audit slices listed in reference_old-app-extracted-docs.md that are NOT in the workflow scope. Note for the future audit session.
12. Additional gaps not in audits -- gaps you detected during planning that the audits missed.
13. Risk register -- known unknowns. For each: what is unknown, what could go wrong, mitigation.

Per-plan-item structure

Every plan item must contain:

- Audit reference: docs/parity/<file>.md plus the gap-table row(s) it addresses (use the OLD/NEW/Action columns verbatim where helpful).
- Files to create: full paths.
- Files to modify: full paths + nature of change (one or two sentences).
- Files to delete: full paths (only for cleanup phase).
- Code shape: not full code, but a sketch -- method signatures, key invariants, where state-machine guards apply.
- Tests: file path + a list of test method names with one-line descriptions.
- Acceptance criteria: checklist (3-7 bullets) the implementer can verify before marking done.
- Strict-parity note: the verbatim OLD behavior being replicated. If this is an "OLD bug, fixed for correctness" exception, mark it explicitly.

Quality standards (enforce in the plan)

C# / .NET / ABP:

- .NET 10 with latest C# language features. Nullable reference types ON. Async-only data access. No .Result / .Wait().
- Riok.Mapperly partial classes only. NEVER AutoMapper. NEVER ObjectMapper.Map<>.
- AppServices: extend CaseEvaluationAppService. Always [RemoteService(IsEnabled = false)]. Manual controller exposes the API.
- DTO naming per branch CLAUDE.md: {Entity}CreateDto, {Entity}UpdateDto, {Entity}Dto, {Entity}WithNavigationPropertiesDto, Get{Entities}Input. Never CreateUpdate{Entity}Dto.
- Domain logic in domain services (e.g., AppointmentManager.CreateAsync) -- not in AppServices. Per branch CLAUDE.md "never skip the domain service ... business rules belong in domain services".
- State-machine guards on entity setters for status fields. Public setters get replaced with method calls (SetStatus(...)).
- Permissions: nested static class in CaseEvaluationPermissions.cs AND register in CaseEvaluationPermissionDefinitionProvider.cs. Pattern: Default + Create / Edit / Delete children plus any feature-specific (Approve, Reject, RequestCancellation, ApproveCancellation, etc.).
- Localization: every user-facing string goes through IStringLocalizer in C# and | abpLocalization in Angular templates. Keys must exist in en.json before they are referenced.
- Audit: [Audited] attribute on entities that need change tracking. Don't roll a custom audit table.
- Events: IDistributedEventBus.PublishAsync<TEto>(...) for cross-feature signals. Subscriber in a IDistributedEventHandler<TEto> class.

Angular:

- Angular 20 standalone components. NEVER add to NgModules; use imports: [...] arrays directly.
- Reactive forms via FormBuilder for ALL new forms. Do not use ngModel for new code (the View page's ngModel approach is a known gap to refactor).
- Strict TypeScript: no implicit any, no as any casts.
- Auto-generated proxies in angular/src/app/proxy/ -- NEVER edit. Regenerate via abp generate-proxy after backend changes.
- Per branch CLAUDE.md: do not run ng serve / yarn start / ng build --watch. Use npx ng build --configuration development then npx serve -s dist/CaseEvaluation/browser -p 4200.
- A11y: ARIA labels on form inputs, focus management on modals, keyboard navigation supported.
- Branding: use CSS custom properties (--brand-primary, --brand-logo-url, etc.) plus a BrandingService reading from a tenant-scoped config endpoint.
- Production code: no console.log (NEW currently has one in appointment-add.component.ts:1546 -- remove it as part of the relevant phase).

Code-style cross-cutting:

- ASCII only in source files. No smart quotes, em dashes, or Unicode decorations (per code-standards.md).
- Comments: rare. Only when WHY is non-obvious. No "// added by X" comments. No removed-code comments. No multi-paragraph docstrings.
- File size: 400 lines max for C#; 250 lines max for Angular components (per code-standards.md MODERATE tier).
- HIPAA: synthetic data only. The phi-scanner hook runs on every tool use; do not bypass.
- Commit format per ~/.claude/rules/commit-format.md: <type>(<scope>): <subject>, scope required, ASCII only, no AI attribution.

Tests:

- xUnit + Shouldly + Autofac DI per branch CLAUDE.md.
- One test class per AppService / domain service. One test method per business rule.
- Test method naming: MethodName_Scenario_ExpectedResult (e.g., CreateAsync_DuplicateEmail_ThrowsBusinessException).
- Synthetic data only per .claude/rules/{hipaa-data,test-data}.md. Never real patient names, IDs, or PHI.
- The 4 [Fact(Skip="KNOWN GAP: ...")] tests in AppointmentsAppServiceTests.cs -- the plan should include un-skipping each as the corresponding gap closes.

Stop points -- when to pause for human review

You will not pause for the user during planning. You write the entire plan in one shot. The user will review the plan in full and direct any adjustments separately.

The exception: if you discover a contradiction between two audit docs that cannot be resolved by reading OLD code, document the contradiction in the "Additional gaps" section and propose two paths forward for the user to choose between. Do not block plan completion on this --
assume strict OLD parity (whichever interpretation is closer to OLD code) for the plan and flag the question.

Final acceptance for the plan

Self-verify before declaring the plan complete:

- Every gap-table row from every audit doc has at least one corresponding plan item. Cross-check by searching each audit's gap table and tracing each row to a plan section.
- Every cleanup task (#7 Doctor role, #8 SendBackInfo) has a phase entry.
- Every NEW gap you detected has a plan item.
- The role model is honored: 4 external + 3 internal user roles, Doctor is non-user. No plan item adds a Doctor role.
- The naming overrides are honored: Patient Attorney -> Applicant Attorney in NEW; Adjuster stays.
- Every plan item has the per-plan-item structure (audit ref, files, code shape, tests, acceptance criteria, strict-parity note).
- The phase order respects dependencies (foundations before features).
- Modern coding standards from "Quality standards" are reflected in plan items where relevant.
- The plan is self-contained: an implementer reading only the plan + the referenced audit docs can execute without you.
- ASCII only. No smart quotes, em dashes, or Unicode decorations.

Final note on tone

Do not editorialize or flatter. Do not add "great question" / "as we discussed" -- the plan goes to a fresh Claude session that has no shared history. Be terse, declarative, and reference-rich. Every claim that names a file, function, or behavior must cite the audit doc or OLD
source file it comes from.

Write the plan now.
