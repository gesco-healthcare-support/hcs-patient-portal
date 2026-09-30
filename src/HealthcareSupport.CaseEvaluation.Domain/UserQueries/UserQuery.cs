using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.UserQueries;

/// <summary>
/// A free-text question submitted by an external user through the
/// "Help / Need Question?" (Contact-Us) modal. Port of OLD
/// <c>spm.UserQueries</c> (PatientAppointment.DbEntities\Models\UserQuery.cs).
///
/// <para>OLD stored a separate <c>UserId</c> FK plus
/// <c>CreatedById</c>/<c>CreatedDate</c> -- all the same submitter. ABP's
/// <see cref="FullAuditedAggregateRoot{TKey}"/> audit columns
/// (<c>CreatorId</c>/<c>CreationTime</c>) capture that, so the redundant
/// FK is dropped. OLD's transient <c>RequestConfirmationNumber</c>/
/// <c>AppointmentId</c> were <c>[NotMapped]</c> routing inputs and remain
/// off the row (they travel on the submitted event instead).</para>
/// </summary>
[Audited]
public class UserQuery : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    /// <summary>The question text. Required, max 500 chars (OLD parity).</summary>
    [NotNull]
    public virtual string Message { get; set; } = null!;

    protected UserQuery()
    {
    }

    public UserQuery(Guid id, string message)
    {
        Id = id;
        Check.NotNullOrWhiteSpace(message, nameof(message), UserQueryConsts.MessageMaxLength);
        Message = message;
    }
}
