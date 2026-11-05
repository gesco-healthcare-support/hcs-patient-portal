using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The booker's mail choices at submit (office decisions 1 and 2, 2026-11-04). Validates the two
/// submit fields BEFORE anything is written, and applies the result to the appointment's
/// <c>ExtraProperties</c> through <see cref="AppointmentMailPreferences"/>.
/// </summary>
public static class AppointmentMailChoices
{
    /// <summary>What a submit asked for, after validation.</summary>
    /// <param name="BookerFollowsCase">False when an external booker opted out (or asked for a redirect).</param>
    /// <param name="RedirectSide">The side whose lawyer's mail goes to the paralegal; null for no redirect.</param>
    public sealed record SubmitChoices(bool BookerFollowsCase, ChangeRequestSide? RedirectSide);

    /// <summary>
    /// Decision 1: <c>BookerFollowsCase=false</c> is honoured for an external caller and ignored for
    /// staff. Decision 2: <c>SendAttorneyMailToParalegal</c> needs an attorney-role caller (400
    /// <c>AppointmentAttorneyMailRedirectAttorneyOnly</c> otherwise) and a same-side paralegal among
    /// the submit's Authorized Users (400 <c>AppointmentAttorneyMailRedirectNeedsParalegal</c>).
    /// </summary>
    public static SubmitChoices Validate(AppointmentSubmitDto input, IEnumerable<string?>? callerRoles)
    {
        ArgumentNullException.ThrowIfNull(input);
        var roles = (callerRoles ?? Array.Empty<string?>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.Trim())
            .ToList();
        var internalCaller = BookingFlowRoles.IsInternalUserCaller(roles);
        var follows = internalCaller || input.BookerFollowsCase;

        if (!input.SendAttorneyMailToParalegal)
        {
            return new SubmitChoices(follows, null);
        }

        var side = AttorneySideOf(roles);
        if (internalCaller || side == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAttorneyMailRedirectAttorneyOnly);
        }

        var paralegalRole = AppointmentMailPreferences.ParalegalRoleOf(side.Value);
        var hasParalegal = (input.Accessors ?? new List<AppointmentAccessors.AppointmentAccessorCreateDto>())
            .Any(a => string.Equals((a.Role ?? string.Empty).Trim(), paralegalRole, StringComparison.OrdinalIgnoreCase));
        if (!hasParalegal)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAttorneyMailRedirectNeedsParalegal);
        }

        // The redirect implies the booker does not follow either.
        return new SubmitChoices(false, side);
    }

    /// <summary>
    /// Stores the choices on the created appointment. The lawyer's address is the side's attorney
    /// email column as booked, else the caller's verified login email; with neither there is nothing
    /// to redirect and none is stored.
    /// </summary>
    public static void Apply(Appointment appointment, SubmitChoices choices, string? callerVerifiedEmail)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        ArgumentNullException.ThrowIfNull(choices);
        if (!choices.BookerFollowsCase)
        {
            AppointmentMailPreferences.SetBookerFollowsCase(appointment, false);
        }

        if (choices.RedirectSide is { } side)
        {
            var column = side == ChangeRequestSide.SideA
                ? appointment.ApplicantAttorneyEmail
                : appointment.DefenseAttorneyEmail;
            var lawyer = !string.IsNullOrWhiteSpace(column) ? column : callerVerifiedEmail;
            if (!string.IsNullOrWhiteSpace(lawyer))
            {
                AppointmentMailPreferences.SetAttorneyMailRedirect(appointment, side, lawyer);
            }
        }
    }

    /// <summary>Copies the stored choices onto the detail DTO (read-only flags).</summary>
    public static void ApplyTo(Appointment source, AppointmentDto destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        destination.BookerFollowsCase = AppointmentMailPreferences.BookerFollowsCase(source);
        destination.AttorneyMailToParalegal = AppointmentMailPreferences.GetAttorneyMailRedirect(source) != null;
    }

    private static ChangeRequestSide? AttorneySideOf(IReadOnlyCollection<string> roles)
    {
        if (roles.Any(r => string.Equals(r, AppointmentMailPreferences.AttorneyRoleOf(ChangeRequestSide.SideA), StringComparison.OrdinalIgnoreCase)))
        {
            return ChangeRequestSide.SideA;
        }
        if (roles.Any(r => string.Equals(r, AppointmentMailPreferences.AttorneyRoleOf(ChangeRequestSide.SideB), StringComparison.OrdinalIgnoreCase)))
        {
            return ChangeRequestSide.SideB;
        }
        return null;
    }
}
