using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// Dev/default <see cref="ITenantConnectionStringProvider"/>: derives the office
/// connection string from the host "Default" connection string by pointing it at
/// the office database "CaseEvaluation_{slug}". The SQL login therefore lives in
/// the single, secret-managed Default and nowhere else. Set "App:TenantDbTemplate"
/// to override the base server/auth (e.g. to place office databases on a separate
/// SQL instance). Auto-registered by ABP convention, so it is available in every
/// host that loads the Domain module (api, authserver, migrator).
///
/// <para>Called on EVERY office request, not only at creation: <see cref="OfficeConnectionStringResolver"/>
/// reads the office's database name from its row (the row stores nothing else) and asks this for the
/// full string. Office creation calls it too, only to check the server is reachable.</para>
/// </summary>
public class TenantConnectionStringProvider : ITenantConnectionStringProvider, ITransientDependency
{
    /// <summary>Optional override base connection string (server/auth, any database).</summary>
    public const string TenantDbTemplateConfigKey = "App:TenantDbTemplate";

    /// <summary>The host connection string used as the base when no override is set.</summary>
    public const string DefaultConnectionStringName = "Default";

    private readonly IConfiguration _configuration;

    public TenantConnectionStringProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string BuildConnectionString(string slug)
    {
        var baseConnectionString = _configuration[TenantDbTemplateConfigKey];
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            baseConnectionString = _configuration.GetConnectionString(DefaultConnectionStringName);
        }

        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new AbpException(
                $"No base connection string is configured. Set '{TenantDbTemplateConfigKey}' " +
                $"or ConnectionStrings:{DefaultConnectionStringName}.");
        }

        return TenantNaming.BuildConnectionString(baseConnectionString, slug);
    }
}
