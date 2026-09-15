using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Security;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts;

/// <summary>
/// #15: pins the self-scoped booking draft. The endpoint takes no target id -- it
/// resolves the caller from CurrentUser.Id -- so each booker reads/writes only
/// their OWN draft (PHI), upsert keeps a single row per user, and discard removes
/// it. Two distinct user ids prove the scoping comes from the creator. Runs at
/// host level so CreatorId stamps reliably (ABP skips CreatorId when the entity's
/// tenant differs from the caller's tenant claim); the tenant data filter itself
/// is ABP-provided and out of scope here.
/// </summary>
public abstract class AppointmentDraftAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static readonly Guid UserA = Guid.Parse("d1a00001-0000-4000-9000-000000000001");
    private static readonly Guid UserB = Guid.Parse("d1a00002-0000-4000-9000-000000000002");
    private const string PayloadA = "{\"v\":{\"firstName\":\"Anna\"},\"step\":2}";
    private const string PayloadB = "{\"v\":{\"firstName\":\"Bob\"},\"step\":5}";

    private readonly IAppointmentDraftAppService _service;
    private readonly IRepository<AppointmentDraft, Guid> _repository;
    private readonly ICurrentPrincipalAccessor _principal;

    protected AppointmentDraftAppServiceTests()
    {
        _service = GetRequiredService<IAppointmentDraftAppService>();
        _repository = GetRequiredService<IRepository<AppointmentDraft, Guid>>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task Upsert_persists_and_GetMine_returns_it()
    {
        using (WithCurrentUser.Run(_principal, UserA))
        {
            await _service.UpsertAsync(new UpsertAppointmentDraftInput
            {
                PayloadJson = PayloadA,
                CurrentStep = 2,
                Label = "AME",
            });

            var mine = await _service.GetMineAsync();

            mine.ShouldNotBeNull();
            mine!.PayloadJson.ShouldBe(PayloadA);
            mine.CurrentStep.ShouldBe(2);
            mine.Label.ShouldBe("AME");
        }
    }

    [Fact]
    public async Task GetMine_returns_null_when_caller_has_no_draft()
    {
        using (WithCurrentUser.Run(_principal, Guid.NewGuid()))
        {
            var mine = await _service.GetMineAsync();
            mine.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Each_caller_sees_only_their_own_draft()
    {
        using (WithCurrentUser.Run(_principal, UserA))
        {
            await _service.UpsertAsync(new UpsertAppointmentDraftInput { PayloadJson = PayloadA, CurrentStep = 2 });
        }
        using (WithCurrentUser.Run(_principal, UserB))
        {
            await _service.UpsertAsync(new UpsertAppointmentDraftInput { PayloadJson = PayloadB, CurrentStep = 5 });
        }

        using (WithCurrentUser.Run(_principal, UserA))
        {
            var a = await _service.GetMineAsync();
            a.ShouldNotBeNull();
            a!.PayloadJson.ShouldBe(PayloadA);
        }
        using (WithCurrentUser.Run(_principal, UserB))
        {
            var b = await _service.GetMineAsync();
            b.ShouldNotBeNull();
            b!.PayloadJson.ShouldBe(PayloadB);
        }
    }

    [Fact]
    public async Task Upsert_twice_keeps_one_row_and_updates_in_place()
    {
        var user = Guid.NewGuid();
        using (WithCurrentUser.Run(_principal, user))
        {
            await _service.UpsertAsync(new UpsertAppointmentDraftInput { PayloadJson = PayloadA, CurrentStep = 1 });
            await _service.UpsertAsync(new UpsertAppointmentDraftInput { PayloadJson = PayloadB, CurrentStep = 6 });

            var mine = await _service.GetMineAsync();
            mine.ShouldNotBeNull();
            mine!.PayloadJson.ShouldBe(PayloadB);
            mine.CurrentStep.ShouldBe(6);

            // Each service call ran in its own (now-disposed) UoW, so the raw
            // repository read needs a fresh ambient UoW to keep its DbContext alive.
            var count = await WithUnitOfWorkAsync(() => _repository.CountAsync(x => x.CreatorId == user));
            count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task DiscardMine_removes_the_callers_draft()
    {
        var user = Guid.NewGuid();
        using (WithCurrentUser.Run(_principal, user))
        {
            await _service.UpsertAsync(new UpsertAppointmentDraftInput { PayloadJson = PayloadA, CurrentStep = 1 });
            await _service.DiscardMineAsync();

            var mine = await _service.GetMineAsync();
            mine.ShouldBeNull();
        }
    }
}
