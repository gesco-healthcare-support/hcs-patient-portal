using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// F1 self-validation: PROVES the multi-office harness gives real (not false) isolation
/// confidence before any cross-office matrix is built on it (risk RF1).
///
/// The decisive assertion is the physical one: an Appointment seeded in office A is
/// invisible to office B EVEN WITH ABP's IMultiTenant query filter disabled. In the old
/// single-connection harness, disabling that filter would surface office A's row (all
/// tenants share one database); here it does not, because office B's connection string
/// resolves to a genuinely separate in-memory database. If routing silently fell back to
/// a shared/host database (the F-8 failure mode), the filter-disabled assertion would
/// fail -- so this test is the guard on the harness itself.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeHarnessSelfValidationTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;

    public MultiOfficeHarnessSelfValidationTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _dbContextProvider = GetRequiredService<IDbContextProvider<CaseEvaluationDbContext>>();
    }

    [Fact]
    public async Task Office_A_appointment_is_physically_absent_from_Office_B()
    {
        var (officeA, officeB) = await GetSeededOfficesAsync();

        // Control: office A genuinely persisted its appointment.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                (await _appointmentRepository.FindAsync(officeA.AppointmentId)).ShouldNotBeNull();
            }
        }, requiresNew: true);

        // Normal (filtered) path: office B sees nothing of office A's appointment.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB.OfficeId))
            {
                (await _appointmentRepository.FindAsync(officeA.AppointmentId)).ShouldBeNull();
            }
        }, requiresNew: true);

        // Physical proof (F-2): even with the IMultiTenant filter OFF, office B's
        // DbContext cannot see office A's appointment -- it lives in a different database.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB.OfficeId))
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var dbContext = await _dbContextProvider.GetDbContextAsync();
                var visibleInOfficeB = await dbContext.Set<Appointment>()
                    .AnyAsync(a => a.Id == officeA.AppointmentId);
                visibleInOfficeB.ShouldBeFalse();
            }
        }, requiresNew: true);

        // And the row IS physically present in office A's database (filter off).
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var dbContext = await _dbContextProvider.GetDbContextAsync();
                var visibleInOfficeA = await dbContext.Set<Appointment>()
                    .AnyAsync(a => a.Id == officeA.AppointmentId);
                visibleInOfficeA.ShouldBeTrue();
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// A guard on the CONVENTION rather than on a run: no MultiOffice test may compute its own
    /// day. Every slot date must be measured from
    /// <see cref="CaseEvaluationMultiOfficeTestBase.TestToday"/>, which is fixed once per process.
    ///
    /// <para><b>WHY THIS IS A TEST AND NOT A COMMENT.</b> The comment has been tried. This tree
    /// already carried the rule "pick an offset no other test uses"
    /// (<c>MultiOfficeAppointmentsAppServiceTests.cs:442-444</c>) and the convention was
    /// re-derived wrongly THREE times: the arms moved to 45/46, another test moved to 47, and the
    /// result was three consecutive 09:00-10:00 offsets -- a fresh instance of the bug the moves
    /// were fixing. A test fails; a comment is read after the fact.</para>
    ///
    /// <para><b>What it prevents.</b> Recomputing the day per call site makes offsets alias across
    /// local midnight -- offset N computed after midnight equals offset N+1 computed before it --
    /// which took out a real CI run on 2026-09-19T00:00:04Z. Pinning removes the aliasing for
    /// every site that uses the pinned field; this Fact is what stops a NEW site from opting back
    /// out of it.</para>
    ///
    /// <para><b>FAIL-CLOSED ON PURPOSE.</b> If the source directory cannot be found the Fact
    /// FAILS rather than skipping. A guard that quietly stops guarding is worse than no guard: a
    /// skip would read as a pass forever. The directory is located from this file's own
    /// compile-time path, so it resolves wherever the assembly was built, which for both CI jobs
    /// is the same checkout that ran the tests.</para>
    ///
    /// <para><b>SEEN TO FAIL 2026-09-21.</b> One site was reverted to computing its own day and
    /// this Fact failed by name, naming the offender and its line:</para>
    /// <code>
    /// Failed:  1, Passed:  1, Total:  2
    /// MultiOfficeHarnessSelfValidationTests.NoMultiOfficeTestComputesItsOwnDay [FAIL]
    ///   Offenders: MultiOfficeAppointmentChildCascadeTests.cs:313:appointmentDate: ...AddDays(30),
    /// </code>
    /// <para>So it detects a reintroduction and points at it, rather than reporting that something
    /// somewhere is wrong.</para>
    /// </summary>
    [Fact]
    public void NoMultiOfficeTestComputesItsOwnDay()
    {
        // Assembled at compile time so the literal never appears contiguously in this file --
        // otherwise the guard would report ITSELF as an offender on the line below.
        const string Needle = "DateTime" + ".Today";

        var directory = MultiOfficeSourceDirectory();
        Directory.Exists(directory).ShouldBeTrue(
            $"the MultiOffice source directory was not found at '{directory}', so this guard could "
            + "not run. That is reported as a FAILURE deliberately: skipping would look identical "
            + "to passing and the convention would silently stop being enforced.");

        var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
        files.Length.ShouldBeGreaterThan(
            5,
            $"only {files.Length} source files were found under '{directory}'. The MultiOffice tree "
            + "has far more, so the scan is looking at the wrong place and is not guarding anything.");

        var offenders = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.Contains(Needle, StringComparison.Ordinal))
                {
                    continue;
                }

                // Prose may discuss it freely -- one docstring legitimately describes PRODUCTION's
                // clock, which is resolved through IClock and is not this fixture's concern.
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                // The single permitted use: the pinned field's own initialiser.
                if (line.Contains($"TestToday = {Needle}", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{Path.GetFileName(file)}:{i + 1}:{line.Trim()}");
            }
        }

        offenders.ShouldBeEmpty(
            "these MultiOffice sites compute their own day instead of using TestToday, so their "
            + "slot dates can alias with another test's across local midnight and fail with "
            + "'UNIQUE constraint failed: AppDoctorAvailabilities...'. Use TestToday. Offenders: "
            + string.Join(" | ", offenders));
    }

    /// <summary>
    /// This file's directory, taken from its compile-time path. Used rather than the assembly
    /// location because the built output does not carry the source tree.
    /// </summary>
    private static string MultiOfficeSourceDirectory([CallerFilePath] string thisFile = "") =>
        Path.GetDirectoryName(thisFile) ?? string.Empty;
}
