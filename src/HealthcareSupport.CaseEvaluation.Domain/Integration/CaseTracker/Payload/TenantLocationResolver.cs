using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Locations;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Resolves the owning office, the specific clinic, the office's letterhead facts, and which MinIO
/// bucket the document object keys live in.
///
/// <para><c>FacilityId</c> is LOCATION-scoped, not tenant-scoped: an office has many clinics and
/// each carries its own external id, so the value published is the one belonging to THIS
/// appointment's clinic. It can be empty on rows created before the field shipped, which the
/// receiver is told to expect.</para>
///
/// <para>The letterhead (2026-11-15) lives here rather than as a tenth dependency of
/// <see cref="IntakePayloadBuilder"/>: it is a fact about the owning office, and this resolver
/// already owns those. <see cref="OfficeLetterheadResolver"/> reads whichever office is CURRENT, so
/// the read is pinned to the APPOINTMENT's office with an explicit <c>Change</c>. The builder
/// already runs in that office, which makes the switch a no-op today; without it, a caller in
/// another scope would publish another office's address to the Case Tracker, or empty host
/// defaults, and nothing downstream could tell.</para>
/// </summary>
public class TenantLocationResolver : ITransientDependency
{
    /// <summary>Matches <c>CaseEvaluationDomainModule.ConfigureBlobStoring</c>'s fallback.</summary>
    public const string DefaultBucketName = "case-evaluation-documents";

    private const string BucketConfigurationKey = "BlobStoring:Minio:BucketName";

    private readonly IRepository<Location, Guid> _locationRepository;
    private readonly ITenantStore _tenantStore;
    private readonly IConfiguration _configuration;
    private readonly OfficeLetterheadResolver _officeLetterheadResolver;
    private readonly ICurrentTenant _currentTenant;

    public TenantLocationResolver(
        IRepository<Location, Guid> locationRepository,
        ITenantStore tenantStore,
        IConfiguration configuration,
        OfficeLetterheadResolver officeLetterheadResolver,
        ICurrentTenant currentTenant)
    {
        _locationRepository = locationRepository;
        _tenantStore = tenantStore;
        _configuration = configuration;
        _officeLetterheadResolver = officeLetterheadResolver;
        _currentTenant = currentTenant;
    }

    public virtual async Task<TenantLocationSection> ResolveAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        var section = new TenantLocationSection();

        var location = await _locationRepository.FindAsync(appointment.LocationId, cancellationToken: cancellationToken);
        if (location != null)
        {
            section.Location.Name = location.Name;
            section.Location.Address = location.Address;
            section.Location.City = location.City;
            section.Location.ZipCode = location.ZipCode;
            section.Tenant.FacilityId = location.FacilityId;
        }

        section.Tenant.TenantId = appointment.TenantId;
        if (appointment.TenantId is { } tenantId)
        {
            var tenant = await _tenantStore.FindAsync(tenantId);
            section.Tenant.OfficeName = tenant?.Name ?? string.Empty;

            // No office, no letterhead: an appointment without one keeps an all-null section.
            using (_currentTenant.Change(tenantId))
            {
                var letterhead = await _officeLetterheadResolver.ResolveAsync(cancellationToken);
                section.Office = IntakeOfficeSection.From(letterhead);
            }
        }

        return section;
    }

    /// <summary>
    /// Read from configuration rather than hardcoded so the published bucket cannot drift from the
    /// bucket the blob provider actually writes to.
    /// </summary>
    public virtual string ResolveBucketName()
    {
        var configured = _configuration[BucketConfigurationKey];
        return string.IsNullOrWhiteSpace(configured) ? DefaultBucketName : configured;
    }
}

/// <summary>Result of <see cref="TenantLocationResolver"/>.</summary>
public class TenantLocationSection
{
    public IntakeTenantSection Tenant { get; set; } = new();

    public IntakeLocationSection Location { get; set; } = new();

    public IntakeOfficeSection Office { get; set; } = new();
}
