using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
/// <see cref="OfficeLetterheadResolver"/> reads the doctor from the office database and the
/// branding row from the HOST database. These pin which office's row it reads and that the
/// branding read happens with the tenant switched to the host. Substituted repositories, the
/// same shape as PacketTokenResolverTests; all values synthetic.
/// </summary>
public class OfficeLetterheadResolverTests
{
    private static readonly Guid OfficeA = Guid.NewGuid();
    private static readonly Guid OfficeB = Guid.NewGuid();

    private sealed class Rig
    {
        public List<Doctor> Doctors { get; } = new();
        public List<OfficeBranding> Brandings { get; } = new();
        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();

        /// <summary>The tenant id in effect each time the branding queryable was read.</summary>
        public List<Guid?> BrandingReadTenants { get; } = new();

        private Guid? _effectiveTenant;

        public Rig(Guid? officeId)
        {
            _effectiveTenant = officeId;
            Tenant.Id.Returns(_ => _effectiveTenant);
            Tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(ci =>
            {
                var previous = _effectiveTenant;
                _effectiveTenant = ci.ArgAt<Guid?>(0);
                return new Restore(() => _effectiveTenant = previous);
            });
        }

        public OfficeLetterheadResolver Build()
        {
            var doctors = Substitute.For<IReadOnlyRepository<Doctor, Guid>>();
            doctors.GetListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ => Doctors.ToList());

            var brandings = Substitute.For<IReadOnlyRepository<OfficeBranding, Guid>>();
            brandings.AsyncExecuter.Returns(new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>()));
            brandings.GetQueryableAsync().Returns(_ =>
            {
                BrandingReadTenants.Add(_effectiveTenant);
                return Brandings.AsQueryable();
            });

            return new OfficeLetterheadResolver(doctors, brandings, Tenant);
        }
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    private static Doctor NewDoctor(string first, string last, DateTime created)
    {
        var doctor = new Doctor(Guid.NewGuid(), first, last, "doctor@test.local", Gender.Other);
        typeof(Doctor).GetProperty(nameof(Doctor.CreationTime))!.SetValue(doctor, created);
        return doctor;
    }

    [Fact]
    public async Task Host_context_has_no_office_and_reads_nothing()
    {
        var rig = new Rig(officeId: null);

        var letterhead = await rig.Build().ResolveAsync();

        letterhead.PhysicianName.ShouldBeEmpty();
        rig.BrandingReadTenants.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_office_with_no_branding_row_prints_its_own_doctor()
    {
        var rig = new Rig(OfficeA);
        rig.Doctors.Add(NewDoctor("TEST-Ada", "TEST-Example", new DateTime(2031, 1, 1)));

        var letterhead = await rig.Build().ResolveAsync();

        letterhead.LetterheadName.ShouldBe("Dr. TEST-Ada TEST-Example");
    }

    [Fact]
    public async Task Reads_THIS_offices_branding_row_from_the_host_database()
    {
        var rig = new Rig(OfficeA);
        rig.Doctors.Add(NewDoctor("TEST-Ada", "TEST-Example", new DateTime(2031, 1, 1)));
        var mine = new OfficeBranding(Guid.NewGuid(), OfficeA);
        mine.SetLetterhead(new OfficeLetterheadValues { PracticeName = "TEST Office A Institute" });
        var theirs = new OfficeBranding(Guid.NewGuid(), OfficeB);
        theirs.SetLetterhead(new OfficeLetterheadValues { PracticeName = "TEST Office B Institute" });
        rig.Brandings.AddRange(new[] { theirs, mine });

        var letterhead = await rig.Build().ResolveAsync();

        letterhead.PracticeName.ShouldBe("TEST Office A Institute");
        // The row is host-only: reading it in office context would query the office database.
        rig.BrandingReadTenants.ShouldBe(new Guid?[] { null });
    }

    [Fact]
    public async Task The_oldest_doctor_wins_so_the_name_cannot_flip_between_renders()
    {
        var rig = new Rig(OfficeA);
        rig.Doctors.Add(NewDoctor("TEST-Later", "TEST-Doc", new DateTime(2031, 6, 1)));
        rig.Doctors.Add(NewDoctor("TEST-First", "TEST-Doc", new DateTime(2031, 1, 1)));

        var source = await rig.Build().LoadSourceAsync();

        source.DoctorFirstName.ShouldBe("TEST-First");
        source.OfficeId.ShouldBe(OfficeA);
    }
}
