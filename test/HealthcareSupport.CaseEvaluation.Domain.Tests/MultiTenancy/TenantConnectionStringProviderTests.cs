using System.Collections.Generic;
using System.Data.Common;
using HealthcareSupport.CaseEvaluation.Data;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// Unit tests for <see cref="TenantConnectionStringProvider"/> (in-memory config,
/// no DB / DI host). Pins the secret seam behaviour: derive the office connection
/// string from the host "Default", honour the App:TenantDbTemplate override, and
/// fail when neither is configured.
/// </summary>
public class TenantConnectionStringProviderTests
{
    private const string Default =
        "Server=(LocalDb)\\MSSQLLocalDB;Database=CaseEvaluation;Trusted_Connection=True;TrustServerCertificate=true";

    private static TenantConnectionStringProvider Build(params (string Key, string Value)[] settings)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in settings)
        {
            dict[key] = value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();

        return new TenantConnectionStringProvider(configuration);
    }

    [Fact]
    public void Derives_from_Default_when_no_override()
    {
        var provider = Build(("ConnectionStrings:Default", Default));

        var result = provider.BuildConnectionString("falkinstein");

        var parsed = new DbConnectionStringBuilder { ConnectionString = result };
        parsed["Database"].ShouldBe("CaseEvaluation_falkinstein");
        parsed["Server"].ShouldBe("(LocalDb)\\MSSQLLocalDB");
    }

    [Fact]
    public void Uses_the_template_override_when_set()
    {
        var provider = Build(
            ("ConnectionStrings:Default", Default),
            ("App:TenantDbTemplate", "Server=tenant-sql,1433;Database=ignored;User Id=sa;Password=p;TrustServerCertificate=true"));

        var result = provider.BuildConnectionString("drsmith");

        var parsed = new DbConnectionStringBuilder { ConnectionString = result };
        parsed["Server"].ShouldBe("tenant-sql,1433");
        parsed["Database"].ShouldBe("CaseEvaluation_drsmith");
    }

    [Fact]
    public void Throws_when_no_base_connection_string_is_configured()
    {
        var provider = Build();

        Should.Throw<AbpException>(() => provider.BuildConnectionString("falkinstein"));
    }
}
