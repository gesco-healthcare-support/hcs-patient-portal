using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Removes office-side grant rows of <c>CaseEvaluation.Appointments.ViewIntegrationDeadLetters</c>
/// (2026-09-24). That permission became Host-only, because the failures list it gates aggregates every
/// office. Grant rows written inside offices before the change -- ABP auto-granted it to each office's
/// static admin role while it was Both-sided -- no longer open anything, since ABP refuses a Host-only
/// permission inside an office before reading any grant. This clears them so an office's grant table
/// says what the office can actually do.
///
/// <para>WHERE IT RUNS. As a seed contributor, it runs in the DbMigrator's per-office pass on every
/// deploy, and when a new office is seeded. It acts only when the seed context names an office;
/// the host pass is a no-op, so host grants of the same permission are never touched.</para>
///
/// <para>IDEMPOTENT. The first run in an office deletes that office's rows; every later run finds none
/// and does nothing. Nothing can write them back: ABP's own permission seeding grants an office admin
/// only permissions whose side includes the office, and the permission manager refuses to set a
/// Host-only permission inside one.</para>
///
/// <para>WHICH ROWS. Every row with this name in the office, under ANY grant provider (role, user or
/// client), because each is equally inert. Nothing else: other permissions, and the office-side
/// <c>PushToCaseTracker</c> that the in-office push button still uses, stay as they are. Rows are
/// deleted as entities, not in bulk, so ABP's grant cache drops them in the same unit of work.</para>
/// </summary>
public class HostOnlyIntegrationGrantCleanupContributor : IDataSeedContributor, ITransientDependency
{
    /// <summary>
    /// The one permission this removes office grants of. A literal because this project does not
    /// reference the permission constants (the role seed spells them the same way); a test pins it to
    /// <c>CaseEvaluationPermissions.Appointments.ViewIntegrationDeadLetters</c>.
    /// </summary>
    public const string PermissionName = "CaseEvaluation.Appointments.ViewIntegrationDeadLetters";

    private readonly IPermissionGrantRepository _grantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<HostOnlyIntegrationGrantCleanupContributor> _logger;

    public HostOnlyIntegrationGrantCleanupContributor(
        IPermissionGrantRepository grantRepository,
        ICurrentTenant currentTenant,
        ILogger<HostOnlyIntegrationGrantCleanupContributor> logger)
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
                .Where(g => g.Name == PermissionName && g.TenantId == context.TenantId)
                .ToList();

            if (officeRows.Count == 0)
            {
                return;
            }

            await _grantRepository.DeleteManyAsync(officeRows, autoSave: true);

            _logger.LogInformation(
                "HostOnlyIntegrationGrantCleanupContributor: removed {Count} office grant row(s) of {Permission} in office {OfficeId}; it is Host-only.",
                officeRows.Count, PermissionName, context.TenantId);
        }
    }
}
