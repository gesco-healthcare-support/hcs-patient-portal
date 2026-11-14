using System.Globalization;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// Turns one audited entity change into a short, readable action ("Booked", "Approved",
/// "Document rejected", ...) for the change-log page. Pure and deterministic.
///
/// <para>Labels are built ONLY from enum states and the kind of change, never from free-text or
/// sensitive values, so a label can never carry PHI (the reject note, emails, names and dates stay
/// behind the field-level redaction policy). ABP records enum values as the number (or, defensively,
/// the name); both are accepted. The integers of <see cref="AppointmentStatusType"/> are read through
/// the enum, so 9/10/11 (dead) fall through to a generic label and nothing is renumbered.</para>
/// </summary>
public static class AppointmentChangeActionLabeler
{
    public const string Booked = "Booked";
    public const string DetailsEdited = "Details edited";
    public const string PartiesEdited = "Parties edited";

    public static string Label(RawEntityChange change)
    {
        var type = SimpleName(change.EntityTypeFullName);
        var created = IsKind(change, "Created");
        var removed = IsKind(change, "Deleted") || IsTrue(NewValue(change, "IsDeleted"));

        return type switch
        {
            "Appointment" => AppointmentLabel(change, created, removed),
            "AppointmentChangeRequest" => ChangeRequestLabel(change, created, removed),
            "ChangeRequestConsentRound" => ConsentRoundLabel(change, created, removed),
            "AppointmentChangeRequestDocument" => created ? "Change request document added"
                : removed ? "Change request document removed" : "Change request document updated",
            "AppointmentDocument" => DocumentLabel(change, created, removed),
            "AppointmentPacket" => created ? "Packet generated" : removed ? "Packet removed" : "Packet updated",
            "AppointmentInfoRequest" => InfoRequestLabel(change, created, removed),
            "AppointmentAccessor" => created ? "Authorized user added"
                : removed ? "Authorized user removed" : "Authorized user updated",
            "AppointmentApplicantAttorney" or "AppointmentDefenseAttorney" or "AppointmentClaimExaminer"
                => created ? "Party added" : removed ? "Party removed" : PartiesEdited,
            "AppointmentInjuryDetail" or "AppointmentBodyPart" or "AppointmentPrimaryInsurance"
                or "AppointmentEmployerDetail"
                => created ? "Details added" : removed ? "Details removed" : DetailsEdited,
            _ => DetailsEdited,
        };
    }

    private static string AppointmentLabel(RawEntityChange change, bool created, bool removed)
    {
        if (created)
        {
            return Booked;
        }
        if (removed)
        {
            return "Appointment removed";
        }
        var status = Find(change, "AppointmentStatus");
        if (status != null && TryEnum<AppointmentStatusType>(Norm(status.NewValue), out var next))
        {
            AppointmentStatusType? previous = TryEnum<AppointmentStatusType>(Norm(status.OriginalValue), out var p)
                ? p
                : null;
            return StatusLabel(next, previous);
        }
        var partyEmailChanged = change.Properties.Any(p =>
            p.PropertyName is "PatientEmail" or "ApplicantAttorneyEmail" or "DefenseAttorneyEmail" or "ClaimExaminerEmail");
        return partyEmailChanged ? PartiesEdited : DetailsEdited;
    }

    /// <summary>The action for a status the appointment moved INTO, given where it came from.</summary>
    public static string StatusLabel(AppointmentStatusType next, AppointmentStatusType? previous)
    {
        switch (next)
        {
            case AppointmentStatusType.Pending:
                return previous == AppointmentStatusType.InfoRequested ? "Resubmitted" : "Set to pending";
            case AppointmentStatusType.Approved:
                return previous is AppointmentStatusType.RescheduleRequested or AppointmentStatusType.CancellationRequested
                    ? "Back to approved (change request closed)"
                    : "Approved";
            case AppointmentStatusType.Rejected: return "Rejected";
            case AppointmentStatusType.InfoRequested: return "Sent back for more information";
            case AppointmentStatusType.NoShow: return "No show";
            case AppointmentStatusType.NotSeen: return "Not seen";
            case AppointmentStatusType.CancelledNoBill: return "Cancelled (no bill)";
            case AppointmentStatusType.CancelledLate: return "Cancelled (late)";
            case AppointmentStatusType.RescheduledNoBill: return "Rescheduled (no bill)";
            case AppointmentStatusType.RescheduledLate: return "Rescheduled (late)";
            case AppointmentStatusType.RescheduleRequested: return "Reschedule requested";
            case AppointmentStatusType.CancellationRequested: return "Cancellation requested";
            default: return "Status changed";
        }
    }

    private static string ChangeRequestLabel(RawEntityChange change, bool created, bool removed)
    {
        if (created)
        {
            var type = Find(change, "ChangeRequestType");
            if (type != null && TryEnum<ChangeRequestType>(Norm(type.NewValue), out var kind))
            {
                return kind == ChangeRequestType.Cancel ? "Cancellation requested" : "Reschedule requested";
            }
            return "Change requested";
        }
        if (removed)
        {
            return "Change request removed";
        }
        var request = Find(change, "RequestStatus");
        if (request != null && TryEnum<RequestStatusType>(Norm(request.NewValue), out var decided))
        {
            if (decided == RequestStatusType.Accepted)
            {
                return "Change request approved";
            }
            if (decided == RequestStatusType.Rejected)
            {
                return "Change request rejected";
            }
        }
        return ConsentLabel(change) ?? "Change request updated";
    }

    private static string ConsentRoundLabel(RawEntityChange change, bool created, bool removed)
    {
        if (created)
        {
            return "Date set";
        }
        if (removed)
        {
            return "Consent round removed";
        }
        var consent = ConsentLabel(change);
        if (consent != null)
        {
            return consent;
        }
        return !string.IsNullOrEmpty(Norm(Find(change, "SupersededAt")?.NewValue))
            ? "Date superseded"
            : "Consent round updated";
    }

    /// <summary>"Consent agreed (side A)", "Consent declined (both sides)", ... or null when no consent column moved.</summary>
    private static string? ConsentLabel(RawEntityChange change)
    {
        var moves = new List<(string Side, ChangeRequestConsentStatus Status)>();
        foreach (var (name, side) in new[] { ("SideAConsentStatus", "A"), ("SideBConsentStatus", "B") })
        {
            var prop = Find(change, name);
            if (prop != null && TryEnum<ChangeRequestConsentStatus>(Norm(prop.NewValue), out var status))
            {
                moves.Add((side, status));
            }
        }
        if (moves.Count == 0)
        {
            return null;
        }
        var first = moves[0].Status;
        var verb = first switch
        {
            ChangeRequestConsentStatus.Approved => "Consent agreed",
            ChangeRequestConsentStatus.Rejected => "Consent declined",
            ChangeRequestConsentStatus.Expired => "Consent expired",
            ChangeRequestConsentStatus.Pending => "Consent requested",
            _ => "Consent updated",
        };
        var sides = moves.Count > 1 && moves.All(m => m.Status == first)
            ? "both sides"
            : "side " + moves[0].Side;
        return $"{verb} ({sides})";
    }

    private static string DocumentLabel(RawEntityChange change, bool created, bool removed)
    {
        if (created)
        {
            return "Document uploaded";
        }
        if (removed)
        {
            return "Document removed";
        }
        // A replace swaps the stored file (and resets the status), so it is checked before the status.
        if (change.Properties.Any(p => p.PropertyName is "BlobName" or "FileName" or "FileSize"))
        {
            return "Document replaced";
        }
        var status = Find(change, "Status");
        if (status != null && TryEnum<DocumentStatus>(Norm(status.NewValue), out var next))
        {
            if (next == DocumentStatus.Accepted)
            {
                return "Document accepted";
            }
            if (next == DocumentStatus.Rejected)
            {
                return "Document rejected";
            }
        }
        return "Document updated";
    }

    private static string InfoRequestLabel(RawEntityChange change, bool created, bool removed)
    {
        if (created)
        {
            return "Information requested";
        }
        if (removed)
        {
            return "Information request removed";
        }
        var status = Find(change, "Status");
        return status != null
            && TryEnum<InfoRequestStatus>(Norm(status.NewValue), out var next)
            && next == InfoRequestStatus.Resolved
                ? "Information answered"
                : "Information request updated";
    }

    private static RawPropertyChange? Find(RawEntityChange change, string name)
        => change.Properties.FirstOrDefault(p => string.Equals(p.PropertyName, name, StringComparison.OrdinalIgnoreCase));

    private static string? NewValue(RawEntityChange change, string name) => Norm(Find(change, name)?.NewValue);

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static bool IsKind(RawEntityChange change, string kind)
        => string.Equals(change.ChangeType, kind, StringComparison.OrdinalIgnoreCase);

    private static string SimpleName(string fullName)
    {
        var dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName[(dot + 1)..] : fullName;
    }

    /// <summary>ABP stores values as JSON text, so strings arrive quoted; trim quotes and spaces.</summary>
    private static string? Norm(string? value) => value?.Trim().Trim('"');

    private static bool TryEnum<T>(string? value, out T result) where T : struct, Enum
    {
        result = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            if (!Enum.IsDefined(typeof(T), number))
            {
                return false;
            }
            result = (T)Enum.ToObject(typeof(T), number);
            return true;
        }
        return Enum.TryParse(value, ignoreCase: true, out result);
    }
}
