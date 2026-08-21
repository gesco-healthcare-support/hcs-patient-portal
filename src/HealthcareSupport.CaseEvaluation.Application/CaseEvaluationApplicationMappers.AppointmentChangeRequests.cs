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
    // These targets have no source on the entity (the change request stores only
    // AppointmentId and NewDoctorAvailabilityId); they are filled in the AppService from the
    // referenced appointment and slot. Tell Mapperly to skip them so it does not emit an
    // unmapped-target diagnostic. Phase 4b (2026-08-04) added the four appointment/slot
    // context fields for the approval queue's date picker.
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.AppointmentConfirmationNumber))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.AppointmentLocationId))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.AppointmentTypeId))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.RequestedSlotDate))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.RequestedSlotFromTime))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentConsentRoundNumber))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundProposedSlotId))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundProposedDate))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundProposedFromTime))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundSideAStatus))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundSideBStatus))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundSendAttempts))]
    public override partial AppointmentChangeRequestDto Map(AppointmentChangeRequest source);

    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.AppointmentConfirmationNumber))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.AppointmentLocationId))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.AppointmentTypeId))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.RequestedSlotDate))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.RequestedSlotFromTime))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentConsentRoundNumber))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundProposedSlotId))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundProposedDate))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundProposedFromTime))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundSideAStatus))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundSideBStatus))]
    [MapperIgnoreTarget(nameof(AppointmentChangeRequestDto.CurrentRoundSendAttempts))]
    public override partial void Map(AppointmentChangeRequest source, AppointmentChangeRequestDto destination);
}
