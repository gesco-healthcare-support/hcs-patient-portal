using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace HealthcareSupport.CaseEvaluation.Identity;

public class ExternalUserRoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private const string RoleProviderName = "R";
    private const string Group = "CaseEvaluation";

    private readonly IdentityRoleManager _roleManager;
    private readonly IPermissionManager _permissionManager;
    private readonly ICurrentTenant _currentTenant;

    public ExternalUserRoleDataSeedContributor(
        IdentityRoleManager roleManager,
        IPermissionManager permissionManager,
        ICurrentTenant currentTenant)
    {
        _roleManager = roleManager;
        _permissionManager = permissionManager;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        using (_currentTenant.Change(context?.TenantId))
        {
            await EnsureRoleAsync("Patient");
            await EnsureRoleAsync("Claim Examiner");
            await EnsureRoleAsync("Applicant Attorney");
            await EnsureRoleAsync("Defense Attorney");
            // Role-naming reconciliation 2026-05-04 -- OLD has 4 external
            // roles total (verified at
            // P:\PatientPortalOld\PatientAppointment.Models\Enums\Roles.cs):
            //   OLD Patient         = 4  -> NEW Patient
            //   OLD Adjuster        = 5  -> NEW Claim Examiner
            //   OLD PatientAttorney = 6  -> NEW Applicant Attorney
            //   OLD DefenseAttorney = 7  -> NEW Defense Attorney
            // "Adjuster" and "Claim Examiner" are the SAME role (NEW
            // renamed for clarity to align with the
            // AppointmentClaimExaminer entity name). Earlier audit
            // mistakenly listed "Adjuster" as a fifth role; reconciled.

            // Phase 1A (2026-05-06) -- baseline booking permissions for the
            // four external roles. Mirrors OLD where any authenticated
            // external user could see the slot list and create their own
            // appointment. Restrictions on cross-tenant + cross-user data
            // are enforced by IMultiTenant + AppService-level filtering, not
            // by withholding the permission. Skipped on the host pass
            // (TenantId == null) because external roles are tenant-scoped.
            if (context?.TenantId != null)
            {
                foreach (var roleName in new[] { "Patient", "Claim Examiner", "Applicant Attorney", "Defense Attorney" })
                {
                    await GrantAllAsync(roleName, BookingBaselineGrants());
                }
            }
        }
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        var existingRole = await _roleManager.FindByNameAsync(roleName);
        if (existingRole != null)
        {
            return;
        }

        var role = new IdentityRole(Guid.NewGuid(), roleName, _currentTenant.Id);
        await _roleManager.CreateAsync(role);
    }

    private async Task GrantAllAsync(string roleName, IEnumerable<string> permissions)
    {
        foreach (var permission in permissions)
        {
            await _permissionManager.SetAsync(permission, RoleProviderName, roleName, isGranted: true);
        }
    }

    private static IEnumerable<string> BookingBaselineGrants()
    {
        // Read available slots for the booking form's date+time selector.
        yield return $"{Group}.DoctorAvailabilities";
        // Read + create own appointments. Edit/Delete are supervisor-only;
        // external users issue change-requests instead.
        yield return $"{Group}.Appointments";
        yield return $"{Group}.Appointments.Create";
        yield return $"{Group}.Appointments.RequestCancellation";
        yield return $"{Group}.Appointments.RequestReschedule";

        // B7 (2026-05-06): external users need to read AND upload documents
        // for their own appointment, plus view the doctor packet. Document
        // delete / approve / reject + packet regenerate stay supervisor-
        // only because they are clinical-workflow actions, not booker
        // actions. The AppService methods are still expected to enforce
        // per-record ownership (caller must be a party on the appointment).
        // Note: AppointmentPackets has no `.Default` child -- the parent
        // permission name itself is what AppointmentPacketsAppService
        // gates on (CaseEvaluationPermissions.AppointmentPackets.Default
        // resolves to "CaseEvaluation.AppointmentPackets").
        yield return $"{Group}.AppointmentDocuments";
        yield return $"{Group}.AppointmentDocuments.Create";
        yield return $"{Group}.AppointmentPackets";

        // B6 follow-on (2026-05-07): the NEW booking submit fans out into
        // a POST per child resource (injury details, body parts, claim
        // examiners, attorney links, primary insurance, employer, master
        // attorney records). Each child AppService gates on its own
        // Default + Create + Edit permission. OLD's controllers carried
        // NO [Authorize] attributes on these booking endpoints (verified
        // 2026-05-07 in P:\PatientPortalOld\PatientAppointment.Api), so
        // any authenticated user could submit the entire booking graph.
        // Per Adrian's strict-parity directive (option A): preserve NEW's
        // permission-gate architecture but widen the booking baseline to
        // grant every external role the same effective set OLD allowed.
        // Delete stays out -- bookers cannot drop sub-records once
        // committed; that path is admin-only. Per-record ownership
        // enforcement remains the AppService's responsibility.
        yield return $"{Group}.AppointmentInjuryDetails";
        yield return $"{Group}.AppointmentInjuryDetails.Create";
        yield return $"{Group}.AppointmentInjuryDetails.Edit";
        yield return $"{Group}.AppointmentBodyParts";
        yield return $"{Group}.AppointmentBodyParts.Create";
        yield return $"{Group}.AppointmentBodyParts.Edit";
        yield return $"{Group}.AppointmentClaimExaminers";
        yield return $"{Group}.AppointmentClaimExaminers.Create";
        yield return $"{Group}.AppointmentClaimExaminers.Edit";
        yield return $"{Group}.AppointmentApplicantAttorneys";
        yield return $"{Group}.AppointmentApplicantAttorneys.Create";
        yield return $"{Group}.AppointmentApplicantAttorneys.Edit";
        yield return $"{Group}.AppointmentDefenseAttorneys";
        yield return $"{Group}.AppointmentDefenseAttorneys.Create";
        yield return $"{Group}.AppointmentDefenseAttorneys.Edit";
        yield return $"{Group}.AppointmentPrimaryInsurances";
        yield return $"{Group}.AppointmentPrimaryInsurances.Create";
        yield return $"{Group}.AppointmentPrimaryInsurances.Edit";
        yield return $"{Group}.AppointmentEmployerDetails";
        yield return $"{Group}.ApplicantAttorneys";
        yield return $"{Group}.ApplicantAttorneys.Create";
        yield return $"{Group}.ApplicantAttorneys.Edit";
        yield return $"{Group}.DefenseAttorneys";
        yield return $"{Group}.DefenseAttorneys.Create";
        yield return $"{Group}.DefenseAttorneys.Edit";
    }
}
