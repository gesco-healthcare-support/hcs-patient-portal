using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.CustomFields;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Invitations;
using HealthcareSupport.CaseEvaluation.UserQueries;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;
using HealthcareSupport.CaseEvaluation.AppointmentStatuses;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.AppointmentDrafts;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;
using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

[ConnectionStringName("Default")]
public class CaseEvaluationTenantDbContext : CaseEvaluationDbContextBase<CaseEvaluationTenantDbContext>
{
    public DbSet<AppointmentApplicantAttorney> AppointmentApplicantAttorneys { get; set; } = null!;
    public DbSet<ApplicantAttorney> ApplicantAttorneys { get; set; } = null!;
    public DbSet<ClaimExaminer> ClaimExaminers { get; set; } = null!;
    public DbSet<AppointmentDefenseAttorney> AppointmentDefenseAttorneys { get; set; } = null!;
    public DbSet<DefenseAttorney> DefenseAttorneys { get; set; } = null!;
    public DbSet<AppointmentInjuryDetail> AppointmentInjuryDetails { get; set; } = null!;
    public DbSet<AppointmentBodyPart> AppointmentBodyParts { get; set; } = null!;
    public DbSet<AppointmentClaimExaminer> AppointmentClaimExaminers { get; set; } = null!;
    public DbSet<AppointmentPrimaryInsurance> AppointmentPrimaryInsurances { get; set; } = null!;
    public DbSet<AppointmentAccessor> AppointmentAccessors { get; set; } = null!;
    public DbSet<AppointmentEmployerDetail> AppointmentEmployerDetails { get; set; } = null!;
    public DbSet<Doctor> Doctors { get; set; } = null!;
    public DbSet<Appointment> Appointments { get; set; } = null!;
    public DbSet<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocument> AppointmentDocuments { get; set; } = null!;
    public DbSet<HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter> SystemParameters { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<PackageDetail> PackageDetails { get; set; } = null!;
    public DbSet<DocumentPackage> DocumentPackages { get; set; } = null!;
    public DbSet<CustomField> CustomFields { get; set; } = null!;
    public DbSet<CustomFieldValue> CustomFieldValues { get; set; } = null!;
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public DbSet<NotificationTemplateType> NotificationTemplateTypes { get; set; } = null!;
    public DbSet<Invitation> Invitations { get; set; } = null!;
    public DbSet<UserQuery> UserQueries { get; set; } = null!;
    public DbSet<AppointmentChangeRequest> AppointmentChangeRequests { get; set; } = null!;
    public DbSet<AppointmentChangeRequestDocument> AppointmentChangeRequestDocuments { get; set; } = null!;
    public DbSet<ChangeRequestConsentRound> ChangeRequestConsentRounds { get; set; } = null!;
    public DbSet<AppointmentInfoRequest> AppointmentInfoRequests { get; set; } = null!;
    public DbSet<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacket> AppointmentPackets { get; set; } = null!;
    public DbSet<DoctorAvailability> DoctorAvailabilities { get; set; } = null!;
    public DbSet<DoctorPreferredLocation> DoctorPreferredLocations { get; set; } = null!;
    public DbSet<AppointmentLanguage> AppointmentLanguages { get; set; } = null!;
    public DbSet<AppointmentStatus> AppointmentStatuses { get; set; } = null!;
    public DbSet<AppointmentDocumentType> AppointmentDocumentTypes { get; set; } = null!;
    public DbSet<AppointmentType> AppointmentTypes { get; set; } = null!;
    public DbSet<AppointmentTypeFieldConfig> AppointmentTypeFieldConfigs { get; set; } = null!;
    public DbSet<State> States { get; set; } = null!;
    public DbSet<AppointmentDraft> AppointmentDrafts { get; set; } = null!;
    // A2 (db-per-office): these live in the office DB so every FK resolves in-DB.
    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<WcabOffice> WcabOffices { get; set; } = null!;
    // QA item 7: per-office in-app notifications (one row per staff recipient).
    public DbSet<AppNotification> AppNotifications { get; set; } = null!;
    // Phase 2 (T9): durable per-recipient email outbox (delivery ledger).
    public DbSet<NotificationOutboxItem> NotificationOutboxItems { get; set; } = null!;
    // Case Tracker integration Part 1 (2026-07-27): outbound message ledger. Must be configured
    // here as well as in the host context, or office databases get no table at all.
    public DbSet<IntegrationOutboxItem> IntegrationOutboxItems { get; set; } = null!;

    public CaseEvaluationTenantDbContext(DbContextOptions<CaseEvaluationTenantDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.SetMultiTenancySide(MultiTenancySides.Tenant);
        base.OnModelCreating(builder);

        // Shared with CaseEvaluationDbContext -- see
        // CaseEvaluationSharedModelConfiguration for why it is declared once.
        builder.ConfigureCaseEvaluationShared();

        // The host gates these behind IsHostDatabase(); the office databases always
        // need them, so they are called unconditionally here.
        builder.ConfigureDoctor();

        // Declared only for the office databases. This MUST come before
        // ConfigureDoctorJoinEntities: WithOne() names no inverse navigation, so once the
        // join entities have declared the relationship in full, EF treats this as a second
        // relationship and adds a DoctorId1 shadow column.
        builder.Entity<Doctor>(b =>
        {
            b.HasMany(x => x.AppointmentTypes).WithOne().HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.NoAction);
            b.HasMany(x => x.Locations).WithOne().HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.NoAction);
        });

        builder.ConfigureDoctorJoinEntities();
        builder.ConfigurePatient();
    }
}
