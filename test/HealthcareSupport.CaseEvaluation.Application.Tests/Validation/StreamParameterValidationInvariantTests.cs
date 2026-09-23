using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Authorization;
using HealthcareSupport.CaseEvaluation.Documents;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Validation;

/// <summary>
/// #959 -- every public application-service method that takes a <see cref="Stream"/> parameter
/// MUST carry <c>[DisableValidation]</c>.
///
/// <para>WHY. ABP's validation interceptor reflects over a method's parameters before the body
/// runs. For a <see cref="Stream"/> it reaches <c>Stream.ReadTimeout</c>, whose base implementation
/// throws <see cref="System.InvalidOperationException"/> when the stream does not support timeouts
/// (a plain <c>MemoryStream</c>, and -- depending on model binding -- possibly the request stream in
/// production). The method then dies with a <c>TargetInvocationException</c> before its own guards or
/// body ever run. <c>[DisableValidation]</c> suppresses that reflective validation for the method.</para>
///
/// <para>This was already known and applied to five upload methods
/// (<c>AppointmentDocumentsAppService</c> x4, <c>BrandingAppService</c>), but three others crossed the
/// same interceptor without it (<c>UserSignatureAppService.UploadAsync</c>,
/// <c>DocumentsAppService.CreateAsync</c> / <c>ReplaceFileAsync</c>). A per-method attribute is easy to
/// forget on the next upload endpoint, and the failure is invisible until someone calls it with a
/// non-timeout stream. This structural invariant makes the requirement enforced rather than
/// remembered -- add a Stream-taking endpoint without the attribute and this test names it.</para>
///
/// <para>Reflects over the Application assembly using the same <see cref="AuthorizationSurface"/>
/// helper the #707 authorization-surface invariants use.</para>
/// </summary>
public class StreamParameterValidationInvariantTests
{
    private static readonly Assembly ApplicationAssembly = typeof(DocumentsAppService).Assembly;

    [Fact]
    public void Every_public_app_service_method_taking_a_Stream_disables_validation()
    {
        var offenders = new List<string>();

        foreach (var service in AuthorizationSurface.Services(ApplicationAssembly))
        {
            foreach (var method in AuthorizationSurface.Methods(service))
            {
                var takesStream = method.GetParameters()
                    .Any(p => typeof(Stream).IsAssignableFrom(p.ParameterType));
                if (!takesStream)
                {
                    continue;
                }

                if (!method.IsDefined(typeof(DisableValidationAttribute), inherit: true))
                {
                    offenders.Add($"{service.FullName}.{method.Name}");
                }
            }
        }

        offenders.ShouldBeEmpty(
            "these public app-service methods take a Stream but lack [DisableValidation], so ABP's " +
            "validation interceptor will reflect over the stream and throw TargetInvocationException " +
            "(via Stream.ReadTimeout) before the method body runs. Add [DisableValidation]:\n  " +
            string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Sanity check that the invariant is actually finding Stream methods -- a test that scanned zero
    /// methods would pass vacuously and prove nothing. There are eight known upload endpoints today.
    /// </summary>
    [Fact]
    public void The_invariant_actually_scans_the_known_upload_methods()
    {
        var streamMethods = AuthorizationSurface.Services(ApplicationAssembly)
            .SelectMany(AuthorizationSurface.Methods)
            .Count(m => m.GetParameters().Any(p => typeof(Stream).IsAssignableFrom(p.ParameterType)));

        streamMethods.ShouldBeGreaterThanOrEqualTo(
            8, "expected at least the eight known Stream-taking upload endpoints; a lower count means " +
               "the reflection is not seeing them and the invariant above is vacuous");
    }
}
