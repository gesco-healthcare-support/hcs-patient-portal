using HealthcareSupport.CaseEvaluation.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Permissions;

public class CaseEvaluationPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(CaseEvaluationPermissions.GroupName);
        myGroup.AddPermission(CaseEvaluationPermissions.Dashboard.Host, L("Permission:Dashboard"), MultiTenancySides.Host);
        myGroup.AddPermission(CaseEvaluationPermissions.Dashboard.Tenant, L("Permission:Dashboard"), MultiTenancySides.Tenant);
        var booksPermission = myGroup.AddPermission(CaseEvaluationPermissions.Books.Default, L("Permission:Books"));
        booksPermission.AddChild(CaseEvaluationPermissions.Books.Create, L("Permission:Books.Create"));
        booksPermission.AddChild(CaseEvaluationPermissions.Books.Edit, L("Permission:Books.Edit"));
        booksPermission.AddChild(CaseEvaluationPermissions.Books.Delete, L("Permission:Books.Delete"));
        var statePermission = myGroup.AddPermission(CaseEvaluationPermissions.States.Default, L("Permission:States"));
        statePermission.AddChild(CaseEvaluationPermissions.States.Create, L("Permission:Create"));
        statePermission.AddChild(CaseEvaluationPermissions.States.Edit, L("Permission:Edit"));
        statePermission.AddChild(CaseEvaluationPermissions.States.Delete, L("Permission:Delete"));
        var appointmentTypePermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentTypes.Default, L("Permission:AppointmentTypes"));
        appointmentTypePermission.AddChild(CaseEvaluationPermissions.AppointmentTypes.Create, L("Permission:Create"));
        appointmentTypePermission.AddChild(CaseEvaluationPermissions.AppointmentTypes.Edit, L("Permission:Edit"));
        appointmentTypePermission.AddChild(CaseEvaluationPermissions.AppointmentTypes.Delete, L("Permission:Delete"));
        var appointmentStatusPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentStatuses.Default, L("Permission:AppointmentStatuses"));
        appointmentStatusPermission.AddChild(CaseEvaluationPermissions.AppointmentStatuses.Create, L("Permission:Create"));
        appointmentStatusPermission.AddChild(CaseEvaluationPermissions.AppointmentStatuses.Edit, L("Permission:Edit"));
        appointmentStatusPermission.AddChild(CaseEvaluationPermissions.AppointmentStatuses.Delete, L("Permission:Delete"));
        var appointmentLanguagePermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentLanguages.Default, L("Permission:AppointmentLanguages"));
        appointmentLanguagePermission.AddChild(CaseEvaluationPermissions.AppointmentLanguages.Create, L("Permission:Create"));
        appointmentLanguagePermission.AddChild(CaseEvaluationPermissions.AppointmentLanguages.Edit, L("Permission:Edit"));
        appointmentLanguagePermission.AddChild(CaseEvaluationPermissions.AppointmentLanguages.Delete, L("Permission:Delete"));
        var locationPermission = myGroup.AddPermission(CaseEvaluationPermissions.Locations.Default, L("Permission:Locations"));
        locationPermission.AddChild(CaseEvaluationPermissions.Locations.Create, L("Permission:Create"));
        locationPermission.AddChild(CaseEvaluationPermissions.Locations.Edit, L("Permission:Edit"));
        locationPermission.AddChild(CaseEvaluationPermissions.Locations.Delete, L("Permission:Delete"));
        var wcabOfficePermission = myGroup.AddPermission(CaseEvaluationPermissions.WcabOffices.Default, L("Permission:WcabOffices"));
        wcabOfficePermission.AddChild(CaseEvaluationPermissions.WcabOffices.Create, L("Permission:Create"));
        wcabOfficePermission.AddChild(CaseEvaluationPermissions.WcabOffices.Edit, L("Permission:Edit"));
        wcabOfficePermission.AddChild(CaseEvaluationPermissions.WcabOffices.Delete, L("Permission:Delete"));
        var doctorPermission = myGroup.AddPermission(CaseEvaluationPermissions.Doctors.Default, L("Permission:Doctors"));
        doctorPermission.AddChild(CaseEvaluationPermissions.Doctors.Create, L("Permission:Create"));
        doctorPermission.AddChild(CaseEvaluationPermissions.Doctors.Edit, L("Permission:Edit"));
        doctorPermission.AddChild(CaseEvaluationPermissions.Doctors.Delete, L("Permission:Delete"));
        var doctorAvailabilityPermission = myGroup.AddPermission(CaseEvaluationPermissions.DoctorAvailabilities.Default, L("Permission:DoctorAvailabilities"));
        doctorAvailabilityPermission.AddChild(CaseEvaluationPermissions.DoctorAvailabilities.Create, L("Permission:Create"));
        doctorAvailabilityPermission.AddChild(CaseEvaluationPermissions.DoctorAvailabilities.Edit, L("Permission:Edit"));
        doctorAvailabilityPermission.AddChild(CaseEvaluationPermissions.DoctorAvailabilities.Delete, L("Permission:Delete"));
        var patientPermission = myGroup.AddPermission(CaseEvaluationPermissions.Patients.Default, L("Permission:Patients"));
        patientPermission.AddChild(CaseEvaluationPermissions.Patients.Create, L("Permission:Create"));
        patientPermission.AddChild(CaseEvaluationPermissions.Patients.Edit, L("Permission:Edit"));
        patientPermission.AddChild(CaseEvaluationPermissions.Patients.Delete, L("Permission:Delete"));
        var appointmentPermission = myGroup.AddPermission(CaseEvaluationPermissions.Appointments.Default, L("Permission:Appointments"));
        appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.Create, L("Permission:Create"));
        appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.Edit, L("Permission:Edit"));
        appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.Delete, L("Permission:Delete"));
        // Phase 2.5 (2026-05-01) -- per-action gates for the approval +
        // change-request submission flows.
        appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.Approve, L("Permission:Approve"));
        appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.Reject, L("Permission:Reject"));
        appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.RequestCancellation, L("Permission:RequestCancellation"));
        appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.RequestReschedule, L("Permission:RequestReschedule"));
        var appointmentEmployerDetailPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentEmployerDetails.Default, L("Permission:AppointmentEmployerDetails"));
        appointmentEmployerDetailPermission.AddChild(CaseEvaluationPermissions.AppointmentEmployerDetails.Create, L("Permission:Create"));
        appointmentEmployerDetailPermission.AddChild(CaseEvaluationPermissions.AppointmentEmployerDetails.Edit, L("Permission:Edit"));
        appointmentEmployerDetailPermission.AddChild(CaseEvaluationPermissions.AppointmentEmployerDetails.Delete, L("Permission:Delete"));
        var appointmentAccessorPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentAccessors.Default, L("Permission:AppointmentAccessors"));
        appointmentAccessorPermission.AddChild(CaseEvaluationPermissions.AppointmentAccessors.Create, L("Permission:Create"));
        appointmentAccessorPermission.AddChild(CaseEvaluationPermissions.AppointmentAccessors.Edit, L("Permission:Edit"));
        appointmentAccessorPermission.AddChild(CaseEvaluationPermissions.AppointmentAccessors.Delete, L("Permission:Delete"));
        var applicantAttorneyPermission = myGroup.AddPermission(CaseEvaluationPermissions.ApplicantAttorneys.Default, L("Permission:ApplicantAttorneys"));
        applicantAttorneyPermission.AddChild(CaseEvaluationPermissions.ApplicantAttorneys.Create, L("Permission:Create"));
        applicantAttorneyPermission.AddChild(CaseEvaluationPermissions.ApplicantAttorneys.Edit, L("Permission:Edit"));
        applicantAttorneyPermission.AddChild(CaseEvaluationPermissions.ApplicantAttorneys.Delete, L("Permission:Delete"));
        var appointmentApplicantAttorneyPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Default, L("Permission:AppointmentApplicantAttorneys"));
        appointmentApplicantAttorneyPermission.AddChild(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Create, L("Permission:Create"));
        appointmentApplicantAttorneyPermission.AddChild(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Edit, L("Permission:Edit"));
        appointmentApplicantAttorneyPermission.AddChild(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Delete, L("Permission:Delete"));
        var defenseAttorneyPermission = myGroup.AddPermission(CaseEvaluationPermissions.DefenseAttorneys.Default, L("Permission:DefenseAttorneys"));
        defenseAttorneyPermission.AddChild(CaseEvaluationPermissions.DefenseAttorneys.Create, L("Permission:Create"));
        defenseAttorneyPermission.AddChild(CaseEvaluationPermissions.DefenseAttorneys.Edit, L("Permission:Edit"));
        defenseAttorneyPermission.AddChild(CaseEvaluationPermissions.DefenseAttorneys.Delete, L("Permission:Delete"));
        var appointmentDefenseAttorneyPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Default, L("Permission:AppointmentDefenseAttorneys"));
        appointmentDefenseAttorneyPermission.AddChild(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Create, L("Permission:Create"));
        appointmentDefenseAttorneyPermission.AddChild(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Edit, L("Permission:Edit"));
        appointmentDefenseAttorneyPermission.AddChild(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Delete, L("Permission:Delete"));
        var appointmentInjuryDetailPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentInjuryDetails.Default, L("Permission:AppointmentInjuryDetails"));
        appointmentInjuryDetailPermission.AddChild(CaseEvaluationPermissions.AppointmentInjuryDetails.Create, L("Permission:Create"));
        appointmentInjuryDetailPermission.AddChild(CaseEvaluationPermissions.AppointmentInjuryDetails.Edit, L("Permission:Edit"));
        appointmentInjuryDetailPermission.AddChild(CaseEvaluationPermissions.AppointmentInjuryDetails.Delete, L("Permission:Delete"));
        var appointmentBodyPartPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentBodyParts.Default, L("Permission:AppointmentBodyParts"));
        appointmentBodyPartPermission.AddChild(CaseEvaluationPermissions.AppointmentBodyParts.Create, L("Permission:Create"));
        appointmentBodyPartPermission.AddChild(CaseEvaluationPermissions.AppointmentBodyParts.Edit, L("Permission:Edit"));
        appointmentBodyPartPermission.AddChild(CaseEvaluationPermissions.AppointmentBodyParts.Delete, L("Permission:Delete"));
        var appointmentClaimExaminerPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentClaimExaminers.Default, L("Permission:AppointmentClaimExaminers"));
        appointmentClaimExaminerPermission.AddChild(CaseEvaluationPermissions.AppointmentClaimExaminers.Create, L("Permission:Create"));
        appointmentClaimExaminerPermission.AddChild(CaseEvaluationPermissions.AppointmentClaimExaminers.Edit, L("Permission:Edit"));
        appointmentClaimExaminerPermission.AddChild(CaseEvaluationPermissions.AppointmentClaimExaminers.Delete, L("Permission:Delete"));
        var appointmentPrimaryInsurancePermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Default, L("Permission:AppointmentPrimaryInsurances"));
        appointmentPrimaryInsurancePermission.AddChild(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Create, L("Permission:Create"));
        appointmentPrimaryInsurancePermission.AddChild(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Edit, L("Permission:Edit"));
        appointmentPrimaryInsurancePermission.AddChild(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Delete, L("Permission:Delete"));

        // W2-4: read-only audit-log permission. No children -- audit rows are immutable.
        myGroup.AddPermission(CaseEvaluationPermissions.AppointmentChangeLogs.Default, L("Permission:AppointmentChangeLogs"));

        // W2-5: per-AppointmentType field-config admin. Default lets the booker
        // form read the apply-on-change config; Create/Edit/Delete gate admin
        // mutation paths.
        var customFieldsPermission = myGroup.AddPermission(CaseEvaluationPermissions.CustomFields.Default, L("Permission:CustomFields"));
        customFieldsPermission.AddChild(CaseEvaluationPermissions.CustomFields.Create, L("Permission:Create"));
        customFieldsPermission.AddChild(CaseEvaluationPermissions.CustomFields.Edit, L("Permission:Edit"));
        customFieldsPermission.AddChild(CaseEvaluationPermissions.CustomFields.Delete, L("Permission:Delete"));

        var systemParametersPermission = myGroup.AddPermission(CaseEvaluationPermissions.SystemParameters.Default, L("Permission:SystemParameters"));
        systemParametersPermission.AddChild(CaseEvaluationPermissions.SystemParameters.Edit, L("Permission:Edit"));

        var appointmentDocumentsPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentDocuments.Default, L("Permission:AppointmentDocuments"));
        appointmentDocumentsPermission.AddChild(CaseEvaluationPermissions.AppointmentDocuments.Create, L("Permission:Create"));
        appointmentDocumentsPermission.AddChild(CaseEvaluationPermissions.AppointmentDocuments.Edit, L("Permission:Edit"));
        appointmentDocumentsPermission.AddChild(CaseEvaluationPermissions.AppointmentDocuments.Delete, L("Permission:Delete"));
        appointmentDocumentsPermission.AddChild(CaseEvaluationPermissions.AppointmentDocuments.Approve, L("Permission:Approve"));

        var appointmentPacketsPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentPackets.Default, L("Permission:AppointmentPackets"));
        appointmentPacketsPermission.AddChild(CaseEvaluationPermissions.AppointmentPackets.Regenerate, L("Permission:Regenerate"));

        // Phase 2.5 (2026-05-01) -- supervisor approval surface for cancel /
        // reschedule. Default = read-only inbox; Approve / Reject = supervisor.
        var appointmentChangeRequestsPermission = myGroup.AddPermission(CaseEvaluationPermissions.AppointmentChangeRequests.Default, L("Permission:AppointmentChangeRequests"));
        appointmentChangeRequestsPermission.AddChild(CaseEvaluationPermissions.AppointmentChangeRequests.Approve, L("Permission:Approve"));
        appointmentChangeRequestsPermission.AddChild(CaseEvaluationPermissions.AppointmentChangeRequests.Reject, L("Permission:Reject"));

        // Phase 2.5 (2026-05-01) -- IT Admin notification template editor.
        var notificationTemplatesPermission = myGroup.AddPermission(CaseEvaluationPermissions.NotificationTemplates.Default, L("Permission:NotificationTemplates"));
        notificationTemplatesPermission.AddChild(CaseEvaluationPermissions.NotificationTemplates.Edit, L("Permission:Edit"));

        // Phase 5 (2026-05-03) -- IT Admin master Document catalog.
        var documentsPermission = myGroup.AddPermission(CaseEvaluationPermissions.Documents.Default, L("Permission:Documents"));
        documentsPermission.AddChild(CaseEvaluationPermissions.Documents.Create, L("Permission:Create"));
        documentsPermission.AddChild(CaseEvaluationPermissions.Documents.Edit, L("Permission:Edit"));
        documentsPermission.AddChild(CaseEvaluationPermissions.Documents.Delete, L("Permission:Delete"));

        // Phase 5 (2026-05-03) -- IT Admin per-AppointmentType package templates.
        var packageDetailsPermission = myGroup.AddPermission(CaseEvaluationPermissions.PackageDetails.Default, L("Permission:PackageDetails"));
        packageDetailsPermission.AddChild(CaseEvaluationPermissions.PackageDetails.Create, L("Permission:Create"));
        packageDetailsPermission.AddChild(CaseEvaluationPermissions.PackageDetails.Edit, L("Permission:Edit"));
        packageDetailsPermission.AddChild(CaseEvaluationPermissions.PackageDetails.Delete, L("Permission:Delete"));
        packageDetailsPermission.AddChild(CaseEvaluationPermissions.PackageDetails.ManageDocuments, L("Permission:PackageDetails.ManageDocuments"));

        // Phase 7b (2026-05-03) -- Doctor-Location preference toggle.
        var doctorPreferredLocationsPermission = myGroup.AddPermission(
            CaseEvaluationPermissions.DoctorPreferredLocations.Default,
            L("Permission:DoctorPreferredLocations"));
        doctorPreferredLocationsPermission.AddChild(
            CaseEvaluationPermissions.DoctorPreferredLocations.Toggle,
            L("Permission:DoctorPreferredLocations.Toggle"));

        // Phase A (2026-05-05) -- per-user signature upload (internal staff only).
        var userSignaturesPermission = myGroup.AddPermission(
            CaseEvaluationPermissions.UserSignatures.Default,
            L("Permission:UserSignatures"));
        userSignaturesPermission.AddChild(
            CaseEvaluationPermissions.UserSignatures.ManageOwn,
            L("Permission:UserSignatures.ManageOwn"));

        // 2026-05-15 -- admin invite for new external users. The parent
        // Default lets future invite-management surfaces (revoke,
        // resend, history) share the same menu visibility gate without
        // re-mapping every role-seeder; the child InviteExternalUser
        // gates the create-invite endpoint itself.
        var userManagementPermission = myGroup.AddPermission(
            CaseEvaluationPermissions.UserManagement.Default,
            L("Permission:UserManagement"));
        userManagementPermission.AddChild(
            CaseEvaluationPermissions.UserManagement.InviteExternalUser,
            L("Permission:UserManagement.InviteExternalUser"));

        // 2026-05-15 -- IT Admin internal-user creation. Host-side only
        // (MultiTenancySides.Host): IT Admin lives at admin.localhost,
        // Staff Supervisor + Clinic Staff intentionally do not receive
        // this permission per OLD parity. The new user is created
        // INSIDE the tenant carried on the input DTO (CurrentTenant
        // switch inside the AppService).
        var internalUsersPermission = myGroup.AddPermission(
            CaseEvaluationPermissions.InternalUsers.Default,
            L("Permission:InternalUsers"),
            MultiTenancySides.Host);
        internalUsersPermission.AddChild(
            CaseEvaluationPermissions.InternalUsers.Create,
            L("Permission:InternalUsers.Create"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<CaseEvaluationResource>(name);
    }
}