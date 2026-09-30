using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Invitations;

/// <summary>
/// Repository for <see cref="Invitation"/>. Only adds the lookup-by-hash
/// helper the manager needs at validate / accept time; CRUD inherits
/// from <see cref="IRepository{TEntity, TKey}"/>.
/// </summary>
public interface IInvitationRepository : IRepository<Invitation, Guid>
{
    /// <summary>
    /// Returns the invitation row matching the given hex-encoded SHA256
    /// hash, or <c>null</c> when no row exists. Honors ABP's automatic
    /// <c>IMultiTenant</c> filter -- the caller must already be in the
    /// invite's tenant context (set via subdomain on the AuthServer
    /// register page).
    /// </summary>
    Task<Invitation?> FindByTokenHashAsync(
        string tokenHash, CancellationToken cancellationToken = default);
}
