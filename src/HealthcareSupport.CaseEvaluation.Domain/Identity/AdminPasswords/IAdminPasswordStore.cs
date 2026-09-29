using System;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;

/// <summary>
/// B12 -- where a database's generated admin password is kept, so that the process which creates the
/// database and the process which seeds it agree on one value without either holding a config secret.
///
/// <para>Two implementations exist and exactly one is configured outside Development: a Key Vault
/// store on Azure, and a folder store on the in-house server. They obey the SAME rules, which are
/// the whole contract:</para>
///
/// <list type="bullet">
///   <item>generate on first call and store the generated value;</item>
///   <item>on any later call return what is stored, unchanged;</item>
///   <item>never overwrite, and never delete.</item>
/// </list>
///
/// <para>"Never overwrite" is the load-bearing one. An overwrite silently separates the password an
/// operator holds from the password the account has, and nothing observes that until somebody cannot
/// sign in -- so both implementations create conditionally and READ BACK rather than trusting the
/// value they just generated.</para>
/// </summary>
public interface IAdminPasswordStore
{
    /// <summary>
    /// The stored admin password for one database, creating it on first call.
    ///
    /// <para><paramref name="tenantId"/> is null for the host database and the office's tenant id
    /// otherwise. The id rather than the name, so an office rename does not orphan its entry.</para>
    /// </summary>
    Task<string> GetOrCreateAsync(Guid? tenantId, CancellationToken cancellationToken = default);
}
