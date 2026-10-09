using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Phase E (2026-06-25) -- per-office branding (display name + logo), stored in
/// the HOST/management database keyed by office (Volo SaaS tenant) id. Host-only
/// (mapped inside the IsHostDatabase block, never IMultiTenant) so the login page
/// and the host-side central manager can resolve an office's brand pre-auth and
/// without switching into the office database. One row per office (unique index
/// on <see cref="OfficeId"/>).
///
/// <para>The logo image bytes live in
/// <see cref="HealthcareSupport.CaseEvaluation.BlobContainers.OfficeLogosContainer"/>
/// (MinIO), accessed at host scope and keyed by the same office id; this row
/// stores only the blob reference + its content type.</para>
/// </summary>
public class OfficeBranding : FullAuditedAggregateRoot<Guid>
{
    public const int DisplayNameMaxLength = 128;
    public const int LogoBlobNameMaxLength = 256;
    public const int LogoContentTypeMaxLength = 100;

    /// <summary>The office (Volo SaaS tenant) id this branding belongs to.</summary>
    public Guid OfficeId { get; private set; }

    /// <summary>Office display name shown in the shell + browser title; null = fall back to the default.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>Blob key of the uploaded logo in the office-logos container; null = no custom logo.</summary>
    public string? LogoBlobName { get; private set; }

    /// <summary>MIME type of the stored logo (image/png or image/jpeg); null when no logo.</summary>
    public string? LogoContentType { get; private set; }

    protected OfficeBranding()
    {
    }

    public OfficeBranding(Guid id, Guid officeId)
        : base(id)
    {
        OfficeId = officeId;
    }

    /// <summary>Sets (or clears, when null/blank) the office display name.</summary>
    public void SetDisplayName(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            DisplayName = null;
            return;
        }

        Check.Length(trimmed, nameof(displayName), DisplayNameMaxLength);
        DisplayName = trimmed;
    }

    /// <summary>Records the uploaded logo's blob key + content type.</summary>
    public void SetLogo(string blobName, string contentType)
    {
        LogoBlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName), LogoBlobNameMaxLength);
        LogoContentType = Check.NotNullOrWhiteSpace(contentType, nameof(contentType), LogoContentTypeMaxLength);
    }

    /// <summary>Removes the logo reference (the orphaned blob is a cleanup concern).</summary>
    public void ClearLogo()
    {
        LogoBlobName = null;
        LogoContentType = null;
    }

    // -- Packet letterhead (2026-10-09, walkthrough Q5) --------------------------------------
    //
    // What the generated packets print as this office's identity: letterhead, physician name,
    // practice name, mailing address, phone, fax, the two records addresses and the missed-
    // appointment charge. Until this existed every office's packets carried one practice's
    // letterhead, hardcoded in tools/packet-templates. Every field is OPTIONAL here: a null
    // field falls back to a value derived from the office's doctor at render time (see
    // OfficeLetterheadResolver), so a newly created practice has a letterhead without anyone
    // filling this in. Kept on the host branding row beside the logo because both describe
    // how the office presents itself, and the practice-creation flow already writes this row.

    /// <summary>Letterhead heading, e.g. "Jane Doe, M.D., FAAOS"; null = the physician name.</summary>
    public string? LetterheadName { get; private set; }

    /// <summary>Optional line under the letterhead heading (credentials, fellowship).</summary>
    public string? LetterheadTagline { get; private set; }

    /// <summary>The physician as named in letter text and forms; null = "Dr. {First} {Last}".</summary>
    public string? PhysicianName { get; private set; }

    /// <summary>The practice name; null = the display name.</summary>
    public string? PracticeName { get; private set; }

    public string? MailingStreet { get; private set; }

    public string? MailingCity { get; private set; }

    public string? MailingState { get; private set; }

    public string? MailingZip { get; private set; }

    public string? Phone { get; private set; }

    public string? Fax { get; private set; }

    /// <summary>Single-line address medical records are physically delivered to (attorney notice).</summary>
    public string? RecordsDeliveryAddress { get; private set; }

    /// <summary>Multi-line address on the patient's release-of-records form (one line per row).</summary>
    public string? RecordsReleaseAddress { get; private set; }

    /// <summary>Missed-appointment charge quoted to attorneys; null = the sentence is omitted.</summary>
    public decimal? MissedAppointmentFee { get; private set; }

    /// <summary>
    /// Replaces the whole letterhead. Blank strings are stored as null so "cleared" and
    /// "never set" mean the same thing: fall back to the derived default.
    /// </summary>
    public void SetLetterhead(OfficeLetterheadValues values)
    {
        Check.NotNull(values, nameof(values));

        LetterheadName = Normalize(values.LetterheadName, nameof(values.LetterheadName), OfficeLetterheadConsts.NameMaxLength);
        LetterheadTagline = Normalize(values.LetterheadTagline, nameof(values.LetterheadTagline), OfficeLetterheadConsts.NameMaxLength);
        PhysicianName = Normalize(values.PhysicianName, nameof(values.PhysicianName), OfficeLetterheadConsts.NameMaxLength);
        PracticeName = Normalize(values.PracticeName, nameof(values.PracticeName), OfficeLetterheadConsts.NameMaxLength);
        MailingStreet = Normalize(values.MailingStreet, nameof(values.MailingStreet), OfficeLetterheadConsts.StreetMaxLength);
        MailingCity = Normalize(values.MailingCity, nameof(values.MailingCity), OfficeLetterheadConsts.CityMaxLength);
        MailingState = Normalize(values.MailingState, nameof(values.MailingState), OfficeLetterheadConsts.StateMaxLength);
        MailingZip = Normalize(values.MailingZip, nameof(values.MailingZip), OfficeLetterheadConsts.ZipMaxLength);
        Phone = Normalize(values.Phone, nameof(values.Phone), OfficeLetterheadConsts.PhoneMaxLength);
        Fax = Normalize(values.Fax, nameof(values.Fax), OfficeLetterheadConsts.PhoneMaxLength);
        RecordsDeliveryAddress = Normalize(values.RecordsDeliveryAddress, nameof(values.RecordsDeliveryAddress), OfficeLetterheadConsts.AddressMaxLength);
        RecordsReleaseAddress = NormalizeLines(values.RecordsReleaseAddress, nameof(values.RecordsReleaseAddress));

        if (values.MissedAppointmentFee is < 0)
        {
            throw new ArgumentException("The missed-appointment fee cannot be negative.", nameof(values));
        }

        MissedAppointmentFee = values.MissedAppointmentFee;
    }

    /// <summary>The stored letterhead fields as entered (nulls where nothing is set).</summary>
    public OfficeLetterheadValues GetLetterhead()
    {
        return new OfficeLetterheadValues
        {
            LetterheadName = LetterheadName,
            LetterheadTagline = LetterheadTagline,
            PhysicianName = PhysicianName,
            PracticeName = PracticeName,
            MailingStreet = MailingStreet,
            MailingCity = MailingCity,
            MailingState = MailingState,
            MailingZip = MailingZip,
            Phone = Phone,
            Fax = Fax,
            RecordsDeliveryAddress = RecordsDeliveryAddress,
            RecordsReleaseAddress = RecordsReleaseAddress,
            MissedAppointmentFee = MissedAppointmentFee,
        };
    }

    private static string? Normalize(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        Check.Length(trimmed, name, maxLength);
        return trimmed;
    }

    // Trims each line, drops blank ones and joins with "\n" -- the stored form the release
    // form renders one row per line from, whatever line endings the browser sent.
    private static string? NormalizeLines(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var lines = value
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
        return Normalize(string.Join("\n", lines), name, OfficeLetterheadConsts.AddressMaxLength);
    }
}
