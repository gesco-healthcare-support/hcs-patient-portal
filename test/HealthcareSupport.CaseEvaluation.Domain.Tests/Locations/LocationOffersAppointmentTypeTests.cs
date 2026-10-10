using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Locations;

/// <summary>
/// Q2 (2026-10-09): a location with no linked appointment types offers ALL types;
/// one with linked types offers exactly those. Pure unit tests, no database.
/// </summary>
public class LocationOffersAppointmentTypeTests
{
    private static readonly Guid TypeA = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid TypeB = Guid.Parse("b1111111-1111-1111-1111-111111111111");

    private static Location NewLocation(params Guid[] typeIds)
    {
        var location = new Location(Guid.NewGuid(), null, "TEST-Loc", 0m, true);
        location.SetAppointmentTypes(typeIds.ToList());
        return location;
    }

    [Fact]
    public void NoLinkedTypes_OffersEveryType()
    {
        var location = NewLocation();
        location.OffersAppointmentType(TypeA).ShouldBeTrue();
        location.OffersAppointmentType(TypeB).ShouldBeTrue();
    }

    [Fact]
    public void LinkedTypes_OffersOnlyThose()
    {
        var location = NewLocation(TypeA);
        location.OffersAppointmentType(TypeA).ShouldBeTrue();
        location.OffersAppointmentType(TypeB).ShouldBeFalse();
    }

    [Fact]
    public void Expression_MatchesTheInstanceRule()
    {
        var locations = new List<Location> { NewLocation(), NewLocation(TypeA), NewLocation(TypeB) };
        var predicate = Location.OffersAppointmentTypeExpression(TypeA).Compile();
        locations.Select(predicate).ToList().ShouldBe(locations.Select(l => l.OffersAppointmentType(TypeA)).ToList());
        locations.Count(predicate).ShouldBe(2);
    }
}
