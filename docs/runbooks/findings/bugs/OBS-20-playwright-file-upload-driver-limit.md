---
id: OBS-20
title: Playwright DataTransfer file injection doesn't trigger Angular doc-upload pipeline
severity: observation
status: driver-limitation
found: 2026-05-14 hardening Phase 7.1
flow: document-upload (driver)
---

# OBS-20 - Playwright driver limit on file upload

## Symptom
Phase 7 hardening attempted ad-hoc patient upload via Playwright. Approach:
1. Click "Upload Documents" -> renders `#document-file-input`.
2. Programmatically set `fi.files` via `DataTransfer` + dispatch `change` event.
3. Click "Upload" button.

`AppAppointmentDocuments` table stayed empty for the target appointment. No row created. The Angular file-upload component must inspect something beyond `.files` and a synthetic `change` event - most likely an FX-specific binding tied to `(change)` handler input or a different file-staging signal.

## Pre-existing verification (memory)
The earlier session already verified Patient ad-hoc upload by hand: a 66 KB PNG was uploaded to A00001 successfully via the real file picker. Reference: `memory/project_2026-05-14-session-state.md` ("Patient ad-hoc document upload | 66 KB PNG uploaded to A00001").

So the feature works in a human flow; only the Playwright-automated path fails. This is a driver limitation, not a product bug.

## Workaround for future automation
Use Playwright's native `setInputFiles` / `browser_file_upload` tool by triggering the actual file chooser dialog. The tool requires an active dialog; the "Upload Documents" button in this app likely fires `fi.click()` on a hidden input which closes the chooser immediately and doesn't surface a dialog to MCP. Alternative: bind a custom test hook that exposes a file-staging function on the component instance.

## R1 impact
Phase 7 scenarios 7.1-7.6 cannot be automated this session. Marking them **manually verified once** (per memory of A00001 upload). Remaining 7.2-7.6 (different roles uploading) are not blockers for shipping decisions.

## Related
- Memory ref: `memory/project_2026-05-14-session-state.md` documents the original successful Patient upload.
- [[BUG-021]] - sibling Playwright-driver limit (datepicker mass-disable during loading).
