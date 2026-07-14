using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.MyAttorneyProfiles;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.MyAttorneyProfiles;

/// <summary>
/// #9 (2026-06-19): self-scoped attorney profile. An applicant/defense attorney edits ONLY
/// their own master, resolved from CurrentUser.Id (role-directed). The API accepts no target
/// id, so reaching another party's record is structurally impossible. MASTER-ONLY: this
/// never touches an appointment snapshot, so existing appointments keep their booking-time
/// values (snapshots are captured separately when an attorney is linked to an appointment).
/// </summary>
[Authorize]
public class MyAttorneyProfileAppService : CaseEvaluationAppService, IMyAttorneyProfileAppService
{
    private const string ApplicantAttorneyRole = "Applicant Attorney";
    private const string DefenseAttorneyRole = "Defense Attorney";

    private readonly IRepository<ApplicantAttorney, Guid> _applicantRepository;
    private readonly IRepository<DefenseAttorney, Guid> _defenseRepository;
    private readonly ApplicantAttorneyManager _applicantManager;
    private readonly DefenseAttorneyManager _defenseManager;

    public MyAttorneyProfileAppService(
        IRepository<ApplicantAttorney, Guid> applicantRepository,
        IRepository<DefenseAttorney, Guid> defenseRepository,
        ApplicantAttorneyManager applicantManager,
        DefenseAttorneyManager defenseManager)
    {
        _applicantRepository = applicantRepository;
        _defenseRepository = defenseRepository;
        _applicantManager = applicantManager;
        _defenseManager = defenseManager;
    }

    public virtual async Task<MyAttorneyProfileDto> GetAsync()
    {
        var (applicant, defense) = await ResolveOwnMasterAsync();
        return applicant != null ? MapApplicant(applicant) : MapDefense(defense!);
    }

    public virtual async Task<MyAttorneyProfileDto> UpdateAsync(UpdateMyAttorneyProfileInput input)
    {
        Check.NotNull(input, nameof(input));
        var (applicant, defense) = await ResolveOwnMasterAsync();

        // Master-only update; email + identity are preserved. No appointment snapshot is
        // touched here, so past appointments keep their booking-time values.
        if (applicant != null)
        {
            var updated = await _applicantManager.UpdateAsync(
                applicant.Id, input.StateId, applicant.IdentityUserId,
                input.FirmName, applicant.FirmAddress, input.PhoneNumber, input.WebAddress,
                input.FaxNumber, input.Street, input.City, input.ZipCode,
                input.ConcurrencyStamp, applicant.Email, input.FirstName, input.LastName);
            return MapApplicant(updated);
        }

        var updatedDefense = await _defenseManager.UpdateAsync(
            defense!.Id, input.StateId, defense.IdentityUserId,
            input.FirmName, defense.FirmAddress, input.PhoneNumber, input.WebAddress,
            input.FaxNumber, input.Street, input.City, input.ZipCode,
            input.ConcurrencyStamp, defense.Email, input.FirstName, input.LastName);
        return MapDefense(updatedDefense);
    }

    /// <summary>
    /// Resolve the caller's OWN attorney master by CurrentUser.Id, directed by role. Denies
    /// when the caller is not an attorney, or holds the role but has no master record. No
    /// target id is accepted anywhere, so a caller can never reach another party's record.
    /// </summary>
    private async Task<(ApplicantAttorney? applicant, DefenseAttorney? defense)> ResolveOwnMasterAsync()
    {
        var userId = CurrentUser.Id ?? throw new AbpAuthorizationException();

        if (CurrentUser.IsInRole(DefenseAttorneyRole))
        {
            var defense = await _defenseRepository.FirstOrDefaultAsync(x => x.IdentityUserId == userId)
                ?? throw new UserFriendlyException(L["No attorney profile is linked to your account."]);
            return (null, defense);
        }

        if (CurrentUser.IsInRole(ApplicantAttorneyRole))
        {
            var applicant = await _applicantRepository.FirstOrDefaultAsync(x => x.IdentityUserId == userId)
                ?? throw new UserFriendlyException(L["No attorney profile is linked to your account."]);
            return (applicant, null);
        }

        throw new UserFriendlyException(L["You are not registered as an applicant or defense attorney."]);
    }

    private static MyAttorneyProfileDto MapApplicant(ApplicantAttorney a) => new()
    {
        Kind = "applicant",
        FirstName = a.FirstName,
        LastName = a.LastName,
        FirmName = a.FirmName,
        WebAddress = a.WebAddress,
        PhoneNumber = a.PhoneNumber,
        FaxNumber = a.FaxNumber,
        Street = a.Street,
        City = a.City,
        StateId = a.StateId,
        ZipCode = a.ZipCode,
        Email = a.Email,
        ConcurrencyStamp = a.ConcurrencyStamp,
    };

    private static MyAttorneyProfileDto MapDefense(DefenseAttorney d) => new()
    {
        Kind = "defense",
        FirstName = d.FirstName,
        LastName = d.LastName,
        FirmName = d.FirmName,
        WebAddress = d.WebAddress,
        PhoneNumber = d.PhoneNumber,
        FaxNumber = d.FaxNumber,
        Street = d.Street,
        City = d.City,
        StateId = d.StateId,
        ZipCode = d.ZipCode,
        Email = d.Email,
        ConcurrencyStamp = d.ConcurrencyStamp,
    };
}
