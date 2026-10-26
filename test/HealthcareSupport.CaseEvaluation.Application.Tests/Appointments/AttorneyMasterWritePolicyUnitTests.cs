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

    private static readonly Guid MasterId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StateA = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid StateB = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static AttorneyMasterValues Typed() => new(
        StateA, null, "Typed Firm", null, "5550001111", "typed.example", "5550002222",
        "1 Typed St", "Typedville", "90001", "typed@firm.test", "Tina", "Typed");

    private static AttorneyMasterRecord Record(Guid? accountId) => new(MasterId, "stamp-1", new AttorneyMasterValues(
        StateB, accountId, "Record Firm", "Record Address", "", "record.example", null,
        "9 Record Rd", "Recordtown", "90002", "record@firm.test", "Rita", ""));

    [Fact]
    public void Nothing_named_or_matched_creates_a_new_record_from_what_was_typed()
    {
        var write = AttorneyMasterWritePolicy.Decide(false, Caller, Typed(), "typed-stamp", null, null);

        write.ShouldBe(new AttorneyMasterWrite(null, Typed(), null, false));
    }

    [Fact]
    public void A_named_record_someone_else_owns_is_linked_unchanged()
    {
        var write = AttorneyMasterWritePolicy.Decide(false, Caller, Typed(), "typed-stamp", Record(Someone), null);

        write.ShouldBe(new AttorneyMasterWrite(MasterId, null, null, true));
    }

    [Fact]
    public void A_named_record_the_caller_may_change_takes_what_was_typed_but_keeps_its_firm_address()
    {
        var write = AttorneyMasterWritePolicy.Decide(true, Caller, Typed(), "typed-stamp", Record(Someone), Record(Someone));

        write.ShouldBe(new AttorneyMasterWrite(
            MasterId, Typed() with { FirmAddress = "Record Address" }, "typed-stamp", false),
            "staff edit a named record with the stamp they loaded; an email match is not consulted");
    }

    [Fact]
    public void Another_partys_email_matched_record_only_has_its_blanks_filled()
    {
        var write = AttorneyMasterWritePolicy.Decide(false, Caller, Typed(), "typed-stamp", null, Record(null));

        write.TargetId.ShouldBe(MasterId);
        write.ConcurrencyStamp.ShouldBe("stamp-1");
        write.TypedOnAppointmentOnly.ShouldBeTrue();
        write.Values.ShouldBe(new AttorneyMasterValues(
            StateB, null, "Record Firm", "Record Address", "5550001111", "record.example", "5550002222",
            "9 Record Rd", "Recordtown", "90002", "record@firm.test", "Rita", "Typed"));
    }

    [Fact]
    public void An_email_matched_record_with_no_account_takes_the_typed_account()
    {
        var typed = Typed() with { IdentityUserId = Caller };

        var write = AttorneyMasterWritePolicy.Decide(false, Someone, typed, null, null, Record(null));

        write.Values!.IdentityUserId.ShouldBe(Caller);
    }

    [Fact]
    public void The_callers_own_email_matched_record_takes_what_was_typed_over_what_it_holds()
    {
        var typed = Typed() with { FirmName = null, City = null };

        var write = AttorneyMasterWritePolicy.Decide(false, Caller, typed, "typed-stamp", null, Record(Caller));

        write.ShouldBe(new AttorneyMasterWrite(MasterId, new AttorneyMasterValues(
            StateA, Caller, "Record Firm", "Record Address", "5550001111", "typed.example", "5550002222",
            "1 Typed St", "Recordtown", "90001", "typed@firm.test", "Tina", "Typed"), "stamp-1", false));
    }
}
