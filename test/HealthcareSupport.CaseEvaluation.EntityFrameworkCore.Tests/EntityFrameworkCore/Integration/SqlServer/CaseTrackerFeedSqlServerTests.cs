using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.SqlServer;

/// <summary>
/// The feed's SQL on a real SQL Server (#927) -- the promises no SQLite test can check. Each test calls the very
/// statements <see cref="EfCoreCaseTrackerFeedStore"/> runs, through its public static methods, and uses an office
/// of its own so the tests cannot see each other's rows.
///
/// <para>THE LOAD-BEARING ONE is the first: a row written by a transaction still open when the feed reads must
/// not be skipped by a cursor that moves past it before it commits. That is the failure the rowversion design
/// exists to remove, and it is silent when it happens.</para>
/// </summary>
public class CaseTrackerFeedSqlServerTests : IClassFixture<SqlServerFeedFixture>
{
    private readonly SqlServerFeedFixture _fixture;

    public CaseTrackerFeedSqlServerTests(SqlServerFeedFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<long[]> ReadPositionsAsync(Guid officeId, long after, int take = 200)
    {
        await using var context = SqlServerFeedFixture.CreateContext(_fixture.FeedDatabase);
        var rows = await EfCoreCaseTrackerFeedStore.ReadPageAsync(context.Database, officeId, after, take);
        return rows.Select(r => r.Position).ToArray();
    }

    [Fact]
    public async Task ARowCommittedLate_IsWithheld_NotSkipped_WhileAnEarlierTransactionIsOpen()
    {
        var officeId = Guid.NewGuid();
        await using var early = new SqlConnection(_fixture.FeedDatabase);
        await early.OpenAsync();
        await using var open = (SqlTransaction)await early.BeginTransactionAsync();

        // The early transaction writes FIRST, so it holds the lower position, and it stays open.
        var inFlight = await OutboxRows.InsertAsync(early, open, officeId);
        var committed = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId);
        committed.Position.ShouldBeGreaterThan(inFlight.Position);

        // Serving the committed row now would move the consumer's cursor past the in-flight one for good.
        (await ReadPositionsAsync(officeId, after: 0)).ShouldBeEmpty();

        await open.CommitAsync();

        (await ReadPositionsAsync(officeId, after: 0)).ShouldBe(new[] { inFlight.Position, committed.Position });
    }

    [Fact]
    public async Task AnOpenTransactionAnywhereInTheDatabase_HoldsTheFeedBack_UntilItCommits()
    {
        // The long-running-transaction case (research 3.2), through a scratch table with its own rowversion:
        // the horizon is database-wide, so the feed waits even though no outbox row is locked.
        var officeId = Guid.NewGuid();
        await using var pin = new SqlConnection(_fixture.FeedDatabase);
        await pin.OpenAsync();
        await using var open = (SqlTransaction)await pin.BeginTransactionAsync();
        await using (var command = new SqlCommand(
            "INSERT INTO [" + SqlServerFeedFixture.ScratchPinTable + "] DEFAULT VALUES;", pin, open))
        {
            await command.ExecuteNonQueryAsync();
        }

        var row = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId);

        (await ReadPositionsAsync(officeId, after: 0)).ShouldBeEmpty();
        await using (var context = SqlServerFeedFixture.CreateContext(_fixture.FeedDatabase))
        {
            // ...but the stall check still counts it, so a held-back feed shows as a stall, not as silence.
            (await EfCoreCaseTrackerFeedStore.CountOutstandingAsync(context.Database, officeId, 0)).ShouldBe(1);
        }

        await open.CommitAsync();
        (await ReadPositionsAsync(officeId, after: 0)).ShouldBe(new[] { row.Position });
    }

    [Fact]
    public async Task APage_HoldsOnlyThisOfficesLivePendingRows_InAscendingOrder_AndHonoursTake()
    {
        var officeId = Guid.NewGuid();
        var first = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId);
        await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, status: IntegrationOutboxStatus.Failed);
        await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, status: IntegrationOutboxStatus.Sent);
        await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, deleted: true);
        await OutboxRows.InsertAsync(_fixture.FeedDatabase, Guid.NewGuid()); // another office
        var second = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId);
        var third = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId);

        (await ReadPositionsAsync(officeId, after: 0)).ShouldBe(new[] { first.Position, second.Position, third.Position });
        (await ReadPositionsAsync(officeId, after: 0, take: 2)).ShouldBe(new[] { first.Position, second.Position });
        (await ReadPositionsAsync(officeId, after: first.Position)).ShouldBe(new[] { second.Position, third.Position });
    }

    [Fact]
    public async Task ADeadLetterUpdatedAfterCutover_DoesNotResurface_AboveTheFloor()
    {
        // Issue #927, 2026-09-17: a Failed row is still stamped or resolved after cutover, which gives it a new
        // rowversion above the floor. Only the Status = Pending clause keeps it out.
        var officeId = Guid.NewGuid();
        var deadLetter = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, status: IntegrationOutboxStatus.Failed);
        long floor;
        await using (var context = SqlServerFeedFixture.CreateContext(_fixture.FeedDatabase))
        {
            floor = await EfCoreCaseTrackerFeedStore.GetStartFloorAsync(context.Database, officeId);
        }

        var touched = await OutboxRows.TouchAsync(_fixture.FeedDatabase, deadLetter.Id);
        var pending = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId);

        touched.ShouldBeGreaterThan(floor);
        (await ReadPositionsAsync(officeId, after: floor)).ShouldBe(new[] { pending.Position });
    }

    [Fact]
    public async Task TheStartFloor_StaysBelowARowAnOpenTransactionWroteEarlier()
    {
        // Taking only the oldest COMMITTED Pending row would put the floor above the in-flight row, stranding it.
        var officeId = Guid.NewGuid();
        await using var early = new SqlConnection(_fixture.FeedDatabase);
        await early.OpenAsync();
        await using var open = (SqlTransaction)await early.BeginTransactionAsync();
        var inFlight = await OutboxRows.InsertAsync(early, open, officeId);
        await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId);

        long floor;
        await using (var context = SqlServerFeedFixture.CreateContext(_fixture.FeedDatabase))
        {
            floor = await EfCoreCaseTrackerFeedStore.GetStartFloorAsync(context.Database, officeId);
        }

        floor.ShouldBeLessThan(inFlight.Position);
        await open.CommitAsync();
        (await ReadPositionsAsync(officeId, after: floor)).First().ShouldBe(inFlight.Position);
    }

    [Fact]
    public async Task TheStartFloor_KeepsARowStillRetrying_AndNothingOlder()
    {
        var officeId = Guid.NewGuid();
        var sent = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, status: IntegrationOutboxStatus.Sent);
        var retrying = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId);

        long floor;
        await using (var context = SqlServerFeedFixture.CreateContext(_fixture.FeedDatabase))
        {
            floor = await EfCoreCaseTrackerFeedStore.GetStartFloorAsync(context.Database, officeId);
        }

        floor.ShouldBe(retrying.Position - 1);
        floor.ShouldBeGreaterThanOrEqualTo(sent.Position);
    }

    [Fact]
    public async Task AValueChangedAToBToA_IsServedInThatOrder_EndingOnA()
    {
        // The acceptance agreed on #927 (2026-09-16, second comment): the feed's latest row for the appointment reads A.
        // The queue producing three rows is #915's test; this proves the feed serves them in commit order.
        var officeId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        const string a = "{\"data\":{\"claimNumber\":\"SAMPLE-A\"}}";
        const string b = "{\"data\":{\"claimNumber\":\"SAMPLE-B\"}}";
        await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, a, appointmentId: appointmentId);
        await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, b, appointmentId: appointmentId);
        await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, a, appointmentId: appointmentId);

        await using var context = SqlServerFeedFixture.CreateContext(_fixture.FeedDatabase);
        var rows = await EfCoreCaseTrackerFeedStore.ReadPageAsync(context.Database, officeId, 0, 200);

        rows.Select(r => r.Payload).ShouldBe(new[] { a, b, a });
        rows.ShouldAllBe(r => r.AppointmentId == appointmentId && r.MessageType == IntegrationMessageType.Intake);
    }

    [Fact]
    public async Task FindRow_NamesAPendingRowAtExactlyItsPosition_AndNothingElse()
    {
        var officeId = Guid.NewGuid();
        var pending = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId);
        var failed = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, status: IntegrationOutboxStatus.Failed);

        await using var context = SqlServerFeedFixture.CreateContext(_fixture.FeedDatabase);
        (await EfCoreCaseTrackerFeedStore.FindRowAsync(context.Database, officeId, pending.Position))!.Position.ShouldBe(pending.Position);
        (await EfCoreCaseTrackerFeedStore.FindRowAsync(context.Database, officeId, failed.Position)).ShouldBeNull();
        (await EfCoreCaseTrackerFeedStore.FindRowAsync(context.Database, Guid.NewGuid(), pending.Position)).ShouldBeNull();
    }

    [Fact]
    public async Task OutstandingRowsCreatedBefore_CountsOnlyRowsBeyondTheAcknowledgedPositionAndOldEnough()
    {
        var officeId = Guid.NewGuid();
        var old = await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, createdAt: DateTime.UtcNow.AddHours(-2));
        await OutboxRows.InsertAsync(_fixture.FeedDatabase, officeId, createdAt: DateTime.UtcNow);

        await using var context = SqlServerFeedFixture.CreateContext(_fixture.FeedDatabase);
        var halfAnHourAgo = DateTime.UtcNow.AddMinutes(-30);
        (await EfCoreCaseTrackerFeedStore.HasOutstandingCreatedBeforeAsync(context.Database, officeId, 0, halfAnHourAgo)).ShouldBeTrue();
        (await EfCoreCaseTrackerFeedStore.HasOutstandingCreatedBeforeAsync(context.Database, officeId, old.Position, halfAnHourAgo)).ShouldBeFalse();
        (await EfCoreCaseTrackerFeedStore.CountOutstandingAsync(context.Database, officeId, old.Position)).ShouldBe(1);
    }

    [Fact]
    public async Task RowsThatExistedWhenTheColumnWasAdded_AllGetAPosition()
    {
        // The migration's ALTER TABLE writes every existing row (research 3.7); none may be left without a value,
        // or the feed could never serve it.
        await using var connection = new SqlConnection(_fixture.UpgradeDatabase);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM [AppIntegrationOutboxItems] WHERE [ChangeVersion] IS NULL;", connection);

        ((int)(await command.ExecuteScalarAsync())!).ShouldBe(0);
        _fixture.RowsBeforeTheColumn.Length.ShouldBe(2);
    }
}
