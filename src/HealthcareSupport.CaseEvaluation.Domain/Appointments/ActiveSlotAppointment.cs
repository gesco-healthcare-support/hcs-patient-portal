using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// A non-terminal appointment occupying a slot, projected for the staff schedule
/// (phase 3, 2026-07-31).
///
/// <para>A Domain-level record rather than the Application DTO because the repository cannot
/// reference Application.Contracts without inverting the layer dependency -- the same reason the
/// role seeder uses permission strings instead of the constants. The app service maps this onto
/// <c>ScheduleAppointmentDto</c>.</para>
///
/// <para>Carries <see cref="PatientName"/>, so it is PHI: only ever returned through an endpoint
/// gated on <c>CaseEvaluation.DoctorAvailabilities</c>, and never logged.</para>
/// </summary>
public sealed record ActiveSlotAppointment(
    Guid AppointmentId,
    Guid DoctorAvailabilityId,
    string RequestConfirmationNumber,
    string PatientName,
    AppointmentStatusType Status);
