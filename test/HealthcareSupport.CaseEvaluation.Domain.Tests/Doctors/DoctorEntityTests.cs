using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Doctors;

/// <summary>
/// Pure unit tests for the <see cref="Doctor"/> aggregate and its two join entities,
/// <see cref="DoctorAppointmentType"/> and <see cref="DoctorLocation"/>. No DI and no
/// database: every fact builds the aggregate in memory and asserts the resulting
/// collections, keys or the thrown argument error.
/// </summary>
public class DoctorEntityTests
{
    private static Doctor NewDoctor(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "TEST-First", "TEST-Last", "test.doctor@test.local", Gender.Female);

    [Fact]
    public void Constructor_StoresEveryFieldAndStartsWithEmptyCollections()
    {
        var id = Guid.NewGuid();

        var doctor = new Doctor(id, "TEST-Ada", "TEST-Example", "ada@test.local", Gender.Other);

        doctor.Id.ShouldBe(id);
        doctor.FirstName.ShouldBe("TEST-Ada");
        doctor.LastName.ShouldBe("TEST-Example");
        doctor.Email.ShouldBe("ada@test.local");
        doctor.Gender.ShouldBe(Gender.Other);
        doctor.AppointmentTypes.ShouldBeEmpty();
        doctor.Locations.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_NullFirstName_ThrowsArgumentNull()
    {
        var ex = Should.Throw<ArgumentNullException>(
            () => new Doctor(Guid.NewGuid(), null!, "TEST-Last", "a@test.local", Gender.Male));

        ex.ParamName.ShouldBe("firstName");
    }

    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    [InlineData("email")]
    public void Constructor_ValueOverMaxLength_ThrowsForThatParameter(string parameter)
    {
        var first = parameter == "firstName" ? new string('a', DoctorConsts.FirstNameMaxLength + 1) : "TEST-First";
        var last = parameter == "lastName" ? new string('b', DoctorConsts.LastNameMaxLength + 1) : "TEST-Last";
        var email = parameter == "email" ? new string('c', DoctorConsts.EmailMaxLength + 1) : "c@test.local";

        var ex = Should.Throw<ArgumentException>(
            () => new Doctor(Guid.NewGuid(), first, last, email, Gender.Male));

        ex.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_ValuesAtExactMaxLength_AreAccepted()
    {
        var doctor = new Doctor(
            Guid.NewGuid(),
            new string('a', DoctorConsts.FirstNameMaxLength),
            new string('b', DoctorConsts.LastNameMaxLength),
            new string('c', DoctorConsts.EmailMaxLength),
            Gender.Male);

        doctor.FirstName.Length.ShouldBe(DoctorConsts.FirstNameMaxLength);
        doctor.LastName.Length.ShouldBe(DoctorConsts.LastNameMaxLength);
        doctor.Email.Length.ShouldBe(DoctorConsts.EmailMaxLength);
    }

    // ------------------------------------------------------------------------
    // Appointment types
    // ------------------------------------------------------------------------

    [Fact]
    public void AddAppointmentType_AddsAJoinRowKeyedToThisDoctor()
    {
        var doctorId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var doctor = NewDoctor(doctorId);

        doctor.AddAppointmentType(typeId);

        var row = doctor.AppointmentTypes.ShouldHaveSingleItem();
        row.DoctorId.ShouldBe(doctorId);
        row.AppointmentTypeId.ShouldBe(typeId);
        row.GetKeys().ShouldBe(new object[] { doctorId, typeId });
    }

    [Fact]
    public void AddAppointmentType_SameIdTwice_KeepsOneRow()
    {
        var doctor = NewDoctor();
        var typeId = Guid.NewGuid();

        doctor.AddAppointmentType(typeId);
        doctor.AddAppointmentType(typeId);

        doctor.AppointmentTypes.ShouldHaveSingleItem().AppointmentTypeId.ShouldBe(typeId);
    }

    [Fact]
    public void RemoveAppointmentType_RemovesOnlyTheNamedType()
    {
        var doctor = NewDoctor();
        var removed = Guid.NewGuid();
        var kept = Guid.NewGuid();
        doctor.AddAppointmentType(removed);
        doctor.AddAppointmentType(kept);

        doctor.RemoveAppointmentType(removed);

        doctor.AppointmentTypes.ShouldHaveSingleItem().AppointmentTypeId.ShouldBe(kept);
    }

    [Fact]
    public void RemoveAppointmentType_UnknownId_LeavesExistingRowsInPlace()
    {
        // Decoy: an existing row that an unknown-id removal must NOT touch.
        var doctor = NewDoctor();
        var existing = Guid.NewGuid();
        doctor.AddAppointmentType(existing);

        doctor.RemoveAppointmentType(Guid.NewGuid());

        doctor.AppointmentTypes.ShouldHaveSingleItem().AppointmentTypeId.ShouldBe(existing);
    }

    [Fact]
    public void RemoveAllAppointmentTypesExceptGivenIds_KeepsOnlyTheGivenIds()
    {
        var doctor = NewDoctor();
        var kept = Guid.NewGuid();
        var dropped = Guid.NewGuid();
        doctor.AddAppointmentType(kept);
        doctor.AddAppointmentType(dropped);

        doctor.RemoveAllAppointmentTypesExceptGivenIds(new List<Guid> { kept });

        doctor.AppointmentTypes.ShouldHaveSingleItem().AppointmentTypeId.ShouldBe(kept);
    }

    [Fact]
    public void RemoveAllAppointmentTypesExceptGivenIds_EmptyList_Throws()
    {
        var doctor = NewDoctor();
        doctor.AddAppointmentType(Guid.NewGuid());

        var ex = Should.Throw<ArgumentException>(
            () => doctor.RemoveAllAppointmentTypesExceptGivenIds(new List<Guid>()));

        ex.ParamName.ShouldBe("appointmentTypeIds");
        doctor.AppointmentTypes.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveAllAppointmentTypes_ClearsEveryRow()
    {
        var doctor = NewDoctor();
        doctor.AddAppointmentType(Guid.NewGuid());
        doctor.AddAppointmentType(Guid.NewGuid());

        doctor.RemoveAllAppointmentTypes();

        doctor.AppointmentTypes.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------
    // Locations
    // ------------------------------------------------------------------------

    [Fact]
    public void AddLocation_AddsAJoinRowKeyedToThisDoctor()
    {
        var doctorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var doctor = NewDoctor(doctorId);

        doctor.AddLocation(locationId);

        var row = doctor.Locations.ShouldHaveSingleItem();
        row.DoctorId.ShouldBe(doctorId);
        row.LocationId.ShouldBe(locationId);
        row.GetKeys().ShouldBe(new object[] { doctorId, locationId });
    }

    [Fact]
    public void AddLocation_SameIdTwice_KeepsOneRow()
    {
        var doctor = NewDoctor();
        var locationId = Guid.NewGuid();

        doctor.AddLocation(locationId);
        doctor.AddLocation(locationId);

        doctor.Locations.ShouldHaveSingleItem().LocationId.ShouldBe(locationId);
    }

    [Fact]
    public void RemoveLocation_RemovesOnlyTheNamedLocation()
    {
        var doctor = NewDoctor();
        var removed = Guid.NewGuid();
        var kept = Guid.NewGuid();
        doctor.AddLocation(removed);
        doctor.AddLocation(kept);

        doctor.RemoveLocation(removed);

        doctor.Locations.ShouldHaveSingleItem().LocationId.ShouldBe(kept);
    }

    [Fact]
    public void RemoveLocation_UnknownId_LeavesExistingRowsInPlace()
    {
        // Decoy: an existing row that an unknown-id removal must NOT touch.
        var doctor = NewDoctor();
        var existing = Guid.NewGuid();
        doctor.AddLocation(existing);

        doctor.RemoveLocation(Guid.NewGuid());

        doctor.Locations.ShouldHaveSingleItem().LocationId.ShouldBe(existing);
    }

    [Fact]
    public void RemoveAllLocationsExceptGivenIds_KeepsOnlyTheGivenIds()
    {
        var doctor = NewDoctor();
        var kept = Guid.NewGuid();
        var dropped = Guid.NewGuid();
        doctor.AddLocation(kept);
        doctor.AddLocation(dropped);

        doctor.RemoveAllLocationsExceptGivenIds(new List<Guid> { kept });

        doctor.Locations.ShouldHaveSingleItem().LocationId.ShouldBe(kept);
    }

    [Fact]
    public void RemoveAllLocationsExceptGivenIds_EmptyList_Throws()
    {
        var doctor = NewDoctor();
        doctor.AddLocation(Guid.NewGuid());

        var ex = Should.Throw<ArgumentException>(
            () => doctor.RemoveAllLocationsExceptGivenIds(new List<Guid>()));

        ex.ParamName.ShouldBe("locationIds");
        doctor.Locations.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveAllLocations_ClearsEveryRow()
    {
        var doctor = NewDoctor();
        doctor.AddLocation(Guid.NewGuid());
        doctor.AddLocation(Guid.NewGuid());

        doctor.RemoveAllLocations();

        doctor.Locations.ShouldBeEmpty();
    }

    [Fact]
    public void AppointmentTypesAndLocations_AreIndependentCollections()
    {
        var doctor = NewDoctor();
        var sharedId = Guid.NewGuid();
        doctor.AddAppointmentType(sharedId);
        doctor.AddLocation(sharedId);

        doctor.RemoveAllLocations();

        doctor.Locations.ShouldBeEmpty();
        doctor.AppointmentTypes.Select(x => x.AppointmentTypeId).ShouldBe(new[] { sharedId });
    }
}
