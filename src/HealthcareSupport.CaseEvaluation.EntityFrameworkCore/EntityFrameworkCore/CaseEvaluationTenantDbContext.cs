using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
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
    public DbSet<AppointmentAccessor> AppointmentAccessors { get; set; } = null!;
    public DbSet<AppointmentEmployerDetail> AppointmentEmployerDetails { get; set; } = null!;
    public DbSet<Doctor> Doctors { get; set; } = null!;
    public DbSet<Appointment> Appointments { get; set; } = null!;
    public DbSet<AppointmentSendBackInfo> AppointmentSendBackInfos { get; set; } = null!;
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
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.SetNull);
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
    }
}