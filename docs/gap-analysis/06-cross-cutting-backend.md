# 06 -- Cross-cutting Backend: Gap Analysis OLD vs NEW

## Summary

OLD implements 8 cross-cutting concerns in-house: a Serilog-absent console/debug logger, a DB-backed exception log table, direct AWS S3 for blob storage, AWS SES + raw `System.Net.Mail.SmtpClient` for email, Twilio REST for SMS, 9 stored-proc-driven notification schedulers invoked by an external POST endpoint, a custom `DbEntityLog` audit writer tied to `spm` views, and a `MemoryCache` + custom `ApplicationCache` for caching. NEW wires the ABP framework modules that substitute for nearly all of these (Serilog, AuditLogging, FeatureManagement, BackgroundJobs, Emailing, TextTemplateManagement, BlobStoring.Database, FileManagement, DistributedCache), but business code does not yet call any of them: no `IBlobContainer` consumers, no `IBackgroundJobManager` jobs, `IEmailSender` is explicitly replaced by `NullEmailSender` in DEBUG, and no SMS provider is referenced at all. Net result: the framework substrate for the 8 concerns is installed but not yet used for the OLD feature set. MVP risk rating: **High** -- 4 MVP-blocking gaps (email send, SMS send, blob storage for documents, background notification jobs), plus 5 non-MVP gaps.

## Method

Queried on 2026-04-23. Commands and files:
- `Grep` over `P:\PatientPortalOld\**\*.cs` and `W:\patient-portal\development\src\**\*.cs` for each concern name.
- Read OLD: `Infrastructure\ExceptionLogs\LogException.cs`, `Infrastructure\Utilities\{AmazonBlobStorage,SendMail,TwilioSmsService,FileOperations}.cs`, `Infrastructure\AsyncProcess\TaskProcess.cs`, `Infrastructure\Filters\{LogRequestFilter,DateTimeZoneFilter}.cs`, `Domain\Core\SchedulerDomain.cs`, `UnitOfWork\AuditLogs\DbEntityLog.cs`, `Api\Controllers\Api\Core\SchedulerController.cs`, `Api\AppConfiguration\{ApplicationConfiguration,CoreServices}.cs`, `Api\{appsettings.json, server-settings.json}`.
- Read NEW: `HttpApi.Host\{Program.cs, CaseEvaluationHttpApiHostModule.cs, appsettings.json, HealthChecks\HealthChecksBuilderExtensions.cs}`, `Domain\CaseEvaluationDomainModule.cs`, `Application\CaseEvaluationApplicationModule.cs`, `EntityFrameworkCore\EntityFrameworkCore\CaseEvaluationEntityFrameworkCoreModule.cs`, `AuthServer\CaseEvaluationAuthServerModule.cs`.
- Read all 5 ADRs in `docs\decisions\`. None of the 5 ADRs cover the concerns in Track 6 directly; ADR-003 (dual DbContext) is the closest.
- Reproducibility: every file:line citation below was confirmed on 2026-04-23 against the files listed.

## OLD version state

### Logging
- `Microsoft.Extensions.Logging` only; no Serilog. Registered via default host builder.
- Log levels configured in `appsettings.json` (Warning default for `Console` and `Debug`), overridden in `appsettings.Development.json` (Debug default) -- `P:\PatientPortalOld\PatientAppointment.Api\appsettings.json:1-15`, `appsettings.Development.json:1-10`.
- No file sink. No structured logging. No correlation IDs.
- Request-log filter `LogRequest : ActionFilterAttribute` exists at `Infrastructure\Filters\LogRequestFilter.cs:13-63` but its `OnActionExecuted` body is entirely commented out (lines 55-62), so per-request log rows are never written. `RequestLog` rows are constructed but never persisted.

### Error handling / exception logging
- Global `UseExceptionHandler` in `Api\AppConfiguration\ApplicationConfiguration.cs:63-83` catches unhandled exceptions, resolves `ILogException`, writes a `ApplicationExceptionLog` row and returns the user-facing error string in the HTTP 500 body.
- `Infrastructure\ExceptionLogs\LogException.cs:8-48` writes to `dbo.ApplicationExceptionLogs` (schema defined in `DbEntities\Models\ApplicationExceptionLog.cs:12-84`). Captures URL, message, exception type, source, stack trace, inner exception, UTC date, and `UserClaim.UserId`.
- Homegrown. No external sink (no Sentry, no Application Insights, no Serilog enrichers).

### File storage (blob)
- Raw AWS SDK for .NET. `Infrastructure\Utilities\AmazonBlobStorage.cs:14-384` constructs `AmazonS3Client` with access key / secret key from `server-settings.json:29-47` (secrets committed in plaintext -- itself a security gap).
- Region hardcoded `RegionEndpoint.USEast2` (line 18) in `AmazonBlobStorage`; `SendMail.cs:33` hardcodes `USWest1` for SES. Two regions in the same infra tree is a smell.
- Public API: `StoreBlobFile`, `DeleteBlobFile`, `DownloadFile`, `GetBlobFile`, `GetJointAgreementLetter`, `DownloadPackageDocument`. Containers include `patientpacket`, `doctorpacket`, `attornypacketame`, `attornypacketpqme`, `claimexaminerpacketame`, `claimexaminerpacketpqme`, `jointagreementletter` -- 7+ logical containers per tenant.
- `FileOperations.cs:11-106` is a thin wrapper around `IAmazonBlobStorage` and injects `ServerSetting` for container-name lookups.
- Local fallback: `DownloadFile` (line 114-147) reads from `wwwroot\Documents\<bucket>\<file>` instead of S3 -- the S3 code has been commented out. `DownloadPackageDocument` (line 236-370) also reads from the local `wwwroot\Documents\documentBluePrint\<bucket>\` directory rather than S3.

### Email
- AWS SES via `AmazonSimpleEmailServiceClient` AND raw `System.Net.Mail.SmtpClient` both live in `Infrastructure\Utilities\SendMail.cs:23-472`.
- Four public methods on `ISendMail` / `SendMail`:
  - `SendSMTPMailAWS` (line 47-161) -- SES `SendEmailAsync`, single HTML body.
  - `SendSMTPMail` (line 166-265) -- `SmtpClient`, SMTP server info pulled from `SMTPConfiguration` DB row.
  - `SendSMTPMailWithAttachmentAWS` (line 270-364) -- MimeKit build; `mySmtpClient.Send` is **commented out** (line 352-356). Dead method.
  - `SendSMTPMailWithAttachment` (line 388-463) -- `System.Net.Mail` with disk-file attachments; SMTP config from DB.
- CC list is injected from `SystemParameter.CcEmailIds` in every send -- per-tenant CC mailbox list, pulled at send time.
- Exception path writes to `ApplicationExceptionLog` (lines 146-159, 252-264), but the writes are currently commented out so SES failures go to `Console.WriteLine` only.

### SMS
- Twilio REST via `Infrastructure\TwilioSmsService.cs:12-49`.
- Bootstrap reads `twilio.twilioAccountSid` / `twilio.twilioAuthToken` from `ServerSetting` and calls `TwilioClient.Init` (line 16-21). Account/token committed in `server-settings.json:23-28`.
- `SendSms` gated by `isSMSEnable` (`server-settings.json:54 = false`); prepends `twilio.twilioCountryCode` (`+91` -- wrong for US, pre-existing bug).
- Consumed only by `SchedulerDomain` (6 of its 9 jobs call it; 2 more have it commented out).

### Background jobs / scheduler
- `PatientAppointment.Domain\Core\SchedulerDomain.cs:18-379` defines 9 notification jobs dispatched through a single `ConfigureNotificaion(SchedulerParameters)` switch (line 37-70). Each executes a `spm` stored proc, JSON-deserializes the row set, and sends email + SMS per row.
- Trigger: external. POST `api/scheduler/postscheduler` (`SchedulerController.cs:41-47`) accepts `{ ScheduleTypeId = int }` and calls the domain. No in-process cron / Hangfire. External Windows Scheduled Task / cron is assumed.
- `AsyncProcess\TaskProcess.cs:13-20` exposes `TaskProcess.Start` wired into startup, but the body is `Task.Run(() => {})` -- a no-op.

#### The 9 jobs in `SchedulerDomain`

| # | Enum (`ReminderTypes.cs:9-20`) | Method | Stored proc | Email template | SMS? |
|---|---|---|---|---|---|
| 1 | `AppointmentApproveRejectInternalUser = 1` | `AppointmentApproveRejectInternalUserNotification` (line 87-117) | `spm.spAppointmentApproveRejectInternalUserNotification` | `AppointmentApproveRejectInternal` | yes |
| 2 | `AppointmentPackageDocumentPending = 2` | `AppointmentPackageDocumentPendingNotification` (line 119-150) | `spm.spAppointmentPackageDocumentPendingNotification` | `UploadPendingDocuments` | yes |
| 3 | `AppointmentDueDateApproaching = 3` | `AppointmentDueDateApproachingNotification` (line 152-174) | `spm.spAppointmentDueDateApproachingNotification` | `AppointmentDueDateReminder` | no |
| 4 | `AppointmentDueDateDocumentApproaching = 4` | `AppointmentDueDateDocumentApproachingNotification` (line 176-202) | `spm.spAppointmentDueDateDocumentApproachingNotification` | `AppointmentDocumentIncomplete` | yes |
| 5 | `AppointmentJointDeclarationDocumentUpload = 5` | `AppointmentJointDeclarationDocumentUploadNotification` (line 204-232) | `spm.spJointDeclarationDocumentUploadNotification` | `UploadPendingDocuments` | yes |
| 6 | `AppointmentAutoCancelled = 6` | `AppointmentAutoCancelledNotification` (line 234-260) | `spm.spAppointmentAutoCancelledNotification` | `AppointmentCancelledDueDate` | yes |
| 7 | `AppointmentPendingReminderStaffUsers = 7` | `AppointmentPendingReminderStaffUsersNotification` (line 261-312) | `spm.spAppointmentPendingReminderStaffUsersNotification` | `AppointmentPendingNextDay` | no (send commented) |
| 8 | `AppointmentPendingDocumentSendToResponsibleUser = 8` | `AppointmentPendingDocumentSendToResponsibleUser` (line 336-364) | `spm.spAppointmentPendingDocumentSendToResponsibleUser` | `UploadPendingDocuments` | SMS + email commented |
| 9 | `PendingAppointmentDailyNotification = 9` | `AppointmentPendingDailyNotification` (line 72-85) | `spm.spPendingAppointmentNotification` | `PendingAppointmentDailyNotification` | no |

### Caching
- `CoreServices.cs:44` registers `services.AddMemoryCache()`.
- `CoreServices.cs:52` registers `CacheContext : ICacheContext` (from in-house `Rx.Core.Cache`).
- `CoreServices.cs:88` registers `ApplicationCache : IApplicationCache` as singleton.
- `server-settings.json:17-22` declares `{ days: 365, type: "sql" }` -- two-layer MemoryCache + SQL cache (`PatientPortalOld_Cache` DB).
- No Redis. No ABP DistributedCache abstraction.

### Validation
- DataAnnotations on DbEntities.
- Centralized error-code enum `Infrastructure\ValidationMessages\ValidationFailedCode.cs:3-73` -- 61 members. Frontend looks up the message table by code.
- Per-request filters: `DateTimeZoneFilter.cs:17-99` converts response `DateTime` to `ClaimTypes.Locality` via NodaTime. `SessionFilter.cs`, `LockRecordFilter.cs`.

### Audit logging
- `UnitOfWork\AuditLogs\DbEntityLog.cs:16-115` implements `IDbEntityLog`, invoked by UoW commits to produce `AuditRequest`/`AuditRecord`/`AuditRecordDetail`.
- Reflects over entity properties, serializes new/old to JSON, records per-column deltas on `Modified`. Uses `[TableKeyAttribute]`, `[LogPropertyAttribute]`, `[RelationshipTableAttribue]` to locate key/display-name columns.
- Depends on `HttpContext.Items["ApplicationModuleId"]`, headers `x-record`, `x-application-module`.

## NEW version state

### Logging
- Serilog. `Program.cs:15-42` configures console + async file sinks. File target `Logs/logs.txt`. Min level Debug in Debug, Information in Release. ABP Studio sink at line 41.
- `AbpAspNetCoreSerilogModule` dep (`CaseEvaluationHttpApiHostModule.cs:59`). `UseAbpSerilogEnrichers()` middleware (line 324).

### Error handling / exception logging
- ABP default exception pipeline (not re-registered). `UseDeveloperExceptionPage` in Development at `CaseEvaluationHttpApiHostModule.cs:293-296`.
- Business code uses `UserFriendlyException` / `BusinessException` -- 66 occurrences across 10 app services (top: `AppointmentsAppService.cs` with 22).
- No custom `IExceptionSubscriber` / `IExceptionNotifier`. No in-DB exception-log table.

### File storage (blob)
- Module deps wired only. `CaseEvaluationDomainModule.cs:11,22,44,47` -> `BlobStoringDatabaseDomainModule` + `FileManagementDomainModule`. EFCore side `CaseEvaluationEntityFrameworkCoreModule.cs:31,38`. Migration `20260131164316_Initial` creates `AbpBlobContainers` / `AbpBlobs`.
- **Zero business consumers.** Grep for `IBlobContainer|SaveAsync|GetBlobAsync` returns only migration snapshots.
- No S3 / Azure / filesystem provider configured. Default provider = DB BLOB (`varbinary(max)`).

### Email
- `AbpEmailingModule` wired (`CaseEvaluationDomainModule.cs:17,38`).
- `CaseEvaluationDomainModule.cs:59-62` **replaces `IEmailSender` with `NullEmailSender` under `#if DEBUG`** -- silently drops mail in dev.
- No business code calls `IEmailSender`.
- `TextTemplateManagementDomainModule` and `AbpAccountPublicApplicationModule` are wired -- account flows (password reset / confirm) work via ABP defaults. Appointment reminders / document-pending notifications have no sender code.

### SMS
- **Absent.** No Twilio package in any `.csproj`. No `ISmsSender` consumer. No `Volo.Abp.Sms.*` dep. Grep 0 matches.

### Background jobs / scheduler
- `AbpBackgroundJobsDomainModule` wired (`CaseEvaluationDomainModule.cs:16,33`); `AbpBackgroundJobsEntityFrameworkCoreModule` at EFCore layer. Migration creates `AbpBackgroundJobs`.
- AuthServer disables execution (`CaseEvaluationAuthServerModule.cs:198-201 IsJobExecutionEnabled = false`). HttpApi.Host does not override -- default true.
- **Zero `IAsyncBackgroundJob<T>` / `BackgroundJob<T>` classes.** Grep 0 matches. No Hangfire / Quartz.

### Caching
- `AbpCachingStackExchangeRedisModule` wired (`CaseEvaluationHttpApiHostModule.cs:25,58`). `appsettings.json:12-15` -- `Redis.Configuration = "127.0.0.1"`, `Redis.IsEnabled = false` in dev.
- `AbpDistributedCacheOptions.KeyPrefix = "CaseEvaluation:"` set in HttpApi.Host, AuthServer, DbMigrator.
- Business usage: `WcabOfficesAppService.cs:28,33,126` uses `IDistributedCache<WcabOfficeDownloadTokenCacheItem, string>` with 30-sec expiry. `UserExtendedAppService.cs:19` also.
- Distributed locking via Medallion.Threading.Redis (`CaseEvaluationHttpApiHostModule.cs:220-234`).

### Validation
- DataAnnotations on DTOs in `Application.Contracts\**\*CreateDto.cs` / `*UpdateDto.cs` (82 occurrences across 30 files).
- ABP runs DataAnnotations in the MVC filter pipeline; failure -> `AbpValidationException` -> 400 with field-level error envelope.
- `UserFriendlyException` for domain rule failures (e.g., `PatientsAppService.cs:97` `UserFriendlyException(L["Email is required."])`).

### Audit logging
- `AbpAuditLoggingDomainModule` wired (`CaseEvaluationDomainModule.cs:15,31`); EFCore at `CaseEvaluationEntityFrameworkCoreModule.cs:38`.
- All 17 domain aggregates inherit `FullAuditedAggregateRoot<Guid>` / `FullAuditedEntity<Guid>`.
- `UseAuditing()` middleware at `CaseEvaluationHttpApiHostModule.cs:323` writes one `AbpAuditLog` + N `AbpAuditLogActions` per HTTP request.
- AuthServer sets `AbpAuditingOptions.ApplicationName = "AuthServer"` (line 175-178).
- No custom `IAuditingStore`.

## Delta

### MVP-blocking gaps (capability present in OLD, absent in NEW)

| gap-id | capability | evidence-old | evidence-new-absent | effort |
|---|---|---|---|---|
| CC-01 | Send appointment reminder emails | `SendMail.cs:47-265`; called by 9 jobs in `SchedulerDomain.cs:72-364` | `CaseEvaluationDomainModule.cs:61` replaces `IEmailSender` with `NullEmailSender` in DEBUG; 0 `IEmailSender` consumers | M |
| CC-02 | Send appointment reminder SMS | `TwilioSmsService.cs:12-49`; called by 6 of 9 jobs | No Twilio package, no `ISmsSender`, no `Volo.Abp.Sms.*` | M-L |
| CC-03 | Background notification jobs | `SchedulerDomain.cs:18-379` dispatching 9 stored procs; triggered by `SchedulerController.cs:41-47` | `AbpBackgroundJobsDomainModule` wired; 0 `IAsyncBackgroundJob<T>` classes. Procs themselves are stubs | L |
| CC-04 | Blob storage for packet documents | `AmazonBlobStorage.cs:14-384` + `FileOperations.cs:11-106`, 7+ containers | `BlobStoringDatabaseDomainModule` + `FileManagementDomainModule` wired; 0 `IBlobContainer` consumers | M |

### Non-MVP gaps

| gap-id | capability | evidence-old | evidence-new-absent | effort |
|---|---|---|---|---|
| CC-05 | Per-request action log (browser, cookies, auth header, params, timing) | `LogRequestFilter.cs:13-63` (already dead -- write commented) | ABP AuditLog does most of this but not cookies/auth header | S |
| CC-06 | Joint agreement letter + packet document download/upload | `AmazonBlobStorage.cs:178-234` + `236-370` | Depends on CC-04 | S |
| CC-07 | Per-tenant CC mailbox list applied to all outbound mail | `SendMail.cs:49,276` reads `SystemParameter.CcEmailIds` | ABP `IEmailSender` has no parallel | S |
| CC-08 | Timezone-aware response DTOs via `ClaimTypes.Locality` | `DateTimeZoneFilter.cs:17-99` | No parallel. ABP localization = culture only | M |
| CC-09 | `ValidationFailedCode` 61-code enum for FE i18n | `ValidationFailedCode.cs:3-73` | ABP `L["Key"]` + `UserFriendlyException.Code`; map later | S |

### Post-MVP deferred gaps (per Adrian 2026-04-23 -- handle after MVP)

| gap-id | capability | evidence-old | evidence-new-absent | effort |
|---|---|---|---|---|
| BRAND-03 | Email template branding via `AbpTextTemplateContents` + tenant-scoped placeholder substitution (`##CompanyName##`, `##ClinicLogoUrl##`, `##SupportPhone##`, etc.) | `wwwroot\EmailTemplates\*.html` with placeholder tokens resolved against `server-settings.json` clinic details at send time (see `SendMail.cs:276` reading `SystemParameter.CcEmailIds`, logo pulled from `logoName` key, footer from `footertext` etc.) | `AbpTextTemplateContents` table exists (ABP default) but has zero consumers. No tenant-scoped placeholder resolver wired. Blocked by CC-01 (email send) | S (1-2 days after CC-01) |

### Intentional architectural differences (NOT gaps)

| topic | OLD | NEW | why |
|---|---|---|---|
| Logging provider | MS.Ext.Logging, no file sink | Serilog + async file + ABP Studio + context enrichment | Standard ABP template |
| Exception logging | `dbo.ApplicationExceptionLogs` via UoW | ABP exception middleware + Serilog | Log aggregator > DB table |
| Audit log backend | Custom `DbEntityLog.cs` + `spm` views | `AbpAuditLogs` + `AbpAuditLogActions` + `FullAuditedAggregateRoot` per entity | Framework parity |
| Blob provider | Raw AWS S3 SDK with committed keys | ABP `BlobStoringDatabaseModule` (DB BLOB) wired | Provider abstraction (CC-04 = missing consumer) |
| Email provider | AWS SES + SmtpClient with DB-config | ABP `IEmailSender` default `SmtpEmailSender` + `AbpSettings` | Provider abstraction (CC-01 = missing consumer) |
| Cache | `MemoryCache` + SQL `ApplicationCache` | Redis `IDistributedCache` + Medallion lock | Standard ABP choice |
| Timezone | Server-side DTO rewrite via NodaTime | Culture-only via ABP localization | Simplification; CC-08 flagged |
| Validation transport | 61-code enum + FE lookup | ABP `L["Key"]` + `UserFriendlyException.Code` | ABP-native; CC-09 is mapping exercise |

### Extras in NEW (not in OLD)

- Health checks: `HealthChecks\HealthChecksBuilderExtensions.cs:13-74` -- `/health-status`, `/health-ui`.
- Data protection (Redis in non-dev): `CaseEvaluationHttpApiHostModule.cs:202-218`.
- Distributed locking (Medallion.Threading.Redis): `CaseEvaluationHttpApiHostModule.cs:220-234`.
- GDPR module (`AbpGdprDomainModule`).
- Feature management module.
- Text template management (DB templates vs disk files).
- Language management (dynamic localizations).
- Explicit `X-Frame-Options: DENY` at `CaseEvaluationHttpApiHostModule.cs:92-95` (OLD has `SameOrigin`).
- Soft delete on every aggregate via `FullAuditedAggregateRoot`.
- Swagger + OIDC (`CaseEvaluationHttpApiHostModule.cs:187-200`).

## Open questions

- Adrian, please clarify: when NEW sends email, SES-native (custom `IEmailSender`) or ABP SMTP with SES SMTP credentials? SMTP-over-SES is lowest friction.
- Adrian, please clarify: MVP SMS off (matching OLD `isSMSEnable = false`)? Defers the Twilio SDK decision.
- Adrian, please clarify: blob storage provider for MVP -- DB BLOB (works now, slow for large packets) or S3 (parity, needs creds)? Recommendation: DB BLOB first, migrate later.
- Adrian, please clarify: scheduler pattern -- external cron hitting POST endpoint (OLD parity) or Hangfire/Quartz module? ABP native `IBackgroundJobManager` alone is one-shot, not recurring.
- Adrian, please clarify: OLD `server-settings.json` committed AWS keys + Twilio tokens -- burn and rotate before MVP cut-over?
- Adrian, please clarify: `NullEmailSender` DEBUG replacement -- keep silent-drop, or wire smtp4dev default so devs can see outbound mail?
