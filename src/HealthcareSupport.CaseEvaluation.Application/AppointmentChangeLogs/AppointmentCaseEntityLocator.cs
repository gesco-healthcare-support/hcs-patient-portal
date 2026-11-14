using System.Linq.Expressions;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Linq;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// Finds the ids of every audited row that belongs to ONE appointment, so the per-case change log
/// can read each one's audit history. Injury details, party links, claim examiner, insurance,
/// employer, authorized users, documents, packets, info requests and change requests FK to the
/// appointment; body parts hang off injury details; consent rounds and change-request documents hang
/// off change requests.
///
/// <para>Soft-deleted rows are INCLUDED. A removed authorized user or party is exactly the event the
/// history exists to show, and the default soft-delete filter would hide the row and with it the
/// whole audit trail. The tenant filter stays on, so only this office's rows are ever seen. Each type
/// is capped, so one pathological case cannot fan out into thousands of audit queries.</para>
///
/// <para>Repositories are resolved from the provider on demand (not constructor-injected) because
/// there are thirteen of them; that keeps the type to three dependencies.</para>
/// </summary>
public class AppointmentCaseEntityLocator : ITransientDependency
{
    /// <summary>Most rows read per entity type for one case.</summary>
    public const int MaxIdsPerType = 200;

    private readonly IServiceProvider _services;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _executer;

    public AppointmentCaseEntityLocator(
        IServiceProvider services,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter executer)
    {
        _services = services;
        _dataFilter = dataFilter;
        _executer = executer;
    }

    /// <summary>The (entity type, id) pairs of the appointment itself and everything under it.</summary>
    public virtual async Task<List<CaseEntityRef>> FindAsync(Guid appointmentId)
    {
        var refs = new List<CaseEntityRef> { new(AppointmentAuditedEntities.Appointment, appointmentId) };

        using (_dataFilter.Disable<ISoftDelete>())
        {
            var injuryIds = await IdsAsync<AppointmentInjuryDetail>(i => i.AppointmentId == appointmentId);
            var bodyPartIds = injuryIds.Count == 0
                ? new List<Guid>()
                : await IdsAsync<AppointmentBodyPart>(b => injuryIds.Contains(b.AppointmentInjuryDetailId));
            var changeRequestIds = await IdsAsync<AppointmentChangeRequest>(c => c.AppointmentId == appointmentId);

            Add(refs, AppointmentAuditedEntities.InjuryDetail, injuryIds);
            Add(refs, AppointmentAuditedEntities.BodyPart, bodyPartIds);
            // Claim Examiner + Primary Insurance are per-appointment (not per-injury) since #296.
            Add(refs, AppointmentAuditedEntities.ClaimExaminer,
                await IdsAsync<AppointmentClaimExaminer>(c => c.AppointmentId == appointmentId));
            Add(refs, AppointmentAuditedEntities.PrimaryInsurance,
                await IdsAsync<AppointmentPrimaryInsurance>(p => p.AppointmentId == appointmentId));
            Add(refs, AppointmentAuditedEntities.EmployerDetail,
                await IdsAsync<AppointmentEmployerDetail>(e => e.AppointmentId == appointmentId));
            Add(refs, AppointmentAuditedEntities.ApplicantAttorneyLink,
                await IdsAsync<AppointmentApplicantAttorney>(a => a.AppointmentId == appointmentId));
            Add(refs, AppointmentAuditedEntities.DefenseAttorneyLink,
                await IdsAsync<AppointmentDefenseAttorney>(a => a.AppointmentId == appointmentId));
            Add(refs, AppointmentAuditedEntities.Accessor,
                await IdsAsync<AppointmentAccessor>(a => a.AppointmentId == appointmentId));
            Add(refs, AppointmentAuditedEntities.Document,
                await IdsAsync<AppointmentDocument>(d => d.AppointmentId == appointmentId));
            Add(refs, AppointmentAuditedEntities.Packet,
                await IdsAsync<AppointmentPacket>(p => p.AppointmentId == appointmentId));
            Add(refs, AppointmentAuditedEntities.InfoRequest,
                await IdsAsync<AppointmentInfoRequest>(r => r.AppointmentId == appointmentId));
            Add(refs, AppointmentAuditedEntities.ChangeRequest, changeRequestIds);

            if (changeRequestIds.Count > 0)
            {
                Add(refs, AppointmentAuditedEntities.ConsentRound,
                    await IdsAsync<ChangeRequestConsentRound>(r => changeRequestIds.Contains(r.AppointmentChangeRequestId)));
                Add(refs, AppointmentAuditedEntities.ChangeRequestDocument,
                    await IdsAsync<AppointmentChangeRequestDocument>(d => changeRequestIds.Contains(d.AppointmentChangeRequestId)));
            }
        }

        return refs;
    }

    private static void Add(List<CaseEntityRef> refs, string entityType, IEnumerable<Guid> ids)
        => refs.AddRange(ids.Select(id => new CaseEntityRef(entityType, id)));

    private async Task<List<Guid>> IdsAsync<TEntity>(Expression<Func<TEntity, bool>> where)
        where TEntity : class, IEntity<Guid>
    {
        var repository = _services.GetRequiredService<IRepository<TEntity, Guid>>();
        var query = await repository.GetQueryableAsync();
        return await _executer.ToListAsync(
            query.Where(where).Select(e => e.Id).Take(MaxIdsPerType));
    }
}

/// <summary>One audited row of a case: its entity type name and id.</summary>
public sealed record CaseEntityRef(string EntityTypeFullName, Guid Id);
