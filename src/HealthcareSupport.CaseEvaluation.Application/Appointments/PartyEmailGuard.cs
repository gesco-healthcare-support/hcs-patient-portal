using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Validation;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-13 -- refuses placeholder party emails (unknown@..., na@..., xxx@...) with a localized 400.
/// Blank is never refused here (blank means "no value"; whether it is allowed is each field's own rule).
/// </summary>
public static class PartyEmailGuard
{
    /// <summary>Throws <c>AppointmentPlaceholderEmail</c> when any of the values is a placeholder.</summary>
    public static void EnsureNoPlaceholder(IEnumerable<string?> emails)
    {
        ArgumentNullException.ThrowIfNull(emails);
        if (emails.Any(PlaceholderEmail.IsPlaceholder))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentPlaceholderEmail);
        }
    }

    /// <summary>Throws only when <paramref name="value"/> is a placeholder AND differs from <paramref name="stored"/>.</summary>
    public static void EnsureNoPlaceholderIfChanged(string? value, string? stored)
    {
        var typed = value?.Trim();
        if (string.IsNullOrEmpty(typed)
            || string.Equals(typed, stored?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnsureNoPlaceholder(new[] { typed });
    }

    /// <summary>Every party email a booking submit carries, in one list.</summary>
    public static IEnumerable<string?> SubmitEmails(AppointmentSubmitDto input)
    {
        yield return input.PatientEmail;
        yield return input.ApplicantAttorneyEmail;
        yield return input.DefenseAttorneyEmail;
        yield return input.ClaimExaminerEmail;
        yield return input.Patient?.Email;
        yield return input.ApplicantAttorney?.Email;
        yield return input.DefenseAttorney?.Email;
        yield return input.ClaimExaminer?.Email;
        foreach (var accessor in input.Accessors ?? new())
        {
            yield return accessor.Email;
        }
    }
}
