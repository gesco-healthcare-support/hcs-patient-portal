using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.Core;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers;

/// <summary>
/// Every HttpApi controller action that is a straight pass-through to its application service.
/// </summary>
/// <remarks>
/// <para>
/// Almost every controller here is the ABP pattern: one injected app-service interface, and each
/// action returns that service's same-named method called with the action's own arguments. None of
/// the 52 controllers had a test, which is why the whole assembly sat at 0 of 576 lines.
/// </para>
/// <para>
/// Rather than 52 near-identical classes, each such action is one case of a single theory. The
/// contract it pins is the one those actions exist for: the arguments reach the service
/// UNCHANGED (the same instances), and the service's result comes back UNCHANGED. A controller
/// that swapped two arguments, dropped one, called a different overload, or returned something
/// else fails its own named case.
/// </para>
/// <para>
/// An action is a pass-through when an injected interface declares a method with the same name,
/// parameter types and return type. Everything else -- uploads, downloads, anything that shapes
/// its arguments or its result -- is tested by hand beside this file.
/// <see cref="Every_action_is_either_a_pass_through_or_named_as_hand_tested"/> keeps the two sets
/// honest: an action that silently falls out of this theory fails that test by name.
/// </para>
/// </remarks>
public class ControllerDelegationTests
{
    private static readonly Assembly HttpApiAssembly = typeof(CaseEvaluationController).Assembly;

    /// <summary>
    /// Actions that are NOT pass-throughs, each tested in its own class in this folder. Keyed
    /// "Controller.Action(ParamType,...)".
    /// </summary>
    private static readonly string[] HandTested =
    {
        // AppointmentDocuments/AppointmentDocumentControllersTests.cs
        "AppointmentDocumentController.ApproveAsync(Guid,Guid)",
        "AppointmentDocumentController.DeleteAsync(Guid,Guid)",
        "AppointmentDocumentController.DownloadAsync(Guid,Guid)",
        "AppointmentDocumentController.GetListAsync(Guid)",
        "AppointmentDocumentController.RejectAsync(Guid,Guid,RejectDocumentInput)",
        "AppointmentDocumentController.UploadAsync(Guid,UploadAppointmentDocumentForm)",
        "AppointmentDocumentController.UploadJointDeclarationAsync(Guid,UploadAppointmentDocumentForm)",
        "AppointmentDocumentController.UploadPackageAsync(Guid,Guid,UploadAppointmentDocumentForm)",
        "AppointmentPacketController.DownloadAsync(Guid)",
        "AppointmentPacketController.DownloadByKindAsync(Guid,PacketKind)",
        "PublicDocumentUploadController.UploadByVerificationCodeAsync(Guid,Guid,UploadAppointmentDocumentForm)",

        // ShapingControllersTests.cs
        "AppointmentChangeRequestApprovalController.GetPendingAsync(GetChangeRequestsInput)",
        "AppointmentDemographicsController.GetPdfAsync(Guid)",
        "BrandingController.GetLogoAsync()",
        "BrandingController.GetOfficeLogoAsync(Guid)",
        "BrandingController.SetDisplayNameAsync(Nullable`1,SetBrandingDisplayNameInput)",
        "BrandingController.UploadLogoAsync(UploadBrandingLogoForm,Nullable`1)",
        "CaseTrackerDeadLetterController.GetDeadLettersAsync()",
        "CaseTrackerDeadLetterController.RetryAllDeadLettersAsync(Guid)",
        "CaseTrackerDeadLetterController.RetryDeadLetterAsync(Guid,Guid)",
        "CaseTrackerOfficesController.SetPushEnabledAsync(Guid,CaseTrackerPushToggleInput)",
        "DocumentsController.CreateAsync(DocumentCreateDto,IFormFile)",
        "DocumentsController.ReplaceFileAsync(Guid,IFormFile)",
        "ExternalSignupController.DeleteTestUsersAsync(DeleteTestUsersDto)",
        "ExternalSignupController.MarkEmailConfirmedAsync(MarkEmailConfirmedDto)",
        "ExternalSignupController.ResolveTenantByNameAsync(String)",
        "PackageDetailsController.LinkDocumentsAsync(Guid,LinkDocumentsRequest)",
        "PublicChangeRequestConsentController.GetAsync(String)",
        "PublicChangeRequestConsentController.SubmitAsync(String,SubmitChangeRequestConsentDto)",
        "ReportController.ExportCsvAsync(GetAppointmentReportInput)",
        "ReportController.ExportPdfAsync(GetAppointmentReportInput)",
        "UserSignatureController.DownloadAsync()",
        "UserSignatureController.UploadAsync(UploadUserSignatureForm)",
    };

    private sealed record PassThrough(Type Controller, MethodInfo Action, Type Service, MethodInfo ServiceMethod);

    public static IEnumerable<object[]> PassThroughCases() =>
        Discover().PassThroughs.Select(p => new object[] { Key(p.Controller, p.Action) });

    [Theory]
    [MemberData(nameof(PassThroughCases))]
    public async Task Action_hands_its_arguments_to_the_service_and_returns_its_result(string action)
    {
        var p = Discover().PassThroughs.Single(x => Key(x.Controller, x.Action) == action);
        var service = Substitute.For(new[] { p.Service }, Array.Empty<object>());
        var controller = Construct(p.Controller, service);
        var args = p.Action.GetParameters().Select(a => Sample(a.ParameterType)).ToArray();
        var expected = ExpectedResult(p.ServiceMethod.ReturnType, out var expectedValue);

        // Configure the service to answer these exact arguments with a distinguishable result.
        Configure(service, p.ServiceMethod, args, expected);
        service.ClearReceivedCalls();

        var actual = p.Action.Invoke(controller, args);

        if (actual is Task task)
        {
            await task;
        }

        var call = service.ReceivedCalls().ShouldHaveSingleItem();
        call.GetMethodInfo().Name.ShouldBe(p.ServiceMethod.Name);
        call.GetArguments().ShouldBe(args);
        if (expectedValue is not null)
        {
            var returned = Unwrap(actual);
            if (expectedValue.GetType().IsValueType)
            {
                returned.ShouldBe(expectedValue);
            }
            else
            {
                returned.ShouldBeSameAs(expectedValue);
            }
        }
    }

    [Fact]
    public void Every_action_is_either_a_pass_through_or_named_as_hand_tested()
    {
        var discovered = Discover();

        discovered.PassThroughs.Count.ShouldBeGreaterThan(250, "the theory must actually reach the controllers");
        discovered.Other.OrderBy(k => k).ShouldBe(HandTested.OrderBy(k => k));
    }

    [Fact]
    public void The_base_controller_localizes_with_the_application_resource()
    {
        var controller = new ProbeController();
        controller.Resource.ShouldBe(typeof(HealthcareSupport.CaseEvaluation.Localization.CaseEvaluationResource));
    }

    private sealed class ProbeController : CaseEvaluationController
    {
        public Type? Resource => LocalizationResource;
    }

    // ------------------------------------------------------------------ discovery

    private static (List<PassThrough> PassThroughs, List<string> Other) Discover()
    {
        var passThroughs = new List<PassThrough>();
        var other = new List<string>();
        var controllers = HttpApiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic);

        foreach (var controller in controllers)
        {
            var ctor = controller.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
            var dependencies = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
            var singleService = dependencies.Length == 1 && dependencies[0].IsInterface ? dependencies[0] : null;

            foreach (var action in Actions(controller))
            {
                var match = singleService is null ? null : Matching(singleService, action);
                if (match is not null && action.GetParameters().All(a => CanSample(a.ParameterType)))
                {
                    passThroughs.Add(new PassThrough(controller, action, singleService!, match));
                }
                else
                {
                    other.Add(Key(controller, action));
                }
            }
        }

        return (passThroughs, other);
    }

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);

    private static MethodInfo? Matching(Type service, MethodInfo action)
    {
        var parameters = action.GetParameters().Select(p => p.ParameterType).ToArray();
        return service.GetMethods()
            .Concat(service.GetInterfaces().SelectMany(i => i.GetMethods()))
            .FirstOrDefault(m =>
                m.Name == action.Name
                && m.ReturnType == action.ReturnType
                && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
    }

    private static string Key(Type controller, MethodInfo action) =>
        $"{controller.Name}.{action.Name}({string.Join(",", action.GetParameters().Select(p => p.ParameterType.Name))})";

    // ------------------------------------------------------------------ construction and samples

    private static object Construct(Type controller, object service) =>
        controller.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First()
            .Invoke(new[] { service });

    private static bool CanSample(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsValueType
            || underlying == typeof(string)
            || underlying.IsArray
            || underlying.IsInterface
            || (!underlying.IsAbstract && underlying.GetConstructor(Type.EmptyTypes) is not null);
    }

    /// <summary>A fresh, synthetic instance of the type, so reference identity proves pass-through.</summary>
    private static object? Sample(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string)) return "synthetic-value";
        if (underlying == typeof(Guid)) return Guid.NewGuid();
        if (underlying == typeof(CancellationToken)) return CancellationToken.None;
        if (underlying == typeof(bool)) return true;
        if (underlying == typeof(int)) return 7;
        if (underlying == typeof(long)) return 7L;
        if (underlying == typeof(DateTime)) return new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        if (underlying.IsEnum) return Enum.GetValues(underlying).GetValue(0);
        if (underlying.IsValueType) return Activator.CreateInstance(underlying);
        if (underlying.IsArray) return Array.CreateInstance(underlying.GetElementType()!, 0);
        if (underlying.IsInterface)
        {
            if (underlying.IsGenericType && underlying.GetGenericArguments().Length == 1)
            {
                var list = typeof(List<>).MakeGenericType(underlying.GetGenericArguments());
                if (underlying.IsAssignableFrom(list)) return Activator.CreateInstance(list);
            }

            return Substitute.For(new[] { underlying }, Array.Empty<object>());
        }

        return Activator.CreateInstance(underlying);
    }

    /// <summary>What the configured service returns; <paramref name="value"/> is the payload to compare.</summary>
    private static object? ExpectedResult(Type returnType, out object? value)
    {
        if (returnType == typeof(Task))
        {
            value = null;
            return Task.CompletedTask;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var payloadType = returnType.GetGenericArguments()[0];
            value = Sample(payloadType);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(payloadType)
                .Invoke(null, new[] { value });
        }

        value = Sample(returnType);
        return value;
    }

    private static void Configure(object service, MethodInfo method, object?[] args, object? result)
    {
        if (method.ReturnType == typeof(void))
        {
            return;
        }

        var configuredCall = method.Invoke(service, args);
        var returns = typeof(SubstituteExtensions).GetMethods()
            .Single(m => m.Name == nameof(SubstituteExtensions.Returns)
                && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType.IsGenericParameter
                && m.GetParameters()[1].ParameterType.IsGenericParameter)
            .MakeGenericMethod(method.ReturnType);
        returns.Invoke(null, new[] { configuredCall, result, Array.CreateInstance(method.ReturnType, 0) });
    }

    private static object? Unwrap(object? actual) =>
        actual is Task task && task.GetType().IsGenericType
            ? task.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(task)
            : actual;
}
