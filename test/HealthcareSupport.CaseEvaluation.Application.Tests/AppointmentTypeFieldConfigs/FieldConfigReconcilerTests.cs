using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;

/// <summary>
/// Prompt 15 (2026-06-15) -- pure unit tests for the field-config replace-set
/// reconciler. Verified directly via InternalsVisibleTo (same approach as the
/// DoctorAvailabilities helpers; the ABP integration harness is gated).
/// </summary>
public class FieldConfigReconcilerTests
{
    private static FieldConfigReconciler.Existing Existing(
        Guid id,
        string name,
        bool hidden = false,
        bool readOnly = false,
        bool required = false,
        string? dv = null) => new(id, name, hidden, readOnly, required, dv);

    private static FieldConfigReconciler.Desired Desired(
        string name,
        bool hidden = false,
        bool readOnly = false,
        bool required = false,
        string? dv = null) => new(name, hidden, readOnly, required, dv);

    [Fact]
    public void Reconcile_NewFieldName_GoesToCreate()
    {
        var result = FieldConfigReconciler.Reconcile(
            new List<FieldConfigReconciler.Existing>(),
            new[] { Desired("panelNumber", hidden: true) });

        result.ToCreate.Select(d => d.FieldName).ShouldBe(new[] { "panelNumber" });
        result.ToUpdate.ShouldBeEmpty();
        result.ToDelete.ShouldBeEmpty();
    }

    [Fact]
    public void Reconcile_ChangedRow_GoesToUpdateWithExistingId()
    {
        var id = Guid.NewGuid();
        var result = FieldConfigReconciler.Reconcile(
            new[] { Existing(id, "panelNumber", hidden: false, required: false) },
            new[] { Desired("panelNumber", hidden: false, required: true) });

        result.ToCreate.ShouldBeEmpty();
        result.ToDelete.ShouldBeEmpty();
        result.ToUpdate.Count.ShouldBe(1);
        result.ToUpdate[0].Id.ShouldBe(id);
        result.ToUpdate[0].Values.Required.ShouldBeTrue();
    }

    [Fact]
    public void Reconcile_UnchangedRow_IsNoOp()
    {
        var id = Guid.NewGuid();
        var result = FieldConfigReconciler.Reconcile(
            new[] { Existing(id, "ssn", hidden: true, readOnly: true, required: false, dv: "X") },
            new[] { Desired("ssn", hidden: true, readOnly: true, required: false, dv: "X") });

        result.ToCreate.ShouldBeEmpty();
        result.ToUpdate.ShouldBeEmpty();
        result.ToDelete.ShouldBeEmpty();
    }

    [Fact]
    public void Reconcile_RowMissingFromDesired_GoesToDelete()
    {
        var keep = Guid.NewGuid();
        var drop = Guid.NewGuid();
        var result = FieldConfigReconciler.Reconcile(
            new[] { Existing(keep, "ssn"), Existing(drop, "referredBy") },
            new[] { Desired("ssn") });

        result.ToDelete.ShouldBe(new[] { drop });
        result.ToUpdate.ShouldBeEmpty();
        result.ToCreate.ShouldBeEmpty();
    }

    [Fact]
    public void Reconcile_EmptyDesired_DeletesEverything()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var result = FieldConfigReconciler.Reconcile(
            new[] { Existing(a, "ssn"), Existing(b, "referredBy") },
            new List<FieldConfigReconciler.Desired>());

        result.ToDelete.OrderBy(x => x).ShouldBe(new[] { a, b }.OrderBy(x => x).ToArray());
        result.ToCreate.ShouldBeEmpty();
        result.ToUpdate.ShouldBeEmpty();
    }

    [Fact]
    public void Reconcile_DuplicateDesiredNames_LastWins_NoDuplicateCreate()
    {
        var result = FieldConfigReconciler.Reconcile(
            new List<FieldConfigReconciler.Existing>(),
            new[]
            {
                Desired("ssn", hidden: false),
                Desired("ssn", hidden: true),
            });

        result.ToCreate.Count.ShouldBe(1);
        result.ToCreate[0].Hidden.ShouldBeTrue(); // last wins
    }

    [Fact]
    public void Reconcile_BlankFieldNames_AreIgnored()
    {
        var result = FieldConfigReconciler.Reconcile(
            new List<FieldConfigReconciler.Existing>(),
            new[] { Desired("  "), Desired("") });

        result.ToCreate.ShouldBeEmpty();
    }
}
