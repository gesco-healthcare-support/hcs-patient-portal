using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.AdminPasswords;

/// <summary>
/// B12 -- serialises <see cref="IAdminPasswordStore.GetOrCreateAsync"/> across processes.
///
/// <para>Wraps BOTH stores rather than only the Key Vault one, so there is a single code path and
/// no rule of the form "this guarantee applies to one implementation". The folder store keeps its
/// own exclusive create underneath, which means the folder path is safe even if this decorator is
/// ever unregistered by mistake -- and the Key Vault path is not, which is why the decorator is not
/// optional there.</para>
///
/// <para>The lock is taken per ENTRY, not globally, so creating an office's password never waits on
/// an unrelated office. Two racers for the same database are the only case that serialises, which
/// is the only case that can produce two passwords for one account.</para>
/// </summary>
public sealed class LockedAdminPasswordStore : IAdminPasswordStore
{
    private readonly IAdminPasswordStore _inner;
    private readonly IAdminPasswordStoreLock _storeLock;

    public LockedAdminPasswordStore(IAdminPasswordStore inner, IAdminPasswordStoreLock storeLock)
    {
        _inner = Check.NotNull(inner, nameof(inner));
        _storeLock = Check.NotNull(storeLock, nameof(storeLock));
    }

    public async Task<string> GetOrCreateAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var name = AdminPasswordNames.For(tenantId);

        var handle = await _storeLock.AcquireAsync(name, cancellationToken).ConfigureAwait(false);
        await using (handle.ConfigureAwait(false))
        {
            return await _inner.GetOrCreateAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }
    }
}
