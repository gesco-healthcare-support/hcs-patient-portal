using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
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
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.CustomFields;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentStatuses;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

[ConnectionStringName("Default")]
public class CaseEvaluationTenantDbContext : CaseEvaluationDbContextBase<CaseEvaluationTenantDbContext>
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
    public DbSet<AppointmentChangeRequest> AppointmentChangeRequests { get; set; } = null!;
    public DbSet<AppointmentChangeRequestDocument> AppointmentChangeRequestDocuments { get; set; } = null!;
    public DbSet<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacket> AppointmentPackets { get; set; } = null!;
    public DbSet<DoctorAvailability> DoctorAvailabilities { get; set; } = null!;
    public DbSet<AppointmentLanguage> AppointmentLanguages { get; set; } = null!;
    public DbSet<AppointmentStatus> AppointmentStatuses { get; set; } = null!;
    public DbSet<AppointmentType> AppointmentTypes { get; set; } = null!;
    public DbSet<State> States { get; set; } = null!;

    public CaseEvaluationTenantDbContext(DbContextOptions<CaseEvaluationTenantDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.SetMultiTenancySide(MultiTenancySides.Tenant);
        base.OnModelCreating(builder);
        builder.Entity<State>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "States", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).HasColumnName(nameof(State.Name)).IsRequired();
        });
        builder.Entity<AppointmentType>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentTypes", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).HasColumnName(nameof(AppointmentType.Name)).IsRequired().HasMaxLength(AppointmentTypeConsts.NameMaxLength);
            b.Property(x => x.Description).HasColumnName(nameof(AppointmentType.Description)).HasMaxLength(AppointmentTypeConsts.DescriptionMaxLength);
        });
        builder.Entity<AppointmentStatus>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentStatuses", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).HasColumnName(nameof(AppointmentStatus.Name)).IsRequired().HasMaxLength(AppointmentStatusConsts.NameMaxLength);
        });
        builder.Entity<AppointmentLanguage>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentLanguages", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).HasColumnName(nameof(AppointmentLanguage.Name)).IsRequired().HasMaxLength(AppointmentLanguageConsts.NameMaxLength);
        });
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
        builder.Entity<Doctor>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "Doctors", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(Doctor.TenantId));
            b.Property(x => x.FirstName).HasColumnName(nameof(Doctor.FirstName)).IsRequired().HasMaxLength(DoctorConsts.FirstNameMaxLength);
            b.Property(x => x.LastName).HasColumnName(nameof(Doctor.LastName)).IsRequired().HasMaxLength(DoctorConsts.LastNameMaxLength);
            b.Property(x => x.Email).HasColumnName(nameof(Doctor.Email)).IsRequired().HasMaxLength(DoctorConsts.EmailMaxLength);
            b.Property(x => x.Gender).HasColumnName(nameof(Doctor.Gender));
            b.HasMany(x => x.AppointmentTypes).WithOne().HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.NoAction);
            b.HasMany(x => x.Locations).WithOne().HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<DoctorAppointmentType>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "DoctorAppointmentType", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.HasKey(x => new { x.DoctorId, x.AppointmentTypeId });
            b.HasOne<Doctor>().WithMany(x => x.AppointmentTypes).HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.HasOne<AppointmentType>().WithMany().HasForeignKey(x => x.AppointmentTypeId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.DoctorId, x.AppointmentTypeId });
        });
        builder.Entity<DoctorLocation>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "DoctorLocation", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.HasKey(x => new { x.DoctorId, x.LocationId });
            b.HasOne<Doctor>().WithMany(x => x.Locations).HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.DoctorId, x.LocationId });
        });
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
            b.Property(x => x.OriginalAppointmentId).HasColumnName(nameof(Appointment.OriginalAppointmentId));
            b.Property(x => x.ReScheduleReason).HasColumnName(nameof(Appointment.ReScheduleReason)).HasMaxLength(AppointmentConsts.ReasonMaxLength);
            b.Property(x => x.ReScheduledById).HasColumnName(nameof(Appointment.ReScheduledById));
            b.Property(x => x.CancellationReason).HasColumnName(nameof(Appointment.CancellationReason)).HasMaxLength(AppointmentConsts.ReasonMaxLength);
            b.Property(x => x.CancelledById).HasColumnName(nameof(Appointment.CancelledById));
            b.Property(x => x.RejectionNotes).HasColumnName(nameof(Appointment.RejectionNotes)).HasMaxLength(AppointmentConsts.ReasonMaxLength);
            b.Property(x => x.RejectedById).HasColumnName(nameof(Appointment.RejectedById));
            b.Property(x => x.PrimaryResponsibleUserId).HasColumnName(nameof(Appointment.PrimaryResponsibleUserId));
            b.Property(x => x.IsBeyondLimit).HasColumnName(nameof(Appointment.IsBeyondLimit));
            b.HasOne<Patient>().WithMany().IsRequired().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppointmentType>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentTypeId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Location>().WithMany().IsRequired().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<DoctorAvailability>().WithMany().IsRequired().HasForeignKey(x => x.DoctorAvailabilityId).OnDelete(DeleteBehavior.NoAction);
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
            b.Property(x => x.Status).HasColumnName("Status").HasDefaultValue(HealthcareSupport.CaseEvaluation.AppointmentDocuments.DocumentStatus.Uploaded);
            b.Property(x => x.RejectionReason).HasColumnName("RejectionReason").HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacketConsts.RejectionReasonMaxLength);
            b.Property(x => x.ResponsibleUserId).HasColumnName("ResponsibleUserId");
            b.Property(x => x.RejectedByUserId).HasColumnName("RejectedByUserId");
            b.Property(x => x.IsAdHoc).HasColumnName("IsAdHoc");
            b.Property(x => x.IsJointDeclaration).HasColumnName("IsJointDeclaration");
            b.Property(x => x.VerificationCode).HasColumnName("VerificationCode");
            b.HasIndex(x => x.AppointmentId);
            b.HasIndex(x => new { x.AppointmentId, x.Status });
            b.HasIndex(x => x.VerificationCode);
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<Document>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "Documents", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.Name).HasColumnName(nameof(Document.Name)).IsRequired().HasMaxLength(DocumentConsts.NameMaxLength);
            b.Property(x => x.BlobName).HasColumnName(nameof(Document.BlobName)).IsRequired().HasMaxLength(DocumentConsts.BlobNameMaxLength);
            b.Property(x => x.ContentType).HasColumnName(nameof(Document.ContentType)).HasMaxLength(DocumentConsts.ContentTypeMaxLength);
            b.Property(x => x.IsActive).HasColumnName(nameof(Document.IsActive));
        });
        builder.Entity<PackageDetail>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "PackageDetails", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.PackageName).HasColumnName(nameof(PackageDetail.PackageName)).IsRequired().HasMaxLength(PackageDetailConsts.PackageNameMaxLength);
            b.Property(x => x.AppointmentTypeId).HasColumnName(nameof(PackageDetail.AppointmentTypeId));
            b.Property(x => x.IsActive).HasColumnName(nameof(PackageDetail.IsActive));
            b.HasMany(x => x.DocumentPackages).WithOne().HasForeignKey(x => x.PackageDetailId).IsRequired().OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<DocumentPackage>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "DocumentPackages", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.HasKey(x => new { x.PackageDetailId, x.DocumentId });
            b.Property(x => x.PackageDetailId).HasColumnName(nameof(DocumentPackage.PackageDetailId));
            b.Property(x => x.DocumentId).HasColumnName(nameof(DocumentPackage.DocumentId));
            b.Property(x => x.IsActive).HasColumnName(nameof(DocumentPackage.IsActive));
            b.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).IsRequired().OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<CustomField>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "CustomFields", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.FieldLabel).HasColumnName(nameof(CustomField.FieldLabel)).IsRequired().HasMaxLength(CustomFieldConsts.FieldLabelMaxLength);
            b.Property(x => x.DisplayOrder).HasColumnName(nameof(CustomField.DisplayOrder));
            b.Property(x => x.FieldType).HasColumnName(nameof(CustomField.FieldType)).HasConversion<int>();
            b.Property(x => x.FieldLength).HasColumnName(nameof(CustomField.FieldLength));
            b.Property(x => x.MultipleValues).HasColumnName(nameof(CustomField.MultipleValues)).HasMaxLength(CustomFieldConsts.MultipleValuesMaxLength);
            b.Property(x => x.DefaultValue).HasColumnName(nameof(CustomField.DefaultValue)).HasMaxLength(CustomFieldConsts.DefaultValueMaxLength);
            b.Property(x => x.IsMandatory).HasColumnName(nameof(CustomField.IsMandatory));
            b.Property(x => x.AppointmentTypeId).HasColumnName(nameof(CustomField.AppointmentTypeId));
            b.Property(x => x.IsActive).HasColumnName(nameof(CustomField.IsActive));
            b.HasIndex(x => new { x.TenantId, x.AppointmentTypeId, x.IsActive });
        });
        builder.Entity<CustomFieldValue>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "CustomFieldValues", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.CustomFieldId).HasColumnName(nameof(CustomFieldValue.CustomFieldId));
            b.Property(x => x.AppointmentId).HasColumnName(nameof(CustomFieldValue.AppointmentId));
            b.Property(x => x.Value).HasColumnName(nameof(CustomFieldValue.Value)).IsRequired().HasMaxLength(CustomFieldConsts.ValueMaxLength);
            b.HasIndex(x => x.AppointmentId);
            b.HasIndex(x => x.CustomFieldId);
            b.HasOne<CustomField>().WithMany().HasForeignKey(x => x.CustomFieldId).IsRequired().OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).IsRequired().OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<AppointmentChangeRequest>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentChangeRequests", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.AppointmentId).HasColumnName(nameof(AppointmentChangeRequest.AppointmentId)).IsRequired();
            b.Property(x => x.ChangeRequestType).HasColumnName(nameof(AppointmentChangeRequest.ChangeRequestType));
            b.Property(x => x.CancellationReason).HasColumnName(nameof(AppointmentChangeRequest.CancellationReason)).HasMaxLength(AppointmentChangeRequestConsts.ReasonMaxLength);
            b.Property(x => x.ReScheduleReason).HasColumnName(nameof(AppointmentChangeRequest.ReScheduleReason)).HasMaxLength(AppointmentChangeRequestConsts.ReasonMaxLength);
            b.Property(x => x.NewDoctorAvailabilityId).HasColumnName(nameof(AppointmentChangeRequest.NewDoctorAvailabilityId));
            b.Property(x => x.RequestStatus).HasColumnName(nameof(AppointmentChangeRequest.RequestStatus));
            b.Property(x => x.RejectionNotes).HasColumnName(nameof(AppointmentChangeRequest.RejectionNotes)).HasMaxLength(AppointmentChangeRequestConsts.ReasonMaxLength);
            b.Property(x => x.RejectedById).HasColumnName(nameof(AppointmentChangeRequest.RejectedById));
            b.Property(x => x.ApprovedById).HasColumnName(nameof(AppointmentChangeRequest.ApprovedById));
            b.Property(x => x.AdminReScheduleReason).HasColumnName(nameof(AppointmentChangeRequest.AdminReScheduleReason)).HasMaxLength(AppointmentChangeRequestConsts.ReasonMaxLength);
            b.Property(x => x.AdminOverrideSlotId).HasColumnName(nameof(AppointmentChangeRequest.AdminOverrideSlotId));
            b.Property(x => x.IsBeyondLimit).HasColumnName(nameof(AppointmentChangeRequest.IsBeyondLimit));
            b.Property(x => x.CancellationOutcome).HasColumnName(nameof(AppointmentChangeRequest.CancellationOutcome));
            b.HasIndex(x => x.AppointmentId);
            b.HasIndex(x => new { x.AppointmentId, x.RequestStatus });
            b.HasOne<Appointment>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<AppointmentChangeRequestDocument>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "AppointmentChangeRequestDocuments", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.AppointmentChangeRequestId).HasColumnName(nameof(AppointmentChangeRequestDocument.AppointmentChangeRequestId)).IsRequired();
            b.Property(x => x.DocumentName).HasColumnName(nameof(AppointmentChangeRequestDocument.DocumentName)).IsRequired().HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentConsts.DocumentNameMaxLength);
            b.Property(x => x.FileName).HasColumnName(nameof(AppointmentChangeRequestDocument.FileName)).IsRequired().HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentConsts.FileNameMaxLength);
            b.Property(x => x.BlobName).HasColumnName(nameof(AppointmentChangeRequestDocument.BlobName)).IsRequired().HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentConsts.BlobNameMaxLength);
            b.Property(x => x.ContentType).HasColumnName(nameof(AppointmentChangeRequestDocument.ContentType)).HasMaxLength(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentConsts.ContentTypeMaxLength);
            b.Property(x => x.FileSize).HasColumnName(nameof(AppointmentChangeRequestDocument.FileSize));
            b.Property(x => x.UploadedByUserId).HasColumnName(nameof(AppointmentChangeRequestDocument.UploadedByUserId));
            b.HasIndex(x => x.AppointmentChangeRequestId);
            b.HasOne<AppointmentChangeRequest>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentChangeRequestId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<NotificationTemplate>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "NotificationTemplates", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.TemplateCode).HasColumnName(nameof(NotificationTemplate.TemplateCode)).IsRequired().HasMaxLength(NotificationTemplateConsts.TemplateCodeMaxLength);
            b.Property(x => x.TemplateTypeId).HasColumnName(nameof(NotificationTemplate.TemplateTypeId)).IsRequired();
            b.Property(x => x.Subject).HasColumnName(nameof(NotificationTemplate.Subject)).HasMaxLength(NotificationTemplateConsts.SubjectMaxLength);
            b.Property(x => x.BodyEmail).HasColumnName(nameof(NotificationTemplate.BodyEmail)).IsRequired();
            b.Property(x => x.BodySms).HasColumnName(nameof(NotificationTemplate.BodySms)).IsRequired();
            b.Property(x => x.Description).HasColumnName(nameof(NotificationTemplate.Description)).HasMaxLength(NotificationTemplateConsts.DescriptionMaxLength);
            b.Property(x => x.IsActive).HasColumnName(nameof(NotificationTemplate.IsActive));
            b.HasIndex(x => new { x.TenantId, x.TemplateCode }).IsUnique();
            b.HasOne<NotificationTemplateType>().WithMany().HasForeignKey(x => x.TemplateTypeId).IsRequired().OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<NotificationTemplateType>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "NotificationTemplateTypes", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).HasColumnName(nameof(NotificationTemplateType.Name)).IsRequired().HasMaxLength(NotificationTemplateTypeConsts.NameMaxLength);
            b.Property(x => x.IsActive).HasColumnName(nameof(NotificationTemplateType.IsActive));
        });
        builder.Entity<HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter>(b =>
        {
            b.ToTable(CaseEvaluationConsts.DbTablePrefix + "SystemParameters", CaseEvaluationConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName("TenantId");
            b.Property(x => x.AppointmentLeadTime).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.AppointmentLeadTime));
            b.Property(x => x.AppointmentMaxTimePQME).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.AppointmentMaxTimePQME));
            b.Property(x => x.AppointmentMaxTimeAME).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.AppointmentMaxTimeAME));
            b.Property(x => x.AppointmentMaxTimeOTHER).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.AppointmentMaxTimeOTHER));
            b.Property(x => x.AppointmentCancelTime).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.AppointmentCancelTime));
            b.Property(x => x.AppointmentDueDays).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.AppointmentDueDays));
            b.Property(x => x.AppointmentDurationTime).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.AppointmentDurationTime));
            b.Property(x => x.AutoCancelCutoffTime).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.AutoCancelCutoffTime));
            b.Property(x => x.JointDeclarationUploadCutoffDays).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.JointDeclarationUploadCutoffDays));
            b.Property(x => x.PendingAppointmentOverDueNotificationDays).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.PendingAppointmentOverDueNotificationDays));
            b.Property(x => x.ReminderCutoffTime).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.ReminderCutoffTime));
            b.Property(x => x.IsCustomField).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.IsCustomField));
            b.Property(x => x.CcEmailIds).HasColumnName(nameof(HealthcareSupport.CaseEvaluation.SystemParameters.SystemParameter.CcEmailIds)).HasMaxLength(SystemParameterConsts.CcEmailIdsMaxLength);
            b.HasIndex(x => x.TenantId).IsUnique();
        });
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