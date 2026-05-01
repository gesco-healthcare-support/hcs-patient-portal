using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public class EfCoreAppointmentInjuryDetailRepository : EfCoreRepository<CaseEvaluationDbContext, AppointmentInjuryDetail, Guid>, IAppointmentInjuryDetailRepository
{
    public EfCoreAppointmentInjuryDetailRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<AppointmentInjuryDetailWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var injury = await (await GetDbSetAsync()).Where(b => b.Id == id).FirstOrDefaultAsync(cancellationToken);
        if (injury == null) return null;
        return new AppointmentInjuryDetailWithNavigationProperties
        {
            AppointmentInjuryDetail = injury,
            Appointment = await dbContext.Set<Appointment>().FirstOrDefaultAsync(a => a.Id == injury.AppointmentId, cancellationToken),
            WcabOffice = injury.WcabOfficeId.HasValue ? await dbContext.Set<WcabOffice>().FirstOrDefaultAsync(w => w.Id == injury.WcabOfficeId.Value, cancellationToken) : null,
            BodyParts = await dbContext.Set<AppointmentBodyPart>().Where(b => b.AppointmentInjuryDetailId == injury.Id).ToListAsync(cancellationToken),
            ClaimExaminer = await dbContext.Set<AppointmentClaimExaminer>().Where(c => c.AppointmentInjuryDetailId == injury.Id).FirstOrDefaultAsync(cancellationToken),
            PrimaryInsurance = await dbContext.Set<AppointmentPrimaryInsurance>().Where(p => p.AppointmentInjuryDetailId == injury.Id).FirstOrDefaultAsync(cancellationToken),
        };
    }

    public virtual async Task<List<AppointmentInjuryDetailWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, Guid? appointmentId = null, string? claimNumber = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var query = await GetQueryableAsync();
        query = ApplyFilter(query, filterText, appointmentId, claimNumber);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentInjuryDetailConsts.GetDefaultSorting(false) : sorting);
        var injuries = await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
        if (injuries.Count == 0) return new List<AppointmentInjuryDetailWithNavigationProperties>();

        var injuryIds = injuries.Select(x => x.Id).ToList();
        var bodyPartsByInjury = (await dbContext.Set<AppointmentBodyPart>().Where(b => injuryIds.Contains(b.AppointmentInjuryDetailId)).ToListAsync(cancellationToken))
            .GroupBy(b => b.AppointmentInjuryDetailId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var examinersByInjury = (await dbContext.Set<AppointmentClaimExaminer>().Where(c => injuryIds.Contains(c.AppointmentInjuryDetailId)).ToListAsync(cancellationToken))
            .GroupBy(c => c.AppointmentInjuryDetailId)
            .ToDictionary(g => g.Key, g => g.First());
        var insurancesByInjury = (await dbContext.Set<AppointmentPrimaryInsurance>().Where(p => injuryIds.Contains(p.AppointmentInjuryDetailId)).ToListAsync(cancellationToken))
            .GroupBy(p => p.AppointmentInjuryDetailId)
            .ToDictionary(g => g.Key, g => g.First());
        var appointmentIds = injuries.Select(x => x.AppointmentId).Distinct().ToList();
        var appointmentsById = (await dbContext.Set<Appointment>().Where(a => appointmentIds.Contains(a.Id)).ToListAsync(cancellationToken))
            .ToDictionary(a => a.Id);
        var wcabOfficeIds = injuries.Where(x => x.WcabOfficeId.HasValue).Select(x => x.WcabOfficeId!.Value).Distinct().ToList();
        var wcabOfficesById = (await dbContext.Set<WcabOffice>().Where(w => wcabOfficeIds.Contains(w.Id)).ToListAsync(cancellationToken))
            .ToDictionary(w => w.Id);

        return injuries.Select(injury => new AppointmentInjuryDetailWithNavigationProperties
        {
            AppointmentInjuryDetail = injury,
            Appointment = appointmentsById.TryGetValue(injury.AppointmentId, out var ap) ? ap : null,
            WcabOffice = injury.WcabOfficeId.HasValue && wcabOfficesById.TryGetValue(injury.WcabOfficeId.Value, out var wo) ? wo : null,
            BodyParts = bodyPartsByInjury.TryGetValue(injury.Id, out var bp) ? bp : new List<AppointmentBodyPart>(),
            ClaimExaminer = examinersByInjury.TryGetValue(injury.Id, out var ce) ? ce : null,
            PrimaryInsurance = insurancesByInjury.TryGetValue(injury.Id, out var pi) ? pi : null,
        }).ToList();
    }

    protected virtual IQueryable<AppointmentInjuryDetail> ApplyFilter(IQueryable<AppointmentInjuryDetail> query, string? filterText = null, Guid? appointmentId = null, string? claimNumber = null)
    {
        return query
            .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ClaimNumber.Contains(filterText!) || e.BodyPartsSummary.Contains(filterText!))
            .WhereIf(appointmentId.HasValue && appointmentId.Value != Guid.Empty, e => e.AppointmentId == appointmentId)
            .WhereIf(!string.IsNullOrWhiteSpace(claimNumber), e => e.ClaimNumber.Contains(claimNumber!));
    }

    public virtual async Task<List<AppointmentInjuryDetail>> GetListAsync(string? filterText = null, Guid? appointmentId = null, string? claimNumber = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(await GetQueryableAsync(), filterText, appointmentId, claimNumber);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentInjuryDetailConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, Guid? appointmentId = null, string? claimNumber = null, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(await GetQueryableAsync(), filterText, appointmentId, claimNumber);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }
}
