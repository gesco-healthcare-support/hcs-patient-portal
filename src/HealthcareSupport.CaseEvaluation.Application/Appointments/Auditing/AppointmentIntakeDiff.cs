using System.Globalization;
using HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

namespace HealthcareSupport.CaseEvaluation.Appointments.Auditing;

/// <summary>
/// Group K (G-02-03): computes the redacted intake-field diff for the
/// intake-changed email by comparing the pre-update appointment-row values with
/// the new ones, reusing <see cref="AuditFieldDiff"/> so the email honors the
/// same PHI allowlist as the change-log view. Pure + deterministic -> unit-tested.
///
/// Scope: the human-readable appointment-row fields (date/time, panel #, due
/// date). Foreign-key fields (type / location / slot) are Guids in audit and are
/// surfaced in the change-log VIEW; rendering them by name in the email is a
/// later refinement.
/// </summary>
public static class AppointmentIntakeDiff
{
    public static List<AuditDiffRow> Compute(
        DateTime oldAppointmentDate,
        DateTime newAppointmentDate,
        string? oldPanelNumber,
        string? newPanelNumber,
        DateTime? oldDueDate,
        DateTime? newDueDate)
    {
        var rows = new List<AuditDiffRow>();
        AddIfChanged(rows, "AppointmentDate", FormatDateTime(oldAppointmentDate), FormatDateTime(newAppointmentDate));
        AddIfChanged(rows, "PanelNumber", oldPanelNumber, newPanelNumber);
        AddIfChanged(rows, "DueDate", FormatDate(oldDueDate), FormatDate(newDueDate));
        return rows;
    }

    public static bool IsDateOrTimeChanged(DateTime oldAppointmentDate, DateTime newAppointmentDate)
        => oldAppointmentDate != newAppointmentDate;

    private static void AddIfChanged(List<AuditDiffRow> rows, string field, string? oldValue, string? newValue)
    {
        if (string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }
        var row = AuditFieldDiff.BuildRow(AppointmentAuditedEntities.Appointment, field, oldValue, newValue);
        if (row != null)
        {
            rows.Add(row);
        }
    }

    private static string FormatDateTime(DateTime value)
        => value.ToString("MM/dd/yyyy h:mm tt", CultureInfo.InvariantCulture);

    private static string? FormatDate(DateTime? value)
        => value?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
}
