using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Hosting;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// T12 (in-house hosting, 2026-07-09) -- pure unit tests for the fail-fast production config
/// validator. Pins: no-op in Development; passes with real values; throws (listing the offending
/// KEYS, never values) when a required secret/config is missing or a placeholder; the signing-cert
/// passphrase is required only for the AuthServer.
/// </summary>
public class HostingConfigValidatorTests
{
    private static Dictionary<string, string?> ValidProd() => new()
    {
        ["ConnectionStrings:Default"] = "Server=sql-server;Database=CaseEvaluation;User Id=sa;Password=x;TrustServerCertificate=True",
        ["StringEncryption:DefaultPassPhrase"] = "real-16char-key12",
        ["Redis:Configuration"] = "redis",
        ["AuthServer:Authority"] = "https://auth.portal.example.com",
        ["App:SelfUrl"] = "https://auth.portal.example.com",
        ["AuthServer:CertificatePassPhrase"] = "real-pfx-passphrase",
    };

    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Skips_validation_in_development()
    {
        // Empty config would fail in prod, but Development is a no-op.
        Should.NotThrow(() => HostingConfigValidator.ValidateOrThrow(
            Build(new Dictionary<string, string?>()), isDevelopment: true, requireSigningCertificate: true));
    }

    [Fact]
    public void Passes_when_all_required_values_are_present()
    {
        Should.NotThrow(() => HostingConfigValidator.ValidateOrThrow(
            Build(ValidProd()), isDevelopment: false, requireSigningCertificate: true));
    }

    [Fact]
    public void Throws_and_names_a_missing_key()
    {
        var values = ValidProd();
        values.Remove("StringEncryption:DefaultPassPhrase");

        var ex = Should.Throw<AbpException>(() => HostingConfigValidator.ValidateOrThrow(
            Build(values), isDevelopment: false, requireSigningCertificate: true));

        ex.Message.ShouldContain("StringEncryption:DefaultPassPhrase");
    }

    [Fact]
    public void Throws_on_a_placeholder_value_without_leaking_it()
    {
        var values = ValidProd();
        values["StringEncryption:DefaultPassPhrase"] = "REPLACE_ME_LOCALLY";

        var ex = Should.Throw<AbpException>(() => HostingConfigValidator.ValidateOrThrow(
            Build(values), isDevelopment: false, requireSigningCertificate: true));

        ex.Message.ShouldContain("StringEncryption:DefaultPassPhrase");
        // OWASP: the message lists the key, never the (placeholder or real) value.
        ex.Message.ShouldNotContain("REPLACE_ME_LOCALLY");
    }

    [Fact]
    public void Throws_when_the_localdb_dev_default_leaks_into_prod()
    {
        var values = ValidProd();
        values["ConnectionStrings:Default"] =
            "Server=(LocalDb)\\MSSQLLocalDB;Database=CaseEvaluation;Trusted_Connection=True";

        var ex = Should.Throw<AbpException>(() => HostingConfigValidator.ValidateOrThrow(
            Build(values), isDevelopment: false, requireSigningCertificate: true));

        ex.Message.ShouldContain("ConnectionStrings:Default");
    }

    [Fact]
    public void Requires_the_signing_certificate_passphrase_only_for_the_authserver()
    {
        var values = ValidProd();
        values.Remove("AuthServer:CertificatePassPhrase");

        // API (no signing cert): fine without the passphrase.
        Should.NotThrow(() => HostingConfigValidator.ValidateOrThrow(
            Build(values), isDevelopment: false, requireSigningCertificate: false));

        // AuthServer: must have it.
        var ex = Should.Throw<AbpException>(() => HostingConfigValidator.ValidateOrThrow(
            Build(values), isDevelopment: false, requireSigningCertificate: true));
        ex.Message.ShouldContain("AuthServer:CertificatePassPhrase");
    }

    [Fact]
    public void Lists_every_offending_key_at_once()
    {
        var ex = Should.Throw<AbpException>(() => HostingConfigValidator.ValidateOrThrow(
            Build(new Dictionary<string, string?>()), isDevelopment: false, requireSigningCertificate: true));

        ex.Message.ShouldContain("ConnectionStrings:Default");
        ex.Message.ShouldContain("StringEncryption:DefaultPassPhrase");
        ex.Message.ShouldContain("Redis:Configuration");
        ex.Message.ShouldContain("AuthServer:Authority");
        ex.Message.ShouldContain("App:SelfUrl");
        ex.Message.ShouldContain("AuthServer:CertificatePassPhrase");
    }
}
