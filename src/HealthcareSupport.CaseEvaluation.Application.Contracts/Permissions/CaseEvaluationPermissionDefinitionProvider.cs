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

        var systemParametersPermission = myGroup.AddPermission(CaseEvaluationPermissions.SystemParameters.Default, L("Permission:SystemParameters"));
        systemParametersPermission.AddChild(CaseEvaluationPermissions.SystemParameters.Edit, L("Permission:Edit"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<CaseEvaluationResource>(name);
    }
}