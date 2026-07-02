using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Security;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// QA item 7: pins the self-scoped notification reads. Every endpoint resolves the
/// caller from CurrentUser.Id and takes no recipient id, so each user reads/marks
/// only their OWN rows (per-user fan-out). Two distinct user ids prove the scoping;
/// the cross-user MarkRead no-op proves a caller cannot touch another user's row.
/// Runs at host level (like the draft tests); the tenant data filter is
/// ABP-provided and covered by the live db-per-office gate.
/// </summary>
public abstract class AppNotificationAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static readonly Guid UserA = Guid.Parse("a9900001-0000-4000-9000-000000000001");
    private static readonly Guid UserB = Guid.Parse("a9900002-0000-4000-9000-000000000002");

    private readonly IAppNotificationAppService _service;
    private readonly IRepository<AppNotification, Guid> _repository;
    private readonly ICurrentPrincipalAccessor _principal;

    protected AppNotificationAppServiceTests()
    {
        _service = GetRequiredService<IAppNotificationAppService>();
        _repository = GetRequiredService<IRepository<AppNotification, Guid>>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private Task<AppNotification> SeedAsync(Guid recipientId)
    {
        return WithUnitOfWorkAsync(() => _repository.InsertAsync(
            new AppNotification(
                Guid.NewGuid(),
                recipientId,
                AppNotificationType.AppointmentRequested,
                "New appointment request",
                "Request A00001 was submitted and needs review.",
                "/appointments/view/x"),
            autoSave: true));
    }

    [Fact]
    public async Task GetMine_and_unread_count_are_scoped_to_the_caller()
    {
        await SeedAsync(UserA);
        await SeedAsync(UserA);
        await SeedAsync(UserB);

        using (WithCurrentUser.Run(_principal, UserA))
        {
            (await _service.GetMyUnreadCountAsync()).ShouldBe(2);
            var page = await _service.GetMyNotificationsAsync(
                new PagedAndSortedResultRequestDto { MaxResultCount = 20 });
            page.TotalCount.ShouldBe(2);
            page.Items.Count.ShouldBe(2);
        }

        using (WithCurrentUser.Run(_principal, UserB))
        {
            (await _service.GetMyUnreadCountAsync()).ShouldBe(1);
        }
    }

    [Fact]
    public async Task MarkRead_marks_the_callers_own_and_lowers_the_unread_count()
    {
        var mine = await SeedAsync(UserA);

        using (WithCurrentUser.Run(_principal, UserA))
        {
            await _service.MarkReadAsync(mine.Id);
            (await _service.GetMyUnreadCountAsync()).ShouldBe(0);
        }
    }

    [Fact]
    public async Task MarkRead_does_not_touch_another_users_notification()
    {
        var othersRow = await SeedAsync(UserB);

        using (WithCurrentUser.Run(_principal, UserA))
        {
            // Not the caller's row -> silent no-op (isolation), not an exception.
            await _service.MarkReadAsync(othersRow.Id);
        }

        var reloaded = await WithUnitOfWorkAsync(() => _repository.GetAsync(othersRow.Id));
        reloaded.IsRead.ShouldBeFalse();
    }

    [Fact]
    public async Task MarkAllRead_marks_only_the_callers_rows()
    {
        await SeedAsync(UserA);
        await SeedAsync(UserA);
        var othersRow = await SeedAsync(UserB);

        using (WithCurrentUser.Run(_principal, UserA))
        {
            await _service.MarkAllReadAsync();
            (await _service.GetMyUnreadCountAsync()).ShouldBe(0);
        }

        var reloaded = await WithUnitOfWorkAsync(() => _repository.GetAsync(othersRow.Id));
        reloaded.IsRead.ShouldBeFalse();
    }
}
