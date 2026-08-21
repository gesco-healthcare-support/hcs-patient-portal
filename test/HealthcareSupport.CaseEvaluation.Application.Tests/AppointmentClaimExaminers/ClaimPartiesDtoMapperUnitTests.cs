using System;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

/// <summary>
/// CI1 (2026-06-05) -- regression test for the Mapperly auto-map of the
/// re-pointed FK on the AppointmentClaimExaminer + AppointmentPrimaryInsurance
/// read DTOs (AppointmentInjuryDetailId -> AppointmentId). Riok.Mapperly maps
/// source -> target by name; without this a future refactor that drops or
/// renames AppointmentId on the DTO would silently regress the appointment-level
/// read path. Proves the appointment-level link is server-queryable.
/// Plan: docs/plans/2026-06-05-claim-parties-per-appointment.md.
/// </summary>
public class ClaimPartiesDtoMapperUnitTests
{
    [Fact]
    public void Map_ClaimExaminer_FlowsAppointmentIdToDto()
    {
        var appointmentId = Guid.NewGuid();
        var entity = new AppointmentClaimExaminer(Guid.NewGuid(), appointmentId, isActive: true)
        {
            Name = "Jane Examiner",
            Email = "ce@gesco.com",
        };

        var dto = new AppointmentClaimExaminerToAppointmentClaimExaminerDtoMapper().Map(entity);

        dto.AppointmentId.ShouldBe(appointmentId);
        dto.Name.ShouldBe("Jane Examiner");
        dto.Email.ShouldBe("ce@gesco.com");
    }

    [Fact]
    public void Map_PrimaryInsurance_FlowsAppointmentIdToDto()
    {
        var appointmentId = Guid.NewGuid();
        var entity = new AppointmentPrimaryInsurance(Guid.NewGuid(), appointmentId, isActive: true)
        {
            Name = "ACME Insurance",
        };

        var dto = new AppointmentPrimaryInsuranceToAppointmentPrimaryInsuranceDtoMapper().Map(entity);

        dto.AppointmentId.ShouldBe(appointmentId);
        dto.Name.ShouldBe("ACME Insurance");
    }
}
