using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.AppointmentStatuses;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.States;
using System;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Uow;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.LanguageManagement.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TextTemplateManagement.EntityFrameworkCore;
using Volo.Saas.EntityFrameworkCore;
using Volo.FileManagement.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Gdpr;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.Studio;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

[DependsOn(typeof(CaseEvaluationDomainModule), typeof(AbpIdentityProEntityFrameworkCoreModule), typeof(AbpOpenIddictProEntityFrameworkCoreModule), typeof(AbpPermissionManagementEntityFrameworkCoreModule), typeof(AbpSettingManagementEntityFrameworkCoreModule), typeof(AbpEntityFrameworkCoreSqlServerModule), typeof(AbpBackgroundJobsEntityFrameworkCoreModule), typeof(AbpAuditLoggingEntityFrameworkCoreModule), typeof(AbpFeatureManagementEntityFrameworkCoreModule), typeof(LanguageManagementEntityFrameworkCoreModule), typeof(FileManagementEntityFrameworkCoreModule), typeof(SaasEntityFrameworkCoreModule), typeof(TextTemplateManagementEntityFrameworkCoreModule), typeof(AbpGdprEntityFrameworkCoreModule), typeof(BlobStoringDatabaseEntityFrameworkCoreModule))]
public class CaseEvaluationEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        CaseEvaluationEfCoreEntityExtensionMappings.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<CaseEvaluationDbContext>(options =>
        {
            /* Remove "includeAllEntities: true" to create
                 * default repositories only for aggregate roots */
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<State, States.EfCoreStateRepository>();
            options.AddRepository<AppointmentType, AppointmentTypes.EfCoreAppointmentTypeRepository>();
            options.AddRepository<AppointmentStatus, AppointmentStatuses.EfCoreAppointmentStatusRepository>();
            options.AddRepository<AppointmentLanguage, AppointmentLanguages.EfCoreAppointmentLanguageRepository>();
            options.AddRepository<Location, Locations.EfCoreLocationRepository>();
            options.AddRepository<WcabOffice, WcabOffices.EfCoreWcabOfficeRepository>();
            options.AddRepository<Doctor, Doctors.EfCoreDoctorRepository>();
            options.AddRepository<DoctorAvailability, DoctorAvailabilities.EfCoreDoctorAvailabilityRepository>();
            options.AddRepository<Patient, Patients.EfCorePatientRepository>();
            options.AddRepository<Appointment, Appointments.EfCoreAppointmentRepository>();
            options.AddRepository<AppointmentEmployerDetail, AppointmentEmployerDetails.EfCoreAppointmentEmployerDetailRepository>();
            options.AddRepository<AppointmentAccessor, AppointmentAccessors.EfCoreAppointmentAccessorRepository>();
            options.AddRepository<ApplicantAttorney, ApplicantAttorneys.EfCoreApplicantAttorneyRepository>();
            options.AddRepository<AppointmentApplicantAttorney, AppointmentApplicantAttorneys.EfCoreAppointmentApplicantAttorneyRepository>();
        });
        context.Services.AddAbpDbContext<CaseEvaluationTenantDbContext>(options =>
        {
            /* Remove "includeAllEntities: true" to create
                 * default repositories only for aggregate roots */
            options.AddDefaultRepositories(includeAllEntities: true);
        });
        if (AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            return;
        }

        Configure<AbpDbContextOptions>(options =>
        {
            /* The main point to change your DBMS.
            * See also CaseEvaluationDbContextFactoryBase for EF Core tooling. */
            options.UseSqlServer();
        });
    }
}