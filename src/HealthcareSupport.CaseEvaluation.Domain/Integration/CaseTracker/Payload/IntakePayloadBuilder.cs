using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Thin facade over the four focused resolvers. Owns only the plain scalar copies that need no
/// lookup; everything requiring I/O belongs to a resolver, so each piece stays small enough to
/// unit-test and to satisfy the repo's complexity thresholds.
/// </summary>
public class IntakePayloadBuilder : IIntakePayloadBuilder, ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly AppointmentCoreResolver _coreResolver;
    private readonly PartyResolver _partyResolver;
    private readonly TenantLocationResolver _tenantLocationResolver;
    private readonly DocumentListResolver _documentListResolver;
    private readonly InjuryResolver _injuryResolver;
    private readonly PartyDetailResolver _partyDetailResolver;
    private readonly ChangeAttributionResolver _changeAttributionResolver;
    private readonly IGuidGenerator _guidGenerator;

    public IntakePayloadBuilder(
        IRepository<Appointment, Guid> appointmentRepository,
        AppointmentCoreResolver coreResolver,
        PartyResolver partyResolver,
        TenantLocationResolver tenantLocationResolver,
        DocumentListResolver documentListResolver,
        InjuryResolver injuryResolver,
        PartyDetailResolver partyDetailResolver,
        ChangeAttributionResolver changeAttributionResolver,
        IGuidGenerator guidGenerator)
    {
        _appointmentRepository = appointmentRepository;
        _coreResolver = coreResolver;
        _partyResolver = partyResolver;
        _tenantLocationResolver = tenantLocationResolver;
        _documentListResolver = documentListResolver;
        _injuryResolver = injuryResolver;
        _partyDetailResolver = partyDetailResolver;
        _changeAttributionResolver = changeAttributionResolver;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Resolves everything the payload needs, then hands off to <see cref="ComposePayload"/>.
    ///
    /// <para>Split in two because the repo caps a method at 50 lines and the assignment block alone is
    /// most of that. Orchestration here, assignment there.</para>
    /// </summary>
    public virtual async Task<IntakeEnvelope> BuildAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _appointmentRepository.GetAsync(appointmentId, cancellationToken: cancellationToken);

        var core = await _coreResolver.ResolveAsync(appointment, cancellationToken);
        var tenantLocation = await _tenantLocationResolver.ResolveAsync(appointment, cancellationToken);
        var parties = await _partyDetailResolver.ResolveAsync(appointment, cancellationToken);

        var payload = ComposePayload(appointment, core, tenantLocation, parties);

        payload.Patient = await _partyResolver.ResolvePatientAsync(appointment, cancellationToken);
        payload.Doctor = await _partyResolver.ResolveDoctorAsync(cancellationToken);
        payload.Documents = await _documentListResolver.ResolveAsync(appointment, cancellationToken);
        payload.Injuries = await _injuryResolver.ResolveAsync(appointment.Id, cancellationToken);

        // Phase 6 (2026-08-08): who asked for the latest change, and when it was asked and settled.
        var attribution = await _changeAttributionResolver.ResolveAsync(appointment.Id, cancellationToken);
        payload.ChangeRequestedBySide = attribution.RequestedBySide;
        payload.ChangeRequestType = attribution.ChangeRequestType;
        payload.ChangeRequestedAtUtc = attribution.RequestedAtUtc;
        payload.ChangeFinalizedAtUtc = attribution.FinalizedAtUtc;

        return new IntakeEnvelope
        {
            Data = payload,
            Meta = new IntakeMeta
            {
                RequestId = _guidGenerator.Create(),
                Timestamp = IntegrationTimestamp.ToIsoUtc(DateTime.UtcNow),
            },
        };
    }

    /// <summary>The plain assignment step: scalars off the appointment plus the already-resolved sections.</summary>
    private IntakePayload ComposePayload(
        Appointment appointment,
        AppointmentCoreSection core,
        TenantLocationSection tenantLocation,
        PartyDetailSection parties)
    {
        return new IntakePayload
        {
            AppointmentId = appointment.Id,
            ConfirmationNumber = appointment.RequestConfirmationNumber,
            Status = appointment.AppointmentStatus.ToString(),
            BillingStatus = BillingStatusWire.ToWire(appointment.AppointmentStatus),
            CancellationReason = appointment.CancellationReason,
            ApprovedAtUtc = IntegrationTimestamp.ToIsoUtcOrNull(appointment.AppointmentApproveDate),
            SubmittedAtUtc = IntegrationTimestamp.ToIsoUtc(appointment.CreationTime),
            UpdatedAt = IntegrationTimestamp.ToIsoUtc(
                appointment.LastModificationTime ?? appointment.CreationTime),
            EvaluationKind = EvaluationKindWire.ToWire(appointment.EvaluationKind),
            PreviousAppointmentId = appointment.OriginalAppointmentId,
            PreviousConfirmationNumber = core.PreviousConfirmationNumber,
            RescheduledFromAppointmentId = appointment.RescheduledFromAppointmentId,
            RescheduledFromConfirmationNumber = core.RescheduledFromConfirmationNumber,
            SupersededByAppointmentId = core.SupersededByAppointmentId,
            SupersededReason = core.SupersededReason,
            PanelNumber = appointment.PanelNumber,
            AppointmentDateLocal = core.AppointmentDateLocal,
            AppointmentTimeLocal = core.AppointmentTimeLocal,
            TimeZone = core.TimeZone,
            DurationMinutes = core.DurationMinutes,
            Tenant = tenantLocation.Tenant,
            Location = tenantLocation.Location,
            AppointmentType = new IntakeAppointmentTypeSection
            {
                Id = core.AppointmentTypeId,
                Name = core.AppointmentTypeName,
            },
            Storage = new IntakeStorageSection
            {
                Bucket = _tenantLocationResolver.ResolveBucketName(),
            },
            ApplicantAttorney = parties.ApplicantAttorney,
            DefenseAttorney = parties.DefenseAttorney,
            PrimaryInsurances = parties.PrimaryInsurances,
            ClaimExaminers = parties.ClaimExaminers,
        };
    }
}
