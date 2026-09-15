using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.CustomFields;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.CustomFieldsControllers;

/// <summary>
/// Manual HTTP surface for the IT-Admin custom-intake-field catalog.
/// Mirrors OLD <c>CustomFieldController</c>'s POST / GET / PUT / DELETE
/// endpoints (P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\CustomField\CustomFieldController.cs).
///
/// Authorization is enforced at the AppService layer per repo convention;
/// this controller is a pure pass-through.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("CustomFields")]
[Route("api/app/custom-fields")]
public class CustomFieldsController : AbpController
{
    protected ICustomFieldsAppService CustomFieldsAppService { get; }

    public CustomFieldsController(ICustomFieldsAppService customFieldsAppService)
    {
        CustomFieldsAppService = customFieldsAppService;
    }

    [HttpGet("{id}")]
    public virtual Task<CustomFieldDto> GetAsync(Guid id)
    {
        return CustomFieldsAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<CustomFieldDto>> GetListAsync([FromQuery] GetCustomFieldsInput input)
    {
        return CustomFieldsAppService.GetListAsync(input);
    }

    /// <summary>
    /// Booking-form helper. Returns active fields for an AppointmentTypeId
    /// in DisplayOrder so the appointment-add component can render them
    /// when <c>SystemParameter.IsCustomField = true</c>.
    /// </summary>
    [HttpGet("by-appointment-type/{appointmentTypeId}")]
    public virtual Task<List<CustomFieldDto>> GetActiveForAppointmentTypeAsync(Guid appointmentTypeId)
    {
        return CustomFieldsAppService.GetActiveForAppointmentTypeAsync(appointmentTypeId);
    }

    [HttpPost]
    public virtual Task<CustomFieldDto> CreateAsync(CustomFieldCreateDto input)
    {
        return CustomFieldsAppService.CreateAsync(input);
    }

    [HttpPut("{id}")]
    public virtual Task<CustomFieldDto> UpdateAsync(Guid id, CustomFieldUpdateDto input)
    {
        return CustomFieldsAppService.UpdateAsync(id, input);
    }

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return CustomFieldsAppService.DeleteAsync(id);
    }
}
