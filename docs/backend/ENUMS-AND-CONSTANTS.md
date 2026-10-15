# Enums & Constants

> Purpose: Consolidate all domain enums and max-length constants for the CaseEvaluation solution. Audience: backend developer.

[Home](../index.md) > [Backend](./) > Enums & Constants

---

This page consolidates all enums and max-length constants across the codebase. For per-entity details, see the feature CLAUDE.md files linked in [Domain Model](../database/EF-CORE-DESIGN.md).

## Enums

Enums live in `src/HealthcareSupport.CaseEvaluation.Domain.Shared/`: 11 in `Enums/`, the rest beside their feature
(tolerated -- do not relocate). The table is generated from the enum declarations, so it lists every enum and its
numeric values exactly; the last column lists the entity properties typed with it (`--` where the enum is used only
in DTOs, events or services).

30 enums (generated 2026-09-30 from `src/HealthcareSupport.CaseEvaluation.Domain.Shared/`; re-list with `git grep -hoE 'public enum [A-Za-z0-9_]+' -- 'src/HealthcareSupport.CaseEvaluation.Domain.Shared/*.cs'`):

| Enum | File | Values | Entity properties of this type |
|---|---|---|---|
| `AccessType` | `Enums/AccessType.cs` | View(23), Edit(24) | `AppointmentAccessor.AccessTypeId` |
| `AppNotificationType` | `Notifications/AppNotificationType.cs` | AppointmentRequested(1), ChangeRequestSubmitted(2), QuerySubmitted(3), DocumentUploaded(4), InfoRequestResubmitted(5) | `AppNotification.NotificationType` |
| `AppointmentMaxTimeCategory` | `Enums/AppointmentMaxTimeCategory.cs` | Pqme(0), Ame(1), Other(2) | `AppointmentType.MaxTimeCategory` |
| `AppointmentStatusType` | `Enums/AppointmentStatusType.cs` | Pending(1), Approved(2), Rejected(3), NoShow(4), CancelledNoBill(5), CancelledLate(6), RescheduledNoBill(7), RescheduledLate(8), CheckedIn(9), CheckedOut(10), Billed(11), RescheduleRequested(12), CancellationRequested(13), InfoRequested(14), NotSeen(15) | `Appointment.AppointmentStatus`, `AppointmentChangeRequest.CancellationOutcome`, `CaseTrackerMissingIntakeReport.Status` |
| `BookingStatus` | `Enums/BookingStatus.cs` | Available(8), Booked(9), Reserved(10) | `DoctorAvailability.BookingStatusId` |
| `BookingSubmitMode` | `Enums/BookingSubmitMode.cs` | Create(0), ReSubmit(1), Reval(2), ReBook(3) | -- |
| `CaseTrackerFeedAlertKind` | `Notifications/Events/CaseTrackerFeedAlertEto.cs` | SilenceStarted(0), SilenceCleared(1), StallStarted(2), StallCleared(3), CursorAhead(4), SkipReported(5), InboundAttendanceRefused(6) | -- |
| `CaseTrackerInboundRefusalReason` | `Enums/CaseTrackerInboundRefusalReason.cs` | IntegrationDisabled(0), AppointmentNotFound(1) | -- |
| `CaseTrackerPushAlertKind` | `Notifications/Events/CaseTrackerPushFailedEto.cs` | DeadLettered(0), StillRetrying(1) | -- |
| `ChangeRequestConsentStatus` | `AppointmentChangeRequests/ChangeRequestConsentStatus.cs` | NotRequired(0), Pending(1), Approved(2), Rejected(3), Expired(4) | `AppointmentChangeRequest.SideAConsentStatus`, `AppointmentChangeRequest.SideBConsentStatus`, `ChangeRequestConsentRound.SideAConsentStatus`, `ChangeRequestConsentRound.SideBConsentStatus` |
| `ChangeRequestSide` | `AppointmentChangeRequests/ChangeRequestSide.cs` | SideA(1), SideB(2) | `AppointmentChangeRequest.RequestingSide` |
| `ChangeRequestType` | `AppointmentChangeRequests/ChangeRequestType.cs` | Cancel(1), Reschedule(2) | `AppointmentChangeRequest.ChangeRequestType` |
| `CustomFieldType` | `Enums/CustomFieldType.cs` | Alphanumeric(12), Numeric(13), Picklist(14), Tickbox(15), Date(16), Radio(17), Time(18) | `CustomField.FieldType` |
| `DocumentStatus` | `AppointmentDocuments/DocumentStatus.cs` | Uploaded(1), Accepted(2), Rejected(3), Pending(4) | `AppointmentDocument.Status` |
| `EvaluationKind` | `Appointments/EvaluationKind.cs` | Evaluation(1), ReEvaluation(2) | `Appointment.EvaluationKind` |
| `EvaluationType` | `Enums/EvaluationType.cs` | Normal(0), Re(1), Both(2) | `AppointmentType.EvaluationType` |
| `ExternalUserType` | `ExternalSignups/ExternalUserType.cs` | Patient(1), ClaimExaminer(2), ApplicantAttorney(3), DefenseAttorney(4) | `Invitation.UserType` |
| `Gender` | `Enums/Gender.cs` | Unspecified(0), Male(1), Female(2), Other(3) | `Appointment.PatientGenderId`, `Doctor.Gender`, `Patient.GenderId` |
| `InfoRequestStatus` | `AppointmentInfoRequests/InfoRequestStatus.cs` | Open(1), Resolved(2) | `AppointmentInfoRequest.Status` |
| `IntegrationMessageType` | `Integration/CaseTracker/IntegrationMessageType.cs` | Intake(1), DocumentUpdate(2) | `IntegrationOutboxItem.MessageType` |
| `IntegrationOutboxStatus` | `Integration/CaseTracker/IntegrationOutboxStatus.cs` | Pending(1), Sent(2), Failed(3), Resolved(4) | `IntegrationOutboxItem.Status` |
| `InvitationStatus` | `Invitations/InvitationStatus.cs` | Pending(0), Accepted(1), Expired(2), Revoked(3) | -- |
| `NotificationKind` | `Appointments/Notifications/NotificationKind.cs` | Submitted(1), Approved(2), Rejected(3), RequestSchedulingReminder(5), CancellationRescheduleReminder(6), AppointmentDayReminder(7), DocumentUploaded(8), DocumentAccepted(9), DocumentRejected(10), JdfAutoCancelled(11), PackageDocumentReminder(12), PendingDailyDigest(13), InternalStaffQueueDigest(14), DueDateApproachingReminder(15), DueDateDocumentIncompleteReminder(16), PacketAttyCEDelivery(17), IntakeChanged(18) | -- |
| `NotificationOutboxStatus` | `Notifications/Outbox/NotificationOutboxStatus.cs` | Pending(1), Sent(2), Failed(3) | `NotificationOutboxItem.Status` |
| `PacketGenerationStatus` | `AppointmentDocuments/PacketGenerationStatus.cs` | Generating(1), Generated(2), Failed(3) | `AppointmentPacket.Status` |
| `PacketKind` | `AppointmentDocuments/PacketKind.cs` | Patient(1), Doctor(2), AttorneyClaimExaminer(3) | `AppointmentPacket.Kind`, `GenerateAppointmentPacketJob.Kind`, `NotificationOutboxItem.PacketKind` |
| `PhoneNumberType` | `Enums/PhoneNumberType.cs` | Work(28), Home(29) | `Appointment.PatientPhoneNumberTypeId`, `Patient.PhoneNumberTypeId` |
| `RecipientRole` | `Appointments/Notifications/RecipientRole.cs` | Patient(1), ApplicantAttorney(2), DefenseAttorney(3), ClaimExaminer(4), InsuranceCarrierContact(5), OfficeAdmin(6), Employer(7) | -- |
| `RequestStatusType` | `Enums/RequestStatusType.cs` | Pending(25), Accepted(26), Rejected(27) | `AppointmentChangeRequest.RequestStatus` |
| `RequiredDocumentState` | `AppointmentDocuments/RequiredDocumentState.cs` | NotUploaded(0), AwaitingReview(1), Rejected(2) | -- |

Several enums keep the numbers of the previous system's centralized enum table (for example `BookingStatus` starts at
8, `CustomFieldType` at 12, `RequestStatusType` at 25; see the comments in `Enums/CustomFieldType.cs` and
`Enums/RequestStatusType.cs`), which is why their values do not start at 0 or 1. Stored rows hold these numbers, so
never renumber a member.

## Max-Length Constants

Constants are defined per entity in `src/HealthcareSupport.CaseEvaluation.Domain.Shared/{Feature}/{Entity}Consts.cs`.
The six most-used entities, read from those files on 2026-09-30 (re-read one with
`grep -oE '[A-Za-z]+MaxLength = [0-9]+' <file>`):

| File | `...MaxLength` constants |
|---|---|
| `Appointments/AppointmentConsts.cs` | PanelNumber 50, RequestConfirmationNumber 50, InternalUserComments 250, PartyEmail 256, RefferedBy 50, Reason 1000 |
| `Doctors/DoctorConsts.cs` | FirstName 50, LastName 50, Email 49 |
| `Patients/PatientConsts.cs` | FirstName 50, LastName 50, MiddleName 50, Email 50, PhoneNumber 20, SocialSecurityNumber 20, Address 100, City 50, ZipCode 15, CellPhoneNumber 12, Street 255, InterpreterVendorName 255, ApptNumber 100, OthersLanguageName 100 |
| `Locations/LocationConsts.cs` | Name 50, Address 100, City 50, ZipCode 15, FacilityId 50 |
| `ApplicantAttorneys/ApplicantAttorneyConsts.cs` | FirstName 50, LastName 50, FirmName 50, FirmAddress 100, WebAddress 100, PhoneNumber 20, FaxNumber 19, Street 255, City 50, ZipCode 10, Email 100 |
| `WcabOffices/WcabOfficeConsts.cs` | Name 50, Abbreviation 50, Address 100, City 50, ZipCode 15 |

**Notable:** `Doctor.Email` has max length 49, not 50 or 100. `StateConsts.cs` defines no max-length constants.

---

**Related:**

- [Domain Model](../database/EF-CORE-DESIGN.md) -- entity index with CLAUDE.md links
- [Appointment Lifecycle](../business-domain/APPOINTMENT-LIFECYCLE.md) -- AppointmentStatusType state machine
- [Doctor Availability](../business-domain/DOCTOR-AVAILABILITY.md) -- BookingStatus lifecycle
