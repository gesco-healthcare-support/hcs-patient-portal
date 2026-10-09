# Uploads -- malware scan before any user file is stored

Every user-supplied file is scanned by clamd before it reaches MinIO. Fails CLOSED: an
unreachable or confused scanner refuses the upload (503), a found signature refuses it (422),
and nothing is stored in either case.

## What lives here

| Path | Purpose |
|---|---|
| `IUploadScanner.cs` | The scan seam. Throws `UploadScanUnavailableException` rather than ever answering "clean" without a verdict |
| `ClamdUploadScanner.cs` | Minimal clamd client speaking INSTREAM over TCP; our own client, not nClam |
| `ClamdOptions.cs` | clamd host, port and timeout settings |
| `UploadScanResult.cs` | The verdict: clean, or the signature found |
| `UploadScanUnavailableException.cs` | Raised whenever no verdict could be reached |
| `ScanningMinioBlobProvider.cs` | MinIO provider that scans BEFORE saving; set per container for the seven user-file containers. Generated packets keep the plain provider |

## Gotchas

- The scan lives in the blob provider, not in each app service, so a new upload path into a
  scanned container is covered without anyone remembering to call a scanner.
- Uploads return 503 for about a minute after a clamd restart while signatures load. That is the
  fail-closed behaviour working, not a broken upload.
