namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 13b (2026-05-04) -- field-level filter that hides internal-
/// only fields from external users on appointment read DTOs.
///
/// Mirrors OLD's behavior of returning the same view-model shape to
/// every caller but UI-side masking the internal fields. NEW pulls
/// the mask down to the API layer so direct API callers cannot see
/// the masked fields by inspecting the JSON payload.
///
/// Pure (no DI / no DB): the AppService passes in the live DTO and
/// the "is external" flag (computed by
/// <see cref="BookingFlowRoles.IsInternalUserCaller"/>'s inverse).
/// </summary>
internal static class ExternalUserDtoFilter
{
    /// <summary>
    /// When <paramref name="isExternalUser"/> is true, sets
    /// <see cref="AppointmentDto.InternalUserComments"/> to <c>null</c>
    /// in place. Returns the same instance so the call chains.
    /// Internal callers see the field unchanged.
    /// </summary>
    internal static AppointmentDto MaskInternalFields(AppointmentDto dto, bool isExternalUser)
    {
        if (dto == null) return null!;
        if (isExternalUser)
        {
            dto.InternalUserComments = null;
        }
        return dto;
    }

    /// <summary>
    /// Convenience overload for the WithNav DTO; forwards to the
    /// non-nav variant after extracting the wrapped <see cref="AppointmentDto"/>.
    /// </summary>
    internal static AppointmentWithNavigationPropertiesDto MaskInternalFields(
        AppointmentWithNavigationPropertiesDto dto,
        bool isExternalUser)
    {
        if (dto == null) return null!;
        if (dto.Appointment != null)
        {
            MaskInternalFields(dto.Appointment, isExternalUser);
        }
        return dto;
    }
}
