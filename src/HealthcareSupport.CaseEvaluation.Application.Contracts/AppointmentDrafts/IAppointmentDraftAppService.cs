using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts;

/// <summary>
/// #15 (2026-06-22): self-scoped booking-draft store. Lets a signed-in booker
/// save / resume / discard ONLY their own in-progress wizard draft (resolved from
/// CurrentUser.Id). No method accepts a target id, so reaching another user's PHI
/// draft is structurally impossible. Gated Appointments.Create -- the same right
/// that lets the caller book in the first place.
/// </summary>
public interface IAppointmentDraftAppService : IApplicationService
{
    /// <summary>The caller's own active draft, or null when none exists.</summary>
    Task<AppointmentDraftDto?> GetMineAsync();

    /// <summary>Creates or replaces the caller's single active draft.</summary>
    Task<AppointmentDraftDto> UpsertAsync(UpsertAppointmentDraftInput input);

    /// <summary>Physically deletes the caller's draft (no-op when none).</summary>
    Task DiscardMineAsync();
}
