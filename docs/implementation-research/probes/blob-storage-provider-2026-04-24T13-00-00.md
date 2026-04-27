# Probe log: blob-storage-provider

**Timestamp (local):** 2026-04-24T13:00:00
**Purpose:** confirm that (a) the File Management + BlobStoring module surface is live (proves the ABP primitives are installed) and (b) no NEW business code yet consumes `IBlobContainer` (proves the gap is still live on 2026-04-24 `main`).

## Probe 1 -- Swagger scan for file-management / blob paths

### Command
```
curl -sk https://localhost:44327/swagger/v1/swagger.json -o /tmp/swagger-probe.json
python -c "import json; d=json.load(open('/tmp/swagger-probe.json')); paths=[p for p in d['paths'] if 'file-management' in p or 'blob' in p.lower() or 'file-descriptor' in p.lower() or '/files/' in p.lower() or '/documents' in p.lower()]; print('\n'.join(paths)); print('Total matched:', len(paths)); print('Total all paths:', len(d['paths']))"
```

### Response
Status: 200 (swagger.json download), 317 total paths in the document, 12 matched:

```
/api/file-management/directory-descriptor/{id}
/api/file-management/directory-descriptor/sub-directories
/api/file-management/directory-descriptor
/api/file-management/directory-descriptor/move
/api/file-management/file-descriptor/{id}
/api/file-management/file-descriptor
/api/file-management/file-descriptor/upload
/api/file-management/file-descriptor/move
/api/file-management/file-descriptor/pre-upload-info
/api/file-management/file-descriptor/content
/api/file-management/file-descriptor/download/{id}/token
/api/file-management/file-descriptor/download/{id}
```

Zero `blob`, `/files/`, or `/documents` paths (the File Management Pro module is live; no bespoke document API exists yet).

### Interpretation

Proves the File Management module's HTTP surface is auto-wired at `/api/file-management/**` without any `[RemoteService(false)]` suppression. The module depends-on graph (verified in `src/HealthcareSupport.CaseEvaluation.HttpApi/CaseEvaluationHttpApiModule.cs:12,31`) resolves to 12 hand-off endpoints for directory + file descriptor CRUD, download-token issue, and upload. This is not a gap -- it is a wired-but-unused REST surface that appointment-document flows could either (a) sit on top of, or (b) coexist with by using a bespoke `[BlobContainerName]`-annotated container set. The recommendation above chooses (b).

## Probe 2 -- IBlobContainer consumer grep in NEW code

### Analysis

```
pattern: IBlobContainer<|BlobContainerFactory|BlobContainerName\(
path: W:/patient-portal/main/src
glob: *.cs
result: No matches found
```

### Interpretation

Confirms the static claim from `docs/gap-analysis/06-cross-cutting-backend.md:101` (and its Delta row at line 145) that zero NEW-side business code consumes the blob abstractions. All lockfile hits are transitive NuGet metadata, not source code. The gap is live on 2026-04-24 `main`.

## Cleanup

No mutating probes. No state to revert. Swagger JSON dump left at `/tmp/swagger-probe.json` for Phase 3 cross-referencing; not persisted state.
