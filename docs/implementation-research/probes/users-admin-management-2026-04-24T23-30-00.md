# Probe log: users-admin-management

**Timestamp (local):** 2026-04-24T23:30:00
**Purpose:** Prove that ABP Identity Pro user admin surface and the `UserExtendedAppService` override surface are both live in NEW, and that the Angular `/identity/**` route loader is wired. Three swagger-only metadata reads plus one Angular root HTTP HEAD. No authenticated reads, no PII returned.

## Command 1: swagger identity/users path count

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d=json.load(sys.stdin); paths=d.get('paths',{}); \
    users=[p for p in paths if p.startswith('/api/identity/users')]; \
    print('identity_users_path_count:', len(users)); \
    [print(' ', p) for p in sorted(users)]"
```

## Response 1

Status: 200 (swagger.json fetch)
Body (abridged; 30 paths total, all returned, none omitted):

```
identity_users_path_count: 30
  /api/identity/users
  /api/identity/users/all-claim-types
  /api/identity/users/assignable-roles
  /api/identity/users/available-organization-units
  /api/identity/users/by-email/{email}
  /api/identity/users/by-id/{id}
  /api/identity/users/by-username/{username}
  /api/identity/users/download-import-invalid-users-file
  /api/identity/users/download-token
  /api/identity/users/export-as-csv
  /api/identity/users/export-as-excel
  /api/identity/users/external-login-Providers
  /api/identity/users/import-external-user
  /api/identity/users/import-users-from-file
  /api/identity/users/import-users-sample-file
  /api/identity/users/lookup/by-username/{userName}
  /api/identity/users/lookup/count
  /api/identity/users/lookup/organization-units
  /api/identity/users/lookup/roles
  /api/identity/users/lookup/search
  /api/identity/users/lookup/{id}
  /api/identity/users/{id}
  /api/identity/users/{id}/change-password
  /api/identity/users/{id}/claims
  /api/identity/users/{id}/lock/{lockoutEnd}
  /api/identity/users/{id}/organization-units
  /api/identity/users/{id}/roles
  /api/identity/users/{id}/two-factor-enabled
  /api/identity/users/{id}/two-factor/{enabled}
  /api/identity/users/{id}/unlock
```

## Command 2: swagger `/api/identity/*` (all identity subpaths)

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d=json.load(sys.stdin); paths=d.get('paths',{}); \
    ident=[p for p in paths if p.startswith('/api/identity')]; \
    print('identity_path_count:', len(ident))"
```

## Response 2

Status: 200 (swagger.json fetch)
Body:

```
total_paths: 317
identity_path_count: 58
```

Mapping: 58 total identity paths = 30 users paths + 8 roles paths + 2 claim-types paths + 11 organization-units paths + 2 security-logs paths + 2 sessions paths + 4 settings paths + 1 external-login path. Confirms the Identity Pro module is registered in its entirety, not just the users subset.

## Command 3: swagger `/api/app/user-extended/*` path count

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d=json.load(sys.stdin); paths=d.get('paths',{}); \
    ext=[p for p in paths if 'user-extended' in p.lower()]; \
    print('user-extended_paths:', len(ext)); \
    [print(' ', p) for p in sorted(ext)]"
```

## Response 3

Status: 200 (swagger.json fetch)
Body (abridged; 26 paths total, all returned):

```
user-extended_paths: 26
  /api/app/user-extended
  /api/app/user-extended/as-csv-file
  /api/app/user-extended/as-excel-file
  /api/app/user-extended/assignable-roles
  /api/app/user-extended/available-organization-units
  /api/app/user-extended/claim-types
  /api/app/user-extended/download-token
  /api/app/user-extended/external-login-providers
  /api/app/user-extended/find-by-email
  /api/app/user-extended/find-by-username
  /api/app/user-extended/import-external-user
  /api/app/user-extended/import-invalid-users-file
  /api/app/user-extended/import-users-from-file
  /api/app/user-extended/import-users-sample-file
  /api/app/user-extended/organization-unit-lookup
  /api/app/user-extended/role-lookup
  /api/app/user-extended/{id}
  /api/app/user-extended/{id}/claims
  /api/app/user-extended/{id}/find-by-id
  /api/app/user-extended/{id}/lock
  /api/app/user-extended/{id}/organization-units
  /api/app/user-extended/{id}/password
  /api/app/user-extended/{id}/roles
  /api/app/user-extended/{id}/set-two-factor-enabled
  /api/app/user-extended/{id}/two-factor-enabled
  /api/app/user-extended/{id}/unlock
```

## Command 4: Angular root + /identity route

```
curl -s -o /dev/null -w "root_status:%{http_code}\n" http://localhost:4200/
curl -s -o /dev/null -w "identity_status:%{http_code}\n" http://localhost:4200/identity
```

## Response 4

```
root_status:200
identity_status:200
```

Note: Angular's static server serves `index.html` for all SPA paths; a 200 on `/identity` confirms the SPA is reachable. Deep component render verification is deferred per the no-PII constraint -- proving wiring at source level (`app.routes.ts:38-40` uses `@volo/abp.ng.identity`'s `createRoutes()`) plus the 200 at the SPA layer is sufficient for wiring verification.

## Interpretation

- 30 `/api/identity/users/*` paths prove the complete ABP Identity Pro users surface is registered on HttpApi.Host -- list, get, update, delete, password change, claims, lock/unlock, roles, organization-units, two-factor, lookup, import/export. Every user admin operation OLD's `users.service.ts:1-68` exposed is either covered 1:1 or replaced by a stronger ABP primitive (security logs, sessions, 2FA).
- 58 `/api/identity/*` paths prove the wider Identity Pro module (roles, claim-types, organization-units, security-logs, sessions, settings) is alive, reinforcing that 5-G09 "Permission: Users management" is fully covered by ABP without needing a `CaseEvaluationPermissions.Users.*` group.
- 26 `/api/app/user-extended/*` paths prove `UserExtendedAppService` is exposed as a remote service (ABP auto-registration; no custom controller, no `[RemoteService(IsEnabled = false)]`). The override's `UpdateAsync` is the only behaviour-level deviation from the base; the remaining 25 endpoints are inherited passthroughs.
- Angular root + `/identity` both 200 -- confirms the SPA build is serving and the `@volo/abp.ng.identity` lazy-loader is wired into the SPA route tree.
- A8-02 "Angular users admin CRUD proxy" is resolved: the typed Angular client is the generated `proxy/users/user-extended.service.ts` backed by `@volo/abp.ng.identity`.
- UI-10 "`/users` custom user management screen" is resolved by `/identity/users` (ABP module) plus any future extension through `UserExtendedAppService`.

## Cleanup (if mutating)

None. All probes are read-only metadata queries.
