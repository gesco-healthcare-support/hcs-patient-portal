using System;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;

/// <summary>
/// B12 -- the Development store: it hands back the published default password, which is what this
/// product seeded everywhere before B12 and what every local runbook, docker guide and onboarding
/// page tells a developer to sign in with.
///
/// <para>Registered ONLY when <see cref="AdminPasswordStoreSelector"/> returns
/// <see cref="AdminPasswordStoreKind.None"/>, which it can only do in Development. Outside
/// Development the selector throws rather than reaching this class, so there is no configuration in
/// which a deployed host quietly gets it.</para>
///
/// <para>It exists so the seeding call sites have ONE code path. The alternative -- an
/// <c>if (isDevelopment)</c> at each of the four places a password is seeded -- is four chances to
/// add a fifth site and forget the check, and the failure mode of forgetting is an account on a
/// published password with nothing reporting it.</para>
/// </summary>
public sealed class PublishedDefaultAdminPasswordStore : IAdminPasswordStore
{
    public Task<string> GetOrCreateAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CaseEvaluationConsts.AdminPasswordDefaultValue);
    }
}
