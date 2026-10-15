using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using HealthcareSupport.CaseEvaluation.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DataProtection;

/// <summary>
/// The Data Protection key ring, as BOTH host processes configure it.
///
/// <para><b>What is asserted is the stored result, not the configuration calls.</b> Each test runs a
/// module's real <c>ConfigureDataProtection</c> on a bare <see cref="ServiceCollection"/>, as
/// <c>TenantResolverChainTests</c> does for the resolver chain, lets the framework mint a key, and
/// then reads the XML the framework wrote. The only substitution is the store: an in-memory
/// repository in place of Redis, shared between the two processes exactly as Redis is.</para>
///
/// <para><b>Why the two processes are tested together.</b> They must share one key ring, protected
/// the same way. If they drift -- another application name, another store, or keys one of them cannot
/// decrypt -- a cookie or identity token issued by one host is unreadable at the other, and sign-in
/// fails in a way that looks like a bad password.</para>
/// </summary>
public sealed class DataProtectionKeyRingTests : IDisposable
{
    private const string CertificatePassPhrase = "test-only-key-ring-passphrase";

    private readonly X509Certificate2 _certificate;
    private readonly string _certificatePath;
    private readonly List<ServiceProvider> _providers = new();

    public DataProtectionKeyRingTests()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=CaseEvaluation key ring test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        _certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        _certificatePath = Path.Combine(Path.GetTempPath(), $"key-ring-test-{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(_certificatePath, _certificate.Export(X509ContentType.Pkcs12, CertificatePassPhrase));
    }

    public void Dispose()
    {
        _providers.ForEach(provider => provider.Dispose());
        _certificate.Dispose();
        File.Delete(_certificatePath);
    }

    public static TheoryData<string> BothProcesses() => new() { "HttpApi.Host", "AuthServer" };

    [Theory]
    [MemberData(nameof(BothProcesses))]
    public void Each_process_stores_new_keys_encrypted_with_the_configured_certificate(string process)
    {
        var store = new InMemoryKeyStore();

        Protector(process, store).Protect("a sign-in cookie");

        var keys = store.Elements.Where(e => e.Name.LocalName == "key").ToList();
        keys.Count.ShouldBe(1);
        var stored = keys[0].ToString(SaveOptions.DisableFormatting);

        // Plain XML keeps the secret in a <masterKey> element; encrypted, it is gone.
        CaseEvaluationKeyRing.FindUnencryptedKeys(store.Elements).ShouldBeEmpty();
        stored.ShouldNotContain("masterKey");

        // Encrypted with THIS certificate, not by some other default mechanism (on Windows the
        // framework falls back to DPAPI, which would also remove masterKey).
        keys[0].Descendants().Single(d => d.Name.LocalName == "encryptedSecret")
            .Attribute("decryptorType")!.Value.ShouldContain("EncryptedXmlDecryptor");
        stored.ShouldContain(Convert.ToBase64String(_certificate.RawData));
    }

    [Fact]
    public void A_payload_protected_by_the_AuthServer_is_read_by_the_API()
    {
        var store = new InMemoryKeyStore();
        var protectedByAuthServer = Protector("AuthServer", store).Protect("a sign-in cookie");

        Protector("HttpApi.Host", store).Unprotect(protectedByAuthServer).ShouldBe("a sign-in cookie");
    }

    [Fact]
    public void A_payload_protected_by_the_API_is_read_by_the_AuthServer()
    {
        var store = new InMemoryKeyStore();
        var protectedByApi = Protector("HttpApi.Host", store).Protect("an email-confirmation token");

        Protector("AuthServer", store).Unprotect(protectedByApi).ShouldBe("an email-confirmation token");
    }

    [Fact]
    public void Both_processes_persist_the_ring_under_the_same_Redis_key()
    {
        // Nothing listens on port 1 and abortConnect=false returns at once, so no connection is
        // made: this reads the store each module REGISTERED, not a live one.
        var redis = new Dictionary<string, string?> { ["Redis:Configuration"] = "127.0.0.1:1,abortConnect=false,connectTimeout=100" };

        var api = RedisKeyOf(Build("HttpApi.Host", redis));
        var auth = RedisKeyOf(Build("AuthServer", redis));

        api.ShouldBe(CaseEvaluationKeyRing.RedisKey);
        auth.ShouldBe(api);
    }

    [Fact]
    public void Both_processes_use_the_same_application_name()
    {
        string NameOf(string process) => Build(process)
            .GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator!;

        NameOf("HttpApi.Host").ShouldBe(CaseEvaluationKeyRing.ApplicationName);
        NameOf("AuthServer").ShouldBe(CaseEvaluationKeyRing.ApplicationName);
    }

    [Fact]
    public void The_check_reports_a_key_stored_in_plain_text_and_passes_an_encrypted_one()
    {
        // A genuine plain-text key, written by the framework with no encryptor: what Redis holds
        // today, and what the deploy has to retire.
        var plain = new InMemoryKeyStore();
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName(CaseEvaluationKeyRing.ApplicationName);
        services.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = plain;
            options.XmlEncryptor = null;
        });
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("x");

        var plainKeyId = (string)plain.Elements.Single(e => e.Name.LocalName == "key").Attribute("id")!;
        CaseEvaluationKeyRing.FindUnencryptedKeys(plain.Elements).ShouldBe(new[] { plainKeyId });

        var encrypted = new InMemoryKeyStore();
        Protector("HttpApi.Host", encrypted).Protect("x");
        CaseEvaluationKeyRing.FindUnencryptedKeys(encrypted.Elements).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Production_refuses_to_start_without_the_settings_the_key_ring_reads(bool requireSigningCertificate)
    {
        // The validator lives in Domain and names the keys as strings. This pins them to the
        // constants the key ring actually reads, for both processes.
        foreach (var key in new[] { CaseEvaluationKeyRing.CertificatePathKey, CaseEvaluationKeyRing.CertificatePassPhraseKey })
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Server=sql-server;Database=CaseEvaluation;User Id=sa;Password=x",
                ["StringEncryption:DefaultPassPhrase"] = "real-16char-key12",
                ["Redis:Configuration"] = "redis",
                ["AuthServer:Authority"] = "https://auth.portal.example.com",
                ["App:SelfUrl"] = "https://auth.portal.example.com",
                ["AuthServer:CertificatePassPhrase"] = "real-pfx-passphrase",
                [CaseEvaluationKeyRing.CertificatePathKey] = "/app/dataprotection.pfx",
                [CaseEvaluationKeyRing.CertificatePassPhraseKey] = "real-key-ring-passphrase",
            };
            values.Remove(key);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

            Should.Throw<AbpException>(() => HostingConfigValidator.ValidateOrThrow(
                    configuration, isDevelopment: false, requireSigningCertificate))
                .Message.ShouldContain(key);
        }
    }

    private IDataProtector Protector(string process, InMemoryKeyStore store)
    {
        return Build(process, store: store)
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("CaseEvaluation.KeyRingTests");
    }

    /// <summary>Runs one process's real ConfigureDataProtection against a bare collection.</summary>
    private ServiceProvider Build(string process, Dictionary<string, string?>? extra = null, InMemoryKeyStore? store = null)
    {
        var values = new Dictionary<string, string?>
        {
            [CaseEvaluationKeyRing.CertificatePathKey] = _certificatePath,
            [CaseEvaluationKeyRing.CertificatePassPhraseKey] = CertificatePassPhrase,
        };
        foreach (var pair in extra ?? new Dictionary<string, string?>())
        {
            values[pair.Key] = pair.Value;
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var context = new ServiceConfigurationContext(services);
        Action<ServiceConfigurationContext, IConfiguration> configure = process == "AuthServer"
            ? CaseEvaluationAuthServerModule.ConfigureDataProtection
            : CaseEvaluationHttpApiHostModule.ConfigureDataProtection;
        configure(context, configuration);

        if (store != null)
        {
            // Stands in for Redis, and is shared between the two processes as Redis is.
            services.Configure<KeyManagementOptions>(options => options.XmlRepository = store);
        }

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider;
    }

    /// <summary>
    /// The Redis key of the store a module registered. The framework keeps it in a private field of
    /// its Redis repository, so it is found by TYPE; if a framework upgrade moves it, this fails
    /// with that message rather than passing.
    /// </summary>
    private static string RedisKeyOf(ServiceProvider provider)
    {
        var repository = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;
        repository.ShouldNotBeNull("no key store was registered, so Redis persistence is not configured");
        repository!.GetType().Name.ShouldBe("RedisXmlRepository");
        var field = repository.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(f => f.FieldType == typeof(RedisKey));
        field.ShouldNotBeNull("RedisXmlRepository no longer holds its key in a RedisKey field; update this test");
        return ((RedisKey)field!.GetValue(repository)!).ToString();
    }

    /// <summary>An in-memory key store, shared between processes the way Redis is.</summary>
    private sealed class InMemoryKeyStore : IXmlRepository
    {
        private readonly List<XElement> _elements = new();

        public IReadOnlyList<XElement> Elements => _elements;

        public IReadOnlyCollection<XElement> GetAllElements() => _elements.Select(e => new XElement(e)).ToList();

        public void StoreElement(XElement element, string friendlyName) => _elements.Add(new XElement(element));
    }
}
