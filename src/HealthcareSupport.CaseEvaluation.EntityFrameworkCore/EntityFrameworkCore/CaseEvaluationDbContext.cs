using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using HealthcareSupport.CaseEvaluation.CustomFields;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Invitations;
using HealthcareSupport.CaseEvaluation.UserQueries;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using HealthcareSupport.CaseEvaluation.Doctors;
using Volo.Abp.Identity;
using Volo.Saas.Tenants;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.AppointmentStatuses;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.AppointmentDrafts;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

[ConnectionStringName("Default")]
public class CaseEvaluationDbContext : CaseEvaluationDbContextBase<CaseEvaluationDbContext>
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
    public DbSet<Appointment> Appointments { get; set; } = null!;
    public DbSet<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocument> AppointmentDocuments { get; set; } = null!;
    public DbSet<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacket> AppointmentPackets { get; set; } = null!;
    public DbSet<AppointmentTypeFieldConfig> AppointmentTypeFieldConfigs { get; set; } = null!;
    public DbSet<SystemParameter> SystemParameters { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<CustomField> CustomFields { get; set; } = null!;
    public DbSet<CustomFieldValue> CustomFieldValues { get; set; } = null!;
    public DbSet<PackageDetail> PackageDetails { get; set; } = null!;
    public DbSet<DocumentPackage> DocumentPackages { get; set; } = null!;
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public DbSet<NotificationTemplateType> NotificationTemplateTypes { get; set; } = null!;
    public DbSet<Invitation> Invitations { get; set; } = null!;
    public DbSet<UserQuery> UserQueries { get; set; } = null!;
    public DbSet<AppointmentChangeRequest> AppointmentChangeRequests { get; set; } = null!;
    public DbSet<AppointmentChangeRequestDocument> AppointmentChangeRequestDocuments { get; set; } = null!;
    public DbSet<ChangeRequestConsentRound> ChangeRequestConsentRounds { get; set; } = null!;
    public DbSet<AppointmentInfoRequest> AppointmentInfoRequests { get; set; } = null!;
    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<DoctorAvailability> DoctorAvailabilities { get; set; } = null!;
    public DbSet<DoctorPreferredLocation> DoctorPreferredLocations { get; set; } = null!;
    public DbSet<WcabOffice> WcabOffices { get; set; } = null!;
    public DbSet<Doctor> Doctors { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<AppointmentLanguage> AppointmentLanguages { get; set; } = null!;
    public DbSet<AppointmentStatus> AppointmentStatuses { get; set; } = null!;
    public DbSet<AppointmentDocumentType> AppointmentDocumentTypes { get; set; } = null!;
    public DbSet<AppointmentType> AppointmentTypes { get; set; } = null!;
    public DbSet<State> States { get; set; } = null!;
    public DbSet<AppointmentDraft> AppointmentDrafts { get; set; } = null!;
    // QA item 7: in-app notifications (IMultiTenant; mirrored here per the dual-DbContext convention).
    public DbSet<AppNotification> AppNotifications { get; set; } = null!;
    // Phase 2 (T9): durable per-recipient email outbox (delivery ledger).
    public DbSet<NotificationOutboxItem> NotificationOutboxItems { get; set; } = null!;
    // Case Tracker integration Part 1 (2026-07-27): outbound message ledger. IMultiTenant, so it
    // lives in BOTH the host and office DBs -- the office copy is the one that actually carries rows.
    public DbSet<IntegrationOutboxItem> IntegrationOutboxItems { get; set; } = null!;
    // Phase D (2026-06-25): host/management mapping of Intake operators to offices.
    public DbSet<IntakeOfficeAssignment> IntakeOfficeAssignments { get; set; } = null!;
    // Phase E (2026-06-25): host/management per-office branding (name + logo).
    public DbSet<OfficeBranding> OfficeBrandings { get; set; } = null!;

    public CaseEvaluationDbContext(DbContextOptions<CaseEvaluationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.SetMultiTenancySide(MultiTenancySides.Both);
        base.OnModelCreating(builder);

        // Shared with CaseEvaluationTenantDbContext -- see
        // CaseEvaluationSharedModelConfiguration for why it is declared once.
        builder.ConfigureCaseEvaluationShared();

        if (builder.IsHostDatabase())
        {
            builder.ConfigureDoctor();

            // The Saas Tenant table exists only in the host database, so these two
            // foreign keys cannot live in the shared configuration.
            builder.Entity<Doctor>(b =>
            {
                b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.ConfigureDoctorJoinEntities();

            builder.ConfigurePatient();
            builder.Entity<Patient>(b =>
            {
                b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            });

            // Phase D (2026-06-25): host/management table linking a host Intake
            // operator to the offices they may enter. Host-only (inside this
            // IsHostDatabase block) -- it must never live in an office DB. No FK
            // navigation: the app service validates the operator + office exist
            // before inserting; the (OperatorUserId, OfficeId) unique index backs
            // idempotent assign / unassign.
            builder.Entity<IntakeOfficeAssignment>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "IntakeOfficeAssignments", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.OperatorUserId).HasColumnName(nameof(IntakeOfficeAssignment.OperatorUserId)).IsRequired();
                b.Property(x => x.OfficeId).HasColumnName(nameof(IntakeOfficeAssignment.OfficeId)).IsRequired();
                b.HasIndex(x => new { x.OperatorUserId, x.OfficeId })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0")
                    .HasDatabaseName("IX_AppEntity_IntakeOfficeAssignments_Operator_Office");
            });

            // Phase E (2026-06-25): host/management per-office branding (name +
            // logo). Host-only (inside this IsHostDatabase block) so the login
            // page + the host-side central manager resolve an office's brand
            // without an office-DB hop. One row per office; the unique index on
            // OfficeId backs upsert-by-office.
            builder.Entity<OfficeBranding>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "OfficeBrandings", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.OfficeId).HasColumnName(nameof(OfficeBranding.OfficeId)).IsRequired();
                b.Property(x => x.DisplayName).HasColumnName(nameof(OfficeBranding.DisplayName)).HasMaxLength(OfficeBranding.DisplayNameMaxLength);
                b.Property(x => x.LogoBlobName).HasColumnName(nameof(OfficeBranding.LogoBlobName)).HasMaxLength(OfficeBranding.LogoBlobNameMaxLength);
                b.Property(x => x.LogoContentType).HasColumnName(nameof(OfficeBranding.LogoContentType)).HasMaxLength(OfficeBranding.LogoContentTypeMaxLength);
                b.HasIndex(x => x.OfficeId)
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0")
                    .HasDatabaseName("IX_AppEntity_OfficeBrandings_Office");
            });
        }
    }
}
