using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The appointment-adjacent lookups: the booked slot (which carries the authoritative date/time),
/// the appointment type, and -- on a re-evaluation -- the source appointment's confirmation number.
/// Plain scalars that live directly on the appointment are copied by
/// <see cref="IntakePayloadBuilder"/> and deliberately not routed through here.
/// </summary>
public class AppointmentCoreResolver : ITransientDependency
{
    /// <summary>
    /// The zone the clinic-local slot date/time are expressed in. A constant, not a stored field:
    /// the portal persists the slot as wall-clock with no offset, and every office is a California
    /// workers'-comp practice. An office outside Pacific time would need this to become per-office.
    /// </summary>
    public const string ClinicTimeZone = "America/Los_Angeles";

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;

    public AppointmentCoreResolver(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        IRepository<AppointmentType, Guid> appointmentTypeRepository)
    {
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
    }

    public virtual async Task<AppointmentCoreSection> ResolveAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        if (appointment is null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }

        var section = new AppointmentCoreSection
        {
            TimeZone = ClinicTimeZone,
        };

        // The slot is authoritative for date + time; Appointment.AppointmentDate is a denormalised
        // copy that the in-place reschedule keeps in step, but the slot is the source of truth.
        var slot = await _doctorAvailabilityRepository.FindAsync(
            appointment.DoctorAvailabilityId, cancellationToken: cancellationToken);
        if (slot != null)
        {
            section.AppointmentDateLocal = IntegrationTimestamp.ToDateOnly(slot.AvailableDate);
            section.AppointmentTimeLocal = slot.FromTime.ToString("HH\\:mm");
            section.DurationMinutes = (int)(slot.ToTime - slot.FromTime).TotalMinutes;
        }

        var appointmentType = await _appointmentTypeRepository.FindAsync(
            appointment.AppointmentTypeId, cancellationToken: cancellationToken);
        section.AppointmentTypeId = appointment.AppointmentTypeId;
        section.AppointmentTypeName = appointmentType?.Name ?? string.Empty;

        // Display-only aid for their staff: a re-evaluation gets its OWN fresh confirmation number,
        // so the original's is the only human-readable link back.
        if (appointment.OriginalAppointmentId is { } originalId && originalId != Guid.Empty)
        {
            var source = await _appointmentRepository.FindAsync(originalId, cancellationToken: cancellationToken);
            section.PreviousConfirmationNumber = source?.RequestConfirmationNumber;
        }

        return section;
    }
}

/// <summary>Result of <see cref="AppointmentCoreResolver"/>.</summary>
public class AppointmentCoreSection
{
    public string AppointmentDateLocal { get; set; } = string.Empty;

    public string AppointmentTimeLocal { get; set; } = string.Empty;

    public string TimeZone { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public Guid AppointmentTypeId { get; set; }

    public string AppointmentTypeName { get; set; } = string.Empty;

    public string? PreviousConfirmationNumber { get; set; }
}
