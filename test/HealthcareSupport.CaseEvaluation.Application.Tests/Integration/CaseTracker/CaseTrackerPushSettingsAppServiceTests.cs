using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit coverage for <see cref="CaseTrackerPushSettingsAppService"/>, the per-office switch that
/// starts (or stops) patient data flowing to the Case Tracker.
///
/// <para><b>WHAT IS PINNED.</b> The switch reads ON only for a stored "true": absent or unparseable
/// reads OFF, because sending is opt-in and anything ambiguous must mean "do not send". Each office's
/// state carries its name from the tenant store and its count of PENDING pushes only, and offices come
/// back in name order. Setting the switch writes inside THAT office's tenant scope -- the store the
/// drain reads -- and returns the fresh state.</para>
///
/// <para><b>The results asserted are the returned states and the setting written.</b> The setting
/// manager, stores and repository are substitutes; nothing is pushed. The service is built with
/// <c>new</c> and a substituted <see cref="IAbpLazyServiceProvider"/>, which is all its base class
/// needs here. Synthetic data only (HIPAA).</para>
/// </summary>
public class CaseTrackerPushSettingsAppServiceTests
{
    private static readonly Guid OfficeA = new("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid OfficeB = new("bbbbbbbb-0000-0000-0000-00000000000b");

    private sealed class Rig
    {
        public Dictionary<Guid, string?> StoredSwitch { get; } = new();
        public List<IntegrationOutboxItem> Rows { get; } = new();
        public ISettingManager Settings { get; } = Substitute.For<ISettingManager>();
        public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();
        public List<Guid> Offices { get; } = new() { OfficeA };

        /// <summary>The office scope most recently entered; the setting store follows it.</summary>
        public Guid? Scope => _scope;
        private Guid? _scope;

        public CaseTrackerPushSettingsAppService Build()
        {
            // Track the office scope the service enters, so the stored switch can be read per office.
            CurrentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(ci =>
            {
                _scope = ci.ArgAt<Guid?>(0);
                return Substitute.For<IDisposable>();
            });
            Settings.GetOrNullAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
                .Returns(_ => _scope.HasValue && StoredSwitch.TryGetValue(_scope.Value, out var raw) ? raw : null);

            var tenantRunner = Substitute.For<ITenantWorkRunner>();
            tenantRunner.AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<CaseTrackerOfficePushStateDto>>>())
                .Returns(async ci =>
                {
                    var selector = ci.Arg<Func<Guid, Task<CaseTrackerOfficePushStateDto>>>();
                    var states = new List<CaseTrackerOfficePushStateDto>();
                    foreach (var office in Offices)
                    {
                        states.Add(await selector(office));
                    }
                    return states;
                });

            var tenantStore = Substitute.For<ITenantStore>();
            tenantStore.FindAsync(OfficeA).Returns(new TenantConfiguration(OfficeA, "TEST-Zeta Office"));
            tenantStore.FindAsync(OfficeB).Returns(new TenantConfiguration(OfficeB, "TEST-alpha Office"));

            var outbox = Substitute.For<IIntegrationOutboxRepository>();
            outbox.GetQueryableAsync().Returns(_ => Rows.AsQueryable());

            // The REAL feed manager with no feed state stored: every office here is on push, not the
            // feed (#927), which is the state these Facts are about. Explicit, not left to NSubstitute.
            var feedStates = Substitute.For<ICaseTrackerFeedStateRepository>();
            feedStates.FindCurrentAsync(Arg.Any<CancellationToken>()).Returns((CaseTrackerFeedState?)null);
            var feedManager = new CaseTrackerFeedManager(
                feedStates,
                Substitute.For<ICaseTrackerFeedStore>(),
                Substitute.For<IClock>(),
                SimpleGuidGenerator.Instance);

            return new CaseTrackerPushSettingsAppService(
                tenantRunner,
                tenantStore,
                CurrentTenant,
                Settings,
                outbox,
                feedManager,
                NullLogger<CaseTrackerPushSettingsAppService>.Instance)
            {
                LazyServiceProvider = Substitute.For<IAbpLazyServiceProvider>(),
            };
        }
    }

    private static IntegrationOutboxItem Row(bool failed = false)
    {
        var row = new IntegrationOutboxItem(
            Guid.NewGuid(), OfficeA, IntegrationMessageType.Intake, "TEST/intake", Guid.NewGuid(), "{}", $"TEST-key-{Guid.NewGuid():N}");
        if (failed)
        {
            row.MarkFatal(new DateTime(2026, 9, 23, 20, 0, 0, DateTimeKind.Utc), "TEST-error");
        }
        return row;
    }

    /// <summary>
    /// Only a stored "true" reads as ON. Absent, "false" and unparseable text all read OFF -- sending is
    /// opt-in, so anything ambiguous must mean "do not send".
    /// </summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    [InlineData("TEST-yes", false)]
    public async Task GetOfficesAsync_TheSwitchReadsOnOnlyForAStoredTrue(string? stored, bool expected)
    {
        var rig = new Rig();
        rig.StoredSwitch[OfficeA] = stored;

        var state = (await rig.Build().GetOfficesAsync()).ShouldHaveSingleItem();

        state.PushEnabled.ShouldBe(expected);
    }

    [Fact]
    public async Task GetOfficesAsync_CarriesTheOfficeNameAndCountsOnlyPendingPushes()
    {
        var rig = new Rig();
        rig.Rows.Add(Row());
        rig.Rows.Add(Row());
        rig.Rows.Add(Row(failed: true));

        var state = (await rig.Build().GetOfficesAsync()).ShouldHaveSingleItem();

        state.OfficeId.ShouldBe(OfficeA);
        state.OfficeName.ShouldBe("TEST-Zeta Office");
        state.PendingCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetOfficesAsync_ReturnsOfficesInNameOrderIgnoringCase()
    {
        var rig = new Rig();
        rig.Offices.Add(OfficeB);

        var states = await rig.Build().GetOfficesAsync();

        states.Select(s => s.OfficeName).ShouldBe(new[] { "TEST-alpha Office", "TEST-Zeta Office" });
    }

    /// <summary>
    /// Switching an office on writes "true" INSIDE that office's scope (the store the drain reads) and
    /// returns the office's fresh state.
    /// </summary>
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task SetPushEnabledAsync_WritesInsideTheOfficesScopeAndReturnsTheFreshState(bool enabled, string expectedStored)
    {
        var rig = new Rig();
        var service = rig.Build();
        Guid? scopeAtWrite = null;
        rig.Settings.SetAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled, Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(ci =>
            {
                // Record WHICH office scope was active at the moment of the write: the setting store
                // follows the current tenant, so this is the database the value lands in.
                scopeAtWrite = rig.Scope;
                rig.StoredSwitch[OfficeA] = ci.ArgAt<string?>(1);
                return Task.CompletedTask;
            });

        var state = await service.SetPushEnabledAsync(OfficeA, enabled);

        scopeAtWrite.ShouldBe(OfficeA, "the switch must be written inside the office's own scope, not the host's");
        rig.StoredSwitch[OfficeA].ShouldBe(expectedStored);
        state.PushEnabled.ShouldBe(enabled);
    }
}
