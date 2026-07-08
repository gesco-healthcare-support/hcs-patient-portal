using System.Globalization;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Editor type for a flaggable field. Drives server-side parsing of the raw
/// correction string and the fix-it page's editor widget (mirrored client-side in
/// send-back-fields.ts). Selects (StateId / LanguageId / Gender) carry the id/enum
/// as a string and are parsed back here.
/// </summary>
internal enum InfoRequestFieldKind
{
    Text,
    Email,
    Phone,
    Zip,
    Ssn,
    Date,
    StateId,
    LanguageId,
    Gender,
}

/// <summary>The entity a flaggable field's value lives on.</summary>
internal enum InfoRequestFieldOwner
{
    Patient,
    Appointment,
    Employer,
    Insurance,
    ClaimExaminer,
}

/// <summary>
/// The loaded (or newly created) entities a single correction round may read and
/// write. <see cref="Appointment"/> is always present; the linked rows are loaded
/// only when a flagged key targets them and may be created if absent.
/// </summary>
internal sealed class CorrectionBundle
{
    public Patient? Patient { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public AppointmentEmployerDetail? Employer { get; set; }
    public AppointmentPrimaryInsurance? Insurance { get; set; }
    public AppointmentClaimExaminer? ClaimExaminer { get; set; }

    public bool EmployerIsNew { get; set; }
    public bool InsuranceIsNew { get; set; }
    public bool ClaimExaminerIsNew { get; set; }
}

/// <summary>
/// One flaggable field's metadata + its read/write binding to an entity. <see cref="Read"/>
/// returns the current value as a display string (SSN masked, date formatted, ids as
/// their Guid/enum string) -- null/empty means "not set", which the resubmit gate treats
/// as unresolved. <see cref="Write"/> parses the raw correction string per Kind and
/// assigns it to the owning entity (assumed present in the bundle).
/// </summary>
internal sealed class InfoRequestFieldSpec
{
    public required string Key { get; init; }
    public required InfoRequestFieldOwner Owner { get; init; }
    public required InfoRequestFieldKind Kind { get; init; }
    public required Func<CorrectionBundle, string?> Read { get; init; }
    public required Action<CorrectionBundle, string?> Write { get; init; }
}

/// <summary>
/// The single source of truth for the 62 scalar flaggable appointment fields the
/// send-back / fix-it flow can correct (QA item L, 2026-06-30). Replaces the prior
/// hand-maintained per-field properties spread across the input DTO, the correction
/// lock, the snapshot, and the resubmit gate. Pure (no DI) so the mapping is unit-tested
/// directly. The two non-scalar flaggable keys are handled outside this registry:
/// <c>documents</c> (resolved by document count) and -- by product decision -- the
/// repeating <c>claimInformation</c> collection is not a send-back field this pass.
/// Field keys are the booking-form control names; they must equal the frontend
/// send-back-fields registry keys so the two never drift.
/// </summary>
internal static class InfoRequestFields
{
    public static readonly IReadOnlyList<InfoRequestFieldSpec> All = BuildAll();

    public static readonly IReadOnlyDictionary<string, InfoRequestFieldSpec> ByKey =
        All.ToDictionary(s => s.Key, StringComparer.Ordinal);

    /// <summary>Scalar keys in registry order; drives the staff before/after diff ordering.</summary>
    public static readonly IReadOnlyList<string> ScalarKeysInOrder = All.Select(s => s.Key).ToList();

    private static List<InfoRequestFieldSpec> BuildAll()
    {
        var specs = new List<InfoRequestFieldSpec>();

        // ---- Patient Demographics (15; needsInterpreter dropped -- no DB home) ----
        specs.Add(P("firstName", InfoRequestFieldKind.Text, p => p.FirstName, (p, v) => p.FirstName = v ?? string.Empty));
        specs.Add(P("middleName", InfoRequestFieldKind.Text, p => p.MiddleName, (p, v) => p.MiddleName = v));
        specs.Add(P("lastName", InfoRequestFieldKind.Text, p => p.LastName, (p, v) => p.LastName = v ?? string.Empty));
        specs.Add(P("genderId", InfoRequestFieldKind.Gender, p => p.GenderId.ToString(), (p, v) => { if (Enum.TryParse<Gender>(v, out var g)) p.GenderId = g; }));
        specs.Add(P("dateOfBirth", InfoRequestFieldKind.Date, p => FmtDate(p.DateOfBirth), (p, v) => { if (ParseDate(v, out var d)) p.DateOfBirth = d; }));
        specs.Add(P("email", InfoRequestFieldKind.Email, p => p.Email, (p, v) => p.Email = v ?? string.Empty));
        specs.Add(P("cellPhoneNumber", InfoRequestFieldKind.Phone, p => p.CellPhoneNumber, (p, v) => p.CellPhoneNumber = v));
        specs.Add(P("phoneNumber", InfoRequestFieldKind.Phone, p => p.PhoneNumber, (p, v) => p.PhoneNumber = v));
        specs.Add(P("socialSecurityNumber", InfoRequestFieldKind.Ssn, p => MaskSsn(p.SocialSecurityNumber), (p, v) => p.SocialSecurityNumber = v));
        specs.Add(P("street", InfoRequestFieldKind.Text, p => p.Street, (p, v) => p.Street = v));
        specs.Add(P("city", InfoRequestFieldKind.Text, p => p.City, (p, v) => p.City = v));
        specs.Add(P("stateId", InfoRequestFieldKind.StateId, p => p.StateId?.ToString(), (p, v) => { if (ParseGuid(v) is Guid g) p.StateId = g; }));
        specs.Add(P("zipCode", InfoRequestFieldKind.Zip, p => p.ZipCode, (p, v) => p.ZipCode = v));
        specs.Add(P("appointmentLanguageId", InfoRequestFieldKind.LanguageId, p => p.AppointmentLanguageId?.ToString(), (p, v) => { if (ParseGuid(v) is Guid g) p.AppointmentLanguageId = g; }));
        specs.Add(P("interpreterVendorName", InfoRequestFieldKind.Text, p => p.InterpreterVendorName, (p, v) => p.InterpreterVendorName = v));

        // ---- Appointment-level: Referred By + attorney snapshot columns ----
        specs.Add(A("refferedBy", InfoRequestFieldKind.Text, a => a.RefferedBy, (a, v) => a.RefferedBy = v));

        // Applicant Attorney (11) -- snapshot columns on Appointment.
        specs.Add(A("applicantAttorneyFirstName", InfoRequestFieldKind.Text, a => a.ApplicantAttorneyFirstName, (a, v) => a.ApplicantAttorneyFirstName = v));
        specs.Add(A("applicantAttorneyLastName", InfoRequestFieldKind.Text, a => a.ApplicantAttorneyLastName, (a, v) => a.ApplicantAttorneyLastName = v));
        specs.Add(A("applicantAttorneyEmail", InfoRequestFieldKind.Email, a => a.ApplicantAttorneyEmail, (a, v) => a.ApplicantAttorneyEmail = v));
        specs.Add(A("applicantAttorneyFirmName", InfoRequestFieldKind.Text, a => a.ApplicantAttorneyFirmName, (a, v) => a.ApplicantAttorneyFirmName = v));
        specs.Add(A("applicantAttorneyWebAddress", InfoRequestFieldKind.Text, a => a.ApplicantAttorneyWebAddress, (a, v) => a.ApplicantAttorneyWebAddress = v));
        specs.Add(A("applicantAttorneyPhoneNumber", InfoRequestFieldKind.Phone, a => a.ApplicantAttorneyPhoneNumber, (a, v) => a.ApplicantAttorneyPhoneNumber = v));
        specs.Add(A("applicantAttorneyFaxNumber", InfoRequestFieldKind.Phone, a => a.ApplicantAttorneyFaxNumber, (a, v) => a.ApplicantAttorneyFaxNumber = v));
        specs.Add(A("applicantAttorneyStreet", InfoRequestFieldKind.Text, a => a.ApplicantAttorneyStreet, (a, v) => a.ApplicantAttorneyStreet = v));
        specs.Add(A("applicantAttorneyCity", InfoRequestFieldKind.Text, a => a.ApplicantAttorneyCity, (a, v) => a.ApplicantAttorneyCity = v));
        specs.Add(A("applicantAttorneyStateId", InfoRequestFieldKind.StateId, a => a.ApplicantAttorneyStateId?.ToString(), (a, v) => { if (ParseGuid(v) is Guid g) a.ApplicantAttorneyStateId = g; }));
        specs.Add(A("applicantAttorneyZipCode", InfoRequestFieldKind.Zip, a => a.ApplicantAttorneyZipCode, (a, v) => a.ApplicantAttorneyZipCode = v));

        // Defense Attorney (11) -- snapshot columns on Appointment.
        specs.Add(A("defenseAttorneyFirstName", InfoRequestFieldKind.Text, a => a.DefenseAttorneyFirstName, (a, v) => a.DefenseAttorneyFirstName = v));
        specs.Add(A("defenseAttorneyLastName", InfoRequestFieldKind.Text, a => a.DefenseAttorneyLastName, (a, v) => a.DefenseAttorneyLastName = v));
        specs.Add(A("defenseAttorneyEmail", InfoRequestFieldKind.Email, a => a.DefenseAttorneyEmail, (a, v) => a.DefenseAttorneyEmail = v));
        specs.Add(A("defenseAttorneyFirmName", InfoRequestFieldKind.Text, a => a.DefenseAttorneyFirmName, (a, v) => a.DefenseAttorneyFirmName = v));
        specs.Add(A("defenseAttorneyWebAddress", InfoRequestFieldKind.Text, a => a.DefenseAttorneyWebAddress, (a, v) => a.DefenseAttorneyWebAddress = v));
        specs.Add(A("defenseAttorneyPhoneNumber", InfoRequestFieldKind.Phone, a => a.DefenseAttorneyPhoneNumber, (a, v) => a.DefenseAttorneyPhoneNumber = v));
        specs.Add(A("defenseAttorneyFaxNumber", InfoRequestFieldKind.Phone, a => a.DefenseAttorneyFaxNumber, (a, v) => a.DefenseAttorneyFaxNumber = v));
        specs.Add(A("defenseAttorneyStreet", InfoRequestFieldKind.Text, a => a.DefenseAttorneyStreet, (a, v) => a.DefenseAttorneyStreet = v));
        specs.Add(A("defenseAttorneyCity", InfoRequestFieldKind.Text, a => a.DefenseAttorneyCity, (a, v) => a.DefenseAttorneyCity = v));
        specs.Add(A("defenseAttorneyStateId", InfoRequestFieldKind.StateId, a => a.DefenseAttorneyStateId?.ToString(), (a, v) => { if (ParseGuid(v) is Guid g) a.DefenseAttorneyStateId = g; }));
        specs.Add(A("defenseAttorneyZipCode", InfoRequestFieldKind.Zip, a => a.DefenseAttorneyZipCode, (a, v) => a.DefenseAttorneyZipCode = v));

        // ---- Employer Details (7) -- AppointmentEmployerDetail ----
        specs.Add(E("employerName", InfoRequestFieldKind.Text, e => e.EmployerName, (e, v) => e.EmployerName = v ?? string.Empty));
        specs.Add(E("employerOccupation", InfoRequestFieldKind.Text, e => e.Occupation, (e, v) => e.Occupation = v ?? string.Empty));
        specs.Add(E("employerPhoneNumber", InfoRequestFieldKind.Phone, e => e.PhoneNumber, (e, v) => e.PhoneNumber = v));
        specs.Add(E("employerStreet", InfoRequestFieldKind.Text, e => e.Street, (e, v) => e.Street = v));
        specs.Add(E("employerCity", InfoRequestFieldKind.Text, e => e.City, (e, v) => e.City = v));
        specs.Add(E("employerStateId", InfoRequestFieldKind.StateId, e => e.StateId?.ToString(), (e, v) => { if (ParseGuid(v) is Guid g) e.StateId = g; }));
        specs.Add(E("employerZipCode", InfoRequestFieldKind.Zip, e => e.ZipCode, (e, v) => e.ZipCode = v));

        // ---- Insurance Carrier (8) -- AppointmentPrimaryInsurance ----
        specs.Add(I("appointmentInsuranceName", InfoRequestFieldKind.Text, x => x.Name, (x, v) => x.Name = v));
        specs.Add(I("appointmentInsuranceStreet", InfoRequestFieldKind.Text, x => x.Street, (x, v) => x.Street = v));
        specs.Add(I("appointmentInsuranceSuite", InfoRequestFieldKind.Text, x => x.Suite, (x, v) => x.Suite = v));
        specs.Add(I("appointmentInsurancePhoneNumber", InfoRequestFieldKind.Phone, x => x.PhoneNumber, (x, v) => x.PhoneNumber = v));
        specs.Add(I("appointmentInsuranceFaxNumber", InfoRequestFieldKind.Phone, x => x.FaxNumber, (x, v) => x.FaxNumber = v));
        specs.Add(I("appointmentInsuranceCity", InfoRequestFieldKind.Text, x => x.City, (x, v) => x.City = v));
        specs.Add(I("appointmentInsuranceStateId", InfoRequestFieldKind.StateId, x => x.StateId?.ToString(), (x, v) => { if (ParseGuid(v) is Guid g) x.StateId = g; }));
        specs.Add(I("appointmentInsuranceZip", InfoRequestFieldKind.Zip, x => x.Zip, (x, v) => x.Zip = v));

        // ---- Claim Examiner (9) -- AppointmentClaimExaminer ----
        specs.Add(C("appointmentClaimExaminerName", InfoRequestFieldKind.Text, c => c.Name, (c, v) => c.Name = v));
        specs.Add(C("appointmentClaimExaminerEmail", InfoRequestFieldKind.Email, c => c.Email, (c, v) => c.Email = v));
        specs.Add(C("appointmentClaimExaminerStreet", InfoRequestFieldKind.Text, c => c.Street, (c, v) => c.Street = v));
        specs.Add(C("appointmentClaimExaminerSuite", InfoRequestFieldKind.Text, c => c.Suite, (c, v) => c.Suite = v));
        specs.Add(C("appointmentClaimExaminerPhoneNumber", InfoRequestFieldKind.Phone, c => c.PhoneNumber, (c, v) => c.PhoneNumber = v));
        specs.Add(C("appointmentClaimExaminerFax", InfoRequestFieldKind.Phone, c => c.Fax, (c, v) => c.Fax = v));
        specs.Add(C("appointmentClaimExaminerCity", InfoRequestFieldKind.Text, c => c.City, (c, v) => c.City = v));
        specs.Add(C("appointmentClaimExaminerStateId", InfoRequestFieldKind.StateId, c => c.StateId?.ToString(), (c, v) => { if (ParseGuid(v) is Guid g) c.StateId = g; }));
        specs.Add(C("appointmentClaimExaminerZip", InfoRequestFieldKind.Zip, c => c.Zip, (c, v) => c.Zip = v));

        return specs;
    }

    private static InfoRequestFieldSpec P(string key, InfoRequestFieldKind kind, Func<Patient, string?> read, Action<Patient, string?> write)
        => new()
        {
            Key = key,
            Owner = InfoRequestFieldOwner.Patient,
            Kind = kind,
            Read = b => b.Patient == null ? null : read(b.Patient),
            Write = (b, v) => { if (b.Patient != null) { write(b.Patient, v); } },
        };

    private static InfoRequestFieldSpec A(string key, InfoRequestFieldKind kind, Func<Appointment, string?> read, Action<Appointment, string?> write)
        => new()
        {
            Key = key,
            Owner = InfoRequestFieldOwner.Appointment,
            Kind = kind,
            Read = b => read(b.Appointment),
            Write = (b, v) => write(b.Appointment, v),
        };

    private static InfoRequestFieldSpec E(string key, InfoRequestFieldKind kind, Func<AppointmentEmployerDetail, string?> read, Action<AppointmentEmployerDetail, string?> write)
        => new()
        {
            Key = key,
            Owner = InfoRequestFieldOwner.Employer,
            Kind = kind,
            Read = b => b.Employer == null ? null : read(b.Employer),
            Write = (b, v) => { if (b.Employer != null) { write(b.Employer, v); } },
        };

    private static InfoRequestFieldSpec I(string key, InfoRequestFieldKind kind, Func<AppointmentPrimaryInsurance, string?> read, Action<AppointmentPrimaryInsurance, string?> write)
        => new()
        {
            Key = key,
            Owner = InfoRequestFieldOwner.Insurance,
            Kind = kind,
            Read = b => b.Insurance == null ? null : read(b.Insurance),
            Write = (b, v) => { if (b.Insurance != null) { write(b.Insurance, v); } },
        };

    private static InfoRequestFieldSpec C(string key, InfoRequestFieldKind kind, Func<AppointmentClaimExaminer, string?> read, Action<AppointmentClaimExaminer, string?> write)
        => new()
        {
            Key = key,
            Owner = InfoRequestFieldOwner.ClaimExaminer,
            Kind = kind,
            Read = b => b.ClaimExaminer == null ? null : read(b.ClaimExaminer),
            Write = (b, v) => { if (b.ClaimExaminer != null) { write(b.ClaimExaminer, v); } },
        };

    private static string? FmtDate(DateTime value)
        => value == default ? null : value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

    private static bool ParseDate(string? raw, out DateTime value)
        => DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    private static Guid? ParseGuid(string? raw)
        => Guid.TryParse(raw, out var g) ? g : null;

    /// <summary>
    /// Masks a raw SSN to its last four digits ("***-**-1234"); "" for null/empty. The
    /// snapshot stores only this form so a second raw SSN copy never lands in the
    /// AppointmentInfoRequest table (HIPAA).
    /// </summary>
    internal static string MaskSsn(string? ssn)
    {
        if (string.IsNullOrWhiteSpace(ssn))
        {
            return string.Empty;
        }
        var digits = new string(ssn.Where(char.IsDigit).ToArray());
        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
        return "***-**-" + last4;
    }
}
