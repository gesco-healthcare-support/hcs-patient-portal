using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 13 (2026-05-04) -- pure tests for
/// <see cref="AppointmentAccessRules"/>.
/// </summary>
public class AppointmentAccessRulesUnitTests
{
    private static readonly Guid CallerId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    [Fact]
    public void CanRead_InternalUser_BypassesEverything()
    {
        AppointmentAccessRules.CanRead(
            callerUserId: null,
            callerIsInternalUser: true,
            appointmentCreatorId: Guid.NewGuid(),
            accessorEntries: null).ShouldBeTrue();
    }

    [Fact]
    public void CanRead_ExternalUser_CreatorMatch_True()
    {
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: CallerId,
            accessorEntries: null).ShouldBeTrue();
    }

    [Fact]
    public void CanRead_ExternalUser_AccessorRowExists_True()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.View),
        };
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: entries).ShouldBeTrue();
    }

    [Fact]
    public void CanRead_ExternalUser_NoCreatorMatchNoAccessor_False()
    {
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: System.Array.Empty<AppointmentAccessRules.AccessorEntry>()).ShouldBeFalse();
    }

    [Fact]
    public void CanRead_AccessorEntryForDifferentUser_False()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(OtherUserId, AccessType.Edit),
        };
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: Guid.NewGuid(),
            accessorEntries: entries).ShouldBeFalse();
    }

    [Fact]
    public void CanRead_AnonymousCaller_False()
    {
        AppointmentAccessRules.CanRead(
            callerUserId: null,
            callerIsInternalUser: false,
            appointmentCreatorId: Guid.NewGuid(),
            accessorEntries: null).ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_InternalUser_BypassesEverything()
    {
        AppointmentAccessRules.CanEdit(
            callerUserId: null,
            callerIsInternalUser: true,
            appointmentCreatorId: Guid.NewGuid(),
            accessorEntries: null).ShouldBeTrue();
    }

    [Fact]
    public void CanEdit_ExternalUser_Creator_True()
    {
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: CallerId,
            accessorEntries: null).ShouldBeTrue();
    }

    [Fact]
    public void CanEdit_ExternalUser_AccessorWithEdit_True()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.Edit),
        };
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: entries).ShouldBeTrue();
    }

    [Fact]
    public void CanEdit_ExternalUser_AccessorWithViewOnly_False()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.View),
        };
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: entries).ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_ExternalUser_NoMatch_False()
    {
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: OtherUserId,
            accessorEntries: System.Array.Empty<AppointmentAccessRules.AccessorEntry>()).ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_NullCreatorId_FallsThroughToAccessorCheck()
    {
        // System / legacy rows with no CreatorId still grant edit when
        // an Edit accessor row exists for the caller.
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.Edit),
        };
        AppointmentAccessRules.CanEdit(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: null,
            accessorEntries: entries).ShouldBeTrue();
    }

    [Fact]
    public void CanRead_NullCreatorId_FallsThroughToAccessorCheck()
    {
        var entries = new[]
        {
            new AppointmentAccessRules.AccessorEntry(CallerId, AccessType.View),
        };
        AppointmentAccessRules.CanRead(
            callerUserId: CallerId,
            callerIsInternalUser: false,
            appointmentCreatorId: null,
            accessorEntries: entries).ShouldBeTrue();
    }
}
