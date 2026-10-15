using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts;

/// <summary>
/// #15 (2026-06-22): checkpoint-save payload for the signed-in user's own booking
/// draft. Deliberately carries NO draft/user id -- the service resolves the caller
/// from CurrentUser.Id, so a caller can never address another user's draft.
/// </summary>
public class UpsertAppointmentDraftInput
{
    [Required]
    public string PayloadJson { get; set; } = null!;

    public int CurrentStep { get; set; }

    [StringLength(AppointmentDraftConsts.LabelMaxLength)]
    public string? Label { get; set; }
}
