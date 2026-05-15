namespace HealthcareSupport.CaseEvaluation.Permissions;

public static class CaseEvaluationPermissions
{
    public const string GroupName = "CaseEvaluation";

    public static class Dashboard
    {
        public const string DashboardGroup = GroupName + ".Dashboard";
        public const string Host = DashboardGroup + ".Host";
        public const string Tenant = DashboardGroup + ".Tenant";
    }

    public static class Books
    {
        public const string Default = GroupName + ".Books";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class States
    {
        public const string Default = GroupName + ".States";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentTypes
    {
        public const string Default = GroupName + ".AppointmentTypes";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentStatuses
    {
        public const string Default = GroupName + ".AppointmentStatuses";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentLanguages
    {
        public const string Default = GroupName + ".AppointmentLanguages";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Locations
    {
        public const string Default = GroupName + ".Locations";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class WcabOffices
    {
        public const string Default = GroupName + ".WcabOffices";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Doctors
    {
        public const string Default = GroupName + ".Doctors";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class DoctorAvailabilities
    {
        public const string Default = GroupName + ".DoctorAvailabilities";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Patients
    {
        public const string Default = GroupName + ".Patients";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Appointments
    {
        public const string Default = GroupName + ".Appointments";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
        // Phase 2.5 (2026-05-01) -- per-action gates for clinic-staff approval
        // and external-user change-request submission. The booking + view
        // flows live under Default / Create / Edit / Delete.
        public const string Approve = Default + ".Approve";
        public const string Reject = Default + ".Reject";
        public const string RequestCancellation = Default + ".RequestCancellation";
        public const string RequestReschedule = Default + ".RequestReschedule";
    }

    public static class AppointmentDocuments
    {
        public const string Default = GroupName + ".AppointmentDocuments";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";       // W2-11: edit rejection reason / re-action a document.
        public const string Delete = Default + ".Delete";
        public const string Approve = Default + ".Approve"; // W2-11: approve / reject uploaded documents.
    }

    public static class AppointmentPackets
    {
        public const string Default = GroupName + ".AppointmentPackets";
        public const string Regenerate = Default + ".Regenerate";
    }

    public static class AppointmentEmployerDetails
    {
        public const string Default = GroupName + ".AppointmentEmployerDetails";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentAccessors
    {
        public const string Default = GroupName + ".AppointmentAccessors";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class ApplicantAttorneys
    {
        public const string Default = GroupName + ".ApplicantAttorneys";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentApplicantAttorneys
    {
        public const string Default = GroupName + ".AppointmentApplicantAttorneys";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class DefenseAttorneys
    {
        public const string Default = GroupName + ".DefenseAttorneys";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentDefenseAttorneys
    {
        public const string Default = GroupName + ".AppointmentDefenseAttorneys";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentInjuryDetails
    {
        public const string Default = GroupName + ".AppointmentInjuryDetails";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentBodyParts
    {
        public const string Default = GroupName + ".AppointmentBodyParts";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentClaimExaminers
    {
        public const string Default = GroupName + ".AppointmentClaimExaminers";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class AppointmentPrimaryInsurances
    {
        public const string Default = GroupName + ".AppointmentPrimaryInsurances";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    /// <summary>
    /// W2-4: read-only access to per-appointment audit history. Audit rows
    /// themselves are immutable so no Create/Edit/Delete children apply.
    /// </summary>
    public static class AppointmentChangeLogs
    {
        public const string Default = GroupName + ".AppointmentChangeLogs";
    }

    /// <summary>
    /// W2-5: per-AppointmentType field-config admin (Hidden / ReadOnly / DefaultValue).
    /// Default visibility is read-only for non-admin callers (booker form needs Default
    /// to fetch the apply-on-change config); Create/Edit/Delete gate admin actions.
    /// </summary>
    public static class CustomFields
    {
        public const string Default = GroupName + ".CustomFields";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SystemParameters
    {
        public const string Default = GroupName + ".SystemParameters";
        public const string Edit = Default + ".Edit";
    }

    /// <summary>
    /// Phase 2.5 (2026-05-01) -- supervisor approval surface for the
    /// user-submitted cancel / reschedule lifecycle. External roles never
    /// see this group. Staff Supervisor + IT Admin gain Approve / Reject;
    /// Clinic Staff gets Default (read-only inbox view).
    /// </summary>
    public static class AppointmentChangeRequests
    {
        public const string Default = GroupName + ".AppointmentChangeRequests";
        public const string Approve = Default + ".Approve";
        public const string Reject = Default + ".Reject";
    }

    /// <summary>
    /// Phase 2.5 (2026-05-01) -- IT Admin manages tenant-scoped notification
    /// templates. Read access is gated to Default; the editor button gates on
    /// Edit. No Create / Delete -- templates are seeded.
    /// </summary>
    public static class NotificationTemplates
    {
        public const string Default = GroupName + ".NotificationTemplates";
        public const string Edit = Default + ".Edit";
    }

    /// <summary>
    /// Phase 5 (2026-05-03) -- IT Admin maintains the master template catalog.
    /// Each Document is a blank PDF/DOCX form that is later linked to one or
    /// more PackageDetails for use in appointment-specific document packets.
    /// Mirrors OLD's <c>spm.Documents</c> CRUD surface.
    /// </summary>
    public static class Documents
    {
        public const string Default = GroupName + ".Documents";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    /// <summary>
    /// Phase 5 (2026-05-03) -- IT Admin manages per-AppointmentType package
    /// templates and the Documents linked into each. Mirrors OLD's
    /// <c>spm.PackageDetails</c> + <c>spm.DocumentPackages</c> CRUD. The
    /// "one active package per AppointmentType" rule lives in the AppService;
    /// see <c>P:\PatientPortalOld\PatientAppointment.Domain\DocumentManagementModule\PackageDetailDomain.cs</c>:48-53.
    /// <c>ManageDocuments</c> gates Link / Unlink endpoints because those are
    /// distinct user actions (separate from Create/Edit on the package itself).
    /// </summary>
    public static class PackageDetails
    {
        public const string Default = GroupName + ".PackageDetails";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string ManageDocuments = Default + ".ManageDocuments";
    }

    /// <summary>
    /// Phase 7b (2026-05-03) -- IT Admin / Staff Supervisor toggles which
    /// Locations a Doctor accepts appointments at. Mirrors OLD
    /// <c>DoctorPreferredLocationDomain</c>. <c>Default</c> gates the read
    /// path (consumed by the booking-form Location dropdown);
    /// <c>Toggle</c> gates the upsert / on-off flip.
    /// </summary>
    public static class DoctorPreferredLocations
    {
        public const string Default = GroupName + ".DoctorPreferredLocations";
        public const string Toggle = Default + ".Toggle";
    }

    /// <summary>
    /// 2026-05-15 -- internal staff issue invitations for new external
    /// users to register on the tenant portal. Single nested perm
    /// (<c>InviteExternalUser</c>) under the <c>UserManagement</c>
    /// parent so future invite-management actions (revoke, resend,
    /// invitation-history view) can grow as siblings without
    /// renumbering. Granted to IT Admin, Staff Supervisor, and Clinic
    /// Staff per the role-seeder; external roles intentionally never
    /// receive this permission.
    /// </summary>
    public static class UserManagement
    {
        public const string Default = GroupName + ".UserManagement";
        public const string InviteExternalUser = Default + ".InviteExternalUser";
    }

    /// <summary>
    /// Phase A (2026-05-05) -- per-user signature image upload, replicating
    /// OLD's <c>User.SignatureAWSFilePath</c> profile feature. Internal
    /// staff (Clinic Staff / Staff Supervisor / IT Admin) only;
    /// external roles do not have signatures (OLD parity). Used by the
    /// packet-generation flow to stamp the responsible user's signature
    /// on the Patient Packet at <c>##Appointments.Signature##</c>.
    ///
    /// <para><c>ManageOwn</c> gates the upload / download / delete of the
    /// caller's OWN signature -- a permission a user can self-grant via
    /// role membership. There is no separate per-user-target gate because
    /// the AppService scopes every operation to <c>CurrentUser.Id</c>;
    /// internal-only <c>GetByUserIdAsync</c> is hidden via
    /// <c>[RemoteService(IsEnabled = false)]</c> and called by the packet
    /// resolver in-process, so no permission is wired to it.</para>
    /// </summary>
    public static class UserSignatures
    {
        public const string Default = GroupName + ".UserSignatures";
        public const string ManageOwn = Default + ".ManageOwn";
    }
}