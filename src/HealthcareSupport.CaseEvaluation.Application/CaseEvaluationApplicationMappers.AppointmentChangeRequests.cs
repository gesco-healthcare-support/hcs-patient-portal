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
    // AppointmentConfirmationNumber has no source on the entity (the change
    // request stores only AppointmentId); it is filled in the AppService from
    // the referenced appointment. Tell Mapperly to skip it so it does not emit
    // an unmapped-target diagnostic.
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.AppointmentConfirmationNumber))]
    public override partial AppointmentChangeRequestDto Map(AppointmentChangeRequest source);

    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.AppointmentConfirmationNumber))]
    public override partial void Map(AppointmentChangeRequest source, AppointmentChangeRequestDto destination);
}
