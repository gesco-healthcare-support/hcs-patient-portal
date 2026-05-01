using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
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
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using HealthcareSupport.CaseEvaluation.Doctors;
using Volo.Abp.Identity;
using Volo.Saas.Tenants;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.AppointmentStatuses;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.States;
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
    public DbSet<AppointmentDefenseAttorney> AppointmentDefenseAttorneys { get; set; } = null!;
    public DbSet<DefenseAttorney> DefenseAttorneys { get; set; } = null!;
    public DbSet<AppointmentInjuryDetail> AppointmentInjuryDetails { get; set; } = null!;
    public DbSet<AppointmentBodyPart> AppointmentBodyParts { get; set; } = null!;
    public DbSet<AppointmentClaimExaminer> AppointmentClaimExaminers { get; set; } = null!;
    public DbSet<AppointmentPrimaryInsurance> AppointmentPrimaryInsurances { get; set; } = null!;
    public DbSet<AppointmentAccessor> AppointmentAccessors { get; set; } = null!;
    public DbSet<AppointmentEmployerDetail> AppointmentEmployerDetails { get; set; } = null!;
    public DbSet<Appointment> Appointments { get; set; } = null!;
    public DbSet<AppointmentSendBackInfo> AppointmentSendBackInfos { get; set; } = null!;
    public DbSet<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocument> AppointmentDocuments { get; set; } = null!;
    public DbSet<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacket> AppointmentPackets { get; set; } = null!;
    public DbSet<AppointmentTypeFieldConfig> AppointmentTypeFieldConfigs { get; set; } = null!;
    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<DoctorAvailability> DoctorAvailabilities { get; set; } = null!;
    public DbSet<WcabOffice> WcabOffices { get; set; } = null!;
    public DbSet<Doctor> Doctors { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<AppointmentLanguage> AppointmentLanguages { get; set; } = null!;
    public DbSet<AppointmentStatus> AppointmentStatuses { get; set; } = null!;
    public DbSet<AppointmentType> AppointmentTypes { get; set; } = null!;
    public DbSet<State> States { get; set; } = null!;

    public CaseEvaluationDbContext(DbContextOptions<CaseEvaluationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.SetMultiTenancySide(MultiTenancySides.Both);
        base.OnModelCreating(builder);

        if (builder.IsHostDatabase())
        {
            builder.Entity<Location>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "Locations", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).HasColumnName(nameof(Location.Name)).IsRequired().HasMaxLength(LocationConsts.NameMaxLength);
                b.Property(x => x.Address).HasColumnName(nameof(Location.Address)).HasMaxLength(LocationConsts.AddressMaxLength);
                b.Property(x => x.City).HasColumnName(nameof(Location.City)).HasMaxLength(LocationConsts.CityMaxLength);
                b.Property(x => x.ZipCode).HasColumnName(nameof(Location.ZipCode)).HasMaxLength(LocationConsts.ZipCodeMaxLength);
                b.Property(x => x.ParkingFee).HasColumnName(nameof(Location.ParkingFee));
                b.Property(x => x.IsActive).HasColumnName(nameof(Location.IsActive));
                b.HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.SetNull);
                b.HasOne<AppointmentType>().WithMany().HasForeignKey(x => x.AppointmentTypeId).OnDelete(DeleteBehavior.SetNull);
            });
        }

        if (builder.IsHostDatabase())
        {
            builder.Entity<WcabOffice>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "WcabOffices", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).HasColumnName(nameof(WcabOffice.Name)).IsRequired().HasMaxLength(WcabOfficeConsts.NameMaxLength);
                b.Property(x => x.Abbreviation).HasColumnName(nameof(WcabOffice.Abbreviation)).IsRequired().HasMaxLength(WcabOfficeConsts.AbbreviationMaxLength);
                b.Property(x => x.Address).HasColumnName(nameof(WcabOffice.Address)).HasMaxLength(WcabOfficeConsts.AddressMaxLength);
                b.Property(x => x.City).HasColumnName(nameof(WcabOffice.City)).HasMaxLength(WcabOfficeConsts.CityMaxLength);
                b.Property(x => x.ZipCode).HasColumnName(nameof(WcabOffice.ZipCode)).HasMaxLength(WcabOfficeConsts.ZipCodeMaxLength);
                b.Property(x => x.IsActive).HasColumnName(nameof(WcabOffice.IsActive));
                b.HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.SetNull);
            });
        }

        if (builder.IsHostDatabase())
        {
            builder.Entity<Doctor>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "Doctors", CaseEvaluationConsts.DbSchema);
                b.Property(x => x.TenantId).HasColumnName(nameof(Doctor.TenantId));
                b.ConfigureByConvention();
                b.Property(x => x.FirstName).HasColumnName(nameof(Doctor.FirstName)).IsRequired().HasMaxLength(DoctorConsts.FirstNameMaxLength);
                b.Property(x => x.LastName).HasColumnName(nameof(Doctor.LastName)).IsRequired().HasMaxLength(DoctorConsts.LastNameMaxLength);
                b.Property(x => x.Email).HasColumnName(nameof(Doctor.Email)).IsRequired().HasMaxLength(DoctorConsts.EmailMaxLength);
                b.Property(x => x.Gender).HasColumnName(nameof(Doctor.Gender));
                b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.SetNull);
                b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            });
            builder.Entity<DoctorAppointmentType>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "DoctorAppointmentType", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.HasKey(x => new { x.DoctorId, x.AppointmentTypeId });
                b.HasOne(x => x.Doctor).WithMany(x => x.AppointmentTypes).HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.AppointmentType).WithMany(x => x.DoctorAppointmentTypes).HasForeignKey(x => x.AppointmentTypeId).IsRequired().OnDelete(DeleteBehavior.Cascade);
                b.HasIndex(x => new { x.DoctorId, x.AppointmentTypeId });
            });
            builder.Entity<DoctorLocation>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "DoctorLocation", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.HasKey(x => new { x.DoctorId, x.LocationId });
                b.HasOne(x => x.Doctor).WithMany(x => x.Locations).HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.Location).WithMany(x => x.DoctorLocations).HasForeignKey(x => x.LocationId).IsRequired().OnDelete(DeleteBehavior.Cascade);
                b.HasIndex(x => new { x.DoctorId, x.LocationId });
            });
        }

        if (builder.IsHostDatabase())
        {
            builder.Entity<AppointmentStatus>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentStatuses", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).HasColumnName(nameof(AppointmentStatus.Name)).IsRequired().HasMaxLength(AppointmentStatusConsts.NameMaxLength);
            });
        }

        if (builder.IsHostDatabase())
        {
            builder.Entity<AppointmentType>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentTypes", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).HasColumnName(nameof(AppointmentType.Name)).IsRequired().HasMaxLength(AppointmentTypeConsts.NameMaxLength);
                b.Property(x => x.Description).HasColumnName(nameof(AppointmentType.Description)).HasMaxLength(AppointmentTypeConsts.DescriptionMaxLength);
            });
        }

        if (builder.IsHostDatabase())
        {
            builder.Entity<AppointmentLanguage>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentLanguages", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).HasColumnName(nameof(AppointmentLanguage.Name)).IsRequired().HasMaxLength(AppointmentLanguageConsts.NameMaxLength);
            });
        }

        builder.Entity<DoctorAvailability>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "DoctorAvailabilities", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(DoctorAvailability.TenantId));
            b.Property(x => x.AvailableDate).HasColumnName(nameof(DoctorAvailability.AvailableDate));
            b.Property(x => x.FromTime).HasColumnName(nameof(DoctorAvailability.FromTime));
            b.Property(x => x.ToTime).HasColumnName(nameof(DoctorAvailability.ToTime));
            b.Property(x => x.BookingStatusId).HasColumnName(nameof(DoctorAvailability.BookingStatusId));
            b.HasOne<Location>().WithMany().IsRequired().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppointmentType>().WithMany().HasForeignKey(x => x.AppointmentTypeId).OnDelete(DeleteBehavior.SetNull);
        });
        if (builder.IsHostDatabase())
        {
            builder.Entity<Patient>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "Patients", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.FirstName).HasColumnName(nameof(Patient.FirstName)).IsRequired().HasMaxLength(PatientConsts.FirstNameMaxLength);
                b.Property(x => x.LastName).HasColumnName(nameof(Patient.LastName)).IsRequired().HasMaxLength(PatientConsts.LastNameMaxLength);
                b.Property(x => x.MiddleName).HasColumnName(nameof(Patient.MiddleName)).HasMaxLength(PatientConsts.MiddleNameMaxLength);
                b.Property(x => x.Email).HasColumnName(nameof(Patient.Email)).IsRequired().HasMaxLength(PatientConsts.EmailMaxLength);
                b.Property(x => x.GenderId).HasColumnName(nameof(Patient.GenderId));
                b.Property(x => x.DateOfBirth).HasColumnName(nameof(Patient.DateOfBirth));
                b.Property(x => x.PhoneNumber).HasColumnName(nameof(Patient.PhoneNumber)).HasMaxLength(PatientConsts.PhoneNumberMaxLength);
                b.Property(x => x.SocialSecurityNumber).HasColumnName(nameof(Patient.SocialSecurityNumber)).HasMaxLength(PatientConsts.SocialSecurityNumberMaxLength);
                b.Property(x => x.Address).HasColumnName(nameof(Patient.Address)).HasMaxLength(PatientConsts.AddressMaxLength);
                b.Property(x => x.City).HasColumnName(nameof(Patient.City)).HasMaxLength(PatientConsts.CityMaxLength);
                b.Property(x => x.ZipCode).HasColumnName(nameof(Patient.ZipCode)).HasMaxLength(PatientConsts.ZipCodeMaxLength);
                b.Property(x => x.RefferedBy).HasColumnName(nameof(Patient.RefferedBy)).HasMaxLength(PatientConsts.RefferedByMaxLength);
                b.Property(x => x.CellPhoneNumber).HasColumnName(nameof(Patient.CellPhoneNumber)).HasMaxLength(PatientConsts.CellPhoneNumberMaxLength);
                b.Property(x => x.PhoneNumberTypeId).HasColumnName(nameof(Patient.PhoneNumberTypeId));
                b.Property(x => x.Street).HasColumnName(nameof(Patient.Street)).HasMaxLength(PatientConsts.StreetMaxLength);
                b.Property(x => x.InterpreterVendorName).HasColumnName(nameof(Patient.InterpreterVendorName)).HasMaxLength(PatientConsts.InterpreterVendorNameMaxLength);
                b.Property(x => x.ApptNumber).HasColumnName(nameof(Patient.ApptNumber)).HasMaxLength(PatientConsts.ApptNumberMaxLength);
                b.Property(x => x.OthersLanguageName).HasColumnName(nameof(Patient.OthersLanguageName)).HasMaxLength(PatientConsts.OthersLanguageNameMaxLength);
                b.HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.SetNull);
                b.HasOne<AppointmentLanguage>().WithMany().HasForeignKey(x => x.AppointmentLanguageId).OnDelete(DeleteBehavior.SetNull);
                b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            });
        }

        builder.Entity<Appointment>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "Appointments", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(Appointment.TenantId));
            b.Property(x => x.PanelNumber).HasColumnName(nameof(Appointment.PanelNumber)).HasMaxLength(AppointmentConsts.PanelNumberMaxLength);
            b.Property(x => x.AppointmentDate).HasColumnName(nameof(Appointment.AppointmentDate));
            b.Property(x => x.IsPatientAlreadyExist).HasColumnName(nameof(Appointment.IsPatientAlreadyExist));
            b.Property(x => x.RequestConfirmationNumber).HasColumnName(nameof(Appointment.RequestConfirmationNumber)).IsRequired().HasMaxLength(AppointmentConsts.RequestConfirmationNumberMaxLength);
            b.Property(x => x.DueDate).HasColumnName(nameof(Appointment.DueDate));
            b.Property(x => x.InternalUserComments).HasColumnName(nameof(Appointment.InternalUserComments)).HasMaxLength(AppointmentConsts.InternalUserCommentsMaxLength);
            b.Property(x => x.AppointmentApproveDate).HasColumnName(nameof(Appointment.AppointmentApproveDate));
            b.Property(x => x.AppointmentStatus).HasColumnName(nameof(Appointment.AppointmentStatus));
            b.Property(x => x.PatientEmail).HasColumnName(nameof(Appointment.PatientEmail)).HasMaxLength(AppointmentConsts.PartyEmailMaxLength);
            b.Property(x => x.ApplicantAttorneyEmail).HasColumnName(nameof(Appointment.ApplicantAttorneyEmail)).HasMaxLength(AppointmentConsts.PartyEmailMaxLength);
            b.Property(x => x.DefenseAttorneyEmail).HasColumnName(nameof(Appointment.DefenseAttorneyEmail)).HasMaxLength(AppointmentConsts.PartyEmailMaxLength);
            b.Property(x => x.ClaimExaminerEmail).HasColumnName(nameof(Appointment.ClaimExaminerEmail)).HasMaxLength(AppointmentConsts.PartyEmailMaxLength);
            b.HasOne<Patient>().WithMany().IsRequired().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppointmentType>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentTypeId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Location>().WithMany().IsRequired().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<DoctorAvailability>().WithMany().IsRequired().HasForeignKey(x => x.DoctorAvailabilityId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<AppointmentSendBackInfo>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentSendBackInfos", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentSendBackInfo.TenantId));
            b.Property(x => x.AppointmentId).HasColumnName(nameof(AppointmentSendBackInfo.AppointmentId)).IsRequired();
            b.Property(x => x.FlaggedFieldsJson).HasColumnName(nameof(AppointmentSendBackInfo.FlaggedFieldsJson)).IsRequired();
            b.Property(x => x.Note).HasColumnName(nameof(AppointmentSendBackInfo.Note)).HasMaxLength(2000);
            b.Property(x => x.SentBackAt).HasColumnName(nameof(AppointmentSendBackInfo.SentBackAt)).IsRequired();
            b.Property(x => x.SentBackByUserId).HasColumnName(nameof(AppointmentSendBackInfo.SentBackByUserId));
            b.Property(x => x.IsResolved).HasColumnName(nameof(AppointmentSendBackInfo.IsResolved));
            b.Property(x => x.ResolvedAt).HasColumnName(nameof(AppointmentSendBackInfo.ResolvedAt));
            b.HasIndex(x => x.AppointmentId);
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocument>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentDocuments", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.AppointmentId).HasColumnName("AppointmentId").IsRequired();
            b.Property(x => x.DocumentName).HasColumnName("DocumentName").IsRequired().HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentConsts.DocumentNameMaxLength);
            b.Property(x => x.FileName).HasColumnName("FileName").IsRequired().HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentConsts.FileNameMaxLength);
            b.Property(x => x.BlobName).HasColumnName("BlobName").IsRequired().HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentConsts.BlobNameMaxLength);
            b.Property(x => x.ContentType).HasColumnName("ContentType").HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentConsts.ContentTypeMaxLength);
            b.Property(x => x.FileSize).HasColumnName("FileSize");
            b.Property(x => x.UploadedByUserId).HasColumnName("UploadedByUserId");
            // W2-11: review-state columns.
            b.Property(x => x.Status).HasColumnName("Status").HasDefaultValue(HealthcareSupport.CaseEvaluation.AppointmentDocuments.DocumentStatus.Uploaded);
            b.Property(x => x.RejectionReason).HasColumnName("RejectionReason").HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacketConsts.RejectionReasonMaxLength);
            b.Property(x => x.ResponsibleUserId).HasColumnName("ResponsibleUserId");
            b.Property(x => x.RejectedByUserId).HasColumnName("RejectedByUserId");
            b.HasIndex(x => x.AppointmentId);
            b.HasIndex(x => new { x.AppointmentId, x.Status });
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        });

        // W2-11: AppointmentPacket -- per-appointment merged-PDF metadata row.
        builder.Entity<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacket>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentPackets", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.AppointmentId).HasColumnName("AppointmentId").IsRequired();
            b.Property(x => x.BlobName).HasColumnName("BlobName").IsRequired().HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacketConsts.BlobNameMaxLength);
            b.Property(x => x.Status).HasColumnName("Status");
            b.Property(x => x.GeneratedAt).HasColumnName("GeneratedAt");
            b.Property(x => x.RegeneratedAt).HasColumnName("RegeneratedAt");
            b.Property(x => x.ErrorMessage).HasColumnName("ErrorMessage").HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacketConsts.ErrorMessageMaxLength);
            b.HasIndex(x => x.AppointmentId);
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        });

        // W2-5: per-AppointmentType field-config table.
        builder.Entity<AppointmentTypeFieldConfig>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentTypeFieldConfigs", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentTypeFieldConfig.TenantId));
            b.Property(x => x.AppointmentTypeId).HasColumnName(nameof(AppointmentTypeFieldConfig.AppointmentTypeId)).IsRequired();
            b.Property(x => x.FieldName).HasColumnName(nameof(AppointmentTypeFieldConfig.FieldName)).IsRequired().HasMaxLength(AppointmentTypeFieldConfigConsts.FieldNameMaxLength);
            b.Property(x => x.Hidden).HasColumnName(nameof(AppointmentTypeFieldConfig.Hidden));
            b.Property(x => x.ReadOnly).HasColumnName(nameof(AppointmentTypeFieldConfig.ReadOnly));
            b.Property(x => x.DefaultValue).HasColumnName(nameof(AppointmentTypeFieldConfig.DefaultValue)).HasMaxLength(AppointmentTypeFieldConfigConsts.DefaultValueMaxLength);
            b.HasIndex(x => new { x.TenantId, x.AppointmentTypeId, x.FieldName }).IsUnique();
            b.HasOne<AppointmentType>().WithMany().HasForeignKey(x => x.AppointmentTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        if (builder.IsHostDatabase())
        {
            builder.Entity<State>(b =>
            {
                b.ToTable(CaseEvaluationConsts.DbTablePrefix + "States", CaseEvaluationConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).HasColumnName(nameof(State.Name)).IsRequired();
            });
        }

        builder.Entity<AppointmentEmployerDetail>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentEmployerDetails", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentEmployerDetail.TenantId));
            b.Property(x => x.EmployerName).HasColumnName(nameof(AppointmentEmployerDetail.EmployerName)).IsRequired().HasMaxLength(AppointmentEmployerDetailConsts.EmployerNameMaxLength);
            b.Property(x => x.Occupation).HasColumnName(nameof(AppointmentEmployerDetail.Occupation)).IsRequired().HasMaxLength(AppointmentEmployerDetailConsts.OccupationMaxLength);
            b.Property(x => x.PhoneNumber).HasColumnName(nameof(AppointmentEmployerDetail.PhoneNumber)).HasMaxLength(AppointmentEmployerDetailConsts.PhoneNumberMaxLength);
            b.Property(x => x.Street).HasColumnName(nameof(AppointmentEmployerDetail.Street)).HasMaxLength(AppointmentEmployerDetailConsts.StreetMaxLength);
            b.Property(x => x.City).HasColumnName(nameof(AppointmentEmployerDetail.City)).HasMaxLength(AppointmentEmployerDetailConsts.CityMaxLength);
            b.Property(x => x.ZipCode).HasColumnName(nameof(AppointmentEmployerDetail.ZipCode)).HasMaxLength(AppointmentEmployerDetailConsts.ZipCodeMaxLength);
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<AppointmentAccessor>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentAccessors", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentAccessor.TenantId));
            b.Property(x => x.AccessTypeId).HasColumnName(nameof(AppointmentAccessor.AccessTypeId));
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<ApplicantAttorney>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "ApplicantAttorneys", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(ApplicantAttorney.TenantId));
            b.Property(x => x.FirmName).HasColumnName(nameof(ApplicantAttorney.FirmName)).HasMaxLength(ApplicantAttorneyConsts.FirmNameMaxLength);
            b.Property(x => x.FirmAddress).HasColumnName(nameof(ApplicantAttorney.FirmAddress)).HasMaxLength(ApplicantAttorneyConsts.FirmAddressMaxLength);
            b.Property(x => x.WebAddress).HasColumnName(nameof(ApplicantAttorney.WebAddress)).HasMaxLength(ApplicantAttorneyConsts.WebAddressMaxLength);
            b.Property(x => x.PhoneNumber).HasColumnName(nameof(ApplicantAttorney.PhoneNumber)).HasMaxLength(ApplicantAttorneyConsts.PhoneNumberMaxLength);
            b.Property(x => x.FaxNumber).HasColumnName(nameof(ApplicantAttorney.FaxNumber)).HasMaxLength(ApplicantAttorneyConsts.FaxNumberMaxLength);
            b.Property(x => x.Street).HasColumnName(nameof(ApplicantAttorney.Street)).HasMaxLength(ApplicantAttorneyConsts.StreetMaxLength);
            b.Property(x => x.City).HasColumnName(nameof(ApplicantAttorney.City)).HasMaxLength(ApplicantAttorneyConsts.CityMaxLength);
            b.Property(x => x.ZipCode).HasColumnName(nameof(ApplicantAttorney.ZipCode)).HasMaxLength(ApplicantAttorneyConsts.ZipCodeMaxLength);
            b.HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<AppointmentApplicantAttorney>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentApplicantAttorneys", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentApplicantAttorney.TenantId));
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<ApplicantAttorney>().WithMany().IsRequired().HasForeignKey(x => x.ApplicantAttorneyId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<DefenseAttorney>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "DefenseAttorneys", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(DefenseAttorney.TenantId));
            b.Property(x => x.FirmName).HasColumnName(nameof(DefenseAttorney.FirmName)).HasMaxLength(DefenseAttorneyConsts.FirmNameMaxLength);
            b.Property(x => x.FirmAddress).HasColumnName(nameof(DefenseAttorney.FirmAddress)).HasMaxLength(DefenseAttorneyConsts.FirmAddressMaxLength);
            b.Property(x => x.WebAddress).HasColumnName(nameof(DefenseAttorney.WebAddress)).HasMaxLength(DefenseAttorneyConsts.WebAddressMaxLength);
            b.Property(x => x.PhoneNumber).HasColumnName(nameof(DefenseAttorney.PhoneNumber)).HasMaxLength(DefenseAttorneyConsts.PhoneNumberMaxLength);
            b.Property(x => x.FaxNumber).HasColumnName(nameof(DefenseAttorney.FaxNumber)).HasMaxLength(DefenseAttorneyConsts.FaxNumberMaxLength);
            b.Property(x => x.Street).HasColumnName(nameof(DefenseAttorney.Street)).HasMaxLength(DefenseAttorneyConsts.StreetMaxLength);
            b.Property(x => x.City).HasColumnName(nameof(DefenseAttorney.City)).HasMaxLength(DefenseAttorneyConsts.CityMaxLength);
            b.Property(x => x.ZipCode).HasColumnName(nameof(DefenseAttorney.ZipCode)).HasMaxLength(DefenseAttorneyConsts.ZipCodeMaxLength);
            b.HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<AppointmentDefenseAttorney>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentDefenseAttorneys", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentDefenseAttorney.TenantId));
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<DefenseAttorney>().WithMany().IsRequired().HasForeignKey(x => x.DefenseAttorneyId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<AppointmentInjuryDetail>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentInjuryDetails", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentInjuryDetail.TenantId));
            b.Property(x => x.DateOfInjury).HasColumnType("date");
            b.Property(x => x.ToDateOfInjury).HasColumnType("date");
            b.Property(x => x.ClaimNumber).HasColumnName(nameof(AppointmentInjuryDetail.ClaimNumber)).IsRequired().HasMaxLength(AppointmentInjuryDetailConsts.ClaimNumberMaxLength);
            b.Property(x => x.WcabAdj).HasColumnName(nameof(AppointmentInjuryDetail.WcabAdj)).HasMaxLength(AppointmentInjuryDetailConsts.WcabAdjMaxLength);
            b.Property(x => x.BodyPartsSummary).HasColumnName(nameof(AppointmentInjuryDetail.BodyPartsSummary)).IsRequired().HasMaxLength(AppointmentInjuryDetailConsts.BodyPartsSummaryMaxLength);
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<WcabOffice>().WithMany().HasForeignKey(x => x.WcabOfficeId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<AppointmentBodyPart>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentBodyParts", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentBodyPart.TenantId));
            b.Property(x => x.BodyPartDescription).HasColumnName(nameof(AppointmentBodyPart.BodyPartDescription)).IsRequired().HasMaxLength(AppointmentBodyPartConsts.BodyPartDescriptionMaxLength);
            b.HasOne<AppointmentInjuryDetail>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentInjuryDetailId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<AppointmentClaimExaminer>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentClaimExaminers", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentClaimExaminer.TenantId));
            b.Property(x => x.Name).HasColumnName(nameof(AppointmentClaimExaminer.Name)).HasMaxLength(AppointmentClaimExaminerConsts.NameMaxLength);
            b.Property(x => x.ClaimExaminerNumber).HasColumnName(nameof(AppointmentClaimExaminer.ClaimExaminerNumber)).HasMaxLength(AppointmentClaimExaminerConsts.ClaimExaminerNumberMaxLength);
            b.Property(x => x.Email).HasColumnName(nameof(AppointmentClaimExaminer.Email)).HasMaxLength(AppointmentClaimExaminerConsts.EmailMaxLength);
            b.Property(x => x.PhoneNumber).HasColumnName(nameof(AppointmentClaimExaminer.PhoneNumber)).HasMaxLength(AppointmentClaimExaminerConsts.PhoneNumberMaxLength);
            b.Property(x => x.Fax).HasColumnName(nameof(AppointmentClaimExaminer.Fax)).HasMaxLength(AppointmentClaimExaminerConsts.FaxMaxLength);
            b.Property(x => x.Street).HasColumnName(nameof(AppointmentClaimExaminer.Street)).HasMaxLength(AppointmentClaimExaminerConsts.StreetMaxLength);
            b.Property(x => x.City).HasColumnName(nameof(AppointmentClaimExaminer.City)).HasMaxLength(AppointmentClaimExaminerConsts.CityMaxLength);
            b.Property(x => x.Zip).HasColumnName(nameof(AppointmentClaimExaminer.Zip)).HasMaxLength(AppointmentClaimExaminerConsts.ZipMaxLength);
            b.HasOne<AppointmentInjuryDetail>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentInjuryDetailId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<AppointmentPrimaryInsurance>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentPrimaryInsurances", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentPrimaryInsurance.TenantId));
            b.Property(x => x.Name).HasColumnName(nameof(AppointmentPrimaryInsurance.Name)).HasMaxLength(AppointmentPrimaryInsuranceConsts.NameMaxLength);
            b.Property(x => x.InsuranceNumber).HasColumnName(nameof(AppointmentPrimaryInsurance.InsuranceNumber)).HasMaxLength(AppointmentPrimaryInsuranceConsts.InsuranceNumberMaxLength);
            b.Property(x => x.Attention).HasColumnName(nameof(AppointmentPrimaryInsurance.Attention)).HasMaxLength(AppointmentPrimaryInsuranceConsts.AttentionMaxLength);
            b.Property(x => x.PhoneNumber).HasColumnName(nameof(AppointmentPrimaryInsurance.PhoneNumber)).HasMaxLength(AppointmentPrimaryInsuranceConsts.PhoneNumberMaxLength);
            b.Property(x => x.FaxNumber).HasColumnName(nameof(AppointmentPrimaryInsurance.FaxNumber)).HasMaxLength(AppointmentPrimaryInsuranceConsts.FaxNumberMaxLength);
            b.Property(x => x.Street).HasColumnName(nameof(AppointmentPrimaryInsurance.Street)).HasMaxLength(AppointmentPrimaryInsuranceConsts.StreetMaxLength);
            b.Property(x => x.City).HasColumnName(nameof(AppointmentPrimaryInsurance.City)).HasMaxLength(AppointmentPrimaryInsuranceConsts.CityMaxLength);
            b.Property(x => x.Zip).HasColumnName(nameof(AppointmentPrimaryInsurance.Zip)).HasMaxLength(AppointmentPrimaryInsuranceConsts.ZipMaxLength);
            b.HasOne<AppointmentInjuryDetail>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentInjuryDetailId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}