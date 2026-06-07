using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
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
/// Group K (G-02-01/02). Read-only change-log over ABP audit data for appointment
/// intake entities. Aggregates the appointment's own <c>EntityChange</c> rows with
/// those of its child entities (injury details + their body parts / claim examiners /
/// primary insurance), explodes them to per-field rows, and redacts PHI before
/// returning. Internal-only via the AppointmentChangeLogs permission.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentChangeLogs.Default)]
public class AppointmentChangeLogsAppService : CaseEvaluationAppService, IAppointmentChangeLogsAppService
{
    // Bound the audit scan so a global query can't fetch unbounded history.
    private const int ScanCap = 1000;

    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryRepository;
    private readonly IRepository<AppointmentBodyPart, Guid> _bodyPartRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _primaryInsuranceRepository;

    public AppointmentChangeLogsAppService(
        IAuditLogRepository auditLogRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentInjuryDetail, Guid> injuryRepository,
        IRepository<AppointmentBodyPart, Guid> bodyPartRepository,
        IRepository<AppointmentClaimExaminer, Guid> claimExaminerRepository,
        IRepository<AppointmentPrimaryInsurance, Guid> primaryInsuranceRepository)
    {
        _auditLogRepository = auditLogRepository;
        _appointmentRepository = appointmentRepository;
        _injuryRepository = injuryRepository;
        _bodyPartRepository = bodyPartRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _primaryInsuranceRepository = primaryInsuranceRepository;
    }

    public virtual async Task<List<AppointmentChangeLogDto>> GetByAppointmentAsync(Guid appointmentId)
    {
        var raw = await LoadAppointmentChangesAsync(appointmentId);
        return AppointmentChangeLogBuilder.BuildRows(raw, appointmentId)
            .OrderByDescending(r => r.ChangeTime)
            .ToList();
    }

    public virtual async Task<PagedResultDto<AppointmentChangeLogDto>> GetListAsync(GetAppointmentChangeLogsInput input)
    {
        var appointmentId = await ResolveAppointmentIdAsync(input);

        List<AppointmentChangeLogDto> rows;
        if (appointmentId.HasValue)
        {
            var raw = await LoadAppointmentChangesAsync(appointmentId.Value);
            rows = AppointmentChangeLogBuilder.BuildRows(raw, appointmentId.Value);
        }
        else
        {
            var raw = await LoadGlobalChangesAsync(input);
            rows = AppointmentChangeLogBuilder.BuildRows(raw, appointmentId: null);
        }

        rows = ApplyFilters(rows, input);
        rows = rows.OrderByDescending(r => r.ChangeTime).ToList();

        var totalCount = rows.Count;
        var page = rows.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<AppointmentChangeLogDto>(totalCount, page);
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
        var injuryQuery = await _injuryRepository.GetQueryableAsync();
        var injuryIds = await AsyncExecuter.ToListAsync(
            injuryQuery.Where(i => i.AppointmentId == appointmentId).Select(i => i.Id));

        var bodyPartIds = new List<Guid>();
        if (injuryIds.Count > 0)
        {
            var bodyPartQuery = await _bodyPartRepository.GetQueryableAsync();
            bodyPartIds = await AsyncExecuter.ToListAsync(
                bodyPartQuery.Where(b => injuryIds.Contains(b.AppointmentInjuryDetailId)).Select(b => b.Id));
        }

        // #296 (2026-06-07 merge): Claim Examiner + Primary Insurance are now
        // per-appointment (not per-injury), so query them by AppointmentId.
        var claimExaminerQuery = await _claimExaminerRepository.GetQueryableAsync();
        var claimExaminerIds = await AsyncExecuter.ToListAsync(
            claimExaminerQuery.Where(c => c.AppointmentId == appointmentId).Select(c => c.Id));

        var primaryInsuranceQuery = await _primaryInsuranceRepository.GetQueryableAsync();
        var primaryInsuranceIds = await AsyncExecuter.ToListAsync(
            primaryInsuranceQuery.Where(p => p.AppointmentId == appointmentId).Select(p => p.Id));

        var raw = new List<RawEntityChange>();
        raw.AddRange(await LoadChangesForEntityAsync(appointmentId.ToString(), AppointmentAuditedEntities.Appointment));
        foreach (var id in injuryIds)
        {
            raw.AddRange(await LoadChangesForEntityAsync(id.ToString(), AppointmentAuditedEntities.InjuryDetail));
        }
        foreach (var id in bodyPartIds)
        {
            raw.AddRange(await LoadChangesForEntityAsync(id.ToString(), AppointmentAuditedEntities.BodyPart));
        }
        foreach (var id in claimExaminerIds)
        {
            raw.AddRange(await LoadChangesForEntityAsync(id.ToString(), AppointmentAuditedEntities.ClaimExaminer));
        }
        foreach (var id in primaryInsuranceIds)
        {
            raw.AddRange(await LoadChangesForEntityAsync(id.ToString(), AppointmentAuditedEntities.PrimaryInsurance));
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
                .ToList());

    private static List<AppointmentChangeLogDto> ApplyFilters(
        List<AppointmentChangeLogDto> rows, GetAppointmentChangeLogsInput input)
    {
        IEnumerable<AppointmentChangeLogDto> query = rows;
        if (!string.IsNullOrWhiteSpace(input.EntityType))
        {
            query = query.Where(r => r.EntityType.Equals(input.EntityType, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(input.FieldName))
        {
            query = query.Where(r => r.PropertyName.Contains(input.FieldName!, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(input.ChangeType))
        {
            query = query.Where(r => r.ChangeType.Equals(input.ChangeType, StringComparison.OrdinalIgnoreCase));
        }
        if (input.StartTime.HasValue)
        {
            query = query.Where(r => r.ChangeTime >= input.StartTime.Value);
        }
        if (input.EndTime.HasValue)
        {
            query = query.Where(r => r.ChangeTime <= input.EndTime.Value);
        }
        return query.ToList();
    }

    private static EntityChangeType? ParseChangeType(string? value)
        => Enum.TryParse<EntityChangeType>(value, ignoreCase: true, out var changeType) ? changeType : null;
}
