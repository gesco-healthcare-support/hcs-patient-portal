using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>One office as the Case Tracker's system administrator sees it when linking a doctor to an office.</summary>
public sealed record CaseTrackerOfficeEntry(Guid Id, string PracticeName, string DoctorName);

/// <summary>
/// Every office's id, practice name and doctor name, so the Case Tracker can link each of its doctors to an office
/// without anyone typing a GUID. Nothing else leaves: no address, no admin login, no setting.
///
/// <para>The names are the ones the letterhead prints: the doctor is the letterhead's physician, and the practice
/// follows <see cref="OfficeDisplayNames.Compose"/> (the practice name, else the doctor, else the web-address
/// slug), so an office with nothing filled in is never listed blank.</para>
///
/// <para>A DOMAIN service, behind the token-gated controller, for the reason <see cref="CaseTrackerFeedService"/>
/// gives. An office whose names cannot be read is still listed, under its slug with no doctor, so one bad
/// database never hides the rest.</para>
/// </summary>
public class CaseTrackerOfficeListService : ITransientDependency
{
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly OfficeLetterheadResolver _letterheadResolver;
    private readonly ITenantStore _tenantStore;
    private readonly ILogger<CaseTrackerOfficeListService> _logger;

    public CaseTrackerOfficeListService(
        ITenantWorkRunner tenantWorkRunner,
        OfficeLetterheadResolver letterheadResolver,
        ITenantStore tenantStore,
        ILogger<CaseTrackerOfficeListService> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _letterheadResolver = letterheadResolver;
        _tenantStore = tenantStore;
        _logger = logger;
    }

    public virtual async Task<IReadOnlyList<CaseTrackerOfficeEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _tenantWorkRunner.AggregateAcrossOfficesAsync(officeId => ReadOfficeAsync(officeId, cancellationToken));
        return entries
            .OrderBy(e => e.PracticeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id)
            .ToList();
    }

    private async Task<CaseTrackerOfficeEntry> ReadOfficeAsync(Guid officeId, CancellationToken cancellationToken)
    {
        var slug = (await _tenantStore.FindAsync(officeId))?.Name;
        try
        {
            var letterhead = await _letterheadResolver.ResolveAsync(cancellationToken);
            var names = OfficeDisplayNames.Compose(letterhead.PhysicianName, letterhead.PracticeName, slug);
            return new CaseTrackerOfficeEntry(officeId, names.PracticeName, letterhead.PhysicianName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ids only in the log.
            _logger.LogWarning(ex, "CaseTrackerOfficeListService: could not read the names of office {OfficeId}.", officeId);
            return new CaseTrackerOfficeEntry(officeId, slug ?? string.Empty, string.Empty);
        }
    }
}
