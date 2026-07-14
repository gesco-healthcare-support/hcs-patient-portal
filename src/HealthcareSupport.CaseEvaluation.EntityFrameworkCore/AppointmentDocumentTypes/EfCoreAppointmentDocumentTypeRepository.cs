using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

public class EfCoreAppointmentDocumentTypeRepository : EfCoreRepository<CaseEvaluationDbContext, AppointmentDocumentType, Guid>, IAppointmentDocumentTypeRepository
{
    public EfCoreAppointmentDocumentTypeRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        query = ApplyFilter(query, filterText, appointmentTypeId);
        // Reserved system rows ("Generated Packet") are never bulk-deletable --
        // the per-row path is guarded in AppointmentDocumentTypeManager; the
        // "delete all" convenience must honor the same invariant.
        query = query.Where(x => !x.IsSystem);
        var ids = query.Select(x => x.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<AppointmentDocumentType>> GetListAsync(string? filterText = null, Guid? appointmentTypeId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()).Include(x => x.AppointmentTypes), filterText, appointmentTypeId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentDocumentTypeConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetDbSetAsync()), filterText, appointmentTypeId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<AppointmentDocumentType> GetWithAppointmentTypesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = (await GetQueryableAsync()).Include(x => x.AppointmentTypes).Where(x => x.Id == id);
        var entity = await query.FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
        return entity ?? throw new EntityNotFoundException(typeof(AppointmentDocumentType), id);
    }

    public virtual async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        // Uniqueness is enforced only among ACTIVE rows: a soft-retired
        // (IsActive=false) row with the same name does not block re-using that
        // name, which is the intended "retire then recreate" behavior. The
        // IMultiTenant filter scopes the check to the current tenant (#4: a name
        // is curated once per tenant, no longer per appointment type). ToLower on
        // both sides keeps the check case-insensitive on the SQLite test runner
        // as well as the case-insensitive default SQL Server collation.
        var loweredName = name.ToLower();
        var query = (await GetQueryableAsync())
            .Where(e => e.IsActive && e.Name.ToLower() == loweredName);

        if (excludeId.HasValue)
        {
            query = query.Where(e => e.Id != excludeId.Value);
        }

        return await query.AnyAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<AppointmentDocumentType> ApplyFilter(IQueryable<AppointmentDocumentType> query, string? filterText = null, Guid? appointmentTypeId = null)
    {
        // #4: a category matches an appointment type when it applies to all types
        // OR its M2M set contains that type. Preserves the GetListAsync(appointmentTypeId)
        // contract the upload picker depends on -- only the resolution changed.
        return query
            .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Name!.Contains(filterText!))
            .WhereIf(appointmentTypeId.HasValue,
                e => e.AppliesToAll || e.AppointmentTypes.Any(j => j.AppointmentTypeId == appointmentTypeId!.Value));
    }
}
