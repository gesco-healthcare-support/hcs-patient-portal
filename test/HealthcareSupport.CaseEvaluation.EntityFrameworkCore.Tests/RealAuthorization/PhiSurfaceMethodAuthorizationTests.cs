using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// #707 LAYER 3, WIDENED FROM ONE METHOD PER PHI SURFACE TO EVERY PERMISSION-GATED METHOD ON
/// THE SAME FOUR SERVICES.
///
/// <para><see cref="PhiSurfaceAuthorizationTests"/> pins the headline call on each surface
/// (SSN reveal, document download, packet download, Case Tracker push). The same services
/// carry many more permission attributes guarding the same data -- a second packet download
/// path (<c>DownloadByKindAsync</c>), the document list, upload, delete and approve calls,
/// and the patient record reads and writes. Measured 2026-10-01: deleting any of those
/// attributes left the whole suite green. This file closes that.</para>
///
/// <para><b>HOW EACH CASE IS ATTRIBUTABLE TO THE ATTRIBUTE.</b> Each method is called twice:
/// once by <see cref="CaseEvaluationRealAuthorizationTestBase.MinimalRoleName"/>, which holds
/// none of these permissions, and once by a role built here that holds EXACTLY the one
/// permission the method declares and nothing else. The first must be refused by the
/// interceptor; the second must get past it. A pass on the second cannot come from some
/// other grant, because there is none.</para>
///
/// <para><b>THE PROBES ARE CHOSEN SO THE METHOD BODY CANNOT ALSO REFUSE.</b> Several of these
/// methods carry an in-code ownership or visibility check that throws the same
/// <see cref="AbpAuthorizationException"/> the attribute does (see the remarks on
/// <see cref="PhiSurfaceAuthorizationTests"/>). If such a check runs for the refused caller,
/// the refusal test passes with the attribute deleted. Every probe below was confirmed by
/// deleting every attribute this file covers and watching every refusal case fail; the PR
/// records the measurement.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class PhiSurfaceMethodAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string RoleProviderName = "R";
    private const string SingleGrantRolePrefix = "F2-authz-only-";

    /// <summary>An id no seeded row carries.</summary>
    private static readonly Guid UnknownId = Guid.Parse("00000000-0000-0000-0000-0000000000fe");

    /// <summary>
    /// The probe for the packet methods. Each runs <c>PacketVisibility</c>, an in-code role
    /// check that refuses any role it does not recognise -- including the single-grant role
    /// here -- before any lookup. Measured 2026-10-01: with an unknown id, the permitted case
    /// for <c>DownloadByKindAsync</c> was refused by that check, which also meant its refusal
    /// case could not tell the attribute from the check. Every packet method rejects an empty
    /// id as its FIRST statement, before the visibility check, so <c>Guid.Empty</c> separates
    /// the two gates. Same reasoning as <see cref="PhiSurfaceAuthorizationTests"/>.
    /// </summary>
    private static readonly Guid EmptyIdProbe = Guid.Empty;

    private static readonly SemaphoreSlim RoleLock = new(1, 1);
    private static readonly HashSet<string> SeededRoles = new(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, SurfaceCase> Cases = BuildCases()
        .ToDictionary(c => c.Name, StringComparer.Ordinal);

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public PhiSurfaceMethodAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in Cases.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            data.Add(name);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task IsRefused_ForARoleWithoutThePermission(string caseName)
    {
        var surface = Cases[caseName];
        var fixture = await GetFixtureAsync();

        var outcome = await InvokeAsync(fixture, MinimalRoleName, surface.Call);

        outcome.ShouldBeOfType<AbpAuthorizationException>(
            $"{caseName} declares {surface.Permission}, which {MinimalRoleName} does not hold, so " +
            "the authorization interceptor should have refused the call before the method body " +
            "ran. Any other outcome means the body WAS reached: the [Authorize] attribute is " +
            "missing or is not being enforced.");
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task IsNotRefused_ForARoleHoldingOnlyThatPermission(string caseName)
    {
        var surface = Cases[caseName];
        var fixture = await GetFixtureAsync();
        var role = await EnsureSingleGrantRoleAsync(fixture, surface.Permission);

        var outcome = await InvokeAsync(fixture, role, surface.Call);

        (outcome is AbpAuthorizationException).ShouldBeFalse(
            $"{role} holds {surface.Permission}, the permission {caseName} declares, so the call " +
            "should have got past the interceptor. A refusal here means either the method needs " +
            "more than it declares, or the probe reaches an in-code check that refuses on its own " +
            $"-- in which case the refusal case for {caseName} proves nothing. Got: {outcome}");
    }

    /// <summary>
    /// The table. Each entry is one permission-gated method on the four PHI services, the
    /// permission its attribute names, and a probe call.
    ///
    /// <para>The four methods <see cref="PhiSurfaceAuthorizationTests"/> already covers are
    /// deliberately not repeated. Methods carrying only a bare <c>[Authorize]</c> (the booking
    /// flow and the caller's own profile) are out of scope: they declare no permission, so
    /// there is nothing for a role to lack.</para>
    /// </summary>
    private static IEnumerable<SurfaceCase> BuildCases()
    {
        // Patient records -- CaseEvaluation.Patients and its children.
        yield return new("Patients.GetList", CaseEvaluationPermissions.Patients.Default,
            sp => Patients(sp).GetListAsync(new GetPatientsInput()));
        yield return new("Patients.GetWithNavigationProperties", CaseEvaluationPermissions.Patients.Default,
            sp => Patients(sp).GetWithNavigationPropertiesAsync(UnknownId));
        yield return new("Patients.Get", CaseEvaluationPermissions.Patients.Default,
            sp => Patients(sp).GetAsync(UnknownId));
        yield return new("Patients.GetPatientByEmailForAppointmentBooking", CaseEvaluationPermissions.Patients.Default,
            sp => Patients(sp).GetPatientByEmailForAppointmentBookingAsync("TEST-probe@example.test"));
        yield return new("Patients.GetIdentityUserLookup", CaseEvaluationPermissions.Patients.Default,
            sp => Patients(sp).GetIdentityUserLookupAsync(new LookupRequestDto()));
        yield return new("Patients.GetTenantLookup", CaseEvaluationPermissions.Patients.Default,
            sp => Patients(sp).GetTenantLookupAsync(new LookupRequestDto()));
        yield return new("Patients.Create", CaseEvaluationPermissions.Patients.Create,
            sp => Patients(sp).CreateAsync(new PatientCreateDto()));
        yield return new("Patients.Update", CaseEvaluationPermissions.Patients.Edit,
            sp => Patients(sp).UpdateAsync(UnknownId, new PatientUpdateDto()));
        yield return new("Patients.Delete", CaseEvaluationPermissions.Patients.Delete,
            sp => Patients(sp).DeleteAsync(UnknownId));

        // Appointment documents -- CaseEvaluation.AppointmentDocuments and its children.
        yield return new("AppointmentDocuments.GetListByAppointment", CaseEvaluationPermissions.AppointmentDocuments.Default,
            sp => Documents(sp).GetListByAppointmentAsync(UnknownId));
        yield return new("AppointmentDocuments.GetDocumentTypeOptions", CaseEvaluationPermissions.AppointmentDocuments.Default,
            sp => Documents(sp).GetDocumentTypeOptionsAsync(UnknownId));
        yield return new("AppointmentDocuments.GetDocumentTypeOptionsByAppointmentType", CaseEvaluationPermissions.AppointmentDocuments.Default,
            sp => Documents(sp).GetDocumentTypeOptionsByAppointmentTypeAsync(UnknownId));
        yield return new("AppointmentDocuments.GetMissingRequiredDocuments", CaseEvaluationPermissions.AppointmentDocuments.Default,
            sp => Documents(sp).GetMissingRequiredDocumentsAsync(UnknownId));
        yield return new("AppointmentDocuments.UploadStream", CaseEvaluationPermissions.AppointmentDocuments.Create,
            sp => Documents(sp).UploadStreamAsync(UnknownId, "TEST-probe", "TEST-probe.pdf", "application/pdf", 1, new MemoryStream(new byte[] { 0x25 })));
        yield return new("AppointmentDocuments.UploadPackageDocument", CaseEvaluationPermissions.AppointmentDocuments.Create,
            sp => Documents(sp).UploadPackageDocumentAsync(UnknownId, "TEST-probe.pdf", "application/pdf", 1, new MemoryStream(new byte[] { 0x25 })));
        yield return new("AppointmentDocuments.UploadJointDeclaration", CaseEvaluationPermissions.AppointmentDocuments.Create,
            sp => Documents(sp).UploadJointDeclarationAsync(UnknownId, "TEST-probe", "TEST-probe.pdf", "application/pdf", 1, new MemoryStream(new byte[] { 0x25 })));
        yield return new("AppointmentDocuments.Delete", CaseEvaluationPermissions.AppointmentDocuments.Delete,
            sp => Documents(sp).DeleteAsync(UnknownId));
        yield return new("AppointmentDocuments.Approve", CaseEvaluationPermissions.AppointmentDocuments.Approve,
            sp => Documents(sp).ApproveAsync(UnknownId));
        yield return new("AppointmentDocuments.Reject", CaseEvaluationPermissions.AppointmentDocuments.Approve,
            sp => Documents(sp).RejectAsync(UnknownId, new RejectDocumentInput { Reason = "TEST-probe" }));
        yield return new("AppointmentDocuments.RegeneratePacket", CaseEvaluationPermissions.AppointmentPackets.Regenerate,
            sp => Documents(sp).RegeneratePacketAsync(UnknownId));

        // Appointment packets -- CaseEvaluation.AppointmentPackets.
        yield return new("AppointmentPackets.GetByAppointment", CaseEvaluationPermissions.AppointmentPackets.Default,
            sp => Packets(sp).GetByAppointmentAsync(EmptyIdProbe));
        yield return new("AppointmentPackets.GetListByAppointment", CaseEvaluationPermissions.AppointmentPackets.Default,
            sp => Packets(sp).GetListByAppointmentAsync(EmptyIdProbe));
        yield return new("AppointmentPackets.DownloadByKind", CaseEvaluationPermissions.AppointmentPackets.Default,
            sp => Packets(sp).DownloadByKindAsync(EmptyIdProbe, PacketKind.Patient));
    }

    private static IPatientsAppService Patients(IServiceProvider sp) =>
        sp.GetRequiredService<IPatientsAppService>();

    private static IAppointmentDocumentsAppService Documents(IServiceProvider sp) =>
        sp.GetRequiredService<IAppointmentDocumentsAppService>();

    private static IAppointmentPacketsAppService Packets(IServiceProvider sp) =>
        sp.GetRequiredService<IAppointmentPacketsAppService>();

    /// <summary>
    /// A role in the seeded office holding exactly <paramref name="permission"/>. Created once
    /// per permission for the whole run.
    /// </summary>
    private async Task<string> EnsureSingleGrantRoleAsync(AuthorizationFixture fixture, string permission)
    {
        var roleName = SingleGrantRolePrefix + permission;

        await RoleLock.WaitAsync();
        try
        {
            if (SeededRoles.Contains(roleName))
            {
                return roleName;
            }

            await WithUnitOfWorkAsync(async () =>
            {
                using (_currentTenant.Change(fixture.Office.OfficeId))
                {
                    var roleManager = GetRequiredService<IdentityRoleManager>();
                    if (await roleManager.FindByNameAsync(roleName) == null)
                    {
                        (await roleManager.CreateAsync(
                            new IdentityRole(Guid.NewGuid(), roleName, fixture.Office.OfficeId)))
                            .Succeeded.ShouldBeTrue();
                    }
                    await GetRequiredService<IPermissionManager>().SetAsync(
                        permission, RoleProviderName, roleName, isGranted: true);
                }
            }, requiresNew: true);

            SeededRoles.Add(roleName);
            return roleName;
        }
        finally
        {
            RoleLock.Release();
        }
    }

    /// <summary>
    /// Runs <paramref name="call"/> as <paramref name="role"/> in the seeded office and returns
    /// what it threw, or <c>null</c> when it returned normally. A fresh caller id is used so no
    /// outcome can come from record ownership.
    /// </summary>
    private async Task<Exception?> InvokeAsync(
        AuthorizationFixture fixture,
        string role,
        Func<IServiceProvider, Task> call)
    {
        Exception? caught = null;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, Guid.NewGuid(), role))
            {
                try
                {
                    await call(ServiceProvider);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }
        }, requiresNew: true);

        return caught;
    }

    private sealed record SurfaceCase(string Name, string Permission, Func<IServiceProvider, Task> Call);
}
