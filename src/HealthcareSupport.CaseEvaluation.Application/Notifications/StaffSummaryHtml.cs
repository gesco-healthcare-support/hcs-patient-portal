using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Timing;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Pure rendering of the two twice-daily summaries (the <c>##SummaryContent##</c> block) so the layout can
/// be unit-tested without the event-handler harness. Every value taken from data is HTML-encoded.
/// </summary>
internal static class StaffSummaryHtml
{
    private const string Th = "<th style=\"text-align:left;padding:6px;border:1px solid #d1d5db;\">";
    private const string Td = "<td style=\"padding:6px;border:1px solid #d1d5db;\">";
    private const string TdRight = "<td style=\"padding:6px;border:1px solid #d1d5db;text-align:right;\">";

    /// <summary>"Morning" for the 08:00 run, "Afternoon" for the 16:00 run (Pacific).</summary>
    internal static string LabelFor(DateTime runUtc) => PacificTime.FromUtc(runUtc).Hour < 12 ? "Morning" : "Afternoon";

    internal static string BuildOffice(OfficeSummaryEto summary)
    {
        var c = summary.Counts;
        var sb = new StringBuilder();

        if (c.PendingOverdue > 0)
        {
            sb.Append("<p style=\"margin:12px 0;padding:8px 12px;border-left:4px solid #b91c1c;background:#fef2f2;color:#991b1b;font-size:14px;font-weight:600;\">");
            sb.Append(c.PendingOverdue).Append(c.PendingOverdue == 1 ? " pending request is " : " pending requests are ");
            sb.Append("past the ").Append(summary.DecisionDueDays).Append("-day decision deadline and need a decision now.");
            sb.Append("</p>");
        }

        sb.Append("<table style=\"border-collapse:collapse;font-size:13px;margin:12px 0;\"><tbody>");
        CountRow(sb, "Pending requests", c.Pending);
        CountRow(sb, "Pending past the decision deadline", c.PendingOverdue);
        CountRow(sb, "Reschedule requests waiting", c.RescheduleRequested);
        CountRow(sb, "Cancellation requests waiting", c.CancellationRequested);
        CountRow(sb, "Waiting for more information", c.InfoRequested);
        CountRow(sb, "Approved appointments (all time)", c.Approved);
        sb.Append("</tbody></table>");

        if (summary.Rows.Count > 0)
        {
            sb.Append("<p>Pending requests:</p>");
            sb.Append("<table style=\"border-collapse:collapse;width:100%;font-size:13px;margin:12px 0;\">");
            sb.Append("<thead><tr style=\"background:#f3f4f6;\">");
            foreach (var heading in new[] { "Confirmation #", "Patient", "Appointment date", "Due date", "Decision due" })
            {
                sb.Append(Th).Append(heading).Append("</th>");
            }
            sb.Append("</tr></thead><tbody>");
            foreach (var row in summary.Rows)
            {
                sb.Append("<tr").Append(row.IsOverdue ? " style=\"background:#fef2f2;\"" : string.Empty).Append('>');
                sb.Append(Td).Append(WebUtility.HtmlEncode(row.RequestConfirmationNumber ?? string.Empty)).Append("</td>");
                sb.Append(Td).Append(WebUtility.HtmlEncode(row.PatientName ?? string.Empty)).Append("</td>");
                sb.Append(Td).Append(Date(row.AppointmentDate)).Append("</td>");
                sb.Append(Td).Append(row.DueDate.HasValue ? Date(row.DueDate.Value) : "&mdash;").Append("</td>");
                sb.Append(Td).Append(Date(row.DecisionDue));
                if (row.IsOverdue)
                {
                    sb.Append(" <strong style=\"color:#b91c1c;\">(OVERDUE)</strong>");
                }
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
        }

        return sb.ToString();
    }

    internal static string BuildSupervisor(SupervisorSummaryEto summary)
    {
        var sb = new StringBuilder();
        sb.Append("<table style=\"border-collapse:collapse;width:100%;font-size:13px;margin:12px 0;\">");
        sb.Append("<thead><tr style=\"background:#f3f4f6;\">");
        foreach (var heading in new[] { "Office", "Pending", "Past deadline", "Reschedule requests", "Cancellation requests", "More info needed" })
        {
            sb.Append(Th).Append(heading).Append("</th>");
        }
        sb.Append("</tr></thead><tbody>");
        foreach (var office in summary.Offices)
        {
            var c = office.Counts;
            sb.Append("<tr").Append(c.PendingOverdue > 0 ? " style=\"background:#fef2f2;\"" : string.Empty).Append('>');
            sb.Append(Td).Append(WebUtility.HtmlEncode(office.OfficeName ?? string.Empty)).Append("</td>");
            foreach (var n in new[] { c.Pending, c.PendingOverdue, c.RescheduleRequested, c.CancellationRequested, c.InfoRequested })
            {
                sb.Append(TdRight).Append(n.ToString(CultureInfo.InvariantCulture)).Append("</td>");
            }
            sb.Append("</tr>");
        }

        var offices = summary.Offices;
        sb.Append("<tr style=\"background:#f3f4f6;font-weight:600;\">").Append(Td).Append("All offices</td>");
        foreach (var n in new[]
        {
            offices.Sum(o => o.Counts.Pending),
            offices.Sum(o => o.Counts.PendingOverdue),
            offices.Sum(o => o.Counts.RescheduleRequested),
            offices.Sum(o => o.Counts.CancellationRequested),
            offices.Sum(o => o.Counts.InfoRequested),
        })
        {
            sb.Append(TdRight).Append(n.ToString(CultureInfo.InvariantCulture)).Append("</td>");
        }
        sb.Append("</tr></tbody></table>");
        return sb.ToString();
    }

    private static void CountRow(StringBuilder sb, string label, int count) =>
        sb.Append("<tr>").Append(Td).Append(label).Append("</td>")
            .Append(TdRight).Append(count.ToString(CultureInfo.InvariantCulture)).Append("</td></tr>");

    private static string Date(DateTime value) => value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
}
