using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Removes office-side grant rows of the permissions in <see cref="PermissionNames"/>. Each became
/// Host-only because what it gates acts on every office. Grant rows written inside offices before
/// that change -- ABP auto-grants every permission an office can hold to each office's static admin
/// role -- no longer open anything, since ABP refuses a Host-only permission inside an office before
/// reading any grant. This clears them so an office's grant table says what the office can actually
/// do.
///
/// <para>WHERE IT RUNS. As a seed contributor, it runs in the DbMigrator's per-office pass on every
/// deploy, and when a new office is seeded. It acts only when the seed context names an office;
/// the host pass is a no-op, so host grants of the same permissions are never touched.</para>
///
/// <para>IDEMPOTENT. The first run in an office deletes that office's rows; every later run finds none
/// and does nothing. Nothing can write them back: ABP's own permission seeding grants an office admin
/// only permissions whose side includes the office, and the permission manager refuses to set a
/// Host-only permission inside one.</para>
///
/// <para>WHICH ROWS. Every row with one of these names in the office, under ANY grant provider (role,
/// user or client), because each is equally inert. Nothing else: other permissions, including the
/// office-side <c>PushToCaseTracker</c> that the in-office push button still uses, stay as they are.
/// Rows are deleted as entities, not in bulk, so ABP's grant cache drops them in the same unit of
/// work.</para>
///
/// <para>ADDING A NAME. When another permission becomes Host-only after offices were seeded, add it
/// here; the test pins the list to the permission constants.</para>
/// </summary>
public class HostOnlyPermissionGrantCleanupContributor : IDataSeedContributor, ITransientDependency
{
    /// <summary>
    /// The permissions this removes office grants of. Literals because this project does not
    /// reference the permission constants (the role seed spells them the same way); a test pins each
    /// one to its <c>CaseEvaluationPermissions</c> constant.
    /// </summary>
    public static readonly IReadOnlyList<string> PermissionNames =
    [
        "CaseEvaluation.Appointments.ViewIntegrationDeadLetters",
        "CaseEvaluation.IntakeAssignments.Manage",
    ];

    private readonly IPermissionGrantRepository _grantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<HostOnlyPermissionGrantCleanupContributor> _logger;

    public HostOnlyPermissionGrantCleanupContributor(
        IPermissionGrantRepository grantRepository,
        ICurrentTenant currentTenant,
        ILogger<HostOnlyPermissionGrantCleanupContributor> logger)
    {
        _grantRepository = grantRepository;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId == null)
        {
            return;
        }

        using (_currentTenant.Change(context.TenantId))
        {
            // The whole table for this office, filtered here: an office's grant table is small, and
            // the repository has no name-only query. The explicit TenantId match keeps this exact on
            // an office that shares a database with others.
            var officeRows = (await _grantRepository.GetListAsync())
                .Where(g => PermissionNames.Contains(g.Name) && g.TenantId == context.TenantId)
                .ToList();

            if (officeRows.Count == 0)
            {
                return;
            }

            await _grantRepository.DeleteManyAsync(officeRows, autoSave: true);

            foreach (var byName in officeRows.GroupBy(g => g.Name))
            {
                _logger.LogInformation(
                    "HostOnlyPermissionGrantCleanupContributor: removed {Count} office grant row(s) of {Permission} in office {OfficeId}; it is Host-only.",
                    byName.Count(), byName.Key, context.TenantId);
            }
        }
    }
}
