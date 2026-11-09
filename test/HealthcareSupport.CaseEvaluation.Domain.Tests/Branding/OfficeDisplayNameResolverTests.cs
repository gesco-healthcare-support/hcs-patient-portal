using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// 2026-11-09: emails and the register page named an office by its web-address slug ("Dr.
/// falkinstein"). <see cref="OfficeDisplayNames.Compose"/> pins Levon's wording rules;
/// <see cref="OfficeDisplayNameResolver"/> pins where the names come from (the office's effective
/// letterhead, the slug last). All values synthetic.
/// </summary>
public class OfficeDisplayNameResolverTests
{
    private const string Slug = "test-slug";
    private static readonly Guid OfficeA = Guid.NewGuid();

    [Fact]
    public void Doctor_and_practice_read_as_doctor_then_practice_in_brackets()
    {
        var names = OfficeDisplayNames.Compose("TEST-Ada Example, M.D.", "TEST Spine Institute", Slug);

        names.DoctorAndPracticeName.ShouldBe("TEST-Ada Example, M.D. (TEST Spine Institute)");
        names.PracticeName.ShouldBe("TEST Spine Institute");
    }

    [Theory]
    [InlineData("TEST Same Name", "TEST Same Name")]
    [InlineData("TEST Same Name", "  test same name ")]
    public void Equal_names_are_shown_once(string doctor, string practice)
    {
        var names = OfficeDisplayNames.Compose(doctor, practice, Slug);

        names.DoctorAndPracticeName.ShouldBe("TEST Same Name");
        names.PracticeName.ShouldBe(practice.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Only_a_doctor_is_used_for_both(string? practice)
    {
        var names = OfficeDisplayNames.Compose("Dr. TEST-Ada Example", practice, Slug);

        names.DoctorAndPracticeName.ShouldBe("Dr. TEST-Ada Example");
        names.PracticeName.ShouldBe("Dr. TEST-Ada Example");
    }

    [Fact]
    public void Only_a_practice_is_used_for_both()
    {
        var names = OfficeDisplayNames.Compose(" ", " TEST Spine Institute ", Slug);

        names.DoctorAndPracticeName.ShouldBe("TEST Spine Institute");
        names.PracticeName.ShouldBe("TEST Spine Institute");
    }

    [Fact]
    public void Neither_name_falls_back_to_the_slug_never_blank()
    {
        var names = OfficeDisplayNames.Compose(null, "", Slug);

        names.DoctorAndPracticeName.ShouldBe(Slug);
        names.PracticeName.ShouldBe(Slug);
    }

    [Fact]
    public void Nothing_at_all_is_blank_not_an_exception()
    {
        OfficeDisplayNames.Compose(null, null, null).ShouldBe(OfficeDisplayNames.None);
    }

    [Fact]
    public async Task Outside_an_office_nothing_is_read()
    {
        var rig = new Rig(officeId: null);

        var names = await rig.Build().ResolveCurrentAsync();

        names.ShouldBe(OfficeDisplayNames.None);
        await rig.TenantStore.DidNotReceiveWithAnyArgs().FindAsync(default(Guid));
    }

    [Fact]
    public async Task The_stored_letterhead_names_win_and_the_slug_is_not_read()
    {
        var rig = new Rig(OfficeA);
        rig.Doctors.Add(NewDoctor("TEST-Ada", "TEST-Example"));
        var branding = new OfficeBranding(Guid.NewGuid(), OfficeA);
        branding.SetDisplayName("TEST Display Name");
        branding.SetLetterhead(new OfficeLetterheadValues
        {
            PhysicianName = "TEST-Ada Example, M.D.",
            PracticeName = "TEST Spine Institute",
        });
        rig.Brandings.Add(branding);

        var names = await rig.Build().ResolveCurrentAsync();

        names.DoctorAndPracticeName.ShouldBe("TEST-Ada Example, M.D. (TEST Spine Institute)");
        names.PracticeName.ShouldBe("TEST Spine Institute");
        await rig.TenantStore.DidNotReceiveWithAnyArgs().FindAsync(default(Guid));
    }

    [Fact]
    public async Task With_no_letterhead_the_office_doctor_and_display_name_are_used()
    {
        var rig = new Rig(OfficeA);
        rig.Doctors.Add(NewDoctor("TEST-Ada", "TEST-Example"));
        var branding = new OfficeBranding(Guid.NewGuid(), OfficeA);
        branding.SetDisplayName("TEST Display Name");
        rig.Brandings.Add(branding);

        var names = await rig.Build().ResolveCurrentAsync();

        names.DoctorAndPracticeName.ShouldBe("Dr. TEST-Ada TEST-Example (TEST Display Name)");
        names.PracticeName.ShouldBe("TEST Display Name");
    }

    [Fact]
    public async Task An_office_with_no_names_at_all_shows_its_slug()
    {
        var rig = new Rig(OfficeA);
        rig.TenantStore.FindAsync(OfficeA).Returns(new TenantConfiguration(OfficeA, Slug));

        var names = await rig.Build().ResolveCurrentAsync();

        names.DoctorAndPracticeName.ShouldBe(Slug);
        names.PracticeName.ShouldBe(Slug);
        await rig.TenantStore.Received(1).FindAsync(OfficeA);
    }

    private static Doctor NewDoctor(string first, string last) =>
        new(Guid.NewGuid(), first, last, "doctor@test.local", Gender.Other);

    private sealed class Rig
    {
        public List<Doctor> Doctors { get; } = new();
        public List<OfficeBranding> Brandings { get; } = new();
        public ITenantStore TenantStore { get; } = Substitute.For<ITenantStore>();
        private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
        private Guid? _effectiveTenant;

        public Rig(Guid? officeId)
        {
            _effectiveTenant = officeId;
            _tenant.Id.Returns(_ => _effectiveTenant);
            _tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(ci =>
            {
                var previous = _effectiveTenant;
                _effectiveTenant = ci.ArgAt<Guid?>(0);
                return new Restore(() => _effectiveTenant = previous);
            });
        }

        public OfficeDisplayNameResolver Build()
        {
            var doctors = Substitute.For<IReadOnlyRepository<Doctor, Guid>>();
            doctors.GetListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ => Doctors.ToList());

            var brandings = Substitute.For<IReadOnlyRepository<OfficeBranding, Guid>>();
            brandings.AsyncExecuter.Returns(new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>()));
            brandings.GetQueryableAsync().Returns(_ => Brandings.AsQueryable());

            var letterhead = new OfficeLetterheadResolver(doctors, brandings, _tenant);
            return new OfficeDisplayNameResolver(letterhead, TenantStore, _tenant);
        }
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
