# Probe log: templates-email-sms

**Timestamp (local):** 2026-04-24T22:30:00
**Purpose:** Confirm the ABP Text Template Management REST surface is
already live on the running HttpApi.Host (no code change needed for CRUD +
per-tenant override), and that no custom `TemplateDefinitionProvider`
exists yet in the NEW codebase (so definitions must be added by the
capability).

## Probe 1 -- Swagger search for `text-template` endpoints

### Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json -o /tmp/swagger.json
grep -c "text-template" /tmp/swagger.json
```

### Response

Status: 200 for the Swagger fetch; `/tmp/swagger.json` contains multiple
matches on `text-template`. Relevant paths exposed by the ABP
TextTemplateManagement Pro module (per the Pro docs + live Swagger):

```
"/api/text-template-management/text-templates"
"/api/text-template-management/text-templates/{name}"
"/api/text-template-management/text-templates/{name}/content"
"/api/text-template-management/text-templates/{name}/restore-default"
```

### Interpretation

- `TextTemplateManagementDomainModule` + its HTTP API are registered.
- `GET /text-templates` lists all definitions known to the host (so the
  list stays empty until a `TemplateDefinitionProvider` adds entries).
- `GET / PUT /text-templates/{name}/content` reads / writes the tenant-
  scoped body. `restore-default` reverts to the VFS-shipped default.
- The admin Angular UI under `/text-template-management` (confirmed at
  `angular/src/app/app.routes.ts:57-61`) is the same surface a tenant
  admin uses -- it calls these exact endpoints.
- Conclusion: no new REST / controller code is required for
  templates-email-sms. Only the new `CaseEvaluationTemplateDefinitionProvider`
  + embedded `.tpl` resources are needed.

## Probe 2 -- Grep for existing provider (static evidence)

### Command

```
grep -rln "TemplateDefinitionProvider\|ITextTemplateRenderer\|ITextTemplateContentContributor" \
  W:/patient-portal/implementation-research/src
```

### Response

Zero matches in `src/`. (Migration snapshot files mention the
`AbpTextTemplate*` table names, but those are EF scaffolding and do not
count as providers.)

### Interpretation

- No custom `TemplateDefinitionProvider` subclass exists today. Without
  one, the `GET /text-templates` list endpoint from Probe 1 would return
  an empty array. That is the state the capability's "recommended
  solution" moves from.
- No `ITextTemplateRenderer` consumer exists. No send-path currently
  calls `RenderAsync`. Consumer integration is picked up by sibling
  briefs (`scheduler-notifications`, `appointment-change-requests`,
  `appointment-documents`).

## Probe 3 -- Authenticated `GET /text-templates` (not executed)

### Planned command

```
TOKEN=$(curl -sk -X POST https://localhost:44368/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=admin&password=<REDACTED>&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access" \
  | jq -r .access_token)

curl -sk -H "Authorization: Bearer <REDACTED>" \
  https://localhost:44327/api/text-template-management/text-templates
```

### Result

Not executed. Plan-mode constraint prohibits mutating or token-holding
calls. Static evidence from Probes 1 and 2 is sufficient to establish
the live surface. A future session with token-probe authorization can
confirm the empty-list response shape.

### Interpretation

The response shape is specified by the ABP Text Template Management Pro
docs (accessed 2026-04-24):

```
{
  "totalCount": 0,
  "items": []
}
```

An empty `items` array confirms no definitions registered, aligning with
the zero-match grep result in Probe 2.

## Cleanup

None required. All three probes are read-only.
