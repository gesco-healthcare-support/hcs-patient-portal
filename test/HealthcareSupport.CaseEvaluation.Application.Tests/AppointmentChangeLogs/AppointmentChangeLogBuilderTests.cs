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

    [Fact]
    public void Every_row_of_a_change_carries_the_action_label_and_the_audit_log_id()
    {
        var apptId = Guid.NewGuid();
        var log = Guid.NewGuid();
        var changes = new[]
        {
            new RawEntityChange(ApptType, apptId.ToString(), "Updated", new DateTime(2026, 6, 1),
                new[]
                {
                    new RawPropertyChange("AppointmentStatus", "1", "2"),
                    new RawPropertyChange("DueDate", "2026-06-10", "2026-06-12"),
                },
                log),
        };

        var built = AppointmentChangeLogBuilder.BuildRowsWithSource(changes, apptId);

        built.Count.ShouldBe(2);
        built.ShouldAllBe(r => r.Row.ActionLabel == "Approved");
        built.ShouldAllBe(r => r.AuditLogId == log);
    }

    [Fact]
    public void A_named_event_whose_every_property_is_dropped_still_yields_one_row()
    {
        const string accessorType = AppointmentAuditedEntities.Accessor;
        var apptId = Guid.NewGuid();
        var changes = new[]
        {
            new RawEntityChange(accessorType, Guid.NewGuid().ToString(), "Created", new DateTime(2026, 6, 1),
                new[]
                {
                    new RawPropertyChange("IdentityUserId", null, Guid.NewGuid().ToString()),
                    new RawPropertyChange("AppointmentId", null, apptId.ToString()),
                }),
        };

        var rows = AppointmentChangeLogBuilder.BuildRows(changes, apptId);

        rows.Count.ShouldBe(1);
        rows[0].ActionLabel.ShouldBe("Authorized user added");
        rows[0].PropertyName.ShouldBe(string.Empty);
        rows[0].EntityType.ShouldBe("Authorized User");
    }

    [Fact]
    public void A_generic_edit_whose_every_property_is_dropped_yields_no_row()
    {
        var changes = new[]
        {
            new RawEntityChange(BodyPartType, Guid.NewGuid().ToString(), "Updated", new DateTime(2026, 6, 1),
                new[] { new RawPropertyChange("LastModificationTime", "a", "b") }),
        };

        AppointmentChangeLogBuilder.BuildRows(changes, Guid.NewGuid()).ShouldBeEmpty();
    }

    [Fact]
    public void The_per_case_list_covers_every_audited_entity_and_the_global_scan_stays_the_intake_five()
    {
        AppointmentAuditedEntities.All.Count.ShouldBe(5);
        AppointmentAuditedEntities.PerCase.ShouldContain(AppointmentAuditedEntities.Document);
        AppointmentAuditedEntities.PerCase.ShouldContain(AppointmentAuditedEntities.ConsentRound);
        AppointmentAuditedEntities.All.ShouldAllBe(t => AppointmentAuditedEntities.PerCase.Contains(t));
    }
}
