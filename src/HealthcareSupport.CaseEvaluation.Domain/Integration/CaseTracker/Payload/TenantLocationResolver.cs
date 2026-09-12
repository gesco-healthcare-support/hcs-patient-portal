using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Locations;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Resolves the owning office, the specific clinic, and which MinIO bucket the document object
/// keys live in.
///
/// <para><c>FacilityId</c> is LOCATION-scoped, not tenant-scoped: an office has many clinics and
/// each carries its own external id, so the value published is the one belonging to THIS
/// appointment's clinic. It can be empty on rows created before the field shipped, which the
/// receiver is told to expect.</para>
/// </summary>
public class TenantLocationResolver : ITransientDependency
{
    /// <summary>Matches <c>CaseEvaluationDomainModule.ConfigureBlobStoring</c>'s fallback.</summary>
    public const string DefaultBucketName = "case-evaluation-documents";

    private const string BucketConfigurationKey = "BlobStoring:Minio:BucketName";

    private readonly IRepository<Location, Guid> _locationRepository;
    private readonly ITenantStore _tenantStore;
    private readonly IConfiguration _configuration;

    public TenantLocationResolver(
        IRepository<Location, Guid> locationRepository,
        ITenantStore tenantStore,
        IConfiguration configuration)
    {
        _locationRepository = locationRepository;
        _tenantStore = tenantStore;
        _configuration = configuration;
    }

    public virtual async Task<TenantLocationSection> ResolveAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        if (appointment is null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }

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
}
