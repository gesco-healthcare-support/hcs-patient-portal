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
    private readonly IGuidGenerator _guidGenerator;

    public IntakePayloadBuilder(
        IRepository<Appointment, Guid> appointmentRepository,
        AppointmentCoreResolver coreResolver,
        PartyResolver partyResolver,
        TenantLocationResolver tenantLocationResolver,
        DocumentListResolver documentListResolver,
        IGuidGenerator guidGenerator)
    {
        _appointmentRepository = appointmentRepository;
        _coreResolver = coreResolver;
        _partyResolver = partyResolver;
        _tenantLocationResolver = tenantLocationResolver;
        _documentListResolver = documentListResolver;
        _guidGenerator = guidGenerator;
    }

    public virtual async Task<IntakeEnvelope> BuildAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _appointmentRepository.GetAsync(appointmentId, cancellationToken: cancellationToken);

        var core = await _coreResolver.ResolveAsync(appointment, cancellationToken);
        var tenantLocation = await _tenantLocationResolver.ResolveAsync(appointment, cancellationToken);

        var payload = new IntakePayload
        {
            AppointmentId = appointment.Id,
            ConfirmationNumber = appointment.RequestConfirmationNumber,
            Status = appointment.AppointmentStatus.ToString(),
            ApprovedAtUtc = IntegrationTimestamp.ToIsoUtcOrNull(appointment.AppointmentApproveDate),
            SubmittedAtUtc = IntegrationTimestamp.ToIsoUtc(appointment.CreationTime),
            UpdatedAt = IntegrationTimestamp.ToIsoUtc(
                appointment.LastModificationTime ?? appointment.CreationTime),
            EvaluationKind = EvaluationKindWire.ToWire(appointment.EvaluationKind),
            PreviousAppointmentId = appointment.OriginalAppointmentId,
            PreviousConfirmationNumber = core.PreviousConfirmationNumber,
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
            Patient = await _partyResolver.ResolvePatientAsync(appointment, cancellationToken),
            Doctor = await _partyResolver.ResolveDoctorAsync(cancellationToken),
            Storage = new IntakeStorageSection
            {
                Bucket = _tenantLocationResolver.ResolveBucketName(),
            },
            Documents = await _documentListResolver.ResolveAsync(appointment, cancellationToken),
        };

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
}
