using System;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;

/// <summary>
/// B12 -- a cross-PROCESS mutex around one store entry, held while it is created.
///
/// <para>The folder store does not need this: an exclusive file create is already atomic. The Key
/// Vault store does, and the reason is worth keeping, because the obvious alternatives were tried
/// and both fail:</para>
///
/// <list type="bullet">
///   <item>Key Vault has NO conditional create. Setting an existing secret adds a new VERSION, so
///   "set then read back" lets two racers each store a different value and each read back its
///   own.</item>
///   <item>"Oldest version wins" does not repair that. A version's creation time has one-SECOND
///   resolution, so sub-second racers tie, and the first caller can return version 1 before version
///   2 exists while a later tie-break picks version 2.</item>
/// </list>
///
/// <para>Key Vault offers no strict first-write ordering, so the ordering has to come from
/// somewhere else. Both stores are wrapped by the same decorator so there is one code path rather
/// than a rule that applies to one implementation; the folder store keeps its exclusive create as a
/// second, independent guarantee.</para>
///
/// <para>Declared in Domain and implemented in EntityFrameworkCore, so Domain does not take a
/// dependency on a database client to express the idea.</para>
/// </summary>
public interface IAdminPasswordStoreLock
{
    /// <summary>
    /// Takes the lock for <paramref name="name"/>, waiting if another process holds it, and returns
    /// a handle that releases it when disposed.
    ///
    /// <para>Throws rather than returning a failed handle when the lock cannot be taken: a caller
    /// that proceeds without it would create a second password for a database that already has one,
    /// which is the exact outcome this exists to prevent. Silence is not an option here.</para>
    /// </summary>
    Task<IAsyncDisposable> AcquireAsync(string name, CancellationToken cancellationToken = default);
}
