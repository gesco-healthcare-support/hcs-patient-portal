using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

public class GetAppointmentDocumentTypesInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    /// <summary>Narrow the list to a single appointment type. Null returns every
    /// row regardless of scope (the admin "all types" view).</summary>
    public Guid? AppointmentTypeId { get; set; }

    public GetAppointmentDocumentTypesInput()
    {
    }
}
