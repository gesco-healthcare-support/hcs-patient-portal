using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// Dev/default <see cref="ITenantConnectionStringProvider"/>: derives the office
/// connection string from the host "Default" connection string by pointing it at
/// the office database "CaseEvaluation_{slug}". The SQL credentials therefore stay
/// in the single, secret-managed Default and are never duplicated into a second
/// config key. Set "App:TenantDbTemplate" to override the base server/auth (e.g.
/// to place office databases on a separate SQL instance). Auto-registered by ABP
/// convention, so it is available in every host that loads the Domain module
/// (HttpApi.Host runtime provisioning + the DbMigrator bulk path). Routing of
/// tenant queries stays with the stock ABP MultiTenantConnectionStringResolver,
/// which reads the connection string this provider stored on the tenant record.
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
