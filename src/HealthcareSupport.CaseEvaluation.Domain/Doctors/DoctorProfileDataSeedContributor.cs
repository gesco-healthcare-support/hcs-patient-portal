using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Doctors;

/// <summary>
/// Seeds the single office-owner doctor into a freshly provisioned office database.
/// tenant === doctor: exactly one doctor per office (enforced by
/// IX_AppEntity_Doctors_TenantId_Unique), so this runs per office (tenant scope) and
/// is a no-op in the host database and on re-seed. The doctor's email is the office
/// admin's email (the owner). It is linked to all of the office's seeded catalogs
/// (appointment types + locations) by their stable seed GUIDs -- referencing the
/// GUIDs (instead of querying) keeps the link rows and the catalog rows in one
/// FK-safe batch at the end-of-unit-of-work SaveChanges, since with autoSave:false
/// the catalog rows are not yet flushed to query during seeding.
/// </summary>
public class DoctorProfileDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Doctor, Guid> _doctorRepository;
    private readonly ITenantStore _tenantStore;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<DoctorProfileDataSeedContributor> _logger;

    public DoctorProfileDataSeedContributor(
        IRepository<Doctor, Guid> doctorRepository,
        ITenantStore tenantStore,
        IGuidGenerator guidGenerator,
        ILogger<DoctorProfileDataSeedContributor> logger)
    {
        _doctorRepository = doctorRepository;
        _tenantStore = tenantStore;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Per-office only: a doctor belongs to an office database, never the host.
        if (context?.TenantId == null)
        {
            return;
        }

        // One doctor per office (the owner). Idempotent: skip if already seeded.
        if (await _doctorRepository.GetCountAsync() > 0)
        {
            return;
        }

        var adminEmail = context[IdentityDataSeedContributor.AdminEmailPropertyName] as string;
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            _logger.LogWarning(
                "DoctorProfileDataSeedContributor: no admin email in the seed context for tenant {TenantId}; skipping doctor seed.",
                context.TenantId);
            return;
        }

        var tenant = await _tenantStore.FindAsync(context.TenantId.Value);

        var ctxFirstName = context[OfficeSeedProperties.DoctorFirstName] as string;
        var ctxLastName = context[OfficeSeedProperties.DoctorLastName] as string;
        var ctxEmail = context[OfficeSeedProperties.DoctorEmail] as string;
        var office = Saas.OfficeSeedData.FindByTenantName(tenant?.Name);

        // Owner-doctor precedence:
        //  1. New Practice create flow -> the real doctor entered on the form (seed context).
        //  2. Seed config -> the OfficeSeedData doctor.
        //  3. Placeholder -> the tenant name is the doctor's LAST name (an office is
        //     generally named for the doctor's surname, not first name), with the admin
        //     email as a stand-in contact.
        Doctor doctor;
        if (!string.IsNullOrWhiteSpace(ctxFirstName) ||
            !string.IsNullOrWhiteSpace(ctxLastName) ||
            !string.IsNullOrWhiteSpace(ctxEmail))
        {
            doctor = new Doctor(
                id: _guidGenerator.Create(),
                firstName: ctxFirstName?.Trim() ?? string.Empty,
                lastName: ctxLastName?.Trim() ?? string.Empty,
                email: string.IsNullOrWhiteSpace(ctxEmail) ? adminEmail : ctxEmail!.Trim(),
                gender: Gender.Male);
        }
        else if (office != null)
        {
            doctor = new Doctor(
                id: _guidGenerator.Create(),
                firstName: office.DoctorFirstName,
                lastName: office.DoctorLastName,
                email: office.DoctorEmail,
                gender: Gender.Male);
        }
        else
        {
            doctor = new Doctor(
                id: _guidGenerator.Create(),
                firstName: string.Empty,
                lastName: tenant?.Name ?? adminEmail,
                email: adminEmail,
                gender: Gender.Male);
        }

        // tenant === doctor: the office's catalogs belong to its one doctor by default.
        doctor.AddAppointmentType(CaseEvaluationSeedIds.AppointmentTypes.Ame);
        doctor.AddAppointmentType(CaseEvaluationSeedIds.AppointmentTypes.Ime);
        doctor.AddAppointmentType(CaseEvaluationSeedIds.AppointmentTypes.PanelQme);
        doctor.AddLocation(CaseEvaluationSeedIds.Locations.DemoClinicNorth);
        doctor.AddLocation(CaseEvaluationSeedIds.Locations.DemoClinicSouth);

        await _doctorRepository.InsertAsync(doctor, autoSave: false);
    }
}
