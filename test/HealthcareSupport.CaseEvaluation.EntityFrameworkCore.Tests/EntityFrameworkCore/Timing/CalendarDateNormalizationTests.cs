using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Guids;
using Volo.Abp.Json;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Timing;

/// <summary>
/// Enforces the split between the two kinds of value that live in a <c>DateTime</c> column here.
/// This is the companion to the <c>[DisableDateTimeNormalization]</c> attributes in the domain and
/// to the clock pin in <c>CaseEvaluationDomainModule</c>.
///
/// <para>THE PROBLEM, measured rather than assumed (2026-08-27). Pinning
/// <c>AbpClockOptions.Kind = DateTimeKind.Utc</c> -- which the app needs, so that instants reach
/// the browser as unambiguous UTC rather than as a bare local-looking string -- also switches on
/// ABP's <c>AbpDateTimeValueConverter</c> for EVERY mapped <c>DateTime</c> property. On read the
/// converter runs <c>IClock.Normalize</c>, which stamps an <c>Unspecified</c> value as
/// <c>Utc</c>. Measured round trip BEFORE the exemptions were added:</para>
/// <code>
/// WROTE      1985-09-03T00:00:00 Kind=Unspecified
/// READ BACK  1985-09-03T00:00:00.0000000Z Kind=Utc
/// SERIALIZED {"dateOfBirth":"1985-09-03T00:00:00Z"}
/// </code>
/// <para>A browser in Pacific time renders that as 2 September 1985. A patient's date of birth,
/// one day early, on a medical-legal record -- and nothing in the data would show it had moved.</para>
///
/// <para>THE SPLIT. An INSTANT is a moment that happened; it is stored UTC and must keep its
/// <c>Z</c> so the display layer can convert it to Pacific. A CALENDAR DATE is a date somebody
/// wrote down -- a date of birth, a date of injury, the appointment date, a slot date. It has no
/// time zone, so there is nothing to convert and any conversion is corruption. Those carry
/// <c>[DisableDateTimeNormalization]</c>.</para>
///
/// <para>WHY A TEST AND NOT A CODE REVIEW. The attribute is invisible at every call site and its
/// absence changes no behaviour that a build, a type check or an ordinary test would notice -- the
/// value is still a valid DateTime, still the right day in the database, and only wrong once it
/// reaches a reader. So the failure mode of forgetting it is a silently wrong date on a legal
/// document. This test fails instead: a NEW <c>DateTime</c> property must be classified here
/// before it can ship, and a renamed or removed one fails as a stale entry.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class CalendarDateNormalizationTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    /// <summary>
    /// Properties that hold a CALENDAR DATE and must NOT be normalized. Adding a date-shaped
    /// column means adding it here AND putting <c>[DisableDateTimeNormalization]</c> on it.
    /// </summary>
    private static readonly HashSet<string> CalendarDates = new(StringComparer.Ordinal)
    {
        "Appointment.AppointmentDate",
        "Appointment.DueDate",
        "Appointment.PatientDateOfBirth",
        "AppointmentInjuryDetail.DateOfInjury",
        "AppointmentInjuryDetail.ToDateOfInjury",
        "DoctorAvailability.AvailableDate",
        "Patient.DateOfBirth",
    };

    /// <summary>
    /// The assembly whose entities this invariant governs. Scoped deliberately.
    ///
    /// <para>ABP's own module entities (IdentityUser, AuditLog, EntityChange, OpenIddict*, Tenant,
    /// FileDescriptor, ...) turn out NOT to be normalized at all -- measured 2026-08-27, roughly
    /// 55 such properties in the host model carry no value converter. We cannot annotate them and
    /// we should not assert anything about them, because the answer is ABP's to change on any
    /// upgrade and a name list would go stale silently.</para>
    ///
    /// <para>TWO CONSEQUENCES WORTH KNOWING, both outside this test. (1) <c>AuditLog.ExecutionTime</c>
    /// and <c>EntityChange.ChangeTime</c> ARE rendered in this app -- the admin hub's log table and
    /// the appointment change-log timeline -- and since they are not normalized they reach the
    /// browser with no zone designator and are read as local time. Those two surfaces need the
    /// instant converted explicitly; pinning the clock does not reach them. (2)
    /// <c>Tenant.ActivationEndDate</c> is semantically a calendar date we cannot annotate; it is
    /// accepted as-is because only the host tenant-management screen shows it and no patient
    /// record depends on it.</para>
    /// </summary>
    private static readonly System.Reflection.Assembly OwnedDomainAssembly =
        typeof(Patient).Assembly;

    [Fact]
    public async Task HostContext_ClassifiesEveryDateTimeProperty()
    {
        await AssertModelClassifiedAsync<CaseEvaluationDbContext>();
    }

    [Fact]
    public async Task TenantContext_ClassifiesEveryDateTimeProperty()
    {
        // The office databases are a SEPARATE DbContext with its own model. An attribute that
        // lands on the entity covers both, but a mapping that diverges would not, and divergence
        // between the two contexts has bitten this codebase before.
        await AssertModelClassifiedAsync<CaseEvaluationTenantDbContext>();
    }

    private async Task AssertModelClassifiedAsync<TDbContext>()
        where TDbContext : Volo.Abp.EntityFrameworkCore.AbpDbContext<TDbContext>
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetRequiredService<IDbContextProvider<TDbContext>>().GetDbContextAsync();

            var wrongly = new List<string>();
            var unclassified = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in DateTimeProperties(dbContext))
            {
                var key = $"{property.DeclaringType.ClrType.Name}.{property.Name}";
                seen.Add(key);

                var isNormalized = property.GetValueConverter() != null;

                if (CalendarDates.Contains(key))
                {
                    if (isNormalized)
                    {
                        wrongly.Add($"{key} is a calendar date but IS being normalized");
                    }
                }
                else if (!isNormalized)
                {
                    unclassified.Add(key);
                }
            }

            wrongly.ShouldBeEmpty(
                "A calendar date is being normalized to UTC, so it will serialize with a trailing Z " +
                "and every browser will render the previous day. Put [DisableDateTimeNormalization] " +
                "on the property. Offenders: " + string.Join("; ", wrongly));

            unclassified.ShouldBeEmpty(
                "These DateTime properties are exempt from UTC normalization but are not listed as " +
                "calendar dates, so nobody has said which kind of value they hold. Classify each " +
                "one: if it is a moment that happened, remove [DisableDateTimeNormalization]; if it " +
                "is a date somebody wrote down, add it to CalendarDates in this file. Offenders: " +
                string.Join("; ", unclassified));

            // Stale-entry check, scoped to entities this context actually maps. The two contexts do
            // not map the same set, so a missing ENTITY here means "not in this database", which is
            // fine; a missing PROPERTY on an entity that IS mapped means the property was renamed
            // or removed and the list was not updated -- which would let a renamed date slip back
            // into normalization with nothing to show for it.
            var mappedEntities = dbContext.Model.GetEntityTypes()
                .Where(entityType => entityType.ClrType.Assembly == OwnedDomainAssembly)
                .Select(entityType => entityType.ClrType.Name)
                .ToHashSet(StringComparer.Ordinal);

            var stale = CalendarDates
                .Where(key => !seen.Contains(key))
                .Where(key => mappedEntities.Contains(key.Split('.')[0]))
                .ToList();

            stale.ShouldBeEmpty(
                "These entries name a property that no longer exists on an entity this context " +
                $"maps ({typeof(TDbContext).Name}), so the property was renamed or removed and " +
                "this list was not updated. Stale: " + string.Join("; ", stale));
        });
    }

    private static IEnumerable<IProperty> DateTimeProperties(Microsoft.EntityFrameworkCore.DbContext dbContext)
    {
        return dbContext.Model.GetEntityTypes()
            .Where(entityType => entityType.ClrType.Assembly == OwnedDomainAssembly)
            .SelectMany(entityType => entityType.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?));
    }

    [Fact]
    public async Task DateOfBirth_SurvivesARoundTripWithoutGainingAZone()
    {
        // The behavioural half. The model check above proves the CONFIGURATION; this proves the
        // OUTCOME -- that a date of birth written as a bare date comes back as a bare date and
        // goes onto the wire without a zone designator for a browser to reinterpret.
        var repository = GetRequiredService<IRepository<Patient, Guid>>();
        var json = GetRequiredService<IJsonSerializer>();
        var id = GetRequiredService<IGuidGenerator>().Create();
        var dateOfBirth = new DateTime(1985, 9, 3, 0, 0, 0, DateTimeKind.Unspecified);

        await WithUnitOfWorkAsync(async () =>
        {
            await repository.InsertAsync(
                new Patient(
                    id: id,
                    stateId: null,
                    appointmentLanguageId: null,
                    identityUserId: null,
                    tenantId: null,
                    firstName: "Calendar",
                    lastName: "Date",
                    email: "calendar.date@example.test",
                    genderId: Gender.Unspecified,
                    dateOfBirth: dateOfBirth,
                    phoneNumberTypeId: PhoneNumberType.Home),
                autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var reread = await repository.GetAsync(id);

            reread.DateOfBirth.ShouldBe(dateOfBirth);
            reread.DateOfBirth.Kind.ShouldBe(
                DateTimeKind.Unspecified,
                "A date of birth read back as Utc serializes with a trailing Z and a Pacific " +
                "browser then renders the day before. Measured before the exemption: " +
                "wrote 1985-09-03 Unspecified, read back 1985-09-03T00:00:00.0000000Z Kind=Utc.");

            json.Serialize(new { reread.DateOfBirth })
                .ShouldNotContain(
                    "Z",
                    Case.Sensitive,
                    "A calendar date must reach the client with no zone designator, so the client " +
                    "renders the day that was entered rather than converting it.");

            // CreationTime is the contrast case and must keep its Z -- the display layer converts
            // it to Pacific, which it can only do from an unambiguous instant.
            reread.CreationTime.Kind.ShouldBe(DateTimeKind.Utc);
        });
    }
}
