using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>The decisions <see cref="AttorneyMasterWritePolicy"/> makes, pinned without a database.</summary>
public class AttorneyMasterWritePolicyUnitTests
{
    private static readonly Guid Caller = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Someone = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Staff_may_name_any_account_and_an_external_caller_none()
    {
        AttorneyMasterWritePolicy.AccountIdToHonour(true, Someone).ShouldBe(Someone);
        AttorneyMasterWritePolicy.AccountIdToHonour(false, Someone).ShouldBe(Guid.Empty);
        AttorneyMasterWritePolicy.AccountIdToHonour(false, Caller).ShouldBe(Guid.Empty,
            "even their own id: the account comes from the typed email");
    }

    [Fact]
    public void Only_staff_or_the_records_own_account_may_change_a_record()
    {
        AttorneyMasterWritePolicy.MayChangeMaster(true, Someone, Caller).ShouldBeTrue();
        AttorneyMasterWritePolicy.MayChangeMaster(false, Caller, Caller).ShouldBeTrue();
        AttorneyMasterWritePolicy.MayChangeMaster(false, Someone, Caller).ShouldBeFalse();
        AttorneyMasterWritePolicy.MayChangeMaster(false, null, Caller).ShouldBeFalse("an unowned record is nobody's to change");
        AttorneyMasterWritePolicy.MayChangeMaster(false, null, null).ShouldBeFalse();
    }

    [Theory]
    [InlineData("kept", "typed", "kept")]
    [InlineData(null, "typed", "typed")]
    [InlineData("  ", "typed", "typed")]
    public void A_blank_field_is_filled_and_a_set_one_is_kept(string? current, string? typed, string? expected)
    {
        AttorneyMasterWritePolicy.FillBlank(current, typed).ShouldBe(expected);
    }

    [Theory]
    [InlineData("typed", "current", "typed")]
    [InlineData(null, "current", "current")]
    [InlineData("", "current", "current")]
    public void The_appointment_copy_takes_what_was_typed_else_keeps_its_value(string? typed, string? current, string? expected)
    {
        AttorneyMasterWritePolicy.TypedOr(typed, current).ShouldBe(expected);
    }
}
