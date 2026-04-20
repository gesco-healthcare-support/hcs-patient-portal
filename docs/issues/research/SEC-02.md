[Home](../../INDEX.md) > [Issues](../) > Research > SEC-02

# SEC-02: PII Logging Enabled by Default -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` lines 67-71
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json` line 6
- `src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.json` line 7

---

## Current state (verified 2026-04-17)

`CaseEvaluationHttpApiHostModule.ConfigureServices`:

```csharp
if (!configuration.GetValue<bool>("App:DisablePII"))
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
}
```

`App:DisablePII` defaults to `false` in both host appsettings.json files. Net effect: every deployment logs full JWT contents, user claims, and identity-model exception detail unless an operator actively sets `App:DisablePII=true`.

---

## Official documentation

- [IdentityModelEventSource.ShowPII](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.logging.identitymodeleventsource.showpii?view=msal-web-dotnet-latest) -- default `false`. When `true`, logs user claims, token subjects, issuer/audience, exception detail that would otherwise be redacted as `[PII is hidden]`.
- [IdentityModelEventSource.LogCompleteSecurityArtifact](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.logging.identitymodeleventsource.logcompletesecurityartifact?view=msal-web-dotnet-latest) -- default `false`. When `true` (with `ShowPII=true`), logs the complete raw JWT including header/payload/signature -- a usable bearer token ends up in logs.
- [AzureAD identity-model PII wiki](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/wiki/PII) -- "`ShowPII` should only be set to `true` for debugging purposes. Setting it to `true` in a production environment can lead to information disclosure."
- [IdentityModelEventSource.cs source](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/dev/src/Microsoft.IdentityModel.Logging/IdentityModelEventSource.cs) -- confirms both flags default `false`; PII emit is gated behind explicit branches.

## HIPAA implications

- [Kiteworks: HIPAA Audit Logs requirements](https://www.kiteworks.com/hipaa-compliance/hipaa-audit-log-requirements/) -- logs containing PHI identifiers are themselves PHI; must be encrypted at rest and retained 6 years.
- [Censinet: HIPAA Audit Log key requirements](https://censinet.com/perspectives/hipaa-audit-logs-key-requirements-for-phi-transfers) -- explicit: "limit the amount of PHI included in audit logs" -- logging full tokens and claims fails the minimum-necessary standard.
- [Of Ash and Fire: HIPAA API Development 2026](https://www.ofashandfire.com/blog/hipaa-compliant-api-development-securing-phi) -- OAuth token management guidance: tokens must not be logged; logged tokens + claims count as ePHI exposure.

## Community findings

- [Microsoft Q&A: ShowPII in Startup.cs has no effect](https://learn.microsoft.com/en-us/answers/questions/990631/showpii-in-startup-cs-has-no-effect) -- community confirms flag is process-wide; consensus: enable only during incident reproduction.
- [Azure Functions Python App -- ShowPII](https://learn.microsoft.com/en-us/answers/questions/1108594/azure-functions-python-app-enable-identitymodeleve) -- temporary, dev-only.

## Recommended approach

1. Invert the flag: default `App:DisablePII` to `true` in every appsettings.json, or better, rename to `App:EnablePII` so "unsafe" is explicit.
2. Scope the `ShowPII = true` assignment to `hostingEnvironment.IsDevelopment()` only -- never Production/Staging regardless of config.
3. Never set `LogCompleteSecurityArtifact = true` outside a local reproduction. Treat any log line containing a complete JWT as a credential leak -- revoke issued tokens.
4. Audit existing Serilog sinks (`Logs/` directory) for historical PII -- anything older than the fix should be considered leaked.

## Gotchas / blockers

- `IdentityModelEventSource` is a static singleton; flipping the flag at runtime affects all logs immediately but doesn't redact prior lines.
- Flag must be set before first token validation, i.e. in module `ConfigureServices`, not in the request pipeline.
- Current ABP 10.x templates (inference, MEDIUM confidence) do NOT scaffold `App:DisablePII` -- this was added by a prior developer.
- Serilog sinks may forward logs to Azure Log Analytics / Splunk / file share -- the redaction must happen upstream of the sink.

## Open questions

- Which log sinks are active in each environment? File (`Logs/`), console, remote? Redaction plan needs a full sink inventory.
- Are there historical Serilog files in any environment that contain captured JWTs? If yes, those files are now credential material -- rotate and securely delete.
- What is Gesco's log retention policy? HIPAA wants 6 years for audit trails; PII-laden logs need to be re-classified as PHI storage.

## Related

- [SEC-01](SEC-01.md) -- related settings-level hardening
- [docs/issues/SECURITY.md#sec-02-pii-logging-enabled-by-default](../SECURITY.md#sec-02-pii-logging-enabled-by-default)
