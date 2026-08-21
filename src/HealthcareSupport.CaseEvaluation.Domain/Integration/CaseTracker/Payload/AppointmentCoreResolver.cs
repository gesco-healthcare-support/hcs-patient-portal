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
        // copy written at booking, but the slot is the source of truth. (Until phase 4d a reschedule
        // moved the appointment in place and kept the two in step; it now creates a new appointment
        // on the agreed slot instead, so the copy is only ever written once.)
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

        // Phase 4e (2026-08-06) -- the reschedule chain, BACKWARD half. Same shape as the re-eval
        // lookup above and deliberately a separate field: a replacement did not "follow up" the
        // appointment it replaced, it took its place.
        if (appointment.RescheduledFromAppointmentId is { } rescheduledFromId &&
            rescheduledFromId != Guid.Empty)
        {
            var replaced = await _appointmentRepository.FindAsync(
                rescheduledFromId, cancellationToken: cancellationToken);
            section.RescheduledFromConfirmationNumber = replaced?.RequestConfirmationNumber;
        }

        // FORWARD half: the appointment that replaced THIS one, if any. A predicate query rather
        // than a lookup by id, because the link is stored on the successor -- the closed appointment
        // holds no pointer of its own. Without this a closed case is a dead end: it says it was
        // rescheduled but not to where.
        //
        // GetListAsync, not FirstOrDefaultAsync: the latter is an EXTENSION method
        // (RepositoryAsyncExtensions), so it cannot be substituted in a unit test -- an arrangement
        // for it silently does nothing and the real extension runs against the substitute. This is
        // an interface member, so the seam is real. At most one row can match (a closed appointment
        // is terminal and cannot be rescheduled again), so taking the first is not a narrowing.
        var successors = await _appointmentRepository.GetListAsync(
            a => a.RescheduledFromAppointmentId == appointment.Id,
            cancellationToken: cancellationToken);
        var successor = successors.Count > 0 ? successors[0] : null;
        if (successor != null)
        {
            section.SupersededByAppointmentId = successor.Id;
            section.SupersededReason = SupersededReasonWire.ToWire(appointment.AppointmentStatus);
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

    /// <summary>Phase 4e -- the replaced appointment's confirmation number, on a replacement.</summary>
    public string? RescheduledFromConfirmationNumber { get; set; }

    /// <summary>Phase 4e -- the appointment that replaced this one, on a closed original.</summary>
    public Guid? SupersededByAppointmentId { get; set; }

    /// <summary>Phase 4e -- why it was superseded. Set exactly when <see cref="SupersededByAppointmentId"/> is.</summary>
    public string? SupersededReason { get; set; }
}
