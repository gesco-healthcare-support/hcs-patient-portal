# Probe log: email-sender-consumer

**Timestamp (local):** 2026-04-24T12:00:00
**Purpose:** Confirm the ABP Emailing + SettingManagement + Account Pro
endpoints are reachable on the running HttpApi.Host and that no
`Abp.Mailing.Smtp.*` value is configured, which together prove CC-01 is a
wiring gap rather than a code gap.

## Probe 1 -- Anonymous Swagger fetch

### Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json -o /tmp/swagger.json -w "HTTP %{http_code}\n"
```

### Response

Status: 200
Body (redacted -- only paths of interest):

```
"/api/setting-management/emailing"
"/api/setting-management/emailing/send-test-email"
"/api/account/send-email-confirmation-code"
"/api/account/send-email-confirmation-token"
"/api/account/send-password-reset-code"
"/api/account/reset-password"
"/api/account/verify-email-confirmation-token"
"/api/account/confirm-email"
```

### Interpretation

- `AbpSettingManagementDomainModule` is wired and its controllers are
  registered; an authenticated host admin can read or write
  `Abp.Mailing.Smtp.*` via the `/api/setting-management/emailing` endpoint,
  and can send a test email via
  `/api/setting-management/emailing/send-test-email` once SMTP is
  configured.
- `AbpAccountPublicApplicationModule` (via `AbpAccountProApplicationModule`
  transitively) is wired. Every Account-module email path
  internally calls `IEmailSender.SendAsync`. In Debug today those calls
  resolve to `NullEmailSender` (per `CaseEvaluationDomainModule.cs:61`).
  In Release, with no SMTP host configured, they would throw
  `SmtpException`. Both outcomes silently break forgot-password and email
  verification. This is the live signal behind CC-01's severity.
- Conclusion: the endpoints exist, the middleware pipeline is wired; the
  only missing pieces are (a) the `NullEmailSender` `#if DEBUG` narrowing
  and (b) the SMTP settings block. No new code surface is required.

## Probe 2 -- Authenticated GET /api/setting-management/emailing (not executed)

### Planned command

```
TOKEN=$(curl -sk -X POST https://localhost:44368/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=admin&password=<REDACTED>&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access" \
  | jq -r .access_token)

curl -sk -H "Authorization: Bearer <REDACTED>" \
  https://localhost:44327/api/setting-management/emailing
```

### Result

Not executed. The `POST /connect/token` step was denied by the subagent
sandbox in this session (credential-use policy). Static evidence
(grep across `appsettings*.json`, grep for `Abp\.Mailing` across the
repo, both return zero matches) proves the setting values are all
unset/default -- the endpoint would return null or default-valued fields.

### Interpretation

The static evidence is sufficient to establish CC-01. A future session
with token-probe authorization can run the live GET to confirm the
response payload.

## Cleanup

None required. Both probes are read-only.
