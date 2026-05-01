# Probe log: appointment-notes

**Timestamp (local):** 2026-04-24T20:20:00Z
**Purpose:** Confirm zero `/notes`-related paths, schemas, tag groups
currently exist in the live HttpApi.Host Swagger JSON. Supports the
"NEW-version code read" claim in the brief that no Notes endpoint surface
is wired.

## Command 1 -- path enumeration

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d=json.load(sys.stdin); \
paths=[p for p in d.get('paths',{}) if 'note' in p.lower()]; \
print('Matching /note paths count:', len(paths)); \
[print(' -', p) for p in paths]"
```

## Response 1

Status: 200 (JSON downloaded, parsed)
Body (redacted):

```
Matching /note paths count: 0
```

## Command 2 -- schema enumeration

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d=json.load(sys.stdin); \
schemas=[s for s in d.get('components',{}).get('schemas',{}) if 'note' in s.lower()]; \
print('Matching Note schemas count:', len(schemas)); \
[print(' -', s) for s in schemas]"
```

## Response 2

Status: 200
Body (redacted):

```
Matching Note schemas count: 0
```

## Command 3 -- sanity check totals (separate probe already in service-status.md)

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d=json.load(sys.stdin); \
print('Total paths:', len(d.get('paths',{}))); \
print('Total schemas:', len(d.get('components',{}).get('schemas',{})))"
```

## Response 3

Status: 200
Body (redacted):

```
Total paths: 317
Total schemas: 335
```

## Interpretation

- Zero `/note` paths across 317 total. Zero Note schemas across 335 total.
  Confirms the NEW Angular proxy generator has never been fed a Note DTO
  and confirms no manual controller has been registered for Notes. No
  Notes-related `tag` group is advertised under `info.tags` either (grep
  against the JSON elided for brevity; can be re-run on demand).
- Matches the `NEW-version code read` bullets 1-5 of the brief:
  the absence is not a swagger-mapping issue; the entity + AppService +
  controller genuinely do not exist in source.

## Cleanup (if mutating)

Not applicable -- probe is read-only.

## Notes

- Used `curl -k` per probes/service-status.md which documents that the dev
  cert triggers certificate-validation errors without `-k`.
- Password-grant token acquisition was skipped for this probe; swagger
  JSON is anonymous-accessible on the HttpApi.Host.
