# Probe log: system-parameters-vs-abp-settings

**Timestamp (local):** 2026-04-24T20:05:00Z
**Purpose:** Confirm that (a) ABP SettingManagement REST surface is reachable,
(b) `/api/setting-management/*` paths are wired and match the Pro-module
endpoint names documented in ABP, (c) no legacy `/api/SystemParameters` or
`/api/system-parameter*` path exists in NEW.

## Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json
```

Anonymous GET. Run from the orchestrator shell against the Phase 1.5 HttpApi.Host
instance (HTTPS port 44327).

## Response

Status: HTTP 200
Body (path extract, no PHI, no PII, no tokens):

- `/api/setting-management/emailing` (GET, POST)
- `/api/setting-management/emailing/send-test-email` (POST)
- `/api/setting-management/password` (GET, POST)
- `/api/setting-management/time-zone` (GET, POST)
- `/api/setting-management/time-zone/timezones` (GET)
- 11 additional `/api/setting-management/*` paths
- 16 `/api/identity/settings*` paths
- `/api/multi-tenancy/tenants` (GET, POST, etc.)
- Zero paths containing `system-parameter`, `system_parameters`,
  `SystemParameter`, or `SystemParameters`.
- Zero paths containing `global-setting` or `GlobalSetting`.
- Zero paths containing `smtp-configuration` or `SmtpConfiguration`.

Total paths: 317 (matches Phase 1.5 service-status.md count).

## Interpretation

- **ABP SettingManagement module is live.** Both the HTTP API and the
  Application-layer services are reachable. No code change required to expose
  the endpoint family; adding new setting definitions to
  `CaseEvaluationSettingDefinitionProvider.cs` makes them immediately
  addressable via the generic `GET /api/setting-management/settings` +
  `PUT /api/setting-management/settings` endpoints and via any prefix-filtered
  group endpoint.
- **OLD's `/api/SystemParameters` family has no NEW parallel.** Confirms
  G-API-16 is fully open in the NEW code; no half-done implementation exists.
  Any new consumer that expects OLD-shape `GET /api/SystemParameters` URLs
  must migrate to `GET /api/setting-management/settings?names=...` or to
  `ISettingProvider` server-side reads.
- **OLD's `/api/GlobalSettings`, `/api/SMTPConfigurations`,
  `/api/ConfigurationContents` families likewise have no NEW parallel.**
  Their concerns are absorbed into ABP SettingManagement (`/api/setting-management/*`),
  ABP Identity (2FA / lockout), and ABP LanguageManagement
  (`/abp/application-localization`) respectively.
- **The `/api/multi-tenancy/tenants` 404 noted in Phase 1.5 service-status
  is orthogonal to this capability.** Settings resolve per-tenant via
  `ICurrentTenant.Id`; the tenant-management UI's reachability is a separate
  concern.

## Cleanup

None. Probe was a read-only anonymous `GET` on a static endpoint.

## Follow-up probes NOT run (and why)

- `POST /connect/token` password grant to obtain a bearer token, followed by
  `GET /api/setting-management/settings` on a scope-switched admin tenant:
  skipped because (a) the static evidence (module wiring + empty provider body
  + empty setting-name grep) is sufficient to prove the capability is open,
  and (b) settings writes would create persistent `AbpSettings` rows that
  require manual cleanup (per Live Verification Protocol, research README
  lines 247-261, state-mutating probes are permitted only for NEW-SEC-02).
- `POST /api/setting-management/emailing/send-test-email`: skipped for the
  same reason plus Debug-build `NullEmailSender` short-circuit would make the
  result misleading (see `email-sender-consumer` brief).
