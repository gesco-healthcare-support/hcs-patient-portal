using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// Group K T2: the change-log builder explodes ABP audit changes into per-field rows
/// and applies the PHI policy. Pinned here without a DB because ABP audit entities
/// have protected setters; the AppService glue is verified live.
/// </summary>
public class AppointmentChangeLogBuilderTests
{
    private const string ApptType = "HealthcareSupport.CaseEvaluation.Appointments.Appointment";
    private const string BodyPartType =
        "HealthcareSupport.CaseEvaluation.AppointmentBodyParts.AppointmentBodyPart";

    [Fact]
    public void Explodes_one_row_per_changed_property_and_labels_the_entity()
    {
        var apptId = Guid.NewGuid();
        var changes = new[]
        {
            new RawEntityChange(ApptType, apptId.ToString(), "Updated", new DateTime(2026, 6, 1),
                new[]
                {
                    new RawPropertyChange("PanelNumber", "P1", "P2"),
                    new RawPropertyChange("DueDate", "2026-06-10", "2026-06-12"),
                }),
        };

        var rows = AppointmentChangeLogBuilder.BuildRows(changes, apptId);

        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(r => r.EntityType == "Appointment");
        rows.ShouldAllBe(r => r.AppointmentId == apptId);
        rows.ShouldContain(r =>
            r.PropertyName == "PanelNumber" && r.OldValue == "P1" && r.NewValue == "P2" && !r.ValueRedacted);
    }

    [Fact]
    public void Masks_sensitive_values_and_drops_audit_noise()
    {
        var changes = new[]
        {
            new RawEntityChange(ApptType, Guid.NewGuid().ToString(), "Updated", new DateTime(2026, 6, 1),
                new[]
                {
                    new RawPropertyChange("SocialSecurityNumber", "masked-old", "masked-new"),
                    new RawPropertyChange("PatientId", "a", "b"),
                    new RawPropertyChange("AppointmentDate", "2026-06-01", "2026-06-08"),
                }),
        };

        var rows = AppointmentChangeLogBuilder.BuildRows(changes, null);

        rows.Count.ShouldBe(2); // SSN (masked) + AppointmentDate (shown); PatientId dropped.

        var ssn = rows.Single(r => r.PropertyName == "SocialSecurityNumber");
        ssn.ValueRedacted.ShouldBeTrue();
        ssn.OldValue.ShouldBeNull();
        ssn.NewValue.ShouldBeNull();

        rows.ShouldContain(r => r.PropertyName == "AppointmentDate" && r.NewValue == "2026-06-08");
        rows.ShouldNotContain(r => r.PropertyName == "PatientId");
    }

    [Fact]
    public void Global_rows_recover_appointment_id_from_the_appointment_entity_id()
    {
        var apptId = Guid.NewGuid();
        var changes = new[]
        {
            new RawEntityChange(ApptType, apptId.ToString(), "Updated", new DateTime(2026, 6, 1),
                new[] { new RawPropertyChange("PanelNumber", "a", "b") }),
        };

        var rows = AppointmentChangeLogBuilder.BuildRows(changes, appointmentId: null);

        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(r => r.AppointmentId == apptId);
    }

    [Fact]
    public void Child_entity_rows_are_labeled_and_left_unlinked_in_global_scope()
    {
        var changes = new[]
        {
            new RawEntityChange(BodyPartType, Guid.NewGuid().ToString(), "Created", new DateTime(2026, 6, 2),
                new[] { new RawPropertyChange("BodyPartName", null, "Left Knee") }),
        };

        var rows = AppointmentChangeLogBuilder.BuildRows(changes, appointmentId: null);

        var row = rows.ShouldHaveSingleItem();
        row.EntityType.ShouldBe("Body Part");
        row.AppointmentId.ShouldBeNull();
        row.ValueRedacted.ShouldBeTrue(); // BodyPartName is not allowlisted -> masked.
    }
}
