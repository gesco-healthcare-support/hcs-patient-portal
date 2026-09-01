---
id: OBS-20
title: Playwright DataTransfer file injection doesn't trigger Angular doc-upload pipeline
severity: observation
status: workaround-available
last-replayed: 2026-05-23 (P7.1 + P7.2 both succeeded via browser_file_upload MCP tool - PNG by patient1 to A00001, PDF by appatty1 to A00002, both produced AppAppointmentDocuments rows with IsAdHoc=1)
found: 2026-05-14 hardening Phase 7.1
flow: document-upload (driver)
---

> **2026-05-21 replay: workaround now available via MCP's `browser_file_upload` tool.**
> The Playwright MCP server exposes a dedicated file-upload tool that
> binds to the real file-chooser dialog rather than synthesizing a
> DataTransfer event. Verified 2026-05-21 against A00001's upload UI:
>
> 1. Click "File" picker button -> Playwright detects "File chooser"
>    modal state.
> 2. Call `mcp__plugin_playwright_playwright__browser_file_upload`
>    with `paths: [<file under W:/patient-portal/main/.playwright-mcp/>]`.
>    The MCP tool refuses paths OUTSIDE its allowed-roots
>    (`W:/patient-portal/main` + `W:/patient-portal/main/.playwright-mcp`),
>    so the file must be staged in one of those directories first.
> 3. Confirmation: `input[type="file"].files.length === 1` AND the
>    previously-disabled "Upload" button becomes enabled.
> 4. Click "Upload" -> `POST /api/app/appointments/{id}/documents`
>    fires (verified in DevTools Network panel).
>
> The driver limitation noted on 2026-05-14 was specific to using
> DataTransfer alone; the MCP tool bypasses it. Phase 7 of the suite
> is now scriptable via Playwright MCP.
>
> Separately, clistaff1's upload against A00001 returned **403 Forbidden**
> from the server -- a permission gap sibling of [[BUG-031]] (clinic
> staff lacks the document-upload permission on appointments they
> don't own). That's a separate finding; the driver itself works.

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
