using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;

/// <summary>
/// Prompt 15 (2026-06-15): pure replace-set reconciliation for an
/// AppointmentType's field configuration. Given the rows stored today and the
/// desired set (keyed by <c>FieldName</c>), computes which rows to create,
/// update, or delete so the admin can save the whole Field Configuration panel
/// at once. Extracted <c>internal static</c> so the diff is unit-testable
/// without the ABP integration harness (mirrors the DoctorAvailabilities
/// helpers); the AppService applies the plan via the repository + manager.
/// </summary>
internal static class FieldConfigReconciler
{
    internal sealed record Existing(
        Guid Id,
        string FieldName,
        bool Hidden,
        bool ReadOnly,
        bool Required,
        string? DefaultValue);

    internal sealed record Desired(
        string FieldName,
        bool Hidden,
        bool ReadOnly,
        bool Required,
        string? DefaultValue);

    internal sealed record Result(
        IReadOnlyList<Desired> ToCreate,
        IReadOnlyList<(Guid Id, Desired Values)> ToUpdate,
        IReadOnlyList<Guid> ToDelete);

    internal static Result Reconcile(
        IReadOnlyList<Existing> existing,
        IReadOnlyList<Desired> desired)
    {
        // De-duplicate the desired set by FieldName (last wins) and drop blank
        // names, so a malformed batch can never create duplicate rows that would
        // violate the composite unique (TenantId, AppointmentTypeId, FieldName).
        var desiredByName = new Dictionary<string, Desired>(StringComparer.Ordinal);
        foreach (var d in desired)
        {
            if (!string.IsNullOrWhiteSpace(d.FieldName))
            {
                desiredByName[d.FieldName] = d;
            }
        }

        var existingByName = existing
            .GroupBy(e => e.FieldName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var toCreate = new List<Desired>();
        var toUpdate = new List<(Guid Id, Desired Values)>();
        var toDelete = new List<Guid>();

        foreach (var pair in desiredByName)
        {
            if (existingByName.TryGetValue(pair.Key, out var match))
            {
                if (Differs(match, pair.Value))
                {
                    toUpdate.Add((match.Id, pair.Value));
                }
            }
            else
            {
                toCreate.Add(pair.Value);
            }
        }

        foreach (var e in existing)
        {
            if (!desiredByName.ContainsKey(e.FieldName))
            {
                toDelete.Add(e.Id);
            }
        }

        return new Result(toCreate, toUpdate, toDelete);
    }

    private static bool Differs(Existing e, Desired d) =>
        e.Hidden != d.Hidden
        || e.ReadOnly != d.ReadOnly
        || e.Required != d.Required
        || !string.Equals(e.DefaultValue ?? string.Empty, d.DefaultValue ?? string.Empty, StringComparison.Ordinal);
}
