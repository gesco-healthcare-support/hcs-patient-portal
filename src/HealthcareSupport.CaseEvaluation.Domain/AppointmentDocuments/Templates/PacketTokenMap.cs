using System.Text.RegularExpressions;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// Format-agnostic token plumbing for the packet renderer: the
/// <c>##Group.Field## -&gt; value</c> map built from a <see cref="PacketTokenContext"/>, the
/// regex that recognizes a token, and the signature-placeholder constant (HTML packets carry
/// no signature token -- their signatures are blank fillable / handwritten fields).
///
/// <para>This is the single source of truth for token values: all uppercasing / date /
/// multi-row formatting is done upstream by the <see cref="IPacketTokenResolver"/>, and the
/// renderer reads the result through this one map.</para>
/// </summary>
public static class PacketTokenMap
{
    /// <summary>
    /// Matches <c>##Group.Field##</c> (group + field each start with a letter, then letters /
    /// digits / underscores). Compiled because both renderers run it over large documents.
    /// </summary>
    public static readonly Regex TokenRegex = new(
        @"##[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*##",
        RegexOptions.Compiled);

    /// <summary>
    /// The signature placeholder is intentionally excluded from <see cref="Build"/>: the DOCX
    /// renderer replaces it with an inline image (or clears it when no signature is on file).
    /// HTML templates never use it.
    /// </summary>
    public const string SignaturePlaceholder = "##Appointments.Signature##";

    /// <summary>
    /// Builds the <c>##Group.Field## -&gt; value</c> map from a resolved context. The mapping
    /// mirrors the OLD token names to <see cref="PacketTokenContext"/> property names recorded
    /// in the audit doc Section 5. Values are already final render strings (uppercased /
    /// formatted / null-collapsed by the resolver); callers substitute them verbatim.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Build(PacketTokenContext c)
    {
        return new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            // Patients group
            ["##Patients.FirstName##"] = c.PatientFirstName,
            ["##Patients.LastName##"] = c.PatientLastName,
            ["##Patients.MiddleName##"] = c.PatientMiddleName,
            ["##Patients.DateOfBirth##"] = c.PatientDateOfBirth,
            ["##Patients.SocialSecurityNumber##"] = c.PatientSocialSecurityNumber,
            ["##Patients.Street##"] = c.PatientStreet,
            ["##Patients.City##"] = c.PatientCity,
            ["##Patients.State##"] = c.PatientState,
            ["##Patients.ZipCode##"] = c.PatientZipCode,
            ["##Patients.PhoneNumber##"] = c.PatientPhoneNumber,
            // Interpreter pair: NEW tokens (PQME QME form only); derived by the resolver from
            // the patient's language + interpreter-vendor fields (no OLD parity -- OLD had no
            // token for these). InterpreterRequired is "Yes"/"No"; InterpreterLanguage is the
            // (uppercased) non-English language, blank when no interpreter is needed.
            ["##Patients.InterpreterRequired##"] = c.PatientInterpreterRequired,
            ["##Patients.InterpreterLanguage##"] = c.PatientInterpreterLanguage,

            // Appointments group (Signature is intentionally omitted -- handled by StampSignature)
            ["##Appointments.RequestConfirmationNumber##"] = c.RequestConfirmationNumber,
            ["##Appointments.AvailableDate##"] = c.AvailableDate,
            ["##Appointments.AppointmenTime##"] = c.AppointmentTime,    // typo preserved verbatim from OLD
            ["##Appointments.AppointmentType##"] = c.AppointmentType,
            ["##Appointments.Location##"] = c.LocationName,
            ["##Appointments.LocationAddress##"] = c.LocationAddress,
            ["##Appointments.LocationCity##"] = c.LocationCity,
            ["##Appointments.LocationState##"] = c.LocationState,
            ["##Appointments.LocationZipCode##"] = c.LocationZipCode,
            ["##Appointments.LocationParkingFee##"] = c.LocationParkingFee,
            ["##Appointments.PrimaryResponsibleUserName##"] = c.PrimaryResponsibleUserName,
            ["##Appointments.AppointmentCreatedDate##"] = c.AppointmentCreatedDate,
            ["##Appointments.PanelNumber##"] = c.PanelNumber,

            // EmployerDetails group
            ["##EmployerDetails.EmployerName##"] = c.EmployerName,
            ["##EmployerDetails.Street##"] = c.EmployerStreet,
            ["##EmployerDetails.City##"] = c.EmployerCity,
            ["##EmployerDetails.State##"] = c.EmployerState,
            ["##EmployerDetails.Zip##"] = c.EmployerZip,

            // PatientAttorneys group
            ["##PatientAttorneys.AttorneyName##"] = c.PatientAttorneyName,
            ["##PatientAttorneys.Street##"] = c.PatientAttorneyStreet,
            ["##PatientAttorneys.City##"] = c.PatientAttorneyCity,
            ["##PatientAttorneys.State##"] = c.PatientAttorneyState,
            ["##PatientAttorneys.Zip##"] = c.PatientAttorneyZip,

            // DefenseAttorneys group
            ["##DefenseAttorneys.AttorneyName##"] = c.DefenseAttorneyName,
            ["##DefenseAttorneys.Street##"] = c.DefenseAttorneyStreet,
            ["##DefenseAttorneys.City##"] = c.DefenseAttorneyCity,
            ["##DefenseAttorneys.State##"] = c.DefenseAttorneyState,
            ["##DefenseAttorneys.Zip##"] = c.DefenseAttorneyZip,

            // InjuryDetails group (multi-row space-concatenated by the resolver)
            ["##InjuryDetails.ClaimNumber##"] = c.InjuryClaimNumber,
            ["##InjuryDetails.DateOfInjury##"] = c.InjuryDateOfInjury,
            ["##InjuryDetails.WcabAdj##"] = c.InjuryWcabAdj,
            ["##InjuryDetails.WcabOfficeName##"] = c.InjuryWcabOfficeName,
            ["##InjuryDetails.WcabOfficeAddress##"] = c.InjuryWcabOfficeAddress,
            ["##InjuryDetails.WcabOfficeCity##"] = c.InjuryWcabOfficeCity,
            ["##InjuryDetails.WcabOfficeState##"] = c.InjuryWcabOfficeState,
            ["##InjuryDetails.WcabOfficeZipCode##"] = c.InjuryWcabOfficeZipCode,
            ["##InjuryDetails.PrimaryInsuranceName##"] = c.InjuryPrimaryInsuranceName,
            ["##InjuryDetails.PrimaryInsuranceStreet##"] = c.InjuryPrimaryInsuranceStreet,
            ["##InjuryDetails.PrimaryInsuranceCity##"] = c.InjuryPrimaryInsuranceCity,
            ["##InjuryDetails.PrimaryInsuranceState##"] = c.InjuryPrimaryInsuranceState,
            ["##InjuryDetails.PrimaryInsuranceZip##"] = c.InjuryPrimaryInsuranceZip,
            ["##InjuryDetails.PrimaryInsurancePhoneNumber##"] = c.InjuryPrimaryInsurancePhoneNumber,
            ["##InjuryDetails.ClaimExaminerName##"] = c.InjuryClaimExaminerName,
            ["##InjuryDetails.ClaimExaminerStreet##"] = c.InjuryClaimExaminerStreet,
            ["##InjuryDetails.ClaimExaminerCity##"] = c.InjuryClaimExaminerCity,
            ["##InjuryDetails.ClaimExaminerState##"] = c.InjuryClaimExaminerState,
            ["##InjuryDetails.ClaimExaminerZip##"] = c.InjuryClaimExaminerZip,
            ["##InjuryDetails.ClaimExaminerPhoneNumber##"] = c.InjuryClaimExaminerPhoneNumber,

            // Others group
            ["##Others.DateNow##"] = c.DateNow,
        };
    }
}
