using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// The CURRENT office's names as people should read them in emails and on the register page.
///
/// <para>2026-11-09: these places used to print the office's tenant NAME, which is the lowercased
/// web-address slug ("Dr. falkinstein"), because New Practice stores Name = slug on purpose (the
/// resolver matches the subdomain back to it). The slug is still what <c>CurrentTenant.Change</c>,
/// URLs and resolvers use; only DISPLAY strings come from here.</para>
/// </summary>
public interface IOfficeDisplayNameResolver
{
    /// <summary>The current office's names; <see cref="OfficeDisplayNames.None"/> outside an office.</summary>
    Task<OfficeDisplayNames> ResolveCurrentAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// An office's display names. <see cref="PracticeName"/> is for "... at {office}" lines (the
/// <c>##ClinicName##</c> token); <see cref="DoctorAndPracticeName"/> is who an invitation comes
/// from, e.g. "Yuri Falkinstein, M.D. (West Coast Spine Institute)".
/// </summary>
public sealed record OfficeDisplayNames(string PracticeName, string DoctorAndPracticeName)
{
    /// <summary>No office in scope: both names blank.</summary>
    public static readonly OfficeDisplayNames None = new(string.Empty, string.Empty);

    /// <summary>
    /// Levon's rules (2026-11-09): the practice name, else the doctor, else the slug; the invitation
    /// shows "doctor (practice)", but only one of them when they are equal or one is missing, and the
    /// slug only when both are. Never blank while a slug exists.
    /// </summary>
    public static OfficeDisplayNames Compose(string? physicianName, string? practiceName, string? slug)
    {
        var doctor = Clean(physicianName);
        var practice = Clean(practiceName);
        var fallback = Clean(slug);

        var bothNamed = doctor.Length > 0
            && practice.Length > 0
            && !string.Equals(doctor, practice, StringComparison.OrdinalIgnoreCase);

        return new OfficeDisplayNames(
            PracticeName: FirstNonBlank(practice, doctor, fallback),
            DoctorAndPracticeName: bothNamed ? $"{doctor} ({practice})" : FirstNonBlank(doctor, practice, fallback));
    }

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;

    private static string FirstNonBlank(string first, string second, string third) =>
        first.Length > 0 ? first : second.Length > 0 ? second : third;
}

/// <summary>
/// Builds <see cref="OfficeDisplayNames"/> from the office's effective letterhead (the stored
/// physician and practice names, else "Dr. {First} {Last}" from its doctor and its display name),
/// with the tenant name (the slug) only as the last resort.
/// </summary>
public class OfficeDisplayNameResolver : IOfficeDisplayNameResolver, ITransientDependency
{
    private readonly OfficeLetterheadResolver _letterheadResolver;
    private readonly ITenantStore _tenantStore;
    private readonly ICurrentTenant _currentTenant;

    public OfficeDisplayNameResolver(
        OfficeLetterheadResolver letterheadResolver,
        ITenantStore tenantStore,
        ICurrentTenant currentTenant)
    {
        _letterheadResolver = letterheadResolver;
        _tenantStore = tenantStore;
        _currentTenant = currentTenant;
    }

    public virtual async Task<OfficeDisplayNames> ResolveCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTenant.Id is not { } officeId)
        {
            return OfficeDisplayNames.None;
        }

        var letterhead = await _letterheadResolver.ResolveAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(letterhead.PhysicianName) || !string.IsNullOrWhiteSpace(letterhead.PracticeName))
        {
            return OfficeDisplayNames.Compose(letterhead.PhysicianName, letterhead.PracticeName, slug: null);
        }

        // Neither a doctor nor a display name: the slug, so the line is never blank.
        var slug = (await _tenantStore.FindAsync(officeId))?.Name;
        return OfficeDisplayNames.Compose(null, null, slug);
    }
}
