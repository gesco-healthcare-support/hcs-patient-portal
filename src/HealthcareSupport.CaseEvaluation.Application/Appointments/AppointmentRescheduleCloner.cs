using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.CustomFields;
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

    // -----------------------------------------------------------------
    // Phase 11j (2026-05-04) -- per-child-entity deep-clone helpers.
    //
    // Each helper builds a fresh entity using the same constructor the
    // booking flow uses, then back-fills any fields the constructor
    // does not surface. The pattern mirrors BuildScalarClone above:
    // pure, side-effect-free, idiomatic ABP-with-private-setters
    // friendly. Phase 17 (change-request approval) reads the source's
    // child rows from their respective repositories and feeds each
    // through these helpers; persistence stays in the orchestrator.
    //
    // Strict OLD parity: every field that survives the OLD-to-NEW
    // schema mapping is carried forward. Audit / lifecycle fields
    // (CreatorId, CreationTime, etc.) are NOT copied -- ABP regenerates
    // those at insert time so the new row has its own audit trail.
    // -----------------------------------------------------------------

    /// <summary>
    /// Clone an <see cref="AppointmentInjuryDetail"/> for a new
    /// appointment. The new row gets a fresh Id and points at
    /// <paramref name="newAppointmentId"/>; every scalar field is
    /// copied verbatim.
    /// </summary>
    internal static AppointmentInjuryDetail CloneInjuryDetailFor(
        AppointmentInjuryDetail source,
        Guid newId,
        Guid newAppointmentId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var clone = new AppointmentInjuryDetail(
            id: newId,
            appointmentId: newAppointmentId,
            dateOfInjury: source.DateOfInjury,
            claimNumber: source.ClaimNumber,
            isCumulativeInjury: source.IsCumulativeInjury,
            bodyPartsSummary: source.BodyPartsSummary,
            toDateOfInjury: source.ToDateOfInjury,
            wcabAdj: source.WcabAdj,
            wcabOfficeId: source.WcabOfficeId);

        clone.TenantId = newTenantId;
        return clone;
    }

    /// <summary>
    /// Clone an <see cref="AppointmentBodyPart"/> under a new
    /// injury-detail row. Children of the cascade follow their parent
    /// entity, not the appointment directly.
    /// </summary>
    internal static AppointmentBodyPart CloneBodyPartFor(
        AppointmentBodyPart source,
        Guid newId,
        Guid newInjuryDetailId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var clone = new AppointmentBodyPart(
            id: newId,
            appointmentInjuryDetailId: newInjuryDetailId,
            bodyPartDescription: source.BodyPartDescription);

        clone.TenantId = newTenantId;
        return clone;
    }

    /// <summary>
    /// Clone an <see cref="AppointmentClaimExaminer"/> under a new
    /// injury-detail row. The constructor only takes the IsActive
    /// flag, so all the contact-info scalars are back-filled.
    /// </summary>
    internal static AppointmentClaimExaminer CloneClaimExaminerFor(
        AppointmentClaimExaminer source,
        Guid newId,
        Guid newInjuryDetailId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var clone = new AppointmentClaimExaminer(
            id: newId,
            appointmentInjuryDetailId: newInjuryDetailId,
            isActive: source.IsActive);

        clone.TenantId = newTenantId;
        clone.Name = source.Name;
        clone.Suite = source.Suite;
        clone.Email = source.Email;
        clone.PhoneNumber = source.PhoneNumber;
        clone.Fax = source.Fax;
        clone.Street = source.Street;
        clone.City = source.City;
        clone.Zip = source.Zip;
        clone.StateId = source.StateId;
        return clone;
    }

    /// <summary>
    /// Clone an <see cref="AppointmentPrimaryInsurance"/> under a new
    /// injury-detail row. Same back-fill shape as
    /// <see cref="CloneClaimExaminerFor"/> -- the constructor takes
    /// only IsActive; everything else is post-construction.
    /// </summary>
    internal static AppointmentPrimaryInsurance ClonePrimaryInsuranceFor(
        AppointmentPrimaryInsurance source,
        Guid newId,
        Guid newInjuryDetailId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var clone = new AppointmentPrimaryInsurance(
            id: newId,
            appointmentInjuryDetailId: newInjuryDetailId,
            isActive: source.IsActive);

        clone.TenantId = newTenantId;
        clone.Name = source.Name;
        clone.Suite = source.Suite;
        clone.Attention = source.Attention;
        clone.PhoneNumber = source.PhoneNumber;
        clone.FaxNumber = source.FaxNumber;
        clone.Street = source.Street;
        clone.City = source.City;
        clone.Zip = source.Zip;
        clone.StateId = source.StateId;
        return clone;
    }

    /// <summary>
    /// Clone an <see cref="AppointmentEmployerDetail"/>. NEW models
    /// employer 1:1 with appointment today; the audit doc flags 1:N
    /// as the OLD-parity intent. Once the schema lifts the implicit
    /// 1:1 constraint the same helper handles the 1:N case.
    /// </summary>
    internal static AppointmentEmployerDetail CloneEmployerDetailFor(
        AppointmentEmployerDetail source,
        Guid newId,
        Guid newAppointmentId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var clone = new AppointmentEmployerDetail(
            id: newId,
            appointmentId: newAppointmentId,
            stateId: source.StateId,
            employerName: source.EmployerName,
            occupation: source.Occupation);

        clone.TenantId = newTenantId;
        clone.PhoneNumber = source.PhoneNumber;
        clone.Street = source.Street;
        clone.City = source.City;
        clone.ZipCode = source.ZipCode;
        return clone;
    }

    /// <summary>
    /// Clone an <see cref="AppointmentApplicantAttorney"/> link row.
    /// Per-link scalars are just the three FKs; the new row points at
    /// the new appointment but keeps the same ApplicantAttorney +
    /// IdentityUser refs (the legal-party did not change just because
    /// the appointment was rescheduled).
    /// </summary>
    internal static AppointmentApplicantAttorney CloneApplicantAttorneyFor(
        AppointmentApplicantAttorney source,
        Guid newId,
        Guid newAppointmentId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var clone = new AppointmentApplicantAttorney(
            id: newId,
            appointmentId: newAppointmentId,
            applicantAttorneyId: source.ApplicantAttorneyId,
            identityUserId: source.IdentityUserId);

        clone.TenantId = newTenantId;
        return clone;
    }

    /// <summary>
    /// Clone an <see cref="AppointmentDefenseAttorney"/> link row.
    /// Mirror of <see cref="CloneApplicantAttorneyFor"/> for the
    /// defense-side join.
    /// </summary>
    internal static AppointmentDefenseAttorney CloneDefenseAttorneyFor(
        AppointmentDefenseAttorney source,
        Guid newId,
        Guid newAppointmentId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var clone = new AppointmentDefenseAttorney(
            id: newId,
            appointmentId: newAppointmentId,
            defenseAttorneyId: source.DefenseAttorneyId,
            identityUserId: source.IdentityUserId);

        clone.TenantId = newTenantId;
        return clone;
    }

    /// <summary>
    /// Clone an <see cref="AppointmentAccessor"/> grant. Re-reading
    /// the existing grant (same identity + same access type) and
    /// pointing it at the new appointment carries the legal-record
    /// fan-out forward without re-issuing invitation emails.
    /// </summary>
    internal static AppointmentAccessor CloneAccessorFor(
        AppointmentAccessor source,
        Guid newId,
        Guid newAppointmentId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var clone = new AppointmentAccessor(
            id: newId,
            identityUserId: source.IdentityUserId,
            appointmentId: newAppointmentId,
            accessTypeId: source.AccessTypeId);

        clone.TenantId = newTenantId;
        return clone;
    }

    /// <summary>
    /// C6 (Phase 17 cascade-clone gap, 2026-05-04) -- clone a
    /// <see cref="CustomFieldValue"/> for the new appointment row.
    /// Mirrors OLD <c>AppointmentChangeRequestDomain.cs:435-450</c>:
    /// every <c>spm.CustomFieldsValues</c> row pointing at the source
    /// appointment is duplicated to the new appointment with the same
    /// <see cref="CustomFieldValue.CustomFieldId"/> + <c>Value</c> so the
    /// IT-Admin-defined intake answers carry forward verbatim.
    /// </summary>
    internal static CustomFieldValue CloneCustomFieldValueFor(
        CustomFieldValue source,
        Guid newId,
        Guid newAppointmentId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        return new CustomFieldValue(
            id: newId,
            tenantId: newTenantId,
            customFieldId: source.CustomFieldId,
            appointmentId: newAppointmentId,
            value: source.Value);
    }

    /// <summary>
    /// C6 (Phase 17 cascade-clone gap, 2026-05-04) -- clone an ad-hoc
    /// <see cref="AppointmentDocument"/> for the new appointment row.
    /// Mirrors OLD <c>AppointmentChangeRequestDomain.cs:523-549</c>:
    /// OLD's <c>AppointmentNewDocument</c> table only held the ad-hoc
    /// patient uploads; OLD's package-doc table was NOT cloned because
    /// package status is recomputed against the new appointment date.
    /// NEW unified both into <see cref="AppointmentDocument"/> via the
    /// <see cref="AppointmentDocument.IsAdHoc"/> flag (Phase 1.6); the
    /// caller filters to <c>IsAdHoc == true</c> rows so this helper only
    /// runs against the OLD-equivalent subset.
    ///
    /// <para>The clone reuses the same <see cref="AppointmentDocument.BlobName"/>
    /// pointer so both rows reference the same physical blob. Storage is
    /// content-addressed; deletion of either row leaves the blob intact
    /// per the existing retention policy. Status, rejection notes, and
    /// review-time fields are NOT carried forward -- the new appointment
    /// row's review starts fresh (the supervisor approving the reschedule
    /// hasn't reviewed the cloned uploads yet).</para>
    ///
    /// <para>JDF documents (<see cref="AppointmentDocument.IsJointDeclaration"/>=true)
    /// follow the same intent: clone if ad-hoc-flagged, otherwise leave
    /// for the JDF-upload flow on the new appointment. Per OLD line 526
    /// the joint-declaration table also wasn't cloned, so JDF-only docs
    /// stay on the source row.</para>
    /// </summary>
    internal static AppointmentDocument CloneAdHocDocumentFor(
        AppointmentDocument source,
        Guid newId,
        Guid newAppointmentId,
        Guid? newTenantId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var clone = new AppointmentDocument(
            id: newId,
            tenantId: newTenantId,
            appointmentId: newAppointmentId,
            documentName: source.DocumentName,
            fileName: source.FileName,
            blobName: source.BlobName,
            contentType: source.ContentType,
            fileSize: source.FileSize,
            uploadedByUserId: source.UploadedByUserId);

        // Carry forward only the structural / classifier flags. Status
        // resets to the constructor default (Uploaded) so the new
        // appointment's review pipeline picks the document up fresh.
        clone.IsAdHoc = source.IsAdHoc;
        clone.IsJointDeclaration = source.IsJointDeclaration;
        return clone;
    }
}
