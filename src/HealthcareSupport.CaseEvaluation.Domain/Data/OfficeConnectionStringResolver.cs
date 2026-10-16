using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// Routes every query of an office to that office's own database, and refuses rather than falling back.
///
/// <para><b>Replaces ABP's stock <see cref="MultiTenantConnectionStringResolver"/>.</b> The stock one returns
/// a whole connection string stored on the tenant row, and when an office has none -- no row, or a blank
/// Default -- it returns the global Default, which is the HOST database. Under database-per-office that
/// sends an office's reads and writes to the wrong database without any error: its own data looks missing
/// and new rows land in the host database. This resolver throws instead.</para>
///
/// <para><b>Office rows store only their database name</b> (<see cref="TenantNaming.BuildStoredConnectionString"/>).
/// ONLY that name is read from the row; the server, login and options are composed from configuration by
/// <see cref="ITenantConnectionStringProvider"/>, the same builder office creation uses. So the login lives in
/// configuration alone, and a change there applies to every office on its next request. A value written by
/// an older build, carrying a server and login, still routes: those parts are ignored.</para>
///
/// <para><b>In office context it throws <see cref="OfficeDatabaseNotConfiguredException"/></b> when the office
/// is not found, has no stored Default, or the stored value names no "CaseEvaluation_{slug}" database (which
/// includes the host catalog). It never calls the base resolver for an office. Host context is unchanged:
/// no current office means the stock host lookup.</para>
///
/// <para>Every connection-string NAME resolves to the office database in office context, exactly as the stock
/// resolver did here: no module maps a separate database, and offices only ever stored a Default. A second
/// per-office database (an audit store, say) would need its own branch in this class.</para>
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IConnectionStringResolver), typeof(OfficeConnectionStringResolver))]
public class OfficeConnectionStringResolver : MultiTenantConnectionStringResolver
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantConnectionStringProvider _connectionStringProvider;

    public OfficeConnectionStringResolver(
        IOptionsMonitor<AbpDbConnectionOptions> options,
        ICurrentTenant currentTenant,
        IServiceProvider serviceProvider,
        ITenantConnectionStringProvider connectionStringProvider)
        : base(options, currentTenant, serviceProvider)
    {
        _currentTenant = currentTenant;
        _connectionStringProvider = connectionStringProvider;
    }

    public override async Task<string> ResolveAsync(string? connectionStringName = null)
    {
        var officeId = _currentTenant.Id;
        if (officeId == null)
        {
            return await base.ResolveAsync(connectionStringName);
        }

        return ForOffice(officeId.Value, await FindTenantConfigurationAsync(officeId.Value));
    }

    [Obsolete("Use ResolveAsync method.")]
    public override string Resolve(string? connectionStringName = null)
    {
        var officeId = _currentTenant.Id;
        if (officeId == null)
        {
            return base.Resolve(connectionStringName);
        }

        return ForOffice(officeId.Value, FindTenantConfiguration(officeId.Value));
    }

    private string ForOffice(Guid officeId, TenantConfiguration? office)
    {
        if (office == null)
        {
            throw new OfficeDatabaseNotConfiguredException(officeId, "no office record");
        }

        var stored = office.ConnectionStrings?.Default;
        if (!TenantNaming.TryGetSlugFromStoredConnectionString(stored, out var slug))
        {
            throw new OfficeDatabaseNotConfiguredException(officeId, string.IsNullOrWhiteSpace(stored)
                ? "no office database setting"
                : "an office database setting that names no office database");
        }

        return _connectionStringProvider.BuildConnectionString(slug);
    }
}
