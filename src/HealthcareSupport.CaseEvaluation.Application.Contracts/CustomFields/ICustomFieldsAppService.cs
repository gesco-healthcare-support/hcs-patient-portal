using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// IT Admin custom-intake-field catalog. Mirrors OLD's
/// <c>CustomFieldController</c> CRUD surface
/// (P:\PatientPortalOld\PatientAppointment.Domain\CustomFieldModule\CustomFieldDomain.cs)
/// with the per-AppointmentType max-10-active rule corrected to
/// per-type instead of OLD's buggy global count.
///
/// <see cref="GetActiveForAppointmentTypeAsync"/> is the booking-form
/// read path: it returns active fields for an AppointmentTypeId, sorted
/// by DisplayOrder, so the appointment-add component can render them.
/// </summary>
public interface ICustomFieldsAppService : IApplicationService
{
    Task<CustomFieldDto> GetAsync(Guid id);

    Task<PagedResultDto<CustomFieldDto>> GetListAsync(GetCustomFieldsInput input);

    /// <summary>
    /// Active custom fields for the given AppointmentTypeId, sorted by
    /// DisplayOrder ascending. Used by the booking form when
    /// <c>SystemParameter.IsCustomField = true</c>. Open to any
    /// authenticated booker -- callers do not need
    /// <c>CustomFields.Default</c> for this path.
    /// </summary>
    Task<List<CustomFieldDto>> GetActiveForAppointmentTypeAsync(Guid appointmentTypeId);

    Task<CustomFieldDto> CreateAsync(CustomFieldCreateDto input);

    Task<CustomFieldDto> UpdateAsync(Guid id, CustomFieldUpdateDto input);

    /// <summary>
    /// Soft-delete via ABP <c>ISoftDelete</c>. OLD's <c>DeleteValidation</c>
    /// is empty (commented out -- CustomFieldDomain.cs:87-95) and never
    /// checks for in-use rows; NEW preserves that no-op-on-delete behavior
    /// because the audit doc lifecycle treats it as intentional (the
    /// deleted row remains visible to historic AppointmentValues so reports
    /// stay readable).
    /// </summary>
    Task DeleteAsync(Guid id);
}
