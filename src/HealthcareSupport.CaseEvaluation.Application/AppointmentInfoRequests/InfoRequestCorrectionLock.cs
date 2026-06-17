using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Server-side lock for the fix-it flow: maps each corrections field to its
/// flaggable-field key and reports any PROVIDED change that targets a field the
/// open request did not flag. Pure (no DI) so the security rule is unit-tested
/// directly; the app service throws when the result is non-empty. The keys mirror
/// the frontend send-back-fields registry (insurance name + phone share the one
/// "appointmentInsuranceName" flag).
/// </summary>
internal static class InfoRequestCorrectionLock
{
    /// <summary>
    /// Returns the flaggable keys the input tries to change that are NOT in
    /// <paramref name="flaggedKeys"/>. An empty result means the correction
    /// touches only flagged fields and is allowed.
    /// </summary>
    public static IReadOnlyList<string> FindUnflaggedChanges(
        SaveInfoRequestCorrectionsInput input,
        ISet<string> flaggedKeys)
    {
        var violations = new List<string>();

        void Require(bool provided, string key)
        {
            if (provided && !flaggedKeys.Contains(key))
            {
                violations.Add(key);
            }
        }

        Require(input.DateOfBirth.HasValue, "dateOfBirth");
        Require(input.SocialSecurityNumber != null, "socialSecurityNumber");
        Require(input.Address != null, "address");
        Require(input.CellPhoneNumber != null, "cellPhoneNumber");
        Require(input.AppointmentLanguageId.HasValue, "appointmentLanguageId");
        Require(input.ApplicantAttorneyEmail != null, "applicantAttorneyEmail");
        Require(input.ClaimExaminerEmail != null, "appointmentClaimExaminerEmail");
        Require(input.InsuranceName != null, "appointmentInsuranceName");
        Require(input.InsurancePhoneNumber != null, "appointmentInsuranceName");
        Require(input.DefenseAttorneyFirmName != null, "defenseAttorneyFirmName");

        return violations;
    }
}
