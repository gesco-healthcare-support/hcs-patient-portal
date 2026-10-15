# PhiAccess -- who read which patient's data

## What lives here

The PHI access log: one row per read that discloses protected health information. It exists because
the HTTP audit log does not record GET requests (`IsEnabledForGetRequests` is false, the ABP default),
so before 2026-10-13 nobody could say who opened a patient's record, revealed an SSN or downloaded a
document.

| File | Purpose |
|---|---|
| `PhiAccessLogEntry.cs` | One row: who, which patient / appointment / file, what action, when. Ids only |
| `PhiAccessTarget.cs` | The records one read touched |
| `PhiAccessRecorder.cs` | Writes the rows; app services reach it through `CaseEvaluationAppService.PhiAccessRecorder` |

The action enum, `PhiAccessAction`, is in Domain.Shared because its integers are a stored contract
(pinned by `PhiAccessActionContractTests`).

## Invariants

- **Ids only.** A row never holds a name, an SSN or any other PHI, so the log is not itself a second
  copy of the data it protects.
- **Append-only, never deleted.** The recorder only inserts. Nothing updates or deletes a row, and the
  table has no foreign keys, so a row outlives anything it points at.
- **Fails closed.** The recorder writes in its own unit of work, and a failed write throws, so the
  caller gets an error instead of the data. Do not wrap a recorder call in a try/catch: a read that
  cannot be recorded must not happen.
- **Record after the access check, before the data leaves.** A refused caller writes nothing; a later
  failure (a file missing from storage) leaves an extra row, never a missing one. For a download,
  record before opening the file.
- **An export writes one row per appointment in the file**, all with the same time.
- **A repeat VIEW within `PhiAccessRecorder.RepeatViewWindow` (1 minute) is recorded once** -- same
  action, user, impersonator and target. A screen that reloads a record does not flood the log with
  copies of one read. Only `PatientViewed` and `AppointmentViewed` collapse; downloads and exports
  are recorded every time, because each one puts another copy of the data somewhere.
- **A browser-built file is recorded by asking first.** The staff appointment list builds its CSV in
  the browser from rows already on screen, so the server would never see it. The page calls
  `POST api/app/appointment-list-exports/csv` (`AppointmentListExportAppService`, which read-guards
  every id) and builds the file only if that succeeds (`AppointmentListCsvExported`).

## What is logged, and what is not

Logged: opening a patient record (four reads, including the People screen's detail panel since
2026-10-15), the SSN reveal, opening an appointment (by id or by confirmation number), document and
packet downloads, the PDF and CSV report exports, the appointment list's CSV export, and the
demographics PDF. A download of a requested document whose file has not arrived yet is refused
(`Document.NotUploadedYet`) before anything is recorded, because nothing is disclosed.

Not logged, on purpose: list and search screens (they show many masked rows, and logging every page
load is what the ABP default avoids), and a patient reading their own profile. Writes are already in
the HTTP audit log. There is no screen to read this log yet.

Known gap, a product decision rather than a bug: the People screen's patient LIST
(`GET api/app/patients`, asked for 500 at a time) returns full patient records, SSN masked, and is
not logged. The recommended answer is to send less (filter and page on the server, a slim list row)
rather than to log every row of every page load.
