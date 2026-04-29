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
    }

    public static class AppointmentDocuments
    {
        public const string Default = GroupName + ".AppointmentDocuments";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
        // W1-3 cut: Edit + Approve permissions deferred until the doc-status
        // workflow ships post-MVP.
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
}