---
feature: packet-access-by-role
date: 2026-05-29
status: in-progress
base-branch: main
related-issues: []
sequence: 3 of 6 (backend; independent of the booking-form cluster)
branch: feat/packet-access-by-role
---

## Goal

Two changes to the appointment-packet system:
- **(A) Generate the Attorney-CE packet for EVERY appointment type** (today it
  is gated to PQME/AME and is silently broken for "Panel QME").
- **(B) Enforce per-role packet access** in the appointment view: Patient sees
  only the Patient packet; Applicant Attorney / Defense Attorney / Claim
  Examiner see only the Attorney-CE packet; internal users (Clinic Staff /
  Staff Supervisor / IT Admin / Doctor) see all three; no external role may
  see or download the Doctor packet.

## Context

Adrian directive (2026-05-29): for now, AA / DA / CE get the Attorney-CE packet
and can access it from the appointment view for ALL appointment types, not just
PQME/AME. Per-type customization comes later.

### Verified current state (code map + LIVE DB evidence, 2026-05-29)

**Generation is broken/over-narrow today.** Live DB proof from the running
`main` stack (`dbo.AppAppointmentPackets`):

| Appt | Type | Packets present | AttyCE? |
|---|---|---|---|
| A00001 | Qualified Medical Examination (QME) | Patient, Doctor | NO (QME excluded by design today) |
| A00003 | **Panel QME** | Patient, Doctor | **NO -- BUG** |

A00003 is **Panel QME**, which *should* get the Attorney-CE packet, but does
not. Root cause: `GenerateAppointmentPacketJob.IsAttorneyClaimExaminerType`
(`...Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs:71-75`)
tests `name.ToUpperInvariant().Contains("PQME")`, and "PANEL QME" does not
contain the contiguous substring "PQME" (the space breaks it). The comment at
line 65 wrongly claims the match works. **No appointment in the DB currently
has an Attorney-CE packet at all.** Change (A) -- removing the gate -- fixes
both the "all types" requirement and this silent Panel-QME miss.

**View/download has no per-role filtering.**
`AppointmentPacketsAppService` (`src/.../Application/AppointmentDocuments/`)
methods are guarded only by `CaseEvaluationPermissions.AppointmentPackets.Default`
(which all four external roles hold). `GetListByAppointmentAsync` (~87-101)
returns ALL kinds; `DownloadByKindAsync` (~103-143) downloads any kind the
caller names; the legacy `GetByAppointmentAsync` (~51) and `DownloadAsync` (~65)
are hard-coded to `Kind == Patient`. So today any external role can list and
download the Doctor packet.

**Packet generation log statements** (`GenerateAppointmentPacketJob.cs`):
- success ~213: "GenerateAppointmentPacketJob: appointment {AppointmentId} kind
  {Kind} generated (... bytes ...); PacketGeneratedEto published."
- failure ~232: "...kind {Kind} failed; marking Failed (no retry)."
- The Panel-QME miss produces NO log line -- the kind is silently never added
  to `kindsToGenerate`. That is the "nothing happens" the maintainer noticed.

### What does NOT change (verified)

- **PacketKind enum** (`Domain.Shared/AppointmentDocuments/PacketKind.cs`):
  `Patient=1, Doctor=2, AttorneyClaimExaminer=3`. Complete.
- **Email path:** `PatientPacketEmailHandler` (Patient -> patient),
  `AttyCEPacketEmailHandler` (AttyCE -> AA/DA/CE; CE always resolved via
  `AppointmentRecipientResolver`), Doctor never emailed, internal users get no
  packet email. Correct as-is.
- **Angular** `appointment-packet.component` renders whatever the API returns
  (no client-side kind filter) and downloads via `DownloadByKindAsync`. Once the
  server filters, the view is correct with no Angular change. (The Regenerate
  button uses `AppointmentPackets.Regenerate`, internal-only.)
- **Permissions:** no new permission needed -- per-role access is behavior on
  top of the existing `AppointmentPackets.Default`.

## Approach

### (A) Generate Attorney-CE for all types
In `GenerateAppointmentPacketJob.cs` (~137-141) replace the gated list with an
unconditional one:
```
var kindsToGenerate = new List<PacketKind>
    { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer };
```
Delete `IsAttorneyClaimExaminerType` (~71-75) and its now-wrong doc comment
(~59-70) -- it has no other callers.

### (B) Per-role packet allow-list
Add a private helper to `AppointmentPacketsAppService` and apply it at the list
and download boundaries:
```
private IReadOnlyCollection<PacketKind> GetAllowedKindsForCaller()
{
  var roles = CurrentUser.Roles ?? Array.Empty<string>();
  if (BookingFlowRoles.IsInternalUserCaller(roles))
      return new[] { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer };
  if (roles.Any(r => r == "Patient"))
      return new[] { PacketKind.Patient };
  if (roles.Any(r => r is "Applicant Attorney" or "Defense Attorney" or "Claim Examiner"))
      return new[] { PacketKind.AttorneyClaimExaminer };
  return Array.Empty<PacketKind>();
}
```
- `GetListByAppointmentAsync`: filter the returned entities to allowed kinds.
- `DownloadByKindAsync`: throw (e.g. `AbpAuthorizationException` /
  `UserFriendlyException`) if the requested kind is not allowed.
- Also guard / confirm the legacy `DownloadAsync` + `GetByAppointmentAsync`
  (Patient-hardcoded): a non-Patient external caller must not pull the Patient
  packet through them. Simplest: confirm they are unused by the live Angular and
  leave them, OR apply the same allow-list. Build phase verifies usage first.

Role-name source: there is no central Roles consts class; the canonical strings
are in `ExternalUserRoleDataSeedContributor.cs:35-38` ("Patient", "Claim
Examiner", "Applicant Attorney", "Defense Attorney"). Internal detection reuses
`BookingFlowRoles.IsInternalUserCaller` (`...Application/Appointments/`).

**Alternatives rejected:**
- Per-kind Angular filtering only: the API would still return/serve forbidden
  kinds; server-side is the real control. Reject.
- A new ABP permission per kind: heavier than needed; access is role-derived
  behavior, not a grantable permission. Reject.
- Customizing AttyCE per appointment type now: Adrian defers this; all types get
  it for now. Reject.

## Tasks

- T1: Generate the Attorney-CE packet for all appointment types.
  - approach: tdd
  - files-touched: src/.../Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs (remove gate ~137-141; delete IsAttorneyClaimExaminerType ~59-75)
  - acceptance: a unit test for the kind-selection logic asserts all three kinds
    are generated for a non-PQME/AME type (e.g. QME) and for "Panel QME"; the
    `IsAttorneyClaimExaminerType` method is gone.

- T2: Per-role packet view/download allow-list.
  - approach: tdd
  - files-touched: src/.../Application/AppointmentDocuments/AppointmentPacketsAppService.cs (add GetAllowedKindsForCaller; filter GetListByAppointmentAsync; guard DownloadByKindAsync; guard/verify legacy DownloadAsync + GetByAppointmentAsync)
  - acceptance: unit tests cover each role bucket both directions -- internal
    lists/downloads all 3; Patient gets only Patient (download of Doctor/AttyCE
    throws); AA/DA/CE get only AttyCE (download of Patient/Doctor throws); no
    external role can list or download Doctor.

- T3: Confirm the Angular view reflects the server-filtered list.
  - approach: test-after
  - files-touched: angular/src/app/appointment-packet/appointment-packet.component.* (verify it renders the filtered server list; no client change expected)
  - acceptance: logged in as Patient -> only Patient packet shown; as AA/DA/CE ->
    only Attorney-CE; as internal -> all three.

## Risk / Rollback

- Blast radius: packet generation volume rises (AttyCE for all types, plus the
  Panel-QME fix) and packet visibility narrows for external roles. A filter bug
  could over-hide (internal loses a kind) or under-hide (external still sees
  Doctor) -- T2 tests both directions.
- Existing approved appointments (A00001 QME, A00003 Panel QME) have no AttyCE
  packet (generated before the fix). They will only get one via the staff
  Regenerate button -- forward-only by default; backfill is out of scope.
- Rollback: revert the PR; generation + visibility return to current behavior.

## Verification

Rebuild api + db-migrator (`docker compose up -d --build api db-migrator`), then:
1. Approve a NON-PQME/AME appointment (e.g. a QME) and confirm the packet job
   now generates Patient + Doctor + Attorney-CE (DB:
   `dbo.AppAppointmentPackets` has kind=3; api log shows kind AttorneyClaimExaminer
   generated). NOTE: approval also fires the stakeholder approval emails to the
   real AA/DA/CE inboxes -- confirm with Adrian before triggering.
2. As Patient: appointment view shows only the Patient packet; Doctor download
   denied.
3. As AA/DA/CE: only the Attorney-CE packet; Patient/Doctor download denied.
4. As internal staff: all three visible/downloadable.
5. xUnit suite green.
