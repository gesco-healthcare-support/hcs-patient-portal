using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// The case-follower coverage guard (2026-11-04). Followers (the external booker and the accessors) are
/// added to each email handler's recipient list by hand, so a NEW event handler would silently miss them.
/// This test reflects over every event handler in the Application assembly's notification and Case Tracker
/// alert namespaces and requires each to be in exactly ONE of two explicit lists: <see cref="FollowerHandlers"/>
/// (each names the <see cref="CaseFollowerEmailTests"/> or <see cref="BookerMailChoicesEmailTests"/> Fact(s) that prove the accessor is copied, and those
/// Facts must exist) or <see cref="ExcludedHandlers"/> (each with the reason it must not copy followers).
/// A handler in neither list fails the build, so adding one forces the decision.
/// </summary>
public class CaseFollowerCoverageGuardTests
{
    private static readonly string[] HandlerNamespaces =
    {
        "HealthcareSupport.CaseEvaluation.Notifications.Handlers",
        "HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers",
    };

    /// <summary>Handlers that copy the case followers, with the Facts in <see cref="CaseFollowerEmailTests"/> that prove it.</summary>
    private static readonly Dictionary<Type, string[]> FollowerHandlers = new()
    {
        [typeof(BookingSubmissionEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.Requested_CopiesTheAccessor_ButNotTheOfficeNoticeOrTheApproveRejectMail),
        },
        [typeof(StatusChangeEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.Approved_CopiesTheAccessor),
            nameof(CaseFollowerEmailTests.Rejected_CopiesTheAccessor),
            nameof(CaseFollowerEmailTests.SentBack_CopiesTheAccessor),
            nameof(CaseFollowerEmailTests.Cancelled_CopiesTheAccessor),
        },
        [typeof(ChangeRequestSubmittedEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.ACancelRequest_CopiesTheAccessor),
            nameof(CaseFollowerEmailTests.ARescheduleRequest_CopiesTheAccessor),
        },
        [typeof(ChangeRequestConsentInfoEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.ConsentInfo_CopiesTheAccessor),
            nameof(CaseFollowerEmailTests.ConsentInfo_TellsTheBookerAndAccessors_ButNotThePatientOrALinkHolder),
        },
        [typeof(ChangeRequestApprovedEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.AFinishedChange_CopiesTheAccessor),
        },
        [typeof(ChangeRequestRejectedEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.ARejectedChange_CopiesTheAccessor),
        },
        [typeof(DocumentUploadedEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.ADocumentUpload_CopiesTheAccessor),
        },
        [typeof(DocumentAcceptedEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.ADocumentAcceptance_CopiesTheAccessor),
        },
        [typeof(DocumentRejectedEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.ADocumentRejection_CopiesTheAccessor),
        },
        [typeof(IntakeChangedEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.ADateMove_CopiesTheAccessor),
        },
        [typeof(AppointmentReminderEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.TheDueDateReminder_CopiesTheAccessor),
        },
        // Gated by CaseFollowers.AccessorsReceivePackets (on since 2026-11-04): the Facts pin the copy and the gate.
        [typeof(AttyCEPacketEmailHandler)] = new[]
        {
            nameof(CaseFollowerEmailTests.TheAttorneyPacket_GoesToTheAttorneysAndTheAttorneyRoleAccessor),
            nameof(CaseFollowerEmailTests.TheSwitchIsTheOnlyGateOnAccessorPackets),
        },
        // The link goes to the representative and to the same-side law-office followers (decision 3).
        [typeof(ChangeRequestConsentRequestEmailHandler)] = new[]
        {
            nameof(BookerMailChoicesEmailTests.TheConsentLink_GoesToTheSideRepresentativeAndTheSameSideFollowers),
        },
    };

    /// <summary>Handlers that must NOT copy the case followers, each with the reason.</summary>
    private static readonly Dictionary<Type, string> ExcludedHandlers = new()
    {
        [typeof(ChangeRequestConsentDeclinedEmailHandler)] =
            "a decline goes to the office inbox and Intake only, by decision (ADecline_DoesNotCopyTheAccessor)",
        [typeof(AppointmentDayReminderEmailHandler)] =
            "the 7 / 1 day reminder is for the patient and the office (TheDayReminder_DoesNotCopyTheAccessor)",
        [typeof(AccessorAddedEmailHandler)] = "the added accessor's own access mail, not a case email",
        [typeof(AccessorInvitedEmailHandler)] = "the invited accessor's own account mail, not a case email",
        [typeof(PatientPacketEmailHandler)] = "the patient packet goes to the patient only",
        [typeof(OfficePacketCopyEmailHandler)] = "the office copy of the packets goes to the office inbox only",
        [typeof(OfficeSummaryEmailHandler)] = "staff summary (office inbox and Intake)",
        [typeof(SupervisorSummaryEmailHandler)] = "staff summary (supervisors)",
        [typeof(UserQuerySubmittedEmailHandler)] = "a question to the office (inbox and Intake, or IT Admin)",
        [typeof(JdfOverdueInternalEmailHandler)] = "an internal overdue notice to the office",
        [typeof(PackageDocumentQueueHandler)] = "queues the package documents; sends no email",
        [typeof(AppointmentSubmittedInAppNotificationHandler)] = "in-app bell for staff; not an email",
        [typeof(ChangeRequestSubmittedInAppNotificationHandler)] = "in-app bell for staff; not an email",
        [typeof(UserQuerySubmittedInAppNotificationHandler)] = "in-app bell for staff; not an email",
        [typeof(DocumentUploadedInAppNotificationHandler)] = "in-app bell for staff; not an email",
        [typeof(InfoRequestResubmittedInAppNotificationHandler)] = "in-app bell for staff; not an email",
        [typeof(CaseTrackerFeedAlertEmailHandler)] = "technical alert to the technical list only",
        [typeof(CaseTrackerMissingIntakesEmailHandler)] = "technical alert to the technical list only",
        [typeof(CaseTrackerPushFailedEmailHandler)] = "technical alert to the technical list only",
    };

    [Fact]
    public void EveryEventHandlerIsOnExactlyOneList()
    {
        var discovered = DiscoverHandlers();
        discovered.ShouldNotBeEmpty("the reflection found no handlers, so the guard would pass vacuously");

        var listed = FollowerHandlers.Keys.Concat(ExcludedHandlers.Keys).ToList();
        listed.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key.Name).ShouldBeEmpty(
            "a handler is on both lists");

        discovered.Except(listed).Select(t => t.Name).ShouldBeEmpty(
            "a new event handler must be added to FollowerHandlers (copy the accessors, with a behavior Fact) "
            + "or ExcludedHandlers (with the reason)");
        listed.Except(discovered).Select(t => t.Name).ShouldBeEmpty(
            "a listed type is not an event handler any more; remove it");
    }

    [Fact]
    public void EveryFollowerHandlerNamesTheFactsThatProveIt_AndTheyExist()
    {
        foreach (var (handler, tests) in FollowerHandlers)
        {
            tests.ShouldNotBeEmpty($"{handler.Name} names no behavior test");
            foreach (var test in tests)
            {
                var method = typeof(CaseFollowerEmailTests).GetMethod(test) ?? typeof(BookerMailChoicesEmailTests).GetMethod(test);
                method.ShouldNotBeNull($"{handler.Name}: no test method {test}");
                method!.GetCustomAttributes().Any(a => a is FactAttribute).ShouldBeTrue(
                    $"{handler.Name}: {test} is not a [Fact] or [Theory]");
            }
        }
    }

    [Fact]
    public void EveryExcludedHandlerHasAReason()
    {
        ExcludedHandlers.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key.Name).ShouldBeEmpty();
    }

    private static List<Type> DiscoverHandlers() =>
        typeof(StatusChangeEmailHandler).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null && HandlerNamespaces.Contains(t.Namespace))
            .Where(t => t.GetInterfaces().Any(IsEventHandlerInterface))
            .ToList();

    private static bool IsEventHandlerInterface(Type i) =>
        i.IsGenericType
        && (i.GetGenericTypeDefinition() == typeof(ILocalEventHandler<>)
            || i.GetGenericTypeDefinition() == typeof(IDistributedEventHandler<>));
}
