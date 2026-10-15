using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.States;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

/// <summary>
/// Guard G5 (#1034): proves at run time that every EF Core test gets its own database. That is
/// the premise that lets the test classes run in parallel without a shared <c>[Collection]</c>.
///
/// <para>Both facts insert a row with the SAME fixed primary key. With one database per test,
/// each insert lands in an empty table and both pass, whatever order they run in. If the
/// harness ever shared a database across tests again (a <c>static</c> connection, or a template
/// database handed out without a copy), whichever fact ran second would fail on the duplicate
/// key. So a regression shows up here as a named failure, instead of as order-dependent flakes
/// elsewhere.</para>
/// </summary>
public class PerTestDatabaseIsolationTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private static readonly Guid ProbeStateId = Guid.Parse("d1c00000-0000-0000-0000-00000000000c");
    private const string ProbeStateName = "TEST-per-test-database-probe";

    [Fact]
    public async Task FirstInsert_OfTheFixedKey_Succeeds() => await InsertProbeAndReadBackAsync();

    [Fact]
    public async Task SecondInsert_OfTheSameFixedKey_AlsoSucceeds() => await InsertProbeAndReadBackAsync();

    private async Task InsertProbeAndReadBackAsync()
    {
        var repository = GetRequiredService<IRepository<State, Guid>>();

        await WithUnitOfWorkAsync(async () =>
        {
            (await repository.FindAsync(ProbeStateId)).ShouldBeNull(
                "a fresh database must not already hold the probe row; if it does, another test wrote it");
            await repository.InsertAsync(new State(ProbeStateId, ProbeStateName), autoSave: true);
        });

        var stored = await WithUnitOfWorkAsync(() => repository.FindAsync(ProbeStateId));
        stored.ShouldNotBeNull();
        stored.Name.ShouldBe(ProbeStateName);
    }
}
