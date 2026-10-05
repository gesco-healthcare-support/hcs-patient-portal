using System;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

/// <summary>
/// An attorney row with no linked portal account has a null IdentityUserId, while the
/// DTO carries a non-nullable Guid whose "no account" value is the all-zero Guid. Mapperly's
/// default for Guid? -> Guid skips the assignment on null, so the Map(source, destination)
/// overload used to keep whatever id the destination already held. These pin the end state:
/// an unlinked source always reads back as unassigned, on both overloads.
/// </summary>
public class AttorneyIdentityUserIdMapperUnitTests
{
    [Fact]
    public void Map_into_existing_dto_resets_a_stale_id_when_the_attorney_is_unlinked()
    {
        var source = new AppointmentApplicantAttorney(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), identityUserId: null);
        var destination = new AppointmentApplicantAttorneyDto { IdentityUserId = Guid.NewGuid() };

        new AppointmentApplicantAttorneyToAppointmentApplicantAttorneyDtoMappers().Map(source, destination);

        destination.IdentityUserId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Map_into_existing_defense_dto_resets_a_stale_id_when_the_attorney_is_unlinked()
    {
        var source = new DefenseAttorney(Guid.NewGuid(), stateId: null, identityUserId: null);
        var destination = new DefenseAttorneyDto { IdentityUserId = Guid.NewGuid() };

        new DefenseAttorneyToDefenseAttorneyDtoMappers().Map(source, destination);

        destination.IdentityUserId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Map_to_new_dto_carries_a_linked_id_and_reads_unlinked_as_unassigned()
    {
        var linkedId = Guid.NewGuid();
        var mapper = new AppointmentApplicantAttorneyToAppointmentApplicantAttorneyDtoMappers();

        mapper.Map(new AppointmentApplicantAttorney(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), linkedId))
            .IdentityUserId.ShouldBe(linkedId);
        mapper.Map(new AppointmentApplicantAttorney(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), identityUserId: null))
            .IdentityUserId.ShouldBe(Guid.Empty);
    }
}
