using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthcareSupport.CaseEvaluation.DataProtection;

/// <summary>
/// The ASP.NET Core Data Protection key ring, configured ONCE for both host processes.
///
/// <para><b>Both processes must share one key ring, protected the same way.</b> The AuthServer
/// issues the sign-in cookie and the API validates identity tokens the AuthServer minted (and the
/// reverse), so each must read the keys the other wrote. Different application names, different
/// stores, or keys one process cannot decrypt all fail the same quiet way: a cookie or token from
/// one host is unreadable at the other, which looks like a bad password. That is why the name, the
/// Redis key and the protection live here rather than in each module, and why
/// <c>DataProtectionKeyRingTests</c> checks that a payload protected by one process reads in the
/// other.</para>
///
/// <para><b>Why the keys are protected.</b> Persisting keys to an explicit store (Redis, in each
/// module) switches off the framework's default encryption at rest, so without this the key ring
/// is plain XML in Redis and anyone who can read Redis can forge a signed-in session. Each new key
/// is encrypted with the X.509 certificate named by <see cref="CertificatePathKey"/>. The decided
/// design, custody and reset procedure are in <c>docs/security/SESSION-KEY-ENCRYPTION.md</c>.</para>
///
/// <para><b>Only keys written after this is configured are encrypted.</b> A key already in the
/// ring stays readable, and in use, until it expires or is removed. The deploy therefore retires
/// the existing key deliberately (SESSION-KEY-ENCRYPTION.md, section 9), and
/// <see cref="KeyRingAtRestCheck"/> warns at every start while any unencrypted key remains.</para>
///
/// <para><b>On Azure</b> the same section gains <c>DataProtection:KeyVaultKeyId</c> (plan C2): a
/// Key Vault key reached through the VM's managed identity, so no certificate file or passphrase
/// is held on the host. That becomes a second branch in <see cref="ProtectKeysAtRest"/>.</para>
/// </summary>
public static class CaseEvaluationKeyRing
{
    /// <summary>Both processes MUST use the same name, or neither can read the other's payloads.</summary>
    public const string ApplicationName = "CaseEvaluation";

    /// <summary>The Redis key both processes persist the ring under.</summary>
    public const string RedisKey = "CaseEvaluation-Protection-Keys";

    /// <summary>Path to the .pfx that encrypts the key ring. Required outside Development.</summary>
    public const string CertificatePathKey = "DataProtection:CertificatePath";

    /// <summary>Passphrase for <see cref="CertificatePathKey"/>. Required outside Development.</summary>
    public const string CertificatePassPhraseKey = "DataProtection:CertificatePassPhrase";

    /// <summary>
    /// Registers Data Protection under the shared application name and, when a certificate is
    /// configured, encrypts every new key with it. The caller adds the store (Redis).
    /// </summary>
    public static IDataProtectionBuilder AddCaseEvaluationDataProtection(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var builder = services.AddDataProtection().SetApplicationName(ApplicationName);
        ProtectKeysAtRest(builder, configuration);
        services.AddHostedService<KeyRingAtRestCheck>();
        return builder;
    }

    /// <summary>
    /// Encrypts new keys with the configured certificate. Returns false when none is configured,
    /// which HostingConfigValidator refuses outside Development; in Development keys stay
    /// unencrypted, as they always have been there.
    /// </summary>
    public static bool ProtectKeysAtRest(IDataProtectionBuilder builder, IConfiguration configuration)
    {
        var path = configuration[CertificatePathKey];
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // X509CertificateLoader, not new X509Certificate2(...): the constructor is obsolete in
        // .NET 9+ (SYSLIB0057) and this repository treats warnings as errors.
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, configuration[CertificatePassPhraseKey]);
        builder.ProtectKeysWithCertificate(certificate);
        return true;
    }

    /// <summary>
    /// Ids of the keys in <paramref name="elements"/> whose secret is stored in plain text: the
    /// descriptor still holds a <c>masterKey</c> element instead of an encrypted secret.
    /// </summary>
    public static IReadOnlyList<string> FindUnencryptedKeys(IEnumerable<XElement> elements)
    {
        return elements
            .Where(element => element.Name.LocalName == "key")
            .Where(key => key.Descendants().Any(d => d.Name.LocalName == "masterKey"))
            .Select(key => (string?)key.Attribute("id") ?? "(no id)")
            .ToList();
    }
}

/// <summary>
/// Warns at start-up, in both processes, while the key ring still holds a key stored in plain
/// text although a certificate is configured. Enabling protection encrypts only NEW keys, so a key
/// written before the change stays readable in Redis, and usable to forge a session, until it is
/// retired. The deploy retires it (SESSION-KEY-ENCRYPTION.md, section 9); this makes a skipped
/// step visible in the log instead of silent. It never changes the ring itself.
/// </summary>
public sealed class KeyRingAtRestCheck : IHostedService
{
    private readonly IOptions<KeyManagementOptions> _keyManagementOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeyRingAtRestCheck> _logger;

    public KeyRingAtRestCheck(
        IOptions<KeyManagementOptions> keyManagementOptions,
        IConfiguration configuration,
        ILogger<KeyRingAtRestCheck> logger)
    {
        _keyManagementOptions = keyManagementOptions;
        _configuration = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration[CaseEvaluationKeyRing.CertificatePathKey]))
        {
            return Task.CompletedTask;
        }

        var repository = _keyManagementOptions.Value.XmlRepository;
        if (repository == null)
        {
            return Task.CompletedTask;
        }

        try
        {
            var unencrypted = CaseEvaluationKeyRing.FindUnencryptedKeys(repository.GetAllElements());
            if (unencrypted.Count > 0)
            {
                _logger.LogWarning(
                    "The Data Protection key ring holds {UnencryptedKeyCount} key(s) stored unencrypted " +
                    "({UnencryptedKeyIds}). New keys are encrypted, but these stay readable and in use until " +
                    "retired. Retire them as docs/security/SESSION-KEY-ENCRYPTION.md section 9 describes.",
                    unencrypted.Count,
                    string.Join(", ", unencrypted));
            }
        }
        catch (Exception ex)
        {
            // A check that cannot read the ring must not stop the host starting: the ring is read
            // again, and fails loudly, on the first protect or unprotect if the store is really gone.
            _logger.LogWarning(ex, "Could not read the Data Protection key ring to check it is encrypted.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
