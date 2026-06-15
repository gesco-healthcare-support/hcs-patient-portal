using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Application.Dtos;
using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class GetAppointmentsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? PanelNumber { get; set; }

    public DateTime? AppointmentDateMin { get; set; }

    public DateTime? AppointmentDateMax { get; set; }

    public Guid? IdentityUserId { get; set; }

    /// <summary>
    /// When set, filters appointments where the current user is assigned in AppointmentAccessor (for Applicant Attorney / Defense Attorney).
    /// </summary>
    public Guid? AccessorIdentityUserId { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public Guid? LocationId { get; set; }

    /// <summary>
    /// W2-6: filter by appointment status (1=Pending, 2=Approved, etc.).
    /// Powers the dashboard-card deep-link to /appointments?appointmentStatus=N.
    /// </summary>
    public AppointmentStatusType? AppointmentStatus { get; set; }

    /// <summary>
    /// Redesign (Prompt 10, 2026-06-14): multi-status filter for the internal
    /// list's pill chips. A single UI pill (e.g. Cancelled) spans several raw
    /// statuses (CancelledNoBill / CancelledLate / CancellationRequested /
    /// NoShow), so the redesigned list sends the pill's full status set here.
    /// When non-empty it filters in addition to <see cref="AppointmentStatus"/>;
    /// the single-value filter stays for the dashboard deep-link parity.
    /// </summary>
    public List<AppointmentStatusType>? AppointmentStatuses { get; set; }

    public GetAppointmentsInput()
    {
    }
}