[Home](../../INDEX.md) > [Issues](../) > Research > FEAT-05

# FEAT-05: Email System Not Wired Up -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs` lines 38, 42, 60-62

---

## Current state (verified 2026-04-17)

```csharp
#if DEBUG
    context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
#endif
```

- `Volo.Abp.Emailing` in `[DependsOn]` (line 38) -- `IEmailSender` resolvable.
- `TextTemplateManagementDomainModule` in `[DependsOn]` (line 42) -- template system available.
- No `Settings:Abp.Mailing.Smtp.*` keys in any `appsettings.json`.
- No email templates defined.
- No `IEmailSender.SendAsync` call sites in application code.

ABP does NOT automatically fall back to `NullEmailSender` when SMTP host is empty in Release -- it throws `SmtpException` on send. HIGH confidence.

---

## Official documentation

- [ABP Email Sending](https://abp.io/docs/latest/framework/infrastructure/emailing) -- canonical settings keys: `Abp.Mailing.Smtp.Host/Port/UserName/Password/Domain/EnableSsl/UseDefaultCredentials`, plus `Abp.Mailing.DefaultFromAddress`, `Abp.Mailing.DefaultFromDisplayName`. `IEmailSender.SendAsync` is the provider-independent API. Password must be encrypted via `ISettingEncryptionService.Encrypt()`.
- [ABP Settings](https://abp.io/docs/latest/Settings) -- `appsettings.json` `Settings:*` keys are default source, overridable per-tenant.
- [ABP Text Template Management (Commercial)](https://abp.io/docs/latest/modules/text-template-management) -- stores template bodies in `AbpTextTemplateContents` table, admin UI under Administration, per-culture content.
- [ABP Text Templating framework](https://docs.abp.io/en/abp/3.3/Text-Templating) -- `ITemplateRenderer.RenderAsync(templateName, model, cultureName)` returns rendered string for `IEmailSender.SendAsync`.
- [ABP Local Event Bus](https://abp.io/docs/latest/framework/infrastructure/event-bus/local) -- `ILocalEventHandler<TEvent>`; `EntityCreatedEventData<IdentityUser>` published after user creation. Handlers execute just before UoW completion; unhandled exceptions roll back.

## Community findings

- [ABP Support #4036 -- NullEmailSender works in all environments](https://abp.io/support/questions/4036/NullEmailSender-service-works-all-environments-not-only-debug) -- ABP confirms `#if DEBUG` replacement is the documented mechanism; remove or wrap with config flag when real SMTP is desired in non-Release builds.
- [ABP Support #6912 -- Empty SMTP host error message](https://abp.io/support/questions/6912/When-SMTP-settings-are-empty-there-should-be-a-specific-error-message-instead-of-a-generic-error) -- confirms no graceful fallback; empty host produces generic `SmtpException`.
- [ABP Support #2269 -- SMTP setup](https://abp.io/support/questions/2269/SMTP-setup) + [#4095 -- Email sender not working](https://abp.io/support/questions/4095/Email-Sender-not-working) -- common pitfalls: missing `EnableSsl=true` for Gmail/Office365, plaintext password, wrong port.
- [ABP Community -- Replacing Email Templates](https://abp.io/community/articles/replacing-email-templates-and-sending-emails-jkeb8zzh) + [Engincan Veske walkthrough](https://engincanveske.substack.com/p/replacing-email-templates-and-sending-emails-in-abp-frameworkhtml) -- `TemplateDefinitionProvider`, embedded `.tpl` files, `ITemplateRenderer` + `IEmailSender` composition.
- [HIPAA Journal -- SendGrid HIPAA compliance](https://www.hipaajournal.com/sendgrid-hipaa-compliant/) (MEDIUM) -- Twilio signs BAA covering SendGrid only under negotiated enterprise agreements; self-serve does not include one.
- [Jotform compliance checker -- SendGrid](https://www.jotform.com/hipaa/is-hipaa-compliant/sendgrid/) + [Compliancy Group -- SendGrid](https://compliancy-group.com/is-sendgrid-hipaa-compliant/) -- confirm default SendGrid not HIPAA-eligible without separately negotiated BAA.
- [Microsoft Q&A -- ACS HIPAA](https://learn.microsoft.com/en-us/answers/questions/1687871/is-azure-communication-services-hipaa-compliant) -- ACS supports HIPAA with a BAA; thread covers video/SMS but doesn't explicitly name Email.
- [Azure HIPAA compliance offering](https://learn.microsoft.com/en-us/azure/compliance/offerings/offering-hipaa-us) -- lists Azure services in scope of Microsoft's default BAA; ACS is in the HIPAA-eligible list. INFERENCE: Email inherits BAA coverage; verify on Azure Trust Center before production.

## Recommended approach

1. Keep `Volo.Abp.Emailing` + `TextTemplateManagementDomainModule`. Replace `#if DEBUG NullEmailSender` with environment-aware guard: register `NullEmailSender` only when `IHostEnvironment.IsDevelopment()` AND `Settings:Abp.Mailing.Smtp.Host` is empty. Removes compile-time coupling; local Release builds behave predictably.
2. Production delivery: prefer **Azure Communication Services Email** (Microsoft BAA is default; speaks SMTP relay + REST). Keep an SMTP-relay abstraction so a SendGrid-with-Twilio-BAA swap is a config change. Reject direct self-serve SendGrid.
3. Trigger points as `ILocalEventHandler<T>` in Application layer:
   - `EntityCreatedEventData<IdentityUser>` -> registration welcome
   - New domain event from `AppointmentManager` (e.g. `AppointmentBookedEto`, `AppointmentStatusChangedEto`) for booking/confirmation emails
   - Password reset stays in ABP Account module (just override the template).

## Gotchas / blockers

- `Abp.Mailing.Smtp.Password` must be stored encrypted (`ISettingEncryptionService`). Plaintext in `appsettings.json` decrypts to garbage and throws at send time.
- Local event handlers run inside the publishing UoW; send failure rolls back the business transaction unless you swallow exceptions or use `QueueAsync` to defer to a background job.
- Multi-tenant: `Settings:Abp.Mailing.Smtp.*` in `appsettings.json` is host-scoped; per-tenant from-address or SMTP requires surfacing settings in tenant management UI so `SettingManager` resolves per tenant.
- ACS Email BAA coverage is INFERENCE from the Azure HIPAA service list -- confirm in Microsoft Trust Center before sending PHI.
- Enabling `TextTemplateManagementDomainModule` without defining at least one `TemplateDefinitionProvider` gives admin UI but no content to render.

## Open questions

- Will Gesco negotiate a Twilio BAA, or is Azure Communication Services Email the single path? Affects cost and abstraction layer.
- Should email sending be synchronous (fail request if email fails) or queued via ABP background jobs? Healthcare booking flows usually want queued + audited.
- What is the default from-address and display name (`noreply@gesco.com`?)? Needs DKIM/SPF/DMARC pre-deployment.
- Which tenants (if any) need their own sender identity vs using the host's?

## Related

- [SEC-05](SEC-05.md) -- password reset email flow depends on this
- [FEAT-01](FEAT-01.md) -- status-change notifications need this wired first
- [ARC-03](ARC-03.md) -- "complete your profile" email nudge depends on this
- [docs/issues/INCOMPLETE-FEATURES.md#feat-05](../INCOMPLETE-FEATURES.md#feat-05-email-system-is-not-wired-up)
