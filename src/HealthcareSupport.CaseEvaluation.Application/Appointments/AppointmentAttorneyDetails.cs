using System;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The attorney an appointment names, shaped for the booking form. Shared by the per-appointment
/// attorney reads and the re-evaluation lookup (2026-11-06), so both describe an attorney the same way.
///
/// <para>BUG-042: an attorney is returned as soon as the master record exists. The login is optional
/// (a booked attorney who never registered has none), and the booked name, firm and email live on the
/// master. The login's name is only a fallback for rows saved before the name columns existed.</para>
/// </summary>
internal static class AppointmentAttorneyDetails
{
    public static ApplicantAttorneyDetailsDto? From(AppointmentApplicantAttorneyWithNavigationProperties? item)
    {
        if (item?.ApplicantAttorney == null)
        {
            return null;
        }

        var a = item.ApplicantAttorney;
        var u = item.IdentityUser;
        return new ApplicantAttorneyDetailsDto
        {
            ApplicantAttorneyId = a.Id,
            IdentityUserId = u?.Id ?? Guid.Empty,
            FirstName = a.FirstName ?? u?.Name ?? string.Empty,
            LastName = a.LastName ?? u?.Surname ?? string.Empty,
            Email = a.Email ?? u?.Email ?? string.Empty,
            FirmName = a.FirmName,
            WebAddress = a.WebAddress,
            PhoneNumber = a.PhoneNumber,
            FaxNumber = a.FaxNumber,
            Street = a.Street,
            City = a.City,
            StateId = a.StateId,
            ZipCode = a.ZipCode,
            ConcurrencyStamp = a.ConcurrencyStamp,
        };
    }

    public static DefenseAttorneyDetailsDto? From(AppointmentDefenseAttorneyWithNavigationProperties? item)
    {
        if (item?.DefenseAttorney == null)
        {
            return null;
        }

        var d = item.DefenseAttorney;
        var u = item.IdentityUser;
        return new DefenseAttorneyDetailsDto
        {
            DefenseAttorneyId = d.Id,
            IdentityUserId = u?.Id ?? Guid.Empty,
            FirstName = d.FirstName ?? u?.Name ?? string.Empty,
            LastName = d.LastName ?? u?.Surname ?? string.Empty,
            Email = d.Email ?? u?.Email ?? string.Empty,
            FirmName = d.FirmName,
            WebAddress = d.WebAddress,
            PhoneNumber = d.PhoneNumber,
            FaxNumber = d.FaxNumber,
            Street = d.Street,
            City = d.City,
            StateId = d.StateId,
            ZipCode = d.ZipCode,
            ConcurrencyStamp = d.ConcurrencyStamp,
        };
    }
}
