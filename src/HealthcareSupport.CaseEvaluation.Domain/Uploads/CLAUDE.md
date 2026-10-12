# Uploads -- malware scanning of stored uploads

## What lives here

Upload malware scanning (B11). Every upload is scanned by clamd before it is stored; the scan
fails closed, so an unreachable scanner refuses the upload and an infected file is rejected.

| File | Purpose |
|---|---|
| `IUploadScanner.cs` | Seam for scanning a stream |
| `ClamdUploadScanner.cs` | Minimal clamd INSTREAM client over TCP |
| `ClamdOptions.cs` | Host, port and timeout settings for clamd |
| `UploadScanResult.cs` | Clean or infected outcome of one scan |
| `UploadScanUnavailableException.cs` | Raised when clamd cannot be reached (fail closed) |
| `ScanningMinioBlobProvider.cs` | MinIO blob provider that scans before storing |
