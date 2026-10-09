using System.Globalization;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Practices;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// An office's EFFECTIVE packet letterhead: the stored values from
/// <see cref="OfficeBranding"/> with every gap filled, ready to print. Strings are never
/// null; an optional field nobody filled in is the empty string, and the packet templates
/// hide the line that would have carried it.
///
/// <para>The defaults are what makes a new practice print a correct letterhead with no setup:
/// the physician is "Dr. {First} {Last}" from the doctor entered on the New Practice form
/// (the same rule as the default display name, <see cref="PracticeNaming"/>), the letterhead
/// heading is the physician, and the practice name is the office display name.</para>
/// </summary>
public sealed record OfficeLetterhead
{
    /// <summary>Separator between phone and fax on a single footer line.</summary>
    public const string PhoneFaxSeparator = " • ";

    public string LetterheadName { get; init; } = string.Empty;
    public string LetterheadTagline { get; init; } = string.Empty;
    public string PhysicianName { get; init; } = string.Empty;
    public string PracticeName { get; init; } = string.Empty;
    public string MailingStreet { get; init; } = string.Empty;
    public string MailingCity { get; init; } = string.Empty;
    public string MailingState { get; init; } = string.Empty;
    public string MailingZip { get; init; } = string.Empty;

    /// <summary>One line, "{Street}, {City}, {State} {Zip}", skipping whatever is blank.</summary>
    public string MailingAddress { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;
    public string Fax { get; init; } = string.Empty;

    /// <summary>"Phone: {Phone} * Fax: {Fax}" for one-line footers, skipping whatever is blank.</summary>
    public string PhoneFax { get; init; } = string.Empty;

    public string RecordsDeliveryAddress { get; init; } = string.Empty;

    /// <summary>One address row per line, separated by "\n".</summary>
    public string RecordsReleaseAddress { get; init; } = string.Empty;

    /// <summary>The fee as printed after a "$" ("503.75"), or empty when the office set none.</summary>
    public string MissedAppointmentFee { get; init; } = string.Empty;

    /// <summary>
    /// Fills the gaps in the stored <paramref name="values"/> (null when the office has no
    /// branding row: one created before branding existed) from the office display name and
    /// its doctor. Pass empty values to get the pure defaults the editor shows.
    /// </summary>
    public static OfficeLetterhead Compose(
        OfficeLetterheadValues? values,
        string? displayName,
        string? doctorFirstName,
        string? doctorLastName)
    {
        values ??= new OfficeLetterheadValues();
        var physician = FirstNonBlank(
            values.PhysicianName,
            DefaultPhysicianName(doctorFirstName, doctorLastName),
            displayName);
        var street = Text(values.MailingStreet);
        var city = Text(values.MailingCity);
        var state = Text(values.MailingState);
        var zip = Text(values.MailingZip);
        var phone = Text(values.Phone);
        var fax = Text(values.Fax);

        return new OfficeLetterhead
        {
            LetterheadName = FirstNonBlank(values.LetterheadName, physician),
            LetterheadTagline = Text(values.LetterheadTagline),
            PhysicianName = physician,
            PracticeName = FirstNonBlank(values.PracticeName, displayName, physician),
            MailingStreet = street,
            MailingCity = city,
            MailingState = state,
            MailingZip = zip,
            MailingAddress = JoinNonBlank(", ", street, JoinNonBlank(", ", city, JoinNonBlank(" ", state, zip))),
            Phone = phone,
            Fax = fax,
            PhoneFax = JoinNonBlank(
                PhoneFaxSeparator,
                phone.Length == 0 ? null : "Phone: " + phone,
                fax.Length == 0 ? null : "Fax: " + fax),
            RecordsDeliveryAddress = Text(values.RecordsDeliveryAddress),
            RecordsReleaseAddress = Text(values.RecordsReleaseAddress),
            MissedAppointmentFee = values.MissedAppointmentFee?.ToString("#,0.00", CultureInfo.InvariantCulture)
                ?? string.Empty,
        };
    }

    private static string Text(string? value) => value?.Trim() ?? string.Empty;

    /// <summary>
    /// "Dr. {First} {Last}", or null when the office has no doctor names at all -- deliberately
    /// NOT the bare "Dr." <see cref="PracticeNaming.DefaultDisplayName"/> returns in that case,
    /// which would print as a physician named "Dr.".
    /// </summary>
    public static string? DefaultPhysicianName(string? firstName, string? lastName)
    {
        return string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName)
            ? null
            : PracticeNaming.DefaultDisplayName(firstName, lastName);
    }

    private static string FirstNonBlank(params string?[] candidates)
    {
        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim() ?? string.Empty;
    }

    private static string JoinNonBlank(string separator, params string?[] parts)
    {
        return string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
