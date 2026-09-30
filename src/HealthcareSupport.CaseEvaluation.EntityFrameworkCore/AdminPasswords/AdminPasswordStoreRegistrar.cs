using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.AdminPasswords;

/// <summary>
/// B12 -- registers the configured admin-password store, and refuses to start when the
/// configuration does not name exactly one outside Development.
///
/// <para>Called by BOTH the DbMigrator and the API. Both, because the in-house server runs both in
/// Production and either may be the process that first creates a database -- a rule enforced in only
/// one of them would let the other seed a published default. One registrar rather than two copies,
/// so they cannot drift.</para>
/// </summary>
public static class AdminPasswordStoreRegistrar
{
    public static void Register(IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        Check.NotNull(services, nameof(services));
        Check.NotNull(configuration, nameof(configuration));

        // Throws here, at startup, when neither or both keys are set. Deliberately before anything
        // can seed: a process that got this far with no store would create an account on a published
        // password and report a successful start.
        var kind = AdminPasswordStoreSelector.Select(configuration, isDevelopment);

        switch (kind)
        {
            case AdminPasswordStoreKind.None:
                // Development only -- the selector cannot return this otherwise.
                services.AddSingleton<IAdminPasswordStore, PublishedDefaultAdminPasswordStore>();
                return;

            case AdminPasswordStoreKind.Directory:
                var directory = configuration[AdminPasswordStoreSelector.DirectoryKey]!;

                // The lock is registered HERE, beside the only thing that needs it, and not left to
                // ABP's conventional registration. Conventions expose a class only as itself and its
                // default interfaces -- those whose name, less the leading I, ends the class name --
                // and SqlAppLock is not an ...AdminPasswordStoreLock, so a dependency marker on it
                // registered nothing this factory can ask for, and no host could start.
                services.AddTransient<IAdminPasswordStoreLock, SqlAppLock>();
                services.AddSingleton<IAdminPasswordStore>(provider => new LockedAdminPasswordStore(
                    new FileAdminPasswordStore(directory),
                    provider.GetRequiredService<IAdminPasswordStoreLock>()));
                return;

            case AdminPasswordStoreKind.KeyVault:
            default:
                // The Key Vault store is not part of this build. It is refused loudly rather than
                // falling back, because a fallback here is the one outcome worth preventing: an
                // operator who set VaultUri believing passwords are generated, on a host that
                // quietly seeded a published default instead.
                throw new AbpException(
                    AdminPasswordStoreSelector.VaultUriKey + " is set, but the Key Vault store is " +
                    "not available in this build. It lands with the Azure infrastructure work, " +
                    "against a vault it can be verified on. Use " +
                    AdminPasswordStoreSelector.DirectoryKey + " on a self-hosted box until then.");
        }
    }
}
