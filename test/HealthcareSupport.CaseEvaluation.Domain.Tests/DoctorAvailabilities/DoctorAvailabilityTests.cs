using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-05-15 -- entity-level invariants for the capacity + multi-type
/// schema rework. Plan-2 TDD: pins ctor capacity guards and the M2M
/// helper semantics (idempotent Add, RemoveAllExceptGivenIds, TenantId
/// mirroring) so plan 3's logic layer can rely on them.
/// </summary>
public class DoctorAvailabilityTests
{
    private static DoctorAvailability Build(int capacity = 3)
    {
        return new DoctorAvailability(
            id: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            availableDate: new DateTime(2030, 1, 1),
            fromTime: new TimeOnly(9, 0),
            toTime: new TimeOnly(10, 0),
            bookingStatusId: BookingStatus.Available,
            capacity: capacity);
    }

    [Fact]
    public void Ctor_WhenCapacityIsZero_Throws()
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => Build(capacity: 0));
        ex.ParamName.ShouldBe("capacity");
    }

    [Fact]
    public void Ctor_WhenCapacityIsNegative_Throws()
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => Build(capacity: -1));
        ex.ParamName.ShouldBe("capacity");
    }

    [Fact]
    public void Ctor_WhenCapacityOmitted_DefaultsToThree()
    {
        var slot = new DoctorAvailability(
            id: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            availableDate: new DateTime(2030, 1, 1),
            fromTime: new TimeOnly(9, 0),
            toTime: new TimeOnly(10, 0),
            bookingStatusId: BookingStatus.Available);
        slot.Capacity.ShouldBe(3);
    }

    [Fact]
    public void AddAppointmentType_Twice_IsIdempotent()
    {
        var slot = Build();
        var typeId = Guid.NewGuid();

        slot.AddAppointmentType(typeId);
        slot.AddAppointmentType(typeId);

        slot.AppointmentTypes.Count.ShouldBe(1);
        slot.AppointmentTypes.Single().AppointmentTypeId.ShouldBe(typeId);
    }

    [Fact]
    public void RemoveAllAppointmentTypesExceptGivenIds_KeepsOnlyListed()
    {
        var slot = Build();
        var type1 = Guid.NewGuid();
        var type2 = Guid.NewGuid();
        var type3 = Guid.NewGuid();
        slot.AddAppointmentType(type1);
        slot.AddAppointmentType(type2);
        slot.AddAppointmentType(type3);

        slot.RemoveAllAppointmentTypesExceptGivenIds(new List<Guid> { type1, type3 });

        var remaining = slot.AppointmentTypes.Select(x => x.AppointmentTypeId).ToList();
        remaining.Count.ShouldBe(2);
        remaining.ShouldContain(type1);
        remaining.ShouldContain(type3);
        remaining.ShouldNotContain(type2);
    }

    [Fact]
    public void AddAppointmentType_MirrorsTenantId()
    {
        var slot = Build();
        var tenantId = Guid.NewGuid();
        slot.TenantId = tenantId;

        slot.AddAppointmentType(Guid.NewGuid());

        slot.AppointmentTypes.Single().TenantId.ShouldBe(tenantId);
    }
}
