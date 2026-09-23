using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.Appointments.Events;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.CustomFields;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;
using NotificationsEvents = HealthcareSupport.CaseEvaluation.Notifications.Events;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The appointment lifecycle services: staff approval and rejection of a booking, staff decisions
/// on cancellation and reschedule requests, and the consent requests a new change request sends.
/// </summary>
/// <remarks>
/// <para>
/// <c>AppointmentApprovalAppService</c> had no test at all (82 of 82 lines uncovered), and the
/// cancellation half of the change-request approval service, its reschedule rejection and its
/// queue view were also unreached.
/// </para>
/// <para>
/// Runs on the standard integration base (real SQLite; seeded office A with appointment 1, office
/// B with appointment 2 and its defense-attorney accessor). Three collaborators are replaced for
/// THIS class only:
/// <list type="bullet">
///   <item>the local event bus, so every published event is asserted, and so the notification
///   handlers (Session A's area) do not run here;</item>
///   <item>the recipient resolver, so each test states exactly who is on the appointment;</item>
///   <item>the account URL builder, so a consent link is a known synthetic URL.</item>
/// </list>
/// </para>
/// <para>All names, emails and identifiers below are synthetic.</para>
/// </remarks>
public abstract class AppointmentLifecycleServiceFlowTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static readonly Guid StaffUserId = new("9f5b3c4d-0000-4000-9000-000000000001");
    private static readonly Guid AppointmentA = AppointmentsTestData.Appointment1Id;

    private ILocalEventBus _events = null!;
    private IAppointmentRecipientResolver _recipients = null!;
    private IAccountUrlBuilder _urls = null!;

    private readonly IAppointmentApprovalAppService _approval;
    private readonly IAppointmentChangeRequestsApprovalAppService _decisions;
    private readonly IAppointmentChangeRequestsAppService _requests;
    private readonly IAppointmentsAppService _appointments;
    private readonly IRepository<CustomField, Guid> _customFieldRepository;
    private readonly IRepository<CustomFieldValue, Guid> _customFieldValueRepository;
    private readonly IAppointmentChangeRequestRepository _changeRequestRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected AppointmentLifecycleServiceFlowTests()
    {
        _approval = GetRequiredService<IAppointmentApprovalAppService>();
        _decisions = GetRequiredService<IAppointmentChangeRequestsApprovalAppService>();
        _requests = GetRequiredService<IAppointmentChangeRequestsAppService>();
        _appointments = GetRequiredService<IAppointmentsAppService>();
        _customFieldRepository = GetRequiredService<IRepository<CustomField, Guid>>();
        _customFieldValueRepository = GetRequiredService<IRepository<CustomFieldValue, Guid>>();
        _changeRequestRepository = GetRequiredService<IAppointmentChangeRequestRepository>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _slotRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _events = Substitute.For<ILocalEventBus>();
        _recipients = Substitute.For<IAppointmentRecipientResolver>();
        _recipients.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>())
            .Returns(new List<SendAppointmentEmailArgs>());
        _urls = Substitute.For<IAccountUrlBuilder>();
        _urls.BuildChangeRequestConsentUrlAsync(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(ci => $"https://portal.example.test/consent/{ci.ArgAt<string>(1)}");
        services.Replace(ServiceDescriptor.Singleton(typeof(ILocalEventBus), _events));
        services.Replace(ServiceDescriptor.Singleton(typeof(IAppointmentRecipientResolver), _recipients));
        services.Replace(ServiceDescriptor.Singleton(typeof(IAccountUrlBuilder), _urls));
    }

    // ------------------------------------------------------------------ harness

    private async Task InOffice(Guid? tenantId, Guid userId, string role, Func<Task> body)
    {
        using (_currentTenant.Change(tenantId))
        using (WithCurrentUser.Run(_principal, userId, role))
        {
            await body();
        }
    }

    private Task AsStaff(Func<Task> body) =>
        InOffice(TenantsTestData.TenantARef, StaffUserId, "Staff Supervisor", body);

    private List<T> Published<T>() =>
        _events.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILocalEventBus.PublishAsync))
            .Select(c => c.GetArguments()[0])
            .OfType<T>()
            .ToList();

    private void OnAppointment(params (RecipientRole Role, string Email)[] parties) =>
        _recipients.ResolveAsync(Arg.Any<Guid>(), NotificationKind.Submitted)
            .Returns(parties.Select(p => new SendAppointmentEmailArgs { To = p.Email, Role = p.Role }).ToList());

    private async Task<T> InTenantA<T>(Func<Task<T>> read)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await WithUnitOfWorkAsync(read);
        }
    }

    /// <summary>
    /// The seeded slots are dated June 2026, which is inside the office's no-cancel window, so a
    /// cancellation request against them is refused before it reaches the consent step. Moving the
    /// appointment's slot 90 days out puts it well clear of the window.
    /// </summary>
    private async Task MoveSlotOutOfTheCancelWindowAsync(Guid? tenantId, Guid slotId)
    {
        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var slot = await _slotRepository.GetAsync(slotId);
                slot.AvailableDate = DateTime.UtcNow.Date.AddDays(90);
                await _slotRepository.UpdateAsync(slot, autoSave: true);
            });
        }
    }

    /// <summary>
    /// Approval also needs an active claim examiner on the appointment. The seed gives appointment 1
    /// its injury detail but no examiner.
    /// </summary>
    private Task AddActiveClaimExaminerAsync() =>
        InTenantA(async () => await GetRequiredService<IRepository<AppointmentClaimExaminer, Guid>>()
            .InsertAsync(
                new AppointmentClaimExaminer(Guid.NewGuid(), AppointmentA, isActive: true)
                {
                    Name = "Synthetic Examiner",
                    Email = "examiner@example.test",
                },
                autoSave: true));

    /// <summary>
    /// Two custom fields on appointment 1's type in office A: one active, one retired. Returns the
    /// active field's id.
    /// </summary>
    private Task<Guid> InsertCustomFieldsAsync() =>
        InTenantA(async () =>
        {
            var active = await _customFieldRepository.InsertAsync(new CustomField(
                Guid.NewGuid(), TenantsTestData.TenantARef, "Synthetic referral source", 1,
                CustomFieldType.Alphanumeric, AppointmentTypesTestData.AppointmentType1Id), autoSave: true);
            await _customFieldRepository.InsertAsync(new CustomField(
                Guid.NewGuid(), TenantsTestData.TenantARef, "Synthetic retired field", 2,
                CustomFieldType.Alphanumeric, AppointmentTypesTestData.AppointmentType1Id, isActive: false), autoSave: true);
            return active.Id;
        });

    private Task<CustomFieldValue> InsertCustomValueAsync(Guid fieldId, string value) =>
        InTenantA(() => _customFieldValueRepository.InsertAsync(
            new CustomFieldValue(Guid.NewGuid(), TenantsTestData.TenantARef, fieldId, AppointmentA, value), autoSave: true));

    /// <summary>An open slot at appointment 1's location, for its type, 60 days out, 10:00-11:00.</summary>
    private Task<Guid> InsertFutureSlotAsync() =>
        InTenantA(async () =>
        {
            var slot = new DoctorAvailability(
                Guid.NewGuid(), LocationsTestData.Location1Id, DateTime.UtcNow.Date.AddDays(60),
                new TimeOnly(10, 0), new TimeOnly(11, 0), BookingStatus.Available);
            slot.TenantId = TenantsTestData.TenantARef;
            slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            return (await _slotRepository.InsertAsync(slot, autoSave: true)).Id;
        });

    private static AppointmentCreateDto BookingForPatient1() => new()
    {
        PatientId = PatientsTestData.Patient1Id,
        IdentityUserId = IdentityUsersTestData.Patient1UserId,
        AppointmentTypeId = AppointmentTypesTestData.AppointmentType1Id,
        LocationId = LocationsTestData.Location1Id,
        DoctorAvailabilityId = DoctorAvailabilitiesTestData.Slot1Id,
        AppointmentDate = new DateTime(2030, 1, 1, 9, 0, 0, DateTimeKind.Utc),
        RequestConfirmationNumber = "A00001",
        AppointmentStatus = AppointmentStatusType.Pending,
    };

    private Task<AppointmentChangeRequest> InsertRequestAsync(
        ChangeRequestType type, Action<AppointmentChangeRequest>? shape = null) =>
        InTenantA(async () =>
        {
            var request = new AppointmentChangeRequest(
                Guid.NewGuid(), TenantsTestData.TenantARef, AppointmentA, type,
                cancellationReason: type == ChangeRequestType.Cancel ? "Synthetic cancellation reason" : null,
                reScheduleReason: type == ChangeRequestType.Reschedule ? "Synthetic reschedule reason" : null,
                newDoctorAvailabilityId: null);
            shape?.Invoke(request);
            return await _changeRequestRepository.InsertAsync(request, autoSave: true);
        });

    // ------------------------------------------------------------------ booking approval

    [Fact]
    public async Task Approving_records_the_responsible_user_and_comments_and_announces_it()
    {
        await AddActiveClaimExaminerAsync();
        AppointmentDto result = null!;
        await AsStaff(async () => result = await _approval.ApproveAppointmentAsync(AppointmentA,
            new ApproveAppointmentInput { PrimaryResponsibleUserId = StaffUserId, InternalUserComments = "Synthetic note" }));

        result.AppointmentStatus.ShouldBe(AppointmentStatusType.Approved);
        var stored = await InTenantA(() => _appointmentRepository.GetAsync(AppointmentA));
        stored.PrimaryResponsibleUserId.ShouldBe(StaffUserId);
        stored.InternalUserComments.ShouldBe("Synthetic note");
        var approved = Published<AppointmentApprovedEto>().ShouldHaveSingleItem();
        approved.ApprovedByUserId.ShouldBe(StaffUserId);
        approved.PrimaryResponsibleUserId.ShouldBe(StaffUserId);
    }

    [Fact]
    public async Task Approval_needs_a_responsible_user_and_a_booking_not_already_approved()
    {
        await AsStaff(async () =>
            (await Should.ThrowAsync<BusinessException>(() => _approval.ApproveAppointmentAsync(
                    AppointmentA, new ApproveAppointmentInput { PrimaryResponsibleUserId = Guid.Empty })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresResponsibleUser));

        await InOffice(TenantsTestData.TenantBRef, StaffUserId, "Staff Supervisor", async () =>
            (await Should.ThrowAsync<BusinessException>(() => _approval.ApproveAppointmentAsync(
                    AppointmentsTestData.Appointment2Id, new ApproveAppointmentInput { PrimaryResponsibleUserId = StaffUserId })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentNotPendingForApproval));

        Published<AppointmentApprovedEto>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejecting_records_the_reason_and_who_rejected_and_cannot_be_repeated()
    {
        AppointmentDto result = null!;
        await AsStaff(async () =>
        {
            result = await _approval.RejectAppointmentAsync(AppointmentA, new RejectAppointmentInput { Reason = "Synthetic rejection" });
            (await Should.ThrowAsync<BusinessException>(() => _approval.RejectAppointmentAsync(
                    AppointmentA, new RejectAppointmentInput { Reason = "Again" })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentNotPendingForRejection);
        });

        result.AppointmentStatus.ShouldBe(AppointmentStatusType.Rejected);
        var stored = await InTenantA(() => _appointmentRepository.GetAsync(AppointmentA));
        stored.RejectionNotes.ShouldBe("Synthetic rejection");
        stored.RejectedById.ShouldBe(StaffUserId);
        Published<AppointmentRejectedEto>().ShouldHaveSingleItem().RejectedByUserId.ShouldBe(StaffUserId);
    }

    [Fact]
    public async Task The_responsible_user_list_pages_and_filters_the_internal_users()
    {
        PagedResultDto<LookupDto<Guid>> all = null!, none = null!;
        await AsStaff(async () =>
        {
            all = await _approval.GetInternalUserLookupAsync(new LookupRequestDto { MaxResultCount = 5 });
            none = await _approval.GetInternalUserLookupAsync(new LookupRequestDto { Filter = "zz-no-such-person", MaxResultCount = 5 });
        });

        all.Items.Count.ShouldBe((int)Math.Min(all.TotalCount, 5));
        none.TotalCount.ShouldBe(0);
        none.Items.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------ the appointments service

    [Fact]
    public async Task The_pending_count_is_the_offices_pending_bookings()
    {
        var count = -1;
        await AsStaff(async () => count = await _appointments.GetPendingCountAsync());

        count.ShouldBe(1);
    }

    [Fact]
    public async Task The_appointments_service_approves_a_ready_booking()
    {
        await AddActiveClaimExaminerAsync();

        AppointmentDto approved = null!;
        await AsStaff(async () => approved = await _appointments.ApproveAsync(AppointmentA));

        approved.AppointmentStatus.ShouldBe(AppointmentStatusType.Approved);
        (await InTenantA(() => _appointmentRepository.GetAsync(AppointmentA)))
            .AppointmentStatus.ShouldBe(AppointmentStatusType.Approved);
    }

    [Fact]
    public async Task The_appointments_service_rejects_a_pending_booking_with_its_reason()
    {
        AppointmentDto rejected = null!;
        await AsStaff(async () => rejected = await _appointments.RejectAsync(
            AppointmentA, new RejectAppointmentInput { Reason = "Synthetic reason" }));

        rejected.AppointmentStatus.ShouldBe(AppointmentStatusType.Rejected);
        var stored = await InTenantA(() => _appointmentRepository.GetAsync(AppointmentA));
        stored.RejectionNotes.ShouldBe("Synthetic reason");
        stored.RejectedById.ShouldBe(StaffUserId);
    }

    [Fact]
    public async Task Custom_field_values_show_the_active_fields_for_the_type_with_what_was_saved()
    {
        var fieldId = await InsertCustomFieldsAsync();
        await InsertCustomValueAsync(fieldId, "Synthetic answer");

        List<CustomFieldValueDisplayDto> rows = null!;
        await AsStaff(async () => rows = await _appointments.GetAppointmentCustomFieldValuesAsync(AppointmentA));

        var row = rows.ShouldHaveSingleItem();
        row.CustomFieldId.ShouldBe(fieldId);
        row.FieldLabel.ShouldBe("Synthetic referral source");
        row.Value.ShouldBe("Synthetic answer");
    }

    /// <summary>
    /// The positive intake-changed event. <c>AppointmentsAppServiceTests</c> records that it cannot
    /// assert this, because there the real notification handler runs and cannot find a tenant
    /// template. Here the event bus is substituted, so the event itself is what gets asserted.
    /// </summary>
    [Fact]
    public async Task Moving_a_booking_announces_the_slot_and_date_change_and_replaces_its_custom_values()
    {
        var fieldId = await InsertCustomFieldsAsync();
        await InsertCustomValueAsync(fieldId, "Old answer");
        var slotId = await InsertFutureSlotAsync();
        var before = await InTenantA(() => _appointmentRepository.GetAsync(AppointmentA));

        await AsStaff(() => _appointments.UpdateAsync(AppointmentA, new AppointmentUpdateDto
        {
            PatientId = PatientsTestData.Patient1Id,
            IdentityUserId = IdentityUsersTestData.Patient1UserId,
            AppointmentTypeId = AppointmentTypesTestData.AppointmentType1Id,
            LocationId = LocationsTestData.Location1Id,
            DoctorAvailabilityId = slotId,
            AppointmentDate = DateTime.UtcNow.Date.AddDays(60).AddHours(10).AddMinutes(15),
            RequestConfirmationNumber = before.RequestConfirmationNumber,
            PanelNumber = before.PanelNumber,
            DueDate = before.DueDate,
            ConcurrencyStamp = before.ConcurrencyStamp,
            CustomFieldValues =
            {
                new CustomFieldValueInputDto { CustomFieldId = fieldId, Value = "New answer" },
                new CustomFieldValueInputDto { CustomFieldId = fieldId, Value = "   " },
                new CustomFieldValueInputDto { CustomFieldId = Guid.Empty, Value = "Ignored" },
            },
        }));

        var moved = Published<AppointmentStatusChangedEto>().ShouldHaveSingleItem();
        moved.OldDoctorAvailabilityId.ShouldBe(DoctorAvailabilitiesTestData.Slot1Id);
        moved.DoctorAvailabilityId.ShouldBe(slotId);
        moved.FromStatus.ShouldBe(moved.ToStatus);

        var intake = Published<NotificationsEvents.AppointmentIntakeChangedEto>().ShouldHaveSingleItem();
        intake.AppointmentId.ShouldBe(AppointmentA);
        intake.DateOrTimeChanged.ShouldBeTrue();
        intake.ChangedFields.ShouldContain(f => f.FieldName == "AppointmentDate" && f.Section == "Appointment");

        (await InTenantA(() => _customFieldValueRepository.GetListAsync(v => v.AppointmentId == AppointmentA)))
            .ShouldHaveSingleItem().Value.ShouldBe("New answer");
    }

    /// <summary>
    /// The seeded appointment types and locations are host-scoped, so office A cannot see them and
    /// the type check would fire before the location and slot checks. The test inserts its own
    /// office-A type and location so each refusal in the chain is reached in turn.
    /// </summary>
    [Fact]
    public async Task A_booking_is_refused_at_the_first_party_or_reference_that_does_not_exist()
    {
        var (typeId, locationId) = await InTenantA(async () =>
        {
            var type = await GetRequiredService<IRepository<AppointmentTypes.AppointmentType, Guid>>().InsertAsync(
                new AppointmentTypes.AppointmentType(Guid.NewGuid(), "Synthetic office type"), autoSave: true);
            var location = await GetRequiredService<IRepository<Locations.Location, Guid>>().InsertAsync(
                new Locations.Location(Guid.NewGuid(), null, "Synthetic office location", 0m, isActive: true), autoSave: true);
            return (type.Id, location.Id);
        });

        var cases = new (Action<AppointmentCreateDto> Break, string Message)[]
        {
            (b => { b.PatientEmail = "same@example.test"; b.ApplicantAttorneyEmail = "same@example.test"; },
                "must use a different email address"),
            (b => b.IdentityUserId = AppointmentsTestData.NonExistentIdentityUserId, "selected user does not exist"),
            (b => b.AppointmentTypeId = AppointmentsTestData.NonExistentAppointmentTypeId, "appointment type does not exist"),
            (b => { b.AppointmentTypeId = typeId; b.LocationId = AppointmentsTestData.NonExistentLocationId; },
                "location does not exist"),
            (b => { b.AppointmentTypeId = typeId; b.LocationId = locationId; b.DoctorAvailabilityId = AppointmentsTestData.NonExistentDoctorAvailabilityId; },
                "availability slot does not exist"),
        };

        await AsStaff(async () =>
        {
            foreach (var (breakIt, message) in cases)
            {
                var booking = BookingForPatient1();
                breakIt(booking);
                (await Should.ThrowAsync<UserFriendlyException>(() => _appointments.CreateAsync(booking)))
                    .Message.ShouldContain(message);
            }
        });

        (await InTenantA(() => _appointmentRepository.GetCountAsync())).ShouldBe(1);
    }

    // ------------------------------------------------------------------ cancellation decisions

    [Fact]
    public async Task Approving_a_cancellation_closes_the_appointment_with_the_chosen_outcome()
    {
        var request = await InsertRequestAsync(ChangeRequestType.Cancel);
        AppointmentChangeRequestDto result = null!;
        await AsStaff(async () => result = await _decisions.ApproveCancellationAsync(request.Id,
            new ApproveCancellationInput { CancellationOutcome = AppointmentStatusType.CancelledLate, ConcurrencyStamp = request.ConcurrencyStamp }));

        result.RequestStatus.ShouldBe(RequestStatusType.Accepted);
        result.CancellationOutcome.ShouldBe(AppointmentStatusType.CancelledLate);
        var appointment = await InTenantA(() => _appointmentRepository.GetAsync(AppointmentA));
        appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.CancelledLate);
        appointment.CancelledById.ShouldBe(StaffUserId);
        appointment.CancellationReason.ShouldBe("Synthetic cancellation reason");

        var status = Published<AppointmentStatusChangedEto>().ShouldHaveSingleItem();
        status.FromStatus.ShouldBe(AppointmentStatusType.Pending);
        status.ToStatus.ShouldBe(AppointmentStatusType.CancelledLate);
        Published<NotificationsEvents.AppointmentChangeRequestApprovedEto>().ShouldHaveSingleItem()
            .Outcome.ShouldBe(AppointmentStatusType.CancelledLate);
    }

    [Fact]
    public async Task A_cancellation_is_not_approved_with_a_non_cancel_outcome_or_on_a_reschedule_request()
    {
        var cancel = await InsertRequestAsync(ChangeRequestType.Cancel);
        var reschedule = await InsertRequestAsync(ChangeRequestType.Reschedule);

        await AsStaff(async () =>
        {
            (await Should.ThrowAsync<BusinessException>(() => _decisions.ApproveCancellationAsync(cancel.Id,
                    new ApproveCancellationInput { CancellationOutcome = AppointmentStatusType.Approved })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestInvalidCancellationOutcome);
            (await Should.ThrowAsync<BusinessException>(() => _decisions.ApproveCancellationAsync(reschedule.Id,
                    new ApproveCancellationInput { CancellationOutcome = AppointmentStatusType.CancelledNoBill })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestInvalidCancellationOutcome);
            (await Should.ThrowAsync<BusinessException>(() => _decisions.RejectCancellationAsync(reschedule.Id,
                    new RejectChangeRequestInput { Reason = "Synthetic" })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestInvalidCancellationOutcome);
            (await Should.ThrowAsync<BusinessException>(() => _decisions.RejectRescheduleAsync(cancel.Id,
                    new RejectChangeRequestInput { Reason = "Synthetic" })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome);
            (await Should.ThrowAsync<BusinessException>(() => _decisions.ConfirmRescheduleDateAsync(cancel.Id,
                    new ConfirmRescheduleDateInput { DoctorAvailabilityId = DoctorAvailabilitiesTestData.Slot2Id })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome);
        });

        (await InTenantA(() => _appointmentRepository.GetAsync(AppointmentA)))
            .AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
    }

    [Fact]
    public async Task A_cancellation_is_not_finalized_over_the_other_sides_refusal()
    {
        var request = await InsertRequestAsync(ChangeRequestType.Cancel, r =>
        {
            r.IssueSideConsent(ChangeRequestSide.SideB, "synthetic-token-hash", DateTime.UtcNow.AddDays(3));
            r.RecordSideDecision(ChangeRequestSide.SideB, approved: false, "defense@example.test", DateTime.UtcNow);
        });

        await AsStaff(async () =>
            (await Should.ThrowAsync<BusinessException>(() => _decisions.ApproveCancellationAsync(request.Id,
                    new ApproveCancellationInput { CancellationOutcome = AppointmentStatusType.CancelledNoBill })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted));

        (await InTenantA(() => _appointmentRepository.GetAsync(AppointmentA)))
            .AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
    }

    [Fact]
    public async Task A_decision_made_on_a_stale_copy_of_the_request_is_refused()
    {
        var request = await InsertRequestAsync(ChangeRequestType.Cancel);

        await AsStaff(async () =>
            (await Should.ThrowAsync<BusinessException>(() => _decisions.RejectCancellationAsync(request.Id,
                    new RejectChangeRequestInput { Reason = "Synthetic", ConcurrencyStamp = "stale-stamp" })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestAlreadyHandled));
    }

    [Fact]
    public async Task Rejecting_a_cancellation_keeps_the_appointment_and_records_the_reason()
    {
        var request = await InsertRequestAsync(ChangeRequestType.Cancel);
        AppointmentChangeRequestDto result = null!;
        await AsStaff(async () => result = await _decisions.RejectCancellationAsync(request.Id,
            new RejectChangeRequestInput { Reason = "  Synthetic refusal  " }));

        result.RequestStatus.ShouldBe(RequestStatusType.Rejected);
        result.RejectionNotes.ShouldBe("Synthetic refusal");
        (await InTenantA(() => _appointmentRepository.GetAsync(AppointmentA)))
            .AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
        Published<NotificationsEvents.AppointmentChangeRequestRejectedEto>().ShouldHaveSingleItem()
            .RejectionNotes.ShouldBe("Synthetic refusal");
    }

    // ------------------------------------------------------------------ reschedule decisions

    [Fact]
    public async Task Rejecting_a_reschedule_releases_the_slot_it_was_holding()
    {
        await InTenantA(async () =>
        {
            var slot = await _slotRepository.GetAsync(DoctorAvailabilitiesTestData.Slot2Id);
            slot.BookingStatusId = BookingStatus.Reserved;
            return await _slotRepository.UpdateAsync(slot, autoSave: true);
        });
        var request = await InsertRequestAsync(ChangeRequestType.Reschedule,
            r => r.NewDoctorAvailabilityId = DoctorAvailabilitiesTestData.Slot2Id);

        AppointmentChangeRequestDto result = null!;
        await AsStaff(async () => result = await _decisions.RejectRescheduleAsync(request.Id,
            new RejectChangeRequestInput { Reason = "  No suitable date  " }));

        result.RequestStatus.ShouldBe(RequestStatusType.Rejected);
        (await InTenantA(() => _slotRepository.GetAsync(DoctorAvailabilitiesTestData.Slot2Id)))
            .BookingStatusId.ShouldBe(BookingStatus.Available);
        Published<AppointmentStatusChangedEto>().ShouldHaveSingleItem().Reason.ShouldBe("No suitable date");
        Published<NotificationsEvents.AppointmentChangeRequestRejectedEto>().ShouldHaveSingleItem()
            .ChangeRequestType.ShouldBe(ChangeRequestType.Reschedule);
    }

    [Fact]
    public async Task A_new_reschedule_date_must_name_a_slot()
    {
        var request = await InsertRequestAsync(ChangeRequestType.Reschedule);

        await AsStaff(async () =>
            (await Should.ThrowAsync<BusinessException>(() => _decisions.ConfirmRescheduleDateAsync(request.Id,
                    new ConfirmRescheduleDateInput { DoctorAvailabilityId = Guid.Empty })))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotRequired));
    }

    [Fact]
    public async Task The_queue_lists_pending_requests_with_their_confirmation_number_and_slot()
    {
        await InsertRequestAsync(ChangeRequestType.Cancel);
        await InsertRequestAsync(ChangeRequestType.Reschedule, r => r.NewDoctorAvailabilityId = DoctorAvailabilitiesTestData.Slot2Id);
        await InsertRequestAsync(ChangeRequestType.Cancel, r => r.MarkDecided(RequestStatusType.Rejected, StaffUserId, DateTime.UtcNow));

        PagedResultDto<AppointmentChangeRequestDto> page = null!;
        await AsStaff(async () => page = await _decisions.GetPendingChangeRequestsAsync(new GetChangeRequestsInput { MaxResultCount = 10 }));

        page.TotalCount.ShouldBe(2);
        page.Items.ShouldAllBe(dto => dto.RequestStatus == RequestStatusType.Pending);
        page.Items.ShouldAllBe(dto => dto.AppointmentConfirmationNumber == AppointmentsTestData.Appointment1RequestConfirmationNumber);
        page.Items.ShouldAllBe(dto => dto.AppointmentTypeId == AppointmentTypesTestData.AppointmentType1Id);
        var reschedule = page.Items.Single(dto => dto.ChangeRequestType == ChangeRequestType.Reschedule);
        reschedule.RequestedSlotDate.ShouldBe(DoctorAvailabilitiesTestData.Slot2AvailableDate);
        page.Items.Single(dto => dto.ChangeRequestType == ChangeRequestType.Cancel).RequestedSlotDate.ShouldBeNull();
    }

    // ------------------------------------------------------------------ consent on a new request

    [Fact]
    public async Task A_staff_cancellation_asks_both_sides_to_consent()
    {
        await MoveSlotOutOfTheCancelWindowAsync(TenantsTestData.TenantARef, DoctorAvailabilitiesTestData.Slot1Id);
        OnAppointment(
            (RecipientRole.Patient, "patient@example.test"),
            (RecipientRole.ApplicantAttorney, "applicant@example.test"),
            (RecipientRole.DefenseAttorney, "defense@example.test"));

        AppointmentChangeRequestDto result = null!;
        await AsStaff(async () => result = await _requests.RequestCancellationAsync(AppointmentA,
            new RequestCancellationDto { Reason = "Synthetic staff cancellation" }));

        var asked = Published<NotificationsEvents.ChangeRequestConsentRequestedEto>();
        asked.Select(e => e.OpposingRecipientEmail).OrderBy(e => e)
            .ShouldBe(new[] { "applicant@example.test", "defense@example.test" });
        asked.ShouldAllBe(e => e.ChangeRequestId == result.Id
            && e.ConsentUrl.StartsWith("https://portal.example.test/consent/"));
    }

    [Fact]
    public async Task A_staff_cancellation_with_no_representative_on_either_side_asks_nobody()
    {
        await MoveSlotOutOfTheCancelWindowAsync(TenantsTestData.TenantARef, DoctorAvailabilitiesTestData.Slot1Id);
        AppointmentChangeRequestDto result = null!;
        await AsStaff(async () => result = await _requests.RequestCancellationAsync(AppointmentA,
            new RequestCancellationDto { Reason = "Synthetic staff cancellation" }));

        result.Id.ShouldNotBe(Guid.Empty);
        Published<NotificationsEvents.ChangeRequestConsentRequestedEto>().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_partys_cancellation_asks_only_the_other_side()
    {
        await MoveSlotOutOfTheCancelWindowAsync(TenantsTestData.TenantBRef, DoctorAvailabilitiesTestData.Slot3Id);
        OnAppointment(
            (RecipientRole.Patient, "patient@example.test"),
            (RecipientRole.ApplicantAttorney, "applicant@example.test"),
            (RecipientRole.DefenseAttorney, "defense@example.test"));

        await InOffice(TenantsTestData.TenantBRef, IdentityUsersTestData.DefenseAttorney1UserId,
            IdentityUsersTestData.DefenseAttorneyRoleName, () => _requests.RequestCancellationAsync(
                AppointmentsTestData.Appointment2Id, new RequestCancellationDto { Reason = "Synthetic party cancellation" }));

        Published<NotificationsEvents.ChangeRequestConsentRequestedEto>().ShouldHaveSingleItem()
            .OpposingRecipientEmail.ShouldBe("applicant@example.test");
    }

    [Fact]
    public async Task A_partys_cancellation_skips_consent_when_the_other_side_has_nobody()
    {
        await MoveSlotOutOfTheCancelWindowAsync(TenantsTestData.TenantBRef, DoctorAvailabilitiesTestData.Slot3Id);
        OnAppointment((RecipientRole.DefenseAttorney, "defense@example.test"));

        AppointmentChangeRequestDto result = null!;
        await InOffice(TenantsTestData.TenantBRef, IdentityUsersTestData.DefenseAttorney1UserId,
            IdentityUsersTestData.DefenseAttorneyRoleName, async () => result = await _requests.RequestCancellationAsync(
                AppointmentsTestData.Appointment2Id, new RequestCancellationDto { Reason = "Synthetic party cancellation" }));

        result.Id.ShouldNotBe(Guid.Empty);
        Published<NotificationsEvents.ChangeRequestConsentRequestedEto>().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_reschedule_to_a_slot_that_does_not_exist_is_refused()
    {
        await AsStaff(async () =>
            await Should.ThrowAsync<EntityNotFoundException>(() => _requests.RequestRescheduleAsync(AppointmentA,
                new RequestRescheduleDto
                {
                    ReScheduleReason = "Synthetic reschedule",
                    NewDoctorAvailabilityId = DoctorAvailabilitiesTestData.NonExistentSlotId,
                })));
    }
}
