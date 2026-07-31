using HealthcareSupport.CaseEvaluation.Appointments.Auditing;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments.Auditing;

/// <summary>
/// Group K (G-02-02/03): the audit change-log + intake email must never leak PHI.
/// These tests pin the deny-by-default field allowlist: only listed non-sensitive
/// fields may reveal old/new values; everything else is masked or dropped.
/// </summary>
public class AuditFieldPolicyTests
{
    private const string ApptType = "HealthcareSupport.CaseEvaluation.Appointments.Appointment";
    private const string InjuryType =
        "HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails.AppointmentInjuryDetail";

    [Theory]
    [InlineData("AppointmentDate")]
    [InlineData("AppointmentTypeId")]
    [InlineData("LocationId")]
    [InlineData("DoctorAvailabilityId")]
    [InlineData("PanelNumber")]
    [InlineData("DueDate")]
    [InlineData("AppointmentStatus")]
    [InlineData("AppointmentLanguageId")]
    [InlineData("NeedsInterpreter")]
    public void ShowListed_appointment_fields_reveal_their_values(string prop)
    {
        AuditFieldPolicy.ShouldShowValue(ApptType, prop).ShouldBeTrue();

        var row = AuditFieldDiff.BuildRow(ApptType, prop, "old", "new");

        row.ShouldNotBeNull();
        row!.ValueRedacted.ShouldBeFalse();
        row.OldValue.ShouldBe("old");
        row.NewValue.ShouldBe("new");
    }

    [Theory]
    [InlineData("SocialSecurityNumber")]
    [InlineData("DateOfBirth")]
    [InlineData("FirstName")]
    [InlineData("LastName")]
    [InlineData("PatientEmail")]
    [InlineData("ClaimNumber")]
    [InlineData("PhoneNumber")]
    [InlineData("Address")]
    public void Sensitive_fields_are_masked_not_dropped(string prop)
    {
        AuditFieldPolicy.ShouldInclude(ApptType, prop).ShouldBeTrue();
        AuditFieldPolicy.ShouldShowValue(ApptType, prop).ShouldBeFalse();

        var row = AuditFieldDiff.BuildRow(ApptType, prop, "secret-old", "secret-new");

        row.ShouldNotBeNull();
        row!.ValueRedacted.ShouldBeTrue();
        row.OldValue.ShouldBeNull();
        row.NewValue.ShouldBeNull();
    }

    [Theory]
    [InlineData("PatientId")]
    [InlineData("CreatorId")]
    [InlineData("LastModifierId")]
    [InlineData("ConcurrencyStamp")]
    [InlineData("LastModificationTime")]
    [InlineData("TenantId")]
    public void Audit_noise_props_are_dropped_entirely(string prop)
    {
        AuditFieldPolicy.ShouldInclude(ApptType, prop).ShouldBeFalse();
        AuditFieldDiff.BuildRow(ApptType, prop, "x", "y").ShouldBeNull();
    }

    [Fact]
    public void Unlisted_field_is_masked_by_default()
    {
        // Deny-by-default: a field nobody listed must never reveal its value.
        AuditFieldPolicy.ShouldShowValue(ApptType, "SomeFutureField").ShouldBeFalse();

        var row = AuditFieldDiff.BuildRow(ApptType, "SomeFutureField", "a", "b");

        row.ShouldNotBeNull();
        row!.ValueRedacted.ShouldBeTrue();
    }

    [Fact]
    public void Show_list_matches_on_the_simple_entity_type_name()
    {
        // The allowlist keys by simple name, so a fully-qualified child type resolves.
        AuditFieldPolicy.ShouldShowValue(InjuryType, "DateOfInjury").ShouldBeTrue();
    }
}
