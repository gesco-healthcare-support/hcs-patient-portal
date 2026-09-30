using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.MyClaimExaminerProfiles;

/// <summary>
/// R2-4 (2026-06-22): self-scoped claim-examiner profile, mirroring
/// MyAttorneyProfileAppService (#9). A claim examiner edits ONLY their own master,
/// resolved from CurrentUser.Id. The API accepts no target id, so reaching another
/// party's record is structurally impossible. MASTER-ONLY: never touches an
/// appointment snapshot, so existing appointments keep their booking-time values.
/// </summary>
[Authorize]
public class MyClaimExaminerProfileAppService
    : CaseEvaluationAppService, IMyClaimExaminerProfileAppService
{
    private const string ClaimExaminerRole = "Claim Examiner";

    private readonly IRepository<ClaimExaminer, Guid> _claimExaminerRepository;
    private readonly ClaimExaminerManager _claimExaminerManager;

    public MyClaimExaminerProfileAppService(
        IRepository<ClaimExaminer, Guid> claimExaminerRepository,
        ClaimExaminerManager claimExaminerManager)
    {
        _claimExaminerRepository = claimExaminerRepository;
        _claimExaminerManager = claimExaminerManager;
    }

    public virtual async Task<MyClaimExaminerProfileDto> GetAsync()
    {
        return Map(await ResolveOwnMasterAsync());
    }

    public virtual async Task<MyClaimExaminerProfileDto> UpdateAsync(
        UpdateMyClaimExaminerProfileInput input)
    {
        Check.NotNull(input, nameof(input));
        var examiner = await ResolveOwnMasterAsync();

        // Master-only update; email + identity are preserved (not self-editable here).
        var updated = await _claimExaminerManager.UpdateAsync(
            examiner.Id,
            input.StateId,
            examiner.IdentityUserId,
            input.PhoneNumber,
            input.FaxNumber,
            input.Street,
            input.City,
            input.ZipCode,
            input.ConcurrencyStamp,
            examiner.Email,
            input.FirstName,
            input.LastName);
        return Map(updated);
    }

    /// <summary>
    /// Resolve the caller's OWN claim-examiner master by CurrentUser.Id. Denies when the
    /// caller is not a claim examiner, or holds the role but has no master record. No
    /// target id is accepted anywhere, so a caller can never reach another party's record.
    /// </summary>
    private async Task<ClaimExaminer> ResolveOwnMasterAsync()
    {
        var userId = CurrentUser.Id ?? throw new AbpAuthorizationException();
        if (!CurrentUser.IsInRole(ClaimExaminerRole))
        {
            throw new UserFriendlyException(L["You are not registered as a claim examiner."]);
        }

        return await _claimExaminerRepository.FirstOrDefaultAsync(x => x.IdentityUserId == userId)
            ?? throw new UserFriendlyException(L["No claim examiner profile is linked to your account."]);
    }

    private static MyClaimExaminerProfileDto Map(ClaimExaminer c) => new()
    {
        FirstName = c.FirstName,
        LastName = c.LastName,
        PhoneNumber = c.PhoneNumber,
        FaxNumber = c.FaxNumber,
        Street = c.Street,
        City = c.City,
        StateId = c.StateId,
        ZipCode = c.ZipCode,
        Email = c.Email,
        ConcurrencyStamp = c.ConcurrencyStamp,
    };
}
