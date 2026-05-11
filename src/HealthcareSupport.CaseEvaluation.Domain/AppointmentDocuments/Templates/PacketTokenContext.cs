namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// Resolved token values for one appointment, consumed by the OpenXml
/// renderer when filling out PATIENT PACKET NEW, DOCTOR PACKET, and the
/// unified ATTORNEY CLAIM EXAMINER PACKET templates.
///
/// <para>OLD parity behaviors locked in
/// <c>docs/parity/email-packet-parity/document-packets.md</c>:</para>
/// <list type="bullet">
///   <item>Every string property is the FINAL render value -- already
///   <c>.ToUpper()</c>'d (matches OLD <c>AppointmentDocumentDomain.cs:1070</c>),
///   already formatted (dates, parking fee), already null-collapsed to
///   <c>""</c>. Templates render these verbatim.</item>
///   <item><see cref="ResponsibleUserSignature"/> is the per-user signature
///   bytes; <c>null</c> when the responsible user has no signature uploaded.
///   Templates skip the image when null (OLD silent-skip at
///   <c>AppointmentDocumentDomain.cs:657</c>).</item>
///   <item>InjuryDetails are pre-concatenated by the resolver: ALL rows
///   joined with <c>" "</c> separator. Single-injury appointments produce
///   a value with a trailing space (OLD pattern at
///   <c>AppointmentDocumentDomain.cs:1139, :1160, :1184</c>).</item>
/// </list>
///
/// <para>The 60 properties below correspond 1:1 with the union of tokens
/// used by the 3 active OLD templates (44 in Patient Packet + 15 in
/// Doctor Packet + 55 in AttorneyClaimExaminer; with overlap, 60 unique).
/// Property names match the OLD token's column part (after the
/// <c>##Group.</c> prefix). The OLD token name appears in each XML doc
/// comment so a template author can grep for the placeholder string.</para>
/// </summary>
public class PacketTokenContext
{
    // -- Patients group (vPatient view) -----------------------------------

    /// <summary>OLD: <c>##Patients.FirstName##</c></summary>
    public string PatientFirstName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Patients.LastName##</c></summary>
    public string PatientLastName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Patients.MiddleName##</c> (Doctor Packet only)</summary>
    public string PatientMiddleName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Patients.DateOfBirth##</c> (formatted MM/dd/yyyy, ToUpper)</summary>
    public string PatientDateOfBirth { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Patients.SocialSecurityNumber##</c></summary>
    public string PatientSocialSecurityNumber { get; set; } = string.Empty;

    /// <summary>
    /// OLD: <c>##Patients.Street##</c>. NB: Patient entity has both
    /// <c>Street</c> and <c>Address</c>; OLD's vPatient view exposes both
    /// columns and the token resolves to <c>Street</c>. <c>Address</c> is
    /// not used for packet rendering.
    /// </summary>
    public string PatientStreet { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Patients.City##</c></summary>
    public string PatientCity { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Patients.State##</c> (State.Name from FK)</summary>
    public string PatientState { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Patients.ZipCode##</c> (Patient Packet only)</summary>
    public string PatientZipCode { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Patients.PhoneNumber##</c> (AttorneyClaimExaminer Packet only)</summary>
    public string PatientPhoneNumber { get; set; } = string.Empty;

    // -- Appointments group (vAppointmentDetail view) ---------------------

    /// <summary>OLD: <c>##Appointments.RequestConfirmationNumber##</c></summary>
    public string RequestConfirmationNumber { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Appointments.AvailableDate##</c> (DoctorAvailability.AvailableDate, MM/dd/yyyy)</summary>
    public string AvailableDate { get; set; } = string.Empty;

    /// <summary>
    /// OLD: <c>##Appointments.AppointmenTime##</c> (typo preserved verbatim).
    /// Pre-formatted single time string from <c>DoctorAvailability.FromTime</c>
    /// rendered as <c>h:mm tt</c> then ToUpper. Phase 1 sample compares against
    /// OLD; pivot to range "FromTime - ToTime" if visual mismatch.
    /// </summary>
    public string AppointmentTime { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Appointments.AppointmentType##</c> (Doctor Packet only, AppointmentType.Name)</summary>
    public string AppointmentType { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Appointments.Location##</c> (Location.Name)</summary>
    public string LocationName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Appointments.LocationAddress##</c> (Patient Packet only, Location.Address)</summary>
    public string LocationAddress { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Appointments.LocationCity##</c> (Patient Packet only, Location.City)</summary>
    public string LocationCity { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Appointments.LocationState##</c> (Location.StateId -> State.Name)</summary>
    public string LocationState { get; set; } = string.Empty;

    /// <summary>OLD: <c>##Appointments.LocationZipCode##</c></summary>
    public string LocationZipCode { get; set; } = string.Empty;

    /// <summary>
    /// OLD: <c>##Appointments.LocationParkingFee##</c>. <c>decimal?.ToString().ToUpper()</c>
    /// -- raw decimal (e.g. "10.00", "10", "10.5"), NO currency formatting,
    /// NO 2-decimal padding. Empty string when null. Matches OLD
    /// <c>AppointmentDocumentDomain.cs:1070</c>.
    /// </summary>
    public string LocationParkingFee { get; set; } = string.Empty;

    /// <summary>
    /// OLD: <c>##Appointments.PrimaryResponsibleUserName##</c>. Joined as
    /// <c>IdentityUser.Name + " " + IdentityUser.Surname</c> from
    /// <c>Appointment.PrimaryResponsibleUserId</c>; ToUpper'd.
    /// </summary>
    public string PrimaryResponsibleUserName { get; set; } = string.Empty;

    /// <summary>
    /// OLD: <c>##Appointments.Signature##</c> (Patient Packet + AttorneyClaimExaminer Packet).
    /// Per-user signature image bytes -- PNG or JPEG. <c>null</c> when the
    /// responsible user has no signature uploaded; templates skip the image
    /// (matches OLD silent-skip at <c>AppointmentDocumentDomain.cs:657</c>).
    /// Sourced via <c>IUserSignatureAppService.GetBytesByUserIdAsync</c>.
    /// </summary>
    public byte[]? ResponsibleUserSignature { get; set; }

    /// <summary>
    /// OLD: <c>##Appointments.AppointmentCreatedDate##</c> (AttorneyClaimExaminer Packet only).
    /// Pre-formatted creation date (typically MM/dd/yyyy) of the appointment
    /// row. Sourced from <c>Appointment.CreationTime</c> (or OLD's
    /// equivalent column on vAppointmentDetail). ToUpper'd no-op for digits.
    /// </summary>
    public string AppointmentCreatedDate { get; set; } = string.Empty;

    /// <summary>
    /// OLD: <c>##Appointments.PanelNumber##</c> (AttorneyClaimExaminer Packet only).
    /// Sourced from a per-appointment Panel Number field on
    /// vAppointmentDetail. Phase 1B.4 resolver maps to the matching NEW
    /// Appointment property; if NEW has no Panel Number column, the
    /// resolver renders empty string (OLD silently empties unknown columns
    /// per the reflection lookup at <c>AppointmentDocumentDomain.cs:1066-1071</c>).
    /// </summary>
    public string PanelNumber { get; set; } = string.Empty;

    // -- EmployerDetails group (vAppointmentEmployerDetail) ---------------

    /// <summary>OLD: <c>##EmployerDetails.EmployerName##</c> (FirstOrDefault row)</summary>
    public string EmployerName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##EmployerDetails.Street##</c> (Doctor Packet only)</summary>
    public string EmployerStreet { get; set; } = string.Empty;

    /// <summary>OLD: <c>##EmployerDetails.City##</c> (Doctor Packet only)</summary>
    public string EmployerCity { get; set; } = string.Empty;

    /// <summary>OLD: <c>##EmployerDetails.State##</c> (Doctor Packet only, StateId -> State.Name)</summary>
    public string EmployerState { get; set; } = string.Empty;

    /// <summary>
    /// OLD: <c>##EmployerDetails.Zip##</c> (Doctor Packet only).
    /// NB: NEW <c>AppointmentEmployerDetail.ZipCode</c> property is named
    /// inconsistently with the OLD token (<c>Zip</c>). Resolver maps
    /// <c>ZipCode</c> -> this property.
    /// </summary>
    public string EmployerZip { get; set; } = string.Empty;

    // -- PatientAttorneys group -------------------------------------------
    // (Patient Packet only; resolver takes FirstOrDefault row)

    /// <summary>
    /// OLD: <c>##PatientAttorneys.AttorneyName##</c>. Joined as
    /// <c>IdentityUser.Name + " " + Surname</c> via
    /// <c>ApplicantAttorney.IdentityUserId</c>.
    /// </summary>
    public string PatientAttorneyName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##PatientAttorneys.Street##</c></summary>
    public string PatientAttorneyStreet { get; set; } = string.Empty;

    /// <summary>OLD: <c>##PatientAttorneys.City##</c></summary>
    public string PatientAttorneyCity { get; set; } = string.Empty;

    /// <summary>OLD: <c>##PatientAttorneys.State##</c> (StateId -> State.Name)</summary>
    public string PatientAttorneyState { get; set; } = string.Empty;

    /// <summary>OLD: <c>##PatientAttorneys.Zip##</c> (NEW prop is ZipCode -- resolver maps)</summary>
    public string PatientAttorneyZip { get; set; } = string.Empty;

    // -- DefenseAttorneys group -------------------------------------------
    // (Patient Packet only; resolver takes FirstOrDefault row)

    /// <summary>OLD: <c>##DefenseAttorneys.AttorneyName##</c></summary>
    public string DefenseAttorneyName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##DefenseAttorneys.Street##</c></summary>
    public string DefenseAttorneyStreet { get; set; } = string.Empty;

    /// <summary>OLD: <c>##DefenseAttorneys.City##</c></summary>
    public string DefenseAttorneyCity { get; set; } = string.Empty;

    /// <summary>OLD: <c>##DefenseAttorneys.State##</c> (StateId -> State.Name)</summary>
    public string DefenseAttorneyState { get; set; } = string.Empty;

    /// <summary>OLD: <c>##DefenseAttorneys.Zip##</c></summary>
    public string DefenseAttorneyZip { get; set; } = string.Empty;

    // -- InjuryDetails group ----------------------------------------------
    // ALL injury rows are space-concatenated by the resolver per OLD
    // GetColumnValuesForInjury at AppointmentDocumentDomain.cs:1133-1142.
    // Single-injury appointments produce a value with a trailing space.

    /// <summary>OLD: <c>##InjuryDetails.ClaimNumber##</c> (concat all injuries)</summary>
    public string InjuryClaimNumber { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.DateOfInjury##</c> (concat all injuries; each row MM/dd/yyyy)</summary>
    public string InjuryDateOfInjury { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.WcabAdj##</c> (Patient Packet only; concat all injuries)</summary>
    public string InjuryWcabAdj { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.WcabOfficeName##</c> (Patient Packet only; concat all injuries)</summary>
    public string InjuryWcabOfficeName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.WcabOfficeAddress##</c> (Patient Packet only)</summary>
    public string InjuryWcabOfficeAddress { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.WcabOfficeCity##</c> (Patient Packet only)</summary>
    public string InjuryWcabOfficeCity { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.WcabOfficeState##</c> (Patient Packet only)</summary>
    public string InjuryWcabOfficeState { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.WcabOfficeZipCode##</c> (Patient Packet only)</summary>
    public string InjuryWcabOfficeZipCode { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.PrimaryInsuranceName##</c></summary>
    public string InjuryPrimaryInsuranceName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.PrimaryInsuranceStreet##</c> (Patient + AttorneyClaimExaminer)</summary>
    public string InjuryPrimaryInsuranceStreet { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.PrimaryInsuranceCity##</c> (Patient + AttorneyClaimExaminer)</summary>
    public string InjuryPrimaryInsuranceCity { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.PrimaryInsuranceState##</c> (Patient + AttorneyClaimExaminer)</summary>
    public string InjuryPrimaryInsuranceState { get; set; } = string.Empty;

    /// <summary>
    /// OLD: <c>##InjuryDetails.PrimaryInsuranceZip##</c> (Patient + AttorneyClaimExaminer).
    /// NB: NEW <c>AppointmentPrimaryInsurance.Zip</c> -- one of the few NEW
    /// entities that uses <c>Zip</c> instead of <c>ZipCode</c> as the
    /// property name (inconsistent with WcabOffice, ApplicantAttorney etc.).
    /// </summary>
    public string InjuryPrimaryInsuranceZip { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.PrimaryInsurancePhoneNumber##</c> (AttorneyClaimExaminer Packet only). Concat all injuries.</summary>
    public string InjuryPrimaryInsurancePhoneNumber { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.ClaimExaminerName##</c> (Doctor + AttorneyClaimExaminer). Concat all injuries; first active examiner per injury.</summary>
    public string InjuryClaimExaminerName { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.ClaimExaminerStreet##</c> (AttorneyClaimExaminer Packet only). Concat all injuries.</summary>
    public string InjuryClaimExaminerStreet { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.ClaimExaminerCity##</c> (AttorneyClaimExaminer Packet only). Concat all injuries.</summary>
    public string InjuryClaimExaminerCity { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.ClaimExaminerState##</c> (AttorneyClaimExaminer Packet only). Concat all injuries.</summary>
    public string InjuryClaimExaminerState { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.ClaimExaminerZip##</c> (AttorneyClaimExaminer Packet only). Concat all injuries.</summary>
    public string InjuryClaimExaminerZip { get; set; } = string.Empty;

    /// <summary>OLD: <c>##InjuryDetails.ClaimExaminerPhoneNumber##</c> (AttorneyClaimExaminer Packet only). Concat all injuries.</summary>
    public string InjuryClaimExaminerPhoneNumber { get; set; } = string.Empty;

    // -- Others group ------------------------------------------------------

    /// <summary>
    /// OLD: <c>##Others.DateNow##</c>. <c>DateTime.Today.ToString("MM/dd/yyyy")</c>
    /// then ToUpper'd (no-op for digits). Computed by the resolver at the
    /// time the packet is rendered. UTC -> Pacific Time conversion is the
    /// AppService's responsibility, not the resolver's.
    /// </summary>
    public string DateNow { get; set; } = string.Empty;
}
