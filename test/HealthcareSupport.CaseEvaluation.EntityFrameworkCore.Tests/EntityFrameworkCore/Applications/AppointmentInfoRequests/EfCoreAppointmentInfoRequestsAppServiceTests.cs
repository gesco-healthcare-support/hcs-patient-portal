using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Pins <see cref="AppointmentInfoRequestsAppService"/> -- the Send Back / fix-it seam. Staff flag
/// booking fields and add a note (Pending -&gt; InfoRequested); the external party corrects ONLY the
/// flagged fields and resubmits (InfoRequested -&gt; Pending), with a before/after snapshot captured
/// on each side so staff can see what actually changed.
///
/// <para>The four helper types this service leans on (<c>InfoRequestFields</c>,
/// <c>InfoRequestCorrectionLock</c>, <c>InfoRequestSnapshot</c>, the entity's capture methods) are
/// already unit-tested next door in Application.Tests / Domain.Tests. Nothing here re-tests them.
/// What is covered here is only what needs a database and the real ABP pipeline: the status gates,
/// the server-side flagged-field lock as the service applies it, the fail-closed resubmit gate, the
/// owning-row create-if-absent path, the injury-detail replacement, and the history projection.</para>
///
/// <para><b>What these tests do NOT pin.</b></para>
/// <list type="bullet">
/// <item>Authorization. <c>AddAlwaysAllowAuthorization()</c> runs in the test module, so every
/// <c>[Authorize]</c> on this service is a no-op and an authorization-failure assertion could not
/// fail. The access-denial facts below assert the BUSINESS rule in
/// <see cref="AppointmentReadAccessGuard"/> (a <see cref="BusinessException"/> carrying
/// <c>CaseEvaluationDomainErrorCodes.AppointmentAccessDenied</c>), which is a live rule.</item>
/// <item>The three null/whitespace input guards. <c>SendBackAsync</c>'s <c>input == null</c> branch
/// and <c>SaveCorrectionsAsync</c>'s <c>Check.NotNull(input)</c> are unreachable through the public
/// surface because ABP's method-invocation validator rejects a null non-optional reference parameter
/// first; the whitespace-Note branch is likewise pre-empted by <c>[Required]</c> on
/// <c>SendBackAppointmentInput.Note</c>, which rejects whitespace-only strings. A test written for
/// any of the three would assert the validator while appearing to assert the service.</item>
/// <item>The state/language display-name resolution in <c>ResolveDisplayValueAsync</c>. The
/// <c>State</c> / <c>AppointmentLanguage</c> catalogs are seeded HOST-scoped while the entities are
/// <see cref="IMultiTenant"/>, so a <c>FindAsync</c> issued inside a tenant scope returns null and
/// the code falls through to <c>?? raw</c>. Nothing here flags a <c>stateId</c> /
/// <c>appointmentLanguageId</c> key for that reason.</item>
/// <item>Rollback. The rig runs <c>AddAlwaysDisableUnitOfWorkTransaction()</c>, so a partial write
/// before a throw STAYS written. Where a fact asserts a value was not written it is an ORDERING
/// claim (the throw provably precedes the first write), and says so at the assertion.</item>
/// <item>Notification content. The rig seeds only four host-scoped notification templates, so a
/// render inside a tenant throws <c>CaseEvaluation:NotificationTemplate.NotFound</c>.
/// <c>StatusChangeEmailHandler</c> already catches exactly that code and only logs, which is why
/// <c>SendBackAsync</c> completes here at all -- but it also means there is no email to assert on.</item>
/// </list>
///
/// <para>Every fact builds its own appointment AND its own patient, and every assertion filters by
/// an id or a <c>TEST-</c> token the fact itself created. The collection shares one SQLite
/// connection with no rollback between tests, so rows accumulate across the whole run and a count
/// over rows this fixture did not create would be meaningless.</para>
///
/// <para>Application-service calls are made OUTSIDE any test-owned unit of work, on purpose. ABP's
/// interceptor opens and completes its own unit of work around the call, which is what makes the
/// status-changed local event dispatch inside the call exactly as in production; an assertion
/// placed inside a test-owned <c>WithUnitOfWorkAsync</c> would read pre-dispatch state. Seeding and
/// read-back each get their own <c>WithUnitOfWorkAsync</c> so a multi-step sequence shares one
/// ambient DbContext.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentInfoRequestsAppServiceTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IAppointmentInfoRequestsAppService _sut;
    private readonly IRepository<AppointmentInfoRequest, Guid> _infoRequestRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryDetailRepository;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerRepository;
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _insuranceRepository;
    private readonly IRepository<AppointmentAccessor, Guid> _accessorRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    /// <summary>
    /// The external party every fix-it fact acts as. Deliberately NOT internal: "admin" is in
    /// <c>BookingFlowRoles.InternalUserRoles</c> and the rig's ambient default principal holds it,
    /// so a gate fact that forgot to impersonate would short-circuit both CanRead and CanEdit and
    /// pass with the gate deleted.
    /// </summary>
    private static readonly Guid ExternalUserId = IdentityUsersTestData.ApplicantAttorney1UserId;

    private const string ExternalUserEmail = IdentityUsersTestData.ApplicantAttorney1Email;
    private const string ExternalUserRole = IdentityUsersTestData.ApplicantAttorneyRoleName;

    /// <summary>
    /// The seeded users carry no Name / Surname, so <c>ResolveNameAsync</c> falls through to the
    /// UserName. That makes the resolved display name a fixed literal rather than a guess.
    /// </summary>
    private const string ExternalUserDisplayName = IdentityUsersTestData.ApplicantAttorney1UserName;

    public EfCoreAppointmentInfoRequestsAppServiceTests()
    {
        _sut = GetRequiredService<IAppointmentInfoRequestsAppService>();
        _infoRequestRepository = GetRequiredService<IRepository<AppointmentInfoRequest, Guid>>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _injuryDetailRepository = GetRequiredService<IRepository<AppointmentInjuryDetail, Guid>>();
        _documentRepository = GetRequiredService<IRepository<AppointmentDocument, Guid>>();
        _employerRepository = GetRequiredService<IRepository<AppointmentEmployerDetail, Guid>>();
        _insuranceRepository = GetRequiredService<IRepository<AppointmentPrimaryInsurance, Guid>>();
        _accessorRepository = GetRequiredService<IRepository<AppointmentAccessor, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // =====================================================================
    // SendBackAsync
    // =====================================================================

    /// <summary>
    /// The bare <c>Guid</c> route parameter carries no validation attribute, so the service's own
    /// empty-id guard is the first thing that runs and is genuinely reachable -- unlike the null
    /// and whitespace guards beside it, which the DTO validator pre-empts.
    /// </summary>
    [Fact]
    public async Task SendBackAsync_EmptyAppointmentId_ThrowsBeforeTouchingTheDatabase()
    {
        var input = new SendBackAppointmentInput
        {
            Note = "TEST-please correct the cell phone number.",
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.SendBackAsync(Guid.Empty, input));

            // Without the guard the call reaches GetAsync(Guid.Empty) and raises
            // EntityNotFoundException instead, which is not a UserFriendlyException.
            ex.Message.ShouldContain("required", Case.Insensitive);
        }
    }

    [Fact]
    public async Task SendBackAsync_UnknownAppointment_ThrowsNotFound()
    {
        var unknownAppointmentId = Guid.NewGuid();
        var input = new SendBackAppointmentInput
        {
            Note = "TEST-please correct the cell phone number.",
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await Should.ThrowAsync<EntityNotFoundException>(
                async () => await _sut.SendBackAsync(unknownAppointmentId, input));
        }
    }

    /// <summary>
    /// Only a Pending appointment can be sent back. The service checks this itself rather than
    /// letting the state machine refuse, so the caller gets the feature's own wording instead of
    /// the generic invalid-transition code.
    /// </summary>
    [Fact]
    public async Task SendBackAsync_AppointmentNotPending_Throws()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.Approved);
        var input = new SendBackAppointmentInput
        {
            Note = $"TEST-{fixture.Token} please correct the cell phone number.",
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.SendBackAsync(fixture.AppointmentId, input));

            // Drop the status check and the manager refuses the transition instead, with a plain
            // BusinessException carrying AppointmentInvalidTransition -- a different type, and a
            // code the external user cannot act on.
            ex.Message.ShouldContain("pending", Case.Insensitive);
        }
    }

    /// <summary>
    /// The happy path: one Open row is written carrying the trimmed note, the caller's user id and
    /// the appointment's tenant, and the appointment moves to InfoRequested.
    /// </summary>
    [Fact]
    public async Task SendBackAsync_OpensRequestAndMovesAppointmentToInfoRequested()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.Pending);
        var input = new SendBackAppointmentInput
        {
            // Padded on both sides on purpose: the service trims before persisting.
            Note = $"  TEST-{fixture.Token} please correct the cell phone number.  ",
            FlaggedFields = new List<FlaggedFieldDto>
            {
                new FlaggedFieldDto { Key = "cellPhoneNumber", Hint = "TEST-hint" },
            },
        };

        AppointmentInfoRequestDto dto;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            dto = await _sut.SendBackAsync(fixture.AppointmentId, input);
        }

        dto.AppointmentId.ShouldBe(fixture.AppointmentId);
        dto.Note.ShouldBe($"TEST-{fixture.Token} please correct the cell phone number.");
        dto.Status.ShouldBe(InfoRequestStatus.Open);
        dto.RequestedByUserId.ShouldBe(ExternalUserId);
        dto.ResolvedAt.ShouldBeNull();
        dto.FlaggedFields.Count.ShouldBe(1);
        dto.FlaggedFields[0].Key.ShouldBe("cellPhoneNumber");
        dto.FlaggedFields[0].Hint.ShouldBe("TEST-hint");

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var persisted = await _infoRequestRepository.GetAsync(dto.Id);
                persisted.Note.ShouldBe($"TEST-{fixture.Token} please correct the cell phone number.");
                persisted.RequestedByUserId.ShouldBe(ExternalUserId);
                persisted.TenantId.ShouldBe(TenantsTestData.TenantARef);
                persisted.Status.ShouldBe(InfoRequestStatus.Open);

                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.InfoRequested);
            }
        });
    }

    /// <summary>
    /// The "before" half of the staff diff: the flagged field's CURRENT value is snapshotted at
    /// send-back time, because by the time the external party resubmits it is gone.
    /// </summary>
    [Fact]
    public async Task SendBackAsync_CapturesTheFlaggedFieldsCurrentValueAsBefore()
    {
        var fixture = await CreateAppointmentAsync(
            AppointmentStatusType.Pending,
            cellPhoneNumber: "5550100123");

        var input = new SendBackAppointmentInput
        {
            Note = $"TEST-{fixture.Token} please correct the cell phone number.",
            FlaggedFields = new List<FlaggedFieldDto> { new FlaggedFieldDto { Key = "cellPhoneNumber" } },
        };

        AppointmentInfoRequestDto dto;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            dto = await _sut.SendBackAsync(fixture.AppointmentId, input);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var persisted = await _infoRequestRepository.GetAsync(dto.Id);
                var before = persisted.BeforeValues;
                before.ShouldNotBeNull();
                before!.ShouldContain("cellPhoneNumber");
                before!.ShouldContain("5550100123");
            }
        });
    }

    /// <summary>
    /// HIPAA: the snapshot is a SECOND copy of the patient's data in a different table, so the SSN
    /// must be masked before it is written there. The fixture's SSN is hex-shaped synthetic data,
    /// deliberately clear of the XXX-XX-XXXX pattern a PHI scanner matches.
    ///
    /// <para>Assertions are on the digits rather than on the literal "***-**-3456" because
    /// <c>JsonSerializer</c>'s default encoder may escape the asterisks; the digit checks hold
    /// either way. "1234" are the raw value's leading digits and appear ONLY if masking is
    /// bypassed; "3456" are the last four and appear only because masking produced them.</para>
    /// </summary>
    [Fact]
    public async Task SendBackAsync_MasksTheSsnInTheCapturedSnapshot()
    {
        var fixture = await CreateAppointmentAsync(
            AppointmentStatusType.Pending,
            socialSecurityNumber: "ab1234cd56");

        var input = new SendBackAppointmentInput
        {
            Note = $"TEST-{fixture.Token} please correct the social security number.",
            FlaggedFields = new List<FlaggedFieldDto>
            {
                new FlaggedFieldDto { Key = "socialSecurityNumber" },
            },
        };

        AppointmentInfoRequestDto dto;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            dto = await _sut.SendBackAsync(fixture.AppointmentId, input);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var persisted = await _infoRequestRepository.GetAsync(dto.Id);
                var before = persisted.BeforeValues;
                before.ShouldNotBeNull();
                before!.ShouldNotContain("ab1234cd56");
                before!.ShouldNotContain("1234");
                before!.ShouldContain("3456");
            }
        });
    }

    // =====================================================================
    // ResubmitAsync
    // =====================================================================

    /// <summary>
    /// The resubmit transition is gated by the SLIM edit rule (internal / creator-booker /
    /// Edit-accessor), not by the read gate. The caller here is a seeded external user who is
    /// none of the three.
    ///
    /// <para>The Edit-accessor row for a DIFFERENT user is load-bearing, not setup noise. A denial
    /// asserted against an empty accessor table would also pass with the accessor pathway deleted,
    /// because nothing would be admitted either way. The decoy is the contrast case that makes the
    /// denial mean something.</para>
    /// </summary>
    [Fact]
    public async Task ResubmitAsync_CallerWithoutEditAccess_ThrowsAccessDenied()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the cell phone number.",
            new[] { "cellPhoneNumber" });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                // Decoy: SOMEONE holds Edit on this appointment, just not the caller below.
                await _accessorRepository.InsertAsync(
                    new AppointmentAccessor(
                        Guid.NewGuid(),
                        IdentityUsersTestData.DefenseAttorney1UserId,
                        fixture.AppointmentId,
                        AccessType.Edit)
                    {
                        TenantId = TenantsTestData.TenantARef,
                    },
                    autoSave: true);
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(
                   _principalAccessor,
                   IdentityUsersTestData.ClaimExaminer1UserId,
                   IdentityUsersTestData.ClaimExaminer1Email,
                   IdentityUsersTestData.ClaimExaminerRoleName))
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _sut.ResubmitAsync(fixture.AppointmentId));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.InfoRequested);
            }
        });
    }

    /// <summary>
    /// The fix-it page disables Resubmit until every flagged field is addressed, but that gate is
    /// client-side only, so a direct API call could resubmit an un-fixed appointment. The server
    /// re-checks, FAIL-CLOSED. Both halves are asserted: blocked while the flagged scalar is empty,
    /// allowed once it holds a value. Without the second half a deleted gate would be invisible in
    /// the first.
    /// </summary>
    [Fact]
    public async Task ResubmitAsync_UnresolvedFlaggedScalar_BlocksUntilItHoldsAValue()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the cell phone number.",
            new[] { "cellPhoneNumber" });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.ResubmitAsync(fixture.AppointmentId));

            ex.Message.ShouldContain("complete all the requested corrections");
        }

        // The gate throws before the transition, so the status is untouched. This is an ORDERING
        // claim, not a rollback claim: the rig disables unit-of-work transactions.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var stillBlocked = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                stillBlocked.AppointmentStatus.ShouldBe(AppointmentStatusType.InfoRequested);

                var patient = await _patientRepository.GetAsync(fixture.PatientId);
                patient.CellPhoneNumber = "5550100999";
                await _patientRepository.UpdateAsync(patient, autoSave: true);
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await _sut.ResubmitAsync(fixture.AppointmentId);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
            }
        });
    }

    /// <summary>
    /// "documents" is not a scalar registry field; it resolves on document COUNT. Both halves are
    /// asserted so the dedicated branch is killable: delete it and the key falls through to the
    /// registry lookup, which also blocks -- so only the allow half distinguishes them.
    /// </summary>
    [Fact]
    public async Task ResubmitAsync_UnresolvedDocuments_BlocksUntilOneIsUploaded()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please upload the missing report.",
            new[] { "documents" });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.ResubmitAsync(fixture.AppointmentId));

            ex.Message.ShouldContain("complete all the requested corrections");
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _documentRepository.InsertAsync(
                    new AppointmentDocument(
                        id: Guid.NewGuid(),
                        tenantId: TenantsTestData.TenantARef,
                        appointmentId: fixture.AppointmentId,
                        documentName: $"TEST-{fixture.Token}-report",
                        fileName: $"TEST-{fixture.Token}.pdf",
                        blobName: $"TEST-{fixture.Token}",
                        contentType: "application/pdf",
                        fileSize: 1024,
                        uploadedByUserId: ExternalUserId),
                    autoSave: true);
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await _sut.ResubmitAsync(fixture.AppointmentId);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
            }
        });
    }

    /// <summary>
    /// Claim Information is the second non-scalar key: it resolves on injury-row COUNT, mirroring
    /// the documents rule. Same block/allow pair, for the same reason.
    /// </summary>
    [Fact]
    public async Task ResubmitAsync_UnresolvedClaimInformation_BlocksUntilAnInjuryRowExists()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please add the claim information.",
            new[] { "claimInformation" });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.ResubmitAsync(fixture.AppointmentId));

            ex.Message.ShouldContain("complete all the requested corrections");
        }

        await SeedInjuryDetailAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token}-CLM",
            new DateTime(2025, 1, 5));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await _sut.ResubmitAsync(fixture.AppointmentId);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
            }
        });
    }

    /// <summary>
    /// FAIL-CLOSED on an unrecognised key. A flagged key that is in neither the scalar registry nor
    /// the two collection rules blocks the resubmit rather than being skipped -- so a frontend that
    /// drifts from the server registry cannot wave an unfixed appointment through.
    /// </summary>
    [Fact]
    public async Task ResubmitAsync_UnknownFlaggedKey_BlocksResubmit()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the unknown field.",
            new[] { "TEST-not-a-real-field" });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.ResubmitAsync(fixture.AppointmentId));

            ex.Message.ShouldContain("complete all the requested corrections");
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.InfoRequested);
            }
        });
    }

    /// <summary>
    /// The open info-request row is OPTIONAL on the resubmit path; the transition is not. An
    /// InfoRequested appointment with no open row (a row resolved out of band, or one that never
    /// existed) still goes back to Pending rather than blowing up on a null.
    /// </summary>
    [Fact]
    public async Task ResubmitAsync_NoOpenRequest_StillTransitionsToPending()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await _sut.ResubmitAsync(fixture.AppointmentId);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
            }
        });
    }

    // =====================================================================
    // SaveCorrectionsAsync
    // =====================================================================

    /// <summary>
    /// Two gates guard corrections: the appointment must be InfoRequested, and there must be an
    /// open row. This fact pins the ORDER as well as the first gate -- the fixture has an open row,
    /// so only the status gate can be responsible for the refusal, and the message says which one
    /// fired. Swap the two checks and the wrong message comes back.
    /// </summary>
    [Fact]
    public async Task SaveCorrectionsAsync_AppointmentNotInfoRequested_ThrowsTheStatusError()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.Pending);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the cell phone number.",
            new[] { "cellPhoneNumber" });

        var input = new SaveInfoRequestCorrectionsInput
        {
            Corrections = new Dictionary<string, string?> { ["cellPhoneNumber"] = "5550100999" },
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.SaveCorrectionsAsync(fixture.AppointmentId, input));

            ex.Message.ShouldContain("not awaiting corrections");
        }
    }

    /// <summary>
    /// The second gate. An InfoRequested appointment with no open row has no flagged set, so there
    /// is nothing a correction could legitimately be locked against.
    /// </summary>
    [Fact]
    public async Task SaveCorrectionsAsync_NoOpenRequest_Throws()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);

        var input = new SaveInfoRequestCorrectionsInput
        {
            Corrections = new Dictionary<string, string?> { ["cellPhoneNumber"] = "5550100999" },
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.SaveCorrectionsAsync(fixture.AppointmentId, input));

            ex.Message.ShouldContain("no open information request");
        }
    }

    /// <summary>
    /// THE security rule of this endpoint: the external party may only change the fields staff
    /// flagged. Everything else on the booking is off limits even though the same endpoint can
    /// physically write it.
    ///
    /// <para>The unchanged-Street assertion is an ORDERING claim, not a rollback claim -- the rig
    /// disables unit-of-work transactions, so it is only meaningful because the lock throws before
    /// the first write, which is exactly the property worth pinning.</para>
    /// </summary>
    [Fact]
    public async Task SaveCorrectionsAsync_RejectsAChangeToAnUnflaggedField()
    {
        var fixture = await CreateAppointmentAsync(
            AppointmentStatusType.InfoRequested,
            street: "TEST-1 Synthetic Way");
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the cell phone number.",
            new[] { "cellPhoneNumber" });

        var input = new SaveInfoRequestCorrectionsInput
        {
            Corrections = new Dictionary<string, string?>
            {
                ["street"] = $"TEST-{fixture.Token} Unflagged Street",
            },
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.SaveCorrectionsAsync(fixture.AppointmentId, input));

            ex.Message.ShouldContain("only change the fields");
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var patient = await _patientRepository.GetAsync(fixture.PatientId);
                patient.Street.ShouldBe("TEST-1 Synthetic Way");
            }
        });
    }

    /// <summary>
    /// A flagged value supplied in the generic map reaches the entity the registry says owns it.
    /// Two keys with two different owners in one call, so the owner routing is exercised rather
    /// than assumed: drop the Patient load in the bundle builder and the phone stops landing while
    /// the appointment-level field still does.
    /// </summary>
    [Fact]
    public async Task SaveCorrectionsAsync_WritesFlaggedValuesToTheirOwningEntities()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the phone and the referral.",
            new[] { "cellPhoneNumber", "refferedBy" });

        var input = new SaveInfoRequestCorrectionsInput
        {
            Corrections = new Dictionary<string, string?>
            {
                ["cellPhoneNumber"] = "5550100999",
                ["refferedBy"] = $"TEST-{fixture.Token}-ref",
            },
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await _sut.SaveCorrectionsAsync(fixture.AppointmentId, input);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var patient = await _patientRepository.GetAsync(fixture.PatientId);
                patient.CellPhoneNumber.ShouldBe("5550100999");

                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                appointment.RefferedBy.ShouldBe($"TEST-{fixture.Token}-ref");
            }
        });
    }

    /// <summary>
    /// A flagged field can live on a linked section the booking never captured. The bundle builder
    /// creates the owning row in memory and the save inserts it, so the correction is fillable
    /// instead of silently dropped. The Employer leg: the fixture deliberately has no employer row.
    /// </summary>
    [Fact]
    public async Task SaveCorrectionsAsync_CreatesTheEmployerRowWhenTheSectionIsAbsent()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please add the employer name.",
            new[] { "employerName" });

        var input = new SaveInfoRequestCorrectionsInput
        {
            Corrections = new Dictionary<string, string?>
            {
                ["employerName"] = $"TEST-{fixture.Token}-employer",
            },
        };

        await AssertNoRowsAsync(_employerRepository, x => x.AppointmentId == fixture.AppointmentId);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await _sut.SaveCorrectionsAsync(fixture.AppointmentId, input);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            // The service does not stamp TenantId on the row it creates, so the read-back drops the
            // multi-tenant filter and scopes by the appointment id this fixture owns instead.
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var rows = await _employerRepository.GetListAsync(x => x.AppointmentId == fixture.AppointmentId);
                rows.Count.ShouldBe(1);
                rows[0].EmployerName.ShouldBe($"TEST-{fixture.Token}-employer");
            }
        });
    }

    /// <summary>
    /// The Insurance leg of the same create-if-absent rule. Kept as a separate fact from the
    /// employer one because they are separate branches in both the bundle builder and the save, so
    /// deleting either is only visible in its own fact.
    /// </summary>
    [Fact]
    public async Task SaveCorrectionsAsync_CreatesTheInsuranceRowWhenTheSectionIsAbsent()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please add the insurance carrier.",
            new[] { "appointmentInsuranceName" });

        var input = new SaveInfoRequestCorrectionsInput
        {
            Corrections = new Dictionary<string, string?>
            {
                ["appointmentInsuranceName"] = $"TEST-{fixture.Token}-ins",
            },
        };

        await AssertNoRowsAsync(_insuranceRepository, x => x.AppointmentId == fixture.AppointmentId);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await _sut.SaveCorrectionsAsync(fixture.AppointmentId, input);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var rows = await _insuranceRepository.GetListAsync(x => x.AppointmentId == fixture.AppointmentId);
                rows.Count.ShouldBe(1);
                rows[0].Name.ShouldBe($"TEST-{fixture.Token}-ins");
            }
        });
    }

    /// <summary>
    /// "Absent or empty means no change" is applied BEFORE the flagged-field lock, so a form that
    /// posts every control back with the untouched ones blank is not treated as an attack. Remove
    /// the empty-value filter and the blank unflagged key reaches the lock and throws.
    /// </summary>
    [Fact]
    public async Task SaveCorrectionsAsync_EmptyValueForAnUnflaggedKeyIsNotAViolation()
    {
        var fixture = await CreateAppointmentAsync(
            AppointmentStatusType.InfoRequested,
            street: "TEST-1 Synthetic Way");
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the cell phone number.",
            new[] { "cellPhoneNumber" });

        var input = new SaveInfoRequestCorrectionsInput
        {
            Corrections = new Dictionary<string, string?> { ["street"] = "   " },
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await Should.NotThrowAsync(
                async () => await _sut.SaveCorrectionsAsync(fixture.AppointmentId, input));
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var patient = await _patientRepository.GetAsync(fixture.PatientId);
                patient.Street.ShouldBe("TEST-1 Synthetic Way");
            }
        });
    }

    /// <summary>
    /// Claim Information rides alongside the scalar map as a whole replacement set, and it is
    /// under the SAME only-flagged lock. An appointment whose open request did not flag
    /// claimInformation cannot have its injury rows rewritten by supplying a list.
    ///
    /// <para>The "no rows were created" assertion is again an ordering claim: the lock throws
    /// before the replace, and there is no transaction to undo one.</para>
    /// </summary>
    [Fact]
    public async Task SaveCorrectionsAsync_RejectsInjuryDetailsWhenClaimInformationIsNotFlagged()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the cell phone number.",
            new[] { "cellPhoneNumber" });

        var input = new SaveInfoRequestCorrectionsInput
        {
            InjuryDetails = new List<InjuryDetailCorrectionDto>
            {
                BuildInjuryCorrection($"TEST-{fixture.Token}-NEW", new DateTime(2025, 2, 11)),
            },
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _sut.SaveCorrectionsAsync(fixture.AppointmentId, input));

            ex.Message.ShouldContain("only change the fields");
        }

        await AssertNoRowsAsync(_injuryDetailRepository, x => x.AppointmentId == fixture.AppointmentId);
    }

    /// <summary>
    /// When claimInformation IS flagged the supplied list REPLACES the appointment's injury rows
    /// wholesale -- the existing ones are deleted, not merged with.
    ///
    /// <para>The pre-existing row is load-bearing: a negative guarantee cannot be proven against an
    /// empty fixture. Without a row already present the delete loop is a no-op and the fact would
    /// pass with the deletion removed, asserting only what the code ADDS.</para>
    ///
    /// <para>The Corrections map is left empty on purpose. The early return for "no scalar
    /// corrections" sits AFTER the injury replace, so an empty map is exactly the input that
    /// catches someone hoisting that return above it.</para>
    /// </summary>
    [Fact]
    public async Task SaveCorrectionsAsync_ReplacesTheInjuryDetailSetWhenClaimInformationIsFlagged()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the claim information.",
            new[] { "claimInformation" });
        await SeedInjuryDetailAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token}-OLD",
            new DateTime(2025, 1, 5));

        var input = new SaveInfoRequestCorrectionsInput
        {
            InjuryDetails = new List<InjuryDetailCorrectionDto>
            {
                BuildInjuryCorrection($"TEST-{fixture.Token}-NEW", new DateTime(2025, 2, 11)),
            },
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await _sut.SaveCorrectionsAsync(fixture.AppointmentId, input);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            // The soft-delete filter stays ON here on purpose: the deleted row must not come back.
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var rows = await _injuryDetailRepository.GetListAsync(x => x.AppointmentId == fixture.AppointmentId);
                rows.Count.ShouldBe(1);
                rows[0].ClaimNumber.ShouldBe($"TEST-{fixture.Token}-NEW");
                rows[0].WcabAdj.ShouldBe($"TEST-{fixture.Token}-NEW-ADJ");
            }
        });
    }

    // =====================================================================
    // GetInjuryDetailsForCorrectionAsync
    // =====================================================================

    /// <summary>
    /// The fix-it editor prefills from this call, so the rows come back in injury-date order
    /// regardless of insertion order, with every corrected field carried through the projection.
    /// </summary>
    [Fact]
    public async Task GetInjuryDetailsForCorrectionAsync_OrdersByDateOfInjury()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);

        // Inserted out of order on purpose: March, then January, then February.
        await SeedInjuryDetailAsync(fixture.AppointmentId, $"TEST-{fixture.Token}-A", new DateTime(2025, 3, 2));
        await SeedInjuryDetailAsync(fixture.AppointmentId, $"TEST-{fixture.Token}-B", new DateTime(2025, 1, 5));
        await SeedInjuryDetailAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token}-C",
            new DateTime(2025, 2, 11),
            isCumulativeInjury: true);

        List<InjuryDetailCorrectionDto> rows;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            rows = await _sut.GetInjuryDetailsForCorrectionAsync(fixture.AppointmentId);
        }

        rows.Count.ShouldBe(3);
        rows.Select(r => r.ClaimNumber).ShouldBe(new[]
        {
            $"TEST-{fixture.Token}-B",
            $"TEST-{fixture.Token}-C",
            $"TEST-{fixture.Token}-A",
        });

        rows[1].IsCumulativeInjury.ShouldBeTrue();
        rows[1].WcabAdj.ShouldBe($"TEST-{fixture.Token}-C-ADJ");
        rows[1].BodyPartsSummary.ShouldBe($"TEST-{fixture.Token}-C-parts");
        rows[0].IsCumulativeInjury.ShouldBeFalse();
    }

    /// <summary>
    /// This endpoint is gated by the READ-access guard rather than the injury-details CRUD
    /// permission, because external roles do not hold that permission and still have to prefill the
    /// editor. Both halves in one fixture: the named applicant attorney (admitted purely by the
    /// email+role rule -- the booker is a stranger id, so no other pathway can be responsible) gets
    /// the rows; a different external role whose own party-email column is null does not.
    /// </summary>
    [Fact]
    public async Task GetInjuryDetailsForCorrectionAsync_AdmitsTheNamedPartyAndDeniesEveryoneElse()
    {
        var fixture = await CreateAppointmentAsync(
            AppointmentStatusType.InfoRequested,
            bookedByUserId: Guid.NewGuid(),
            applicantAttorneyEmail: ExternalUserEmail);
        await SeedInjuryDetailAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token}-CLM",
            new DateTime(2025, 1, 5));

        List<InjuryDetailCorrectionDto> allowed;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            allowed = await _sut.GetInjuryDetailsForCorrectionAsync(fixture.AppointmentId);
        }

        allowed.Count.ShouldBe(1);
        allowed[0].ClaimNumber.ShouldBe($"TEST-{fixture.Token}-CLM");

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(
                   _principalAccessor,
                   IdentityUsersTestData.ClaimExaminer1UserId,
                   IdentityUsersTestData.ClaimExaminer1Email,
                   IdentityUsersTestData.ClaimExaminerRoleName))
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _sut.GetInjuryDetailsForCorrectionAsync(fixture.AppointmentId));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        }
    }

    // =====================================================================
    // GetOpenAsync
    // =====================================================================

    /// <summary>
    /// GetOpenAsync means OPEN. The fixture holds a Resolved row and nothing else, so the null is
    /// produced by the status predicate rather than by an empty table -- drop the predicate and the
    /// resolved round comes back as if the external party still owed a fix.
    /// </summary>
    [Fact]
    public async Task GetOpenAsync_IgnoresAResolvedRequest()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.Pending);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} already resolved.",
            new[] { "cellPhoneNumber" },
            resolved: true);

        AppointmentInfoRequestDto? open;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            open = await _sut.GetOpenAsync(fixture.AppointmentId);
        }

        open.ShouldBeNull();
    }

    /// <summary>
    /// A corrupt flagged-fields blob degrades to an empty list instead of taking the whole fix-it
    /// page down. The note still has to come back, because that is the part the external user needs
    /// in order to ask what happened.
    /// </summary>
    [Fact]
    public async Task GetOpenAsync_CorruptFlaggedFieldsJson_ReturnsAnEmptyFlaggedList()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} please correct the cell phone number.",
            Array.Empty<string>(),
            rawRequestedFieldsJson: "TEST-not-json");

        AppointmentInfoRequestDto? open;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            open = await _sut.GetOpenAsync(fixture.AppointmentId);
        }

        open.ShouldNotBeNull();
        open!.FlaggedFields.ShouldBeEmpty();
        open!.Note.ShouldBe($"TEST-{fixture.Token} please correct the cell phone number.");
        open!.Status.ShouldBe(InfoRequestStatus.Open);
    }

    // =====================================================================
    // GetHistoryAsync
    // =====================================================================

    /// <summary>
    /// Rounds are NUMBERED oldest-first but RETURNED newest-first, which is two separate decisions
    /// in the same method and is why both are asserted here.
    ///
    /// <para>Two further rules ride along on the same fixture. FlaggedCount counts only the SCALAR
    /// registry keys, so a round that also flagged <c>documents</c> still counts one. And the
    /// requester name resolves to the user's UserName when the account has no Name/Surname, or to
    /// null when the stored user id resolves to nobody -- both branches present, so neither can be
    /// deleted unnoticed.</para>
    /// </summary>
    [Fact]
    public async Task GetHistoryAsync_NumbersRoundsOldestFirstButReturnsNewestFirst()
    {
        var fixture = await CreateAppointmentAsync(AppointmentStatusType.InfoRequested);
        var unknownRequesterId = Guid.NewGuid();

        // CreationTime is set explicitly after insert: two inserts land in the same millisecond
        // often enough that OrderBy(CreationTime) would otherwise be a coin toss.
        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} round one.",
            new[] { "cellPhoneNumber", "documents" },
            requestedByUserId: ExternalUserId,
            creationTime: new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc));

        await SeedInfoRequestAsync(
            fixture.AppointmentId,
            $"TEST-{fixture.Token} round two.",
            new[] { "cellPhoneNumber" },
            requestedByUserId: unknownRequesterId,
            creationTime: new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc));

        List<AppointmentInfoRequestRoundDto> history;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            history = await _sut.GetHistoryAsync(fixture.AppointmentId);
        }

        history.Count.ShouldBe(2);

        history[0].RoundNumber.ShouldBe(2);
        history[0].Note.ShouldBe($"TEST-{fixture.Token} round two.");
        history[0].RequestedByName.ShouldBeNull();

        history[1].RoundNumber.ShouldBe(1);
        history[1].Note.ShouldBe($"TEST-{fixture.Token} round one.");
        history[1].RequestedByName.ShouldBe(ExternalUserDisplayName);

        // documents is not a scalar registry key, so it is excluded from the diff and the count.
        history[1].FlaggedCount.ShouldBe(1);
        history[1].Diffs.Count.ShouldBe(1);
        history[1].Diffs[0].Key.ShouldBe("cellPhoneNumber");

        // Neither round was resubmitted, so nothing reads as fixed and no resubmitter is named.
        history[1].IsResolved.ShouldBeFalse();
        history[1].FixedCount.ShouldBe(0);
        history[1].ResubmittedByName.ShouldBeNull();
    }

    /// <summary>
    /// The whole round end to end through the real service: send back, correct, resubmit, then read
    /// the staff history. This is the only fact that proves the before-snapshot and the
    /// after-snapshot line up into a diff a reviewer can act on -- each half exists in isolation in
    /// the facts above, but nothing there shows them meeting.
    ///
    /// <para><c>ResubmittedByName</c> is deliberately NOT asserted. It resolves from the row's
    /// LastModifierId, which ABP's audit setter NULLS when the acting principal's tenant claim does
    /// not match the entity's tenant -- and the test principal carries no tenant claim. Asserting it
    /// would pin the test rig's impersonation shape, not the service.</para>
    /// </summary>
    [Fact]
    public async Task SendBackCorrectAndResubmit_ProducesAFixedDiffInTheHistory()
    {
        var fixture = await CreateAppointmentAsync(
            AppointmentStatusType.Pending,
            cellPhoneNumber: "5550100123");

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            await _sut.SendBackAsync(
                fixture.AppointmentId,
                new SendBackAppointmentInput
                {
                    Note = $"TEST-{fixture.Token} please correct the cell phone number.",
                    FlaggedFields = new List<FlaggedFieldDto>
                    {
                        new FlaggedFieldDto { Key = "cellPhoneNumber" },
                    },
                });

            await _sut.SaveCorrectionsAsync(
                fixture.AppointmentId,
                new SaveInfoRequestCorrectionsInput
                {
                    Corrections = new Dictionary<string, string?> { ["cellPhoneNumber"] = "5550100999" },
                });

            await _sut.ResubmitAsync(fixture.AppointmentId);
        }

        List<AppointmentInfoRequestRoundDto> history;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, ExternalUserId, ExternalUserEmail, ExternalUserRole))
        {
            history = await _sut.GetHistoryAsync(fixture.AppointmentId);
        }

        history.Count.ShouldBe(1);
        history[0].RoundNumber.ShouldBe(1);
        history[0].IsResolved.ShouldBeTrue();
        history[0].ResolvedAt.ShouldNotBeNull();
        history[0].RequestedByName.ShouldBe(ExternalUserDisplayName);
        history[0].FlaggedCount.ShouldBe(1);
        history[0].FixedCount.ShouldBe(1);
        history[0].Diffs.Count.ShouldBe(1);
        history[0].Diffs[0].Key.ShouldBe("cellPhoneNumber");
        history[0].Diffs[0].OldValue.ShouldBe("5550100123");
        history[0].Diffs[0].NewValue.ShouldBe("5550100999");
        history[0].Diffs[0].Changed.ShouldBeTrue();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);

                var patient = await _patientRepository.GetAsync(fixture.PatientId);
                patient.CellPhoneNumber.ShouldBe("5550100999");
            }
        });
    }

    // =====================================================================
    // Fixture helpers.
    // =====================================================================

    /// <summary>One fact's own appointment, patient and unique token.</summary>
    private sealed record InfoRequestFixture(
        Guid AppointmentId,
        Guid PatientId,
        Guid BookedByUserId,
        string Token);

    /// <summary>
    /// Builds a fresh patient + appointment in TenantA at the requested status.
    ///
    /// <para>A FRESH patient every time, never the seeded Patient1: the corrections path WRITES to
    /// the patient row, and the collection shares one database with no rollback, so reusing the
    /// seeded row would leak mutations into every other test class.</para>
    ///
    /// <para>The insert runs while impersonating the booker so that <c>CreatorId ?? BookedByUserId</c>
    /// -- the coalesce the edit gate uses -- resolves to the booker whether or not ABP's audit
    /// interceptor stamps CreatorId (it skips on a tenant-claim mismatch, and the test principal
    /// carries no tenant claim).</para>
    /// </summary>
    private async Task<InfoRequestFixture> CreateAppointmentAsync(
        AppointmentStatusType status,
        string? cellPhoneNumber = null,
        string? socialSecurityNumber = null,
        string? street = null,
        Guid? bookedByUserId = null,
        string? applicantAttorneyEmail = null)
    {
        var token = Guid.NewGuid().ToString("N").Substring(0, 8);
        var fixture = new InfoRequestFixture(
            AppointmentId: Guid.NewGuid(),
            PatientId: Guid.NewGuid(),
            BookedByUserId: bookedByUserId ?? ExternalUserId,
            Token: token);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.RunWithEmail(
                       _principalAccessor,
                       fixture.BookedByUserId,
                       ExternalUserEmail,
                       ExternalUserRole))
            {
                var patient = new Patient(
                    id: fixture.PatientId,
                    stateId: null,
                    appointmentLanguageId: null,
                    identityUserId: null,
                    tenantId: TenantsTestData.TenantARef,
                    firstName: $"TEST-{token}",
                    lastName: $"TEST-{token}",
                    email: $"TEST-{token}@test.local",
                    genderId: (Gender)PatientsTestData.PatientGenderIdValue,
                    dateOfBirth: PatientsTestData.FixedDateOfBirth,
                    phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue,
                    socialSecurityNumber: socialSecurityNumber,
                    cellPhoneNumber: cellPhoneNumber,
                    street: street);
                await _patientRepository.InsertAsync(patient, autoSave: true);

                var appointment = new Appointment(
                    id: fixture.AppointmentId,
                    patientId: fixture.PatientId,
                    identityUserId: null,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                    appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    // Unique per fixture: (TenantId, RequestConfirmationNumber) is a hard unique
                    // index and these rows accumulate across the whole collection.
                    requestConfirmationNumber: $"A9-IR-{token}",
                    appointmentStatus: status)
                {
                    TenantId = TenantsTestData.TenantARef,
                    ApplicantAttorneyEmail = applicantAttorneyEmail,
                };
                appointment.RecordBookedBy(fixture.BookedByUserId);
                await _appointmentRepository.InsertAsync(appointment, autoSave: true);
            }
        });

        return fixture;
    }

    /// <summary>
    /// Inserts one info-request row directly, so a fact can start from a given flagged set without
    /// going through SendBackAsync. The flagged keys are serialized exactly the way the service
    /// writes and reads them (default options, no naming policy), so the round-trip is the real one.
    /// </summary>
    private async Task SeedInfoRequestAsync(
        Guid appointmentId,
        string note,
        IEnumerable<string> flaggedKeys,
        Guid? requestedByUserId = null,
        bool resolved = false,
        string? rawRequestedFieldsJson = null,
        DateTime? creationTime = null)
    {
        var id = Guid.NewGuid();
        var json = rawRequestedFieldsJson
                   ?? JsonSerializer.Serialize(
                       flaggedKeys.Select(k => new FlaggedFieldDto { Key = k }).ToList());

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var entity = new AppointmentInfoRequest(
                    id,
                    TenantsTestData.TenantARef,
                    appointmentId,
                    note,
                    json,
                    requestedByUserId ?? ExternalUserId);

                if (resolved)
                {
                    entity.MarkResolved(new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc));
                }

                await _infoRequestRepository.InsertAsync(entity, autoSave: true);

                if (creationTime.HasValue)
                {
                    // Set AFTER the insert: ABP's creation-audit setter runs on Added only, so an
                    // update is the one place the value is guaranteed to survive.
                    entity.CreationTime = creationTime.Value;
                    await _infoRequestRepository.UpdateAsync(entity, autoSave: true);
                }
            }
        });
    }

    private async Task SeedInjuryDetailAsync(
        Guid appointmentId,
        string claimNumber,
        DateTime dateOfInjury,
        bool isCumulativeInjury = false)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _injuryDetailRepository.InsertAsync(
                    new AppointmentInjuryDetail(
                        id: Guid.NewGuid(),
                        appointmentId: appointmentId,
                        dateOfInjury: dateOfInjury,
                        claimNumber: claimNumber,
                        isCumulativeInjury: isCumulativeInjury,
                        bodyPartsSummary: $"{claimNumber}-parts",
                        toDateOfInjury: null,
                        wcabAdj: $"{claimNumber}-ADJ",
                        wcabOfficeId: null)
                    {
                        TenantId = TenantsTestData.TenantARef,
                    },
                    autoSave: true);
            }
        });
    }

    /// <summary>
    /// The fix-it page's replacement row. Every string is [Required] on the DTO and re-guarded by
    /// the domain constructor, so all three are populated or ABP's validator pre-empts the service.
    /// </summary>
    private static InjuryDetailCorrectionDto BuildInjuryCorrection(string claimNumber, DateTime dateOfInjury)
    {
        return new InjuryDetailCorrectionDto
        {
            DateOfInjury = dateOfInjury,
            ToDateOfInjury = null,
            ClaimNumber = claimNumber,
            IsCumulativeInjury = false,
            WcabAdj = $"{claimNumber}-ADJ",
            BodyPartsSummary = $"{claimNumber}-parts",
            WcabOfficeId = null,
        };
    }

    /// <summary>
    /// Asserts the fixture starts with no rows of the given kind for its own appointment. Used by
    /// the create-if-absent facts: "the service created the row" is only meaningful if nothing
    /// created it earlier.
    /// </summary>
    private async Task AssertNoRowsAsync<TEntity>(
        IRepository<TEntity, Guid> repository,
        System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate)
        where TEntity : class, IEntity<Guid>
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                (await repository.CountAsync(predicate)).ShouldBe(0);
            }
        });
    }
}
