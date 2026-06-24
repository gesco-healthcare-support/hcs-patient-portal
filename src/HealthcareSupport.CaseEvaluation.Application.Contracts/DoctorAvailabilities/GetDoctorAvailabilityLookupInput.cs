namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Phase 7 (2026-05-03) -- input for the booking-form slot picker.
/// Mirrors OLD's <c>spm.spDoctorsAvailabilitiesLookups</c> stored proc
/// signature (AppointmentTypeId / AvailableDate / LocationId) at
/// <c>P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Lookups\ApplicationDbLookupsController.cs:23-31</c>
/// while widening to a date range so the form can show a calendar
/// month at a time. Phase 11 (Booking) is the primary consumer.
/// </summary>
public class GetDoctorAvailabilityLookupInput
{
    /// <summary>Required. Slots are scoped per Location.</summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Optional. When set, returned slots have either matching
    /// <c>AppointmentTypeId</c> or null <c>AppointmentTypeId</c>
    /// (slot accepts any type). When null, all slots regardless of
    /// type filter are returned. Mirrors OLD's loose-or-strict mode
    /// documented in <c>_slot-generation-deep-dive.md</c>.
    /// </summary>
    public Guid? AppointmentTypeId { get; set; }

    /// <summary>
    /// Optional lower bound. When null, defaults to
    /// <c>DateTime.Today</c> at the AppService layer (callers
    /// generally do not need to look at past slots).
    /// </summary>
    public DateTime? AvailableDateFrom { get; set; }

    /// <summary>
    /// Optional upper bound. When null, no upper-bound filter is
    /// applied -- callers responsible for paging large results.
    /// </summary>
    public DateTime? AvailableDateTo { get; set; }
}
