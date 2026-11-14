using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Auditing;
using Volo.Abp.AuditLogging;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// Group K (G-02-01/02). Read-only change-log over ABP audit data. The per-case page covers the
/// WHOLE life of the case: the appointment itself (its creation is "Booked"), its intake details and
/// parties, documents, change requests and consent rounds, info requests and authorized users, found
/// by <see cref="AppointmentCaseEntityLocator"/>. Each change is exploded to per-field rows, PHI is
/// redacted, a readable action label is added, and the person who made it is resolved from the audit
/// log (<see cref="IChangeAuthorResolver"/>; a host operator acting through an office login is shown
/// as the operator). The global list keeps scanning the five intake types. Internal-only via the
/// AppointmentChangeLogs permission; no table, no extra permission.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentChangeLogs.Default)]
public class AppointmentChangeLogsAppService : CaseEvaluationAppService, IAppointmentChangeLogsAppService
{
    // Bound the audit scan so a global query cannot fetch unbounded history.
    private const int ScanCap = 1000;

    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly AppointmentCaseEntityLocator _caseLocator;
    private readonly IChangeAuthorResolver _authorResolver;

    public AppointmentChangeLogsAppService(
        IAuditLogRepository auditLogRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        AppointmentCaseEntityLocator caseLocator,
        IChangeAuthorResolver authorResolver)
    {
        _auditLogRepository = auditLogRepository;
        _appointmentRepository = appointmentRepository;
        _caseLocator = caseLocator;
        _authorResolver = authorResolver;
    }

    public virtual async Task<List<AppointmentChangeLogDto>> GetByAppointmentAsync(Guid appointmentId)
    {
        var raw = await LoadAppointmentChangesAsync(appointmentId);
        var rows = AppointmentChangeLogBuilder.BuildRowsWithSource(raw, appointmentId)
            .OrderByDescending(r => r.Row.ChangeTime)
            .ToList();
        await StampAuthorsAsync(rows);
        return rows.ConvertAll(r => r.Row);
    }

    public virtual async Task<PagedResultDto<AppointmentChangeLogDto>> GetListAsync(GetAppointmentChangeLogsInput input)
    {
        var appointmentId = await ResolveAppointmentIdAsync(input);

        List<BuiltChangeLogRow> rows;
        if (appointmentId.HasValue)
        {
            var raw = await LoadAppointmentChangesAsync(appointmentId.Value);
            rows = AppointmentChangeLogBuilder.BuildRowsWithSource(raw, appointmentId.Value);
        }
        else
        {
            var raw = await LoadGlobalChangesAsync(input);
            rows = AppointmentChangeLogBuilder.BuildRowsWithSource(raw, appointmentId: null);
        }

        rows = ApplyFilters(rows, input);
        rows = rows.OrderByDescending(r => r.Row.ChangeTime).ToList();

        var totalCount = rows.Count;
        var page = rows.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        // Only the returned page is resolved, so the lookups stay a handful however long the history is.
        await StampAuthorsAsync(page);
        return new PagedResultDto<AppointmentChangeLogDto>(totalCount, page.ConvertAll(r => r.Row));
    }

    /// <summary>Sets who made each row's change. A change with no audit log at all is "System".</summary>
    private async Task StampAuthorsAsync(List<BuiltChangeLogRow> rows)
    {
        var auditLogIds = rows.Where(r => r.AuditLogId.HasValue).Select(r => r.AuditLogId!.Value).Distinct().ToList();
        var authors = await _authorResolver.ResolveAsync(auditLogIds);
        foreach (var built in rows)
        {
            var author = built.AuditLogId is { } id && authors.TryGetValue(id, out var found)
                ? found
                : ChangeAuthor.System;
            built.Row.ChangedByName = author.Name;
            built.Row.ChangedByKind = author.Kind;
        }
    }

    private async Task<Guid?> ResolveAppointmentIdAsync(GetAppointmentChangeLogsInput input)
    {
        if (input.AppointmentId.HasValue)
        {
            return input.AppointmentId;
        }
        if (!string.IsNullOrWhiteSpace(input.RequestConfirmationNumber))
        {
            var query = await _appointmentRepository.GetQueryableAsync();
            var match = await AsyncExecuter.FirstOrDefaultAsync(
                query.Where(a => a.RequestConfirmationNumber == input.RequestConfirmationNumber));
            return match?.Id;
        }
        return null;
    }

    private async Task<List<RawEntityChange>> LoadAppointmentChangesAsync(Guid appointmentId)
    {
        var raw = new List<RawEntityChange>();
        foreach (var entity in await _caseLocator.FindAsync(appointmentId))
        {
            raw.AddRange(await LoadChangesForEntityAsync(entity.Id.ToString(), entity.EntityTypeFullName));
        }
        return raw;
    }

    private async Task<List<RawEntityChange>> LoadChangesForEntityAsync(string entityId, string entityTypeFullName)
    {
        var changes = await _auditLogRepository.GetEntityChangeListAsync(
            entityId: entityId,
            entityTypeFullName: entityTypeFullName,
            includeDetails: true,
            maxResultCount: ScanCap);
        return changes.ConvertAll(ToRaw);
    }

    private async Task<List<RawEntityChange>> LoadGlobalChangesAsync(GetAppointmentChangeLogsInput input)
    {
        var changeType = ParseChangeType(input.ChangeType);
        var raw = new List<RawEntityChange>();
        foreach (var entityTypeFullName in AppointmentAuditedEntities.All)
        {
            var changes = await _auditLogRepository.GetEntityChangeListAsync(
                startTime: input.StartTime,
                endTime: input.EndTime,
                changeType: changeType,
                entityTypeFullName: entityTypeFullName,
                includeDetails: true,
                maxResultCount: ScanCap);
            raw.AddRange(changes.ConvertAll(ToRaw));
        }
        return raw;
    }

    private static RawEntityChange ToRaw(EntityChange change)
        => new(
            change.EntityTypeFullName,
            change.EntityId,
            change.ChangeType.ToString(),
            change.ChangeTime,
            change.PropertyChanges
                .Select(p => new RawPropertyChange(p.PropertyName, p.OriginalValue, p.NewValue))
                .ToList(),
            change.AuditLogId);

    private static List<BuiltChangeLogRow> ApplyFilters(
        List<BuiltChangeLogRow> rows, GetAppointmentChangeLogsInput input)
    {
        IEnumerable<BuiltChangeLogRow> query = rows;
        if (!string.IsNullOrWhiteSpace(input.EntityType))
        {
            query = query.Where(r => r.Row.EntityType.Equals(input.EntityType, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(input.FieldName))
        {
            query = query.Where(r => r.Row.PropertyName.Contains(input.FieldName, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(input.ChangeType))
        {
            query = query.Where(r => r.Row.ChangeType.Equals(input.ChangeType, StringComparison.OrdinalIgnoreCase));
        }
        if (input.StartTime.HasValue)
        {
            query = query.Where(r => r.Row.ChangeTime >= input.StartTime.Value);
        }
        if (input.EndTime.HasValue)
        {
            query = query.Where(r => r.Row.ChangeTime <= input.EndTime.Value);
        }
        return query.ToList();
    }

    private static EntityChangeType? ParseChangeType(string? value)
        => Enum.TryParse<EntityChangeType>(value, ignoreCase: true, out var changeType) ? changeType : null;
}
