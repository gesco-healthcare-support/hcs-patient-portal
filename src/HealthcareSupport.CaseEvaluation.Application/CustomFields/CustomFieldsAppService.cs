using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Guids;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// IT Admin custom-intake-field catalog. Mirrors OLD's
/// <c>CustomFieldDomain</c> CRUD with two strict-parity corrections:
///
///   1. Per-AppointmentTypeId active-row cap, not OLD's global count
///      (CustomFieldDomain.cs:38 -- the global count is an OLD bug; spec
///      line 543 calls for "10 per type"). NEW counts active rows where
///      <c>AppointmentTypeId == input.AppointmentTypeId</c> only.
///   2. Cap check uses <c>&gt;= 10</c>, not OLD's exact-equals
///      <c>== 10</c> (CustomFieldDomain.cs:40 -- a separate OLD bug that
///      lets the count go to 11+ if other paths inserted rows).
///
/// <c>DisplayOrder</c> auto-assignment on create matches OLD verbatim:
/// <c>max(DisplayOrder) + 1</c> across the catalog (NOT scoped per
/// AppointmentType). Update paths accept caller-supplied DisplayOrder
/// for explicit re-ordering.
///
/// Authorization (Phase 6, sharing the existing <c>CustomFields.*</c>
/// permission group with the W2-5 AppointmentTypeFieldConfig service --
/// see audit doc):
///   - Class-level <c>[Authorize]</c> so the booking form's
///     <see cref="GetActiveForAppointmentTypeAsync"/> is reachable by any
///     authenticated booker.
///   - Admin endpoints override with <c>.Default / .Create / .Edit / .Delete</c>.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class CustomFieldsAppService : CaseEvaluationAppService, ICustomFieldsAppService
{
    private readonly ICustomFieldRepository _repository;
    private readonly IGuidGenerator _guidGenerator;

    public CustomFieldsAppService(ICustomFieldRepository repository, IGuidGenerator guidGenerator)
    {
        _repository = repository;
        _guidGenerator = guidGenerator;
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Default)]
    public virtual async Task<CustomFieldDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<CustomField, CustomFieldDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Default)]
    public virtual async Task<PagedResultDto<CustomFieldDto>> GetListAsync(GetCustomFieldsInput input)
    {
        Check.NotNull(input, nameof(input));

        var queryable = await _repository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.FilterText))
        {
            var needle = input.FilterText.Trim();
            queryable = queryable.Where(x => x.FieldLabel.Contains(needle));
        }
        if (input.AppointmentTypeId.HasValue)
        {
            queryable = queryable.Where(x => x.AppointmentTypeId == input.AppointmentTypeId.Value);
        }
        if (input.IsActive.HasValue)
        {
            queryable = queryable.Where(x => x.IsActive == input.IsActive.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var sort = string.IsNullOrWhiteSpace(input.Sorting) ? nameof(CustomField.DisplayOrder) : input.Sorting;
        queryable = ApplySortingAndPaging(queryable, sort, input.SkipCount, input.MaxResultCount);

        var entities = await AsyncExecuter.ToListAsync(queryable);
        return new PagedResultDto<CustomFieldDto>(
            totalCount,
            entities.Select(ObjectMapper.Map<CustomField, CustomFieldDto>).ToList());
    }

    public virtual async Task<List<CustomFieldDto>> GetActiveForAppointmentTypeAsync(Guid appointmentTypeId)
    {
        var queryable = await _repository.GetQueryableAsync();
        var rows = queryable
            .Where(x => x.AppointmentTypeId == appointmentTypeId && x.IsActive)
            .OrderBy(x => x.DisplayOrder);
        var entities = await AsyncExecuter.ToListAsync(rows);
        return entities.Select(ObjectMapper.Map<CustomField, CustomFieldDto>).ToList();
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Create)]
    public virtual async Task<CustomFieldDto> CreateAsync(CustomFieldCreateDto input)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNull(input.AppointmentTypeId, nameof(input.AppointmentTypeId));

        await EnsureUnderActiveCapAsync(input.AppointmentTypeId!.Value);
        await EnsureNoDuplicateLabelAndTypeAsync(input.FieldLabel, input.FieldType, excludingId: null);

        var displayOrder = await ComputeNextDisplayOrderAsync();

        var entity = new CustomField(
            id: _guidGenerator.Create(),
            tenantId: CurrentTenant.Id,
            fieldLabel: input.FieldLabel,
            displayOrder: displayOrder,
            fieldType: input.FieldType,
            appointmentTypeId: input.AppointmentTypeId,
            fieldLength: input.FieldLength,
            multipleValues: input.MultipleValues,
            defaultValue: input.DefaultValue,
            isMandatory: input.IsMandatory,
            isActive: input.IsActive);

        await _repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<CustomField, CustomFieldDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Edit)]
    public virtual async Task<CustomFieldDto> UpdateAsync(Guid id, CustomFieldUpdateDto input)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNull(input.AppointmentTypeId, nameof(input.AppointmentTypeId));

        var entity = await _repository.GetAsync(id);

        await EnsureNoDuplicateLabelAndTypeAsync(input.FieldLabel, input.FieldType, excludingId: id);

        // Cap check on update only fires when toggling IsActive false -> true
        // OR moving the row to a different AppointmentTypeId. Both paths can
        // push the new bucket over the cap; pure label / order edits cannot.
        var becomingActive = !entity.IsActive && input.IsActive;
        var movingToDifferentType = entity.AppointmentTypeId != input.AppointmentTypeId;
        if ((becomingActive || movingToDifferentType) && input.IsActive)
        {
            await EnsureUnderActiveCapAsync(input.AppointmentTypeId!.Value, excludingId: id);
        }

        entity.FieldLabel = input.FieldLabel;
        entity.DisplayOrder = input.DisplayOrder;
        entity.FieldType = input.FieldType;
        entity.FieldLength = input.FieldLength;
        entity.MultipleValues = input.MultipleValues;
        entity.DefaultValue = input.DefaultValue;
        entity.IsMandatory = input.IsMandatory;
        entity.AppointmentTypeId = input.AppointmentTypeId;
        entity.IsActive = input.IsActive;
        await _repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<CustomField, CustomFieldDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.CustomFields.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id, autoSave: true);
    }

    private async Task EnsureUnderActiveCapAsync(Guid appointmentTypeId, Guid? excludingId = null)
    {
        var queryable = await _repository.GetQueryableAsync();
        var activeCount = await AsyncExecuter.CountAsync(queryable.Where(x =>
            x.AppointmentTypeId == appointmentTypeId &&
            x.IsActive &&
            (excludingId == null || x.Id != excludingId)));

        if (IsAtOrOverCap(activeCount))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.CustomFieldMax10ActivePerAppointmentType);
        }
    }

    private async Task EnsureNoDuplicateLabelAndTypeAsync(string fieldLabel, Enums.CustomFieldType fieldType, Guid? excludingId)
    {
        var queryable = await _repository.GetQueryableAsync();
        var conflict = queryable.Where(x =>
            x.FieldLabel == fieldLabel &&
            x.FieldType == fieldType &&
            (excludingId == null || x.Id != excludingId));

        if (await AsyncExecuter.AnyAsync(conflict))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.CustomFieldDuplicateLabelAndType);
        }
    }

    private async Task<int> ComputeNextDisplayOrderAsync()
    {
        var queryable = await _repository.GetQueryableAsync();
        var max = await AsyncExecuter.MaxAsync(queryable.Select(x => (int?)x.DisplayOrder));
        return ComputeNextDisplayOrder(max);
    }

    /// <summary>
    /// Pure helper for the auto-assigned <c>DisplayOrder</c>. Mirrors OLD
    /// <c>CustomFieldDomain.cs:55-61</c>: when the catalog is empty,
    /// start at 1; otherwise increment the current max by 1. Extracted
    /// internal-static so unit tests can verify the boundary cases
    /// without DB / IObjectMapper / IRepository plumbing.
    /// </summary>
    internal static int ComputeNextDisplayOrder(int? currentMax)
    {
        return (currentMax ?? 0) + 1;
    }

    /// <summary>
    /// Pure helper for the per-AppointmentTypeId active-row cap. Mirrors
    /// the spec ("Up to 10 fields per appointment type"). Returns true
    /// when adding one more would put the bucket over the cap. Extracted
    /// for unit-testability via <c>InternalsVisibleTo</c>.
    /// </summary>
    internal static bool IsAtOrOverCap(int activeCount)
    {
        return activeCount >= CustomFieldConsts.MaxActiveCountPerAppointmentType;
    }

    private static System.Linq.IQueryable<CustomField> ApplySortingAndPaging(
        System.Linq.IQueryable<CustomField> queryable, string sorting, int skipCount, int maxResultCount)
    {
        var ordered = sorting.Trim().ToLowerInvariant() switch
        {
            "displayorder desc" => queryable.OrderByDescending(x => x.DisplayOrder),
            "displayorder" or "displayorder asc" => queryable.OrderBy(x => x.DisplayOrder),
            "fieldlabel desc" => queryable.OrderByDescending(x => x.FieldLabel),
            "fieldlabel" or "fieldlabel asc" => queryable.OrderBy(x => x.FieldLabel),
            "creationtime desc" => queryable.OrderByDescending(x => x.CreationTime),
            "creationtime" or "creationtime asc" => queryable.OrderBy(x => x.CreationTime),
            _ => queryable.OrderBy(x => x.DisplayOrder),
        };
        return ordered.Skip(skipCount).Take(maxResultCount);
    }
}
