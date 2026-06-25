using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts;

/// <summary>
/// #15 (2026-06-22): self-scoped booking-draft store. A signed-in booker saves /
/// resumes / discards ONLY their own in-progress wizard draft, resolved from
/// <c>CurrentUser.Id</c>. No method accepts a target id, so reaching another
/// user's PHI draft is structurally impossible (mirrors the #9 MyAttorneyProfile
/// pattern). Gated <c>Appointments.Create</c> -- the same right that lets the
/// caller book. The draft row is physically deleted on discard / submit / TTL
/// purge (CreationAudited base, no soft-delete) so the PHI payload truly leaves.
/// </summary>
[Authorize(CaseEvaluationPermissions.Appointments.Create)]
public class AppointmentDraftAppService : CaseEvaluationAppService, IAppointmentDraftAppService
{
    private readonly IRepository<AppointmentDraft, Guid> _draftRepository;

    public AppointmentDraftAppService(IRepository<AppointmentDraft, Guid> draftRepository)
    {
        _draftRepository = draftRepository;
    }

    public virtual async Task<AppointmentDraftDto?> GetMineAsync()
    {
        var userId = CurrentUser.GetId();
        var draft = await _draftRepository.FirstOrDefaultAsync(x => x.CreatorId == userId);
        return draft == null ? null : Map(draft);
    }

    public virtual async Task<AppointmentDraftDto> UpsertAsync(UpsertAppointmentDraftInput input)
    {
        Check.NotNull(input, nameof(input));

        var userId = CurrentUser.GetId();
        var now = Clock.Now;
        var draft = await _draftRepository.FirstOrDefaultAsync(x => x.CreatorId == userId);

        if (draft == null)
        {
            // TenantId must be passed explicitly: the ctor assigns it, which
            // suppresses ABP's insert-time auto-stamp (the doc-types lesson).
            draft = new AppointmentDraft(
                GuidGenerator.Create(),
                input.PayloadJson,
                input.CurrentStep,
                now,
                input.Label,
                CurrentTenant.Id);
            await _draftRepository.InsertAsync(draft, autoSave: true);
        }
        else
        {
            draft.UpdatePayload(input.PayloadJson, input.CurrentStep, now, input.Label);
            await _draftRepository.UpdateAsync(draft, autoSave: true);
        }

        return Map(draft);
    }

    public virtual async Task DiscardMineAsync()
    {
        var userId = CurrentUser.GetId();
        var draft = await _draftRepository.FirstOrDefaultAsync(x => x.CreatorId == userId);
        if (draft != null)
        {
            await _draftRepository.DeleteAsync(draft, autoSave: true);
        }
    }

    private static AppointmentDraftDto Map(AppointmentDraft draft) => new()
    {
        PayloadJson = draft.PayloadJson,
        CurrentStep = draft.CurrentStep,
        Label = draft.Label,
        LastSavedTime = draft.LastSavedTime,
    };
}
