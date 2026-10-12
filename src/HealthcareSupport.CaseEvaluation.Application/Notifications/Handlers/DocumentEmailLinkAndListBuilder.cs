using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// The login link and outstanding-document list that the document accepted and rejected
/// emails build identically. Shared so the two handlers cannot drift apart, and so the
/// HtmlEncode on the caller-supplied document name lives in exactly one place. #1010
/// </summary>
internal static class DocumentEmailLinkAndListBuilder
{
    /// <summary>
    /// Tenant login link for the shared document body. Reuses E1's login-URL composer for a
    /// link shape identical to the E2 booking email; no per-recipient email pre-fill (the
    /// body is CC'd to many).
    /// </summary>
    internal static async Task<string> BuildLoginUrlAsync(
        IAccountUrlBuilder accountUrlBuilder, Guid? tenantId, string? tenantName)
    {
        var authServerBaseUrl = await accountUrlBuilder.BuildAuthServerRootUrlAsync(tenantId);
        return BookingSubmissionEmailHandler.BuildLoginUrl(authServerBaseUrl, tenantName, string.Empty);
    }

    /// <summary>
    /// Adds the outstanding required documents, by name, plus the tenant-aware appointment
    /// URL to the base variables. HtmlEncode: the name is the uploaded file's, so it is
    /// caller-supplied and reaches an email body as raw HTML.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<string, object?>> BuildVariablesWithRemainingAsync(
        IAccountUrlBuilder accountUrlBuilder,
        Guid? tenantId,
        IReadOnlyDictionary<string, object?> baseVariables,
        Guid appointmentId,
        IReadOnlyList<MissingRequiredDocument> missing)
    {
        var portalUrl = await accountUrlBuilder.BuildPortalRootUrlAsync(tenantId);
        var url = $"{portalUrl.TrimEnd('/')}/appointments/view/{appointmentId:N}";
        var listHtml = string.Concat(
            missing.Select(m => $"<li>{System.Net.WebUtility.HtmlEncode(m.Name ?? string.Empty)}</li>"));
        return new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal)
        {
            ["RemainingDocumentCount"] = missing.Count,
            ["RemainingDocumentList"] = listHtml,
            ["URL"] = url,
        };
    }
}
