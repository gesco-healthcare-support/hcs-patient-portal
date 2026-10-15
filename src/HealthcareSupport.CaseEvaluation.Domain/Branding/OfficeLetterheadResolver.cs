using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Resolves the CURRENT office's effective packet letterhead (<see cref="OfficeLetterhead"/>).
/// Reads two databases on purpose: the doctor from the office database the caller is already
/// in, and the branding row from the host database (it is host-only, see
/// <see cref="OfficeBranding"/>), which is why the branding read runs under
/// <c>CurrentTenant.Change(null)</c>.
///
/// <para>Called from inside an office: the packet job and the letterhead editor both run in
/// office context. In host context there is no office and the result is built from nothing.</para>
/// </summary>
public class OfficeLetterheadResolver : ITransientDependency
{
    private readonly IReadOnlyRepository<Doctor, Guid> _doctorRepository;
    private readonly IReadOnlyRepository<OfficeBranding, Guid> _brandingRepository;
    private readonly ICurrentTenant _currentTenant;

    public OfficeLetterheadResolver(
        IReadOnlyRepository<Doctor, Guid> doctorRepository,
        IReadOnlyRepository<OfficeBranding, Guid> brandingRepository,
        ICurrentTenant currentTenant)
    {
        _doctorRepository = doctorRepository;
        _brandingRepository = brandingRepository;
        _currentTenant = currentTenant;
    }

    /// <summary>The office's effective letterhead, every gap filled.</summary>
    public virtual async Task<OfficeLetterhead> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var source = await LoadSourceAsync(cancellationToken);
        return OfficeLetterhead.Compose(
            source.Branding?.GetLetterhead(),
            source.Branding?.DisplayName,
            source.DoctorFirstName,
            source.DoctorLastName);
    }

    /// <summary>
    /// The raw inputs: the office's branding row (null when it has none) and its doctor's names.
    /// The editor needs these separately to show both what is stored and what a blank falls
    /// back to.
    /// </summary>
    public virtual async Task<OfficeLetterheadSource> LoadSourceAsync(CancellationToken cancellationToken = default)
    {
        var officeId = _currentTenant.Id;
        if (officeId == null)
        {
            return new OfficeLetterheadSource(null, null, null, null);
        }

        // One doctor per office by design ("the office IS the doctor"); the oldest row wins if a
        // second ever appears, so the letterhead cannot flip between two names across renders.
        var doctors = await _doctorRepository.GetListAsync(cancellationToken: cancellationToken);
        var doctor = doctors.OrderBy(d => d.CreationTime).ThenBy(d => d.Id).FirstOrDefault();

        OfficeBranding? branding;
        using (_currentTenant.Change(null))
        {
            branding = await _brandingRepository.FirstOrDefaultAsync(
                b => b.OfficeId == officeId.Value, cancellationToken);
        }

        return new OfficeLetterheadSource(officeId, branding, doctor?.FirstName, doctor?.LastName);
    }
}

/// <summary>What an office's letterhead is computed from; see <see cref="OfficeLetterheadResolver.LoadSourceAsync"/>.</summary>
public sealed record OfficeLetterheadSource(
    Guid? OfficeId,
    OfficeBranding? Branding,
    string? DoctorFirstName,
    string? DoctorLastName);
