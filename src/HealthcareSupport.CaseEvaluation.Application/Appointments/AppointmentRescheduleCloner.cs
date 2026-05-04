using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11c (2026-05-04) -- pure scalar-clone helper used when a
/// supervisor approves an external user's reschedule request.
///
/// OLD's flow (verified against
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs</c>:
/// reschedule-approval block) creates a brand-new Appointment row that
/// copies every scalar field of the original (party emails, claim and
/// injury details on the appointment level, status overrides, due date,
/// internal user comments) plus the original's <c>RequestConfirmationNumber</c>
/// when <paramref name="sameConfirmationNumber"/> is true. The new row's
/// <c>OriginalAppointmentId</c> points back at the source so the chain
/// is auditable across multiple reschedules. The new row's
/// <c>DoctorAvailabilityId</c> is the slot the supervisor chose;
/// <c>AppointmentDate</c> derives from that slot's date in the caller.
///
/// This helper builds the in-memory clone only -- callers persist via
/// the repository. The child-entity cascade (InjuryDetails / BodyParts /
/// ClaimExaminers / PrimaryInsurances, EmployerDetails, ApplicantAttorney,
/// DefenseAttorney, Accessors, CustomFieldValues, Documents) is Phase
/// 11c-extended; Phase 17 (change-request approval) will wire those in
/// as it consumes this helper.
/// </summary>
internal static class AppointmentRescheduleCloner
{
    /// <summary>
    /// Builds a new <see cref="Appointment"/> from <paramref name="source"/>
    /// with the supplied <paramref name="newAppointmentId"/>, slot id and
    /// appointment-date. Caller decides on the confirmation # via
    /// <paramref name="sameConfirmationNumber"/>:
    ///   - <c>true</c>: reuse <c>source.RequestConfirmationNumber</c>
    ///     (Phase 17's default; OLD reuses the confirmation # so the
    ///     end user sees one identifier across the lifecycle).
    ///   - <c>false</c>: caller must supply a fresh value via
    ///     <paramref name="overrideConfirmationNumber"/>; throws on
    ///     null / empty.
    ///
    /// Status defaults to <see cref="AppointmentStatusType.Approved"/>
    /// because the supervisor has already approved the reschedule by
    /// the time this helper runs. <see cref="Appointment.AppointmentApproveDate"/>
    /// is recomputed via the supplied <paramref name="approveDate"/>
    /// (typically <c>DateTime.UtcNow</c> at call site).
    /// </summary>
    internal static Appointment BuildScalarClone(
        Appointment source,
        Guid newAppointmentId,
        Guid? newTenantId,
        Guid newDoctorAvailabilityId,
        DateTime newAppointmentDate,
        bool sameConfirmationNumber,
        string? overrideConfirmationNumber,
        DateTime approveDate,
        bool isBeyondLimit = false)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var confirmationNumber = sameConfirmationNumber
            ? source.RequestConfirmationNumber
            : (string.IsNullOrWhiteSpace(overrideConfirmationNumber)
                ? throw new ArgumentException(
                    "overrideConfirmationNumber must be supplied when sameConfirmationNumber is false.",
                    nameof(overrideConfirmationNumber))
                : overrideConfirmationNumber);

        // Use the same constructor the booking flow uses so all required
        // fields go through the same Check.Length / Check.NotNull guards.
        var clone = new Appointment(
            id: newAppointmentId,
            patientId: source.PatientId,
            identityUserId: source.IdentityUserId,
            appointmentTypeId: source.AppointmentTypeId,
            locationId: source.LocationId,
            doctorAvailabilityId: newDoctorAvailabilityId,
            appointmentDate: newAppointmentDate,
            requestConfirmationNumber: confirmationNumber,
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: source.PanelNumber,
            dueDate: source.DueDate);

        clone.TenantId = newTenantId;

        // Audit / lifecycle fields the constructor does not surface.
        clone.IsPatientAlreadyExist = source.IsPatientAlreadyExist;
        clone.AppointmentApproveDate = approveDate;
        clone.InternalUserComments = source.InternalUserComments;

        // Snapshotted party emails -- these are stored on Appointment
        // for legal-record fan-out (see Appointments/CLAUDE.md S-5.1).
        clone.PatientEmail = source.PatientEmail;
        clone.ApplicantAttorneyEmail = source.ApplicantAttorneyEmail;
        clone.DefenseAttorneyEmail = source.DefenseAttorneyEmail;
        clone.ClaimExaminerEmail = source.ClaimExaminerEmail;

        // Reschedule-chain linkage. OriginalAppointmentId points at the
        // direct parent; multi-step reschedules walk up the chain via
        // repeated parent lookups.
        clone.OriginalAppointmentId = source.Id;

        // Carry forward responsible-user assignment so the new appointment
        // does not lose the staff context it was previously assigned to.
        clone.PrimaryResponsibleUserId = source.PrimaryResponsibleUserId;

        // Beyond-limit override is per-supervisor-decision; carry forward
        // if the source had it OR if the caller asks for it on this clone.
        clone.IsBeyondLimit = source.IsBeyondLimit || isBeyondLimit;

        // The reschedule-specific fields (ReScheduleReason, ReScheduledById)
        // are NOT copied -- those describe the change request, not the
        // resulting appointment. Phase 17 sets them on the source row when
        // it stamps the original as Rescheduled* and creates this clone.

        return clone;
    }
}
