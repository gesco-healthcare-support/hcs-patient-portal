using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// Rewrites each office row written by an older build so that it stores the database name only
/// (<see cref="TenantNaming.BuildStoredConnectionString"/>). The DbMigrator runs it before its office loop.
///
/// <para>Idempotent: a row already in that form is left alone, so every run after the first rewrites
/// nothing. Not needed for routing: <see cref="OfficeConnectionStringResolver"/> already reads only the
/// database name, so an older row routes correctly before and after. It is done so that the rows match what
/// new offices store. It goes through the repository, not SQL, so the entity-change event fires and the
/// cached tenant configuration is refreshed.</para>
///
/// <para>A row whose value names no office database is left unchanged and reported by id: the office is
/// refused until someone fixes it, and guessing a database for it is exactly what must not happen. Named
/// connection strings other than Default are removed; offices never use them, and nothing reads them now.
/// Only counts and office ids are logged, never a value.</para>
/// </summary>
public class OfficeDatabaseSettingRewriter : ITransientDependency
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICurrentTenant _currentTenant;

    public ILogger<OfficeDatabaseSettingRewriter> Logger { get; set; } = NullLogger<OfficeDatabaseSettingRewriter>.Instance;

    public OfficeDatabaseSettingRewriter(
        ITenantRepository tenantRepository,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant)
    {
        _tenantRepository = tenantRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
    }

    /// <summary>Returns how many office rows were rewritten.</summary>
    public async Task<int> RewriteAsync()
    {
        using (_currentTenant.Change(null))
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            var offices = await _tenantRepository.GetListAsync(includeDetails: true);
            var rewritten = 0;
            foreach (var office in offices)
            {
                if (await RewriteOneAsync(office))
                {
                    rewritten++;
                }
            }

            await uow.CompleteAsync();
            Logger.LogInformation(
                "Office database settings: {Rewritten} of {Offices} office rows rewritten to store the database name only.",
                rewritten, offices.Count);
            return rewritten;
        }
    }

    private async Task<bool> RewriteOneAsync(Tenant office)
    {
        var stored = office.FindDefaultConnectionString();
        if (!TenantNaming.TryGetSlugFromStoredConnectionString(stored, out var slug))
        {
            if (!string.IsNullOrWhiteSpace(stored))
            {
                Logger.LogError(
                    "Office {TenantId} has an office database setting that names no office database. Left unchanged; "
                    + "the office's requests are refused until it is corrected.", office.Id);
            }

            // An office with no setting at all is reported by the migrator's office loop.
            return false;
        }

        var named = office.ConnectionStrings
            .Where(c => c.Name != ConnectionStrings.DefaultConnectionStringName)
            .Select(c => c.Name)
            .ToList();
        var databaseNameOnly = TenantNaming.BuildStoredConnectionString(slug);
        if (stored == databaseNameOnly && named.Count == 0)
        {
            return false;
        }

        office.SetDefaultConnectionString(databaseNameOnly);
        foreach (var name in named)
        {
            office.RemoveConnectionString(name);
        }

        await _tenantRepository.UpdateAsync(office);
        return true;
    }
}
