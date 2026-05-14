using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace HealthcareSupport.CaseEvaluation;

/// <summary>
/// Phase 15 (2026-05-04) -- Riok.Mapperly mapper for the
/// <c>AppointmentChangeRequest</c> read DTO. Lives in its own partial-
/// class file per the 2-session split rule (see
/// <c>memory/project_two-session-split.md</c>): each feature gets its
/// own mapper file so the two sessions can land mappers without
/// touching the shared <c>CaseEvaluationApplicationMappers.cs</c>.
/// </summary>
[Mapper]
public partial class AppointmentChangeRequestToAppointmentChangeRequestDtoMapper
    : MapperBase<AppointmentChangeRequest, AppointmentChangeRequestDto>
{
    public override partial AppointmentChangeRequestDto Map(AppointmentChangeRequest source);
    public override partial void Map(AppointmentChangeRequest source, AppointmentChangeRequestDto destination);
}
