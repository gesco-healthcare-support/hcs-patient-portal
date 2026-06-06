using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.Twitter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Volo.Abp.PermissionManagement;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.RateLimiting;
using StackExchange.Redis;
using Microsoft.OpenApi.Models;
using HealthcareSupport.CaseEvaluation.HealthChecks;
using Hangfire;
using Hangfire.SqlServer;
using HealthcareSupport.CaseEvaluation.BackgroundJobs;
using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DistributedLocking;
using Volo.Abp;
using Volo.Abp.Studio;
using Volo.Abp.Account;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Auditing;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Security;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Caching;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.AspNetCore.Authentication.JwtBearer;
using Localization.Resources.AbpUi;
using Volo.Abp.Account.Localization;
using Volo.Abp.Localization;

namespace HealthcareSupport.CaseEvaluation;

[DependsOn(
    typeof(CaseEvaluationHttpApiModule),
    typeof(AbpAutofacModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpDistributedLockingModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpIdentityAspNetCoreModule),
    typeof(CaseEvaluationApplicationModule),
    typeof(CaseEvaluationEntityFrameworkCoreModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpBackgroundJobsHangfireModule),
    typeof(AbpAspNetCoreSerilogModule)
    )]
public class CaseEvaluationHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (!configuration.GetValue<bool>("App:DisablePII"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }

        ConfigureStudio(hostingEnvironment);
        ConfigureUrls(configuration);
        ConfigureConventionalControllers();
        ConfigureAuthentication(context, configuration);
        ConfigureSwagger(context, configuration);
        ConfigureCache(configuration);
        ConfigureVirtualFileSystem(context);
        ConfigureDataProtection(context, configuration, hostingEnvironment);
        ConfigureDistributedLocking(context, configuration);
        ConfigureCors(context, configuration);
        ConfigureExternalProviders(context);
        ConfigureHealthChecks(context);
        ConfigureHangfire(context, configuration);
        ConfigurePasswordResetRateLimiter(context);
        ConfigureUploadLimits(context);
        ConfigureMultiTenancy();

        // OLD-parity label overrides: inject extra JSON into AbpUi +
        // AbpAccount resources so the SPA's /api/abp/application-localization
        // endpoint serves "Sign Up" / "Sign In" / "Already have an account?"
        // (the same overrides registered in AuthServerModule for Razor pages).
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<AbpUiResource>()
                .AddVirtualJson("/Localization/AbpUiOverride");

            options.Resources
                .Get<AccountResource>()
                .AddVirtualJson("/Localization/AccountOverride");
        });

        Configure<PermissionManagementOptions>(options =>
        {
            options.IsDynamicPermissionStoreEnabled = true;
        });

        // W2-4: stamp the audit-row ApplicationName so /audit-logs distinguishes
        // API-side activity from AuthServer activity. Without this, HttpApi.Host
        // entity-change rows ship with an empty ApplicationName, making the
        // audit grid harder to filter. Cosmetic but improves the audit UX.
        Configure<AbpAuditingOptions>(options =>
        {
            options.ApplicationName = "API";
        });

        Configure<AbpSecurityHeadersOptions>(options =>
        {
            options.Headers["X-Frame-Options"] = "DENY";
        });

        // 2026-05-13 -- map domain error codes that ABP would otherwise
        // route to its default HTTP status (403 for BusinessException) to
        // the semantically-correct 4xx client-error status.
        //
        // 2026-05-15 -- also map the six InternalUser:* error codes from
        // the IT-Admin internal-user-creation flow. Without these, the
        // BusinessException default of 403 surfaces as "Forbidden" on
        // every duplicate-email or invalid-role response, which is
        // semantically wrong (the caller IS authorized to use the
        // endpoint; the input is invalid). Same pattern that closes
        // BUG-003 for RegistrationDuplicateEmail.
        //
        // 2026-05-19 -- closes BUG-023 (Registration confirm-password +
        // firm-name validators were returning 403) and adds the new
        // InternalUserTenantMismatch code from the tenant-admin
        // internal-user creation flow to the same 400 mapping list.
        Configure<Volo.Abp.AspNetCore.ExceptionHandling.AbpExceptionHttpStatusCodeOptions>(options =>
        {
            // BUG-012 Sub-bug 2 (2026-05-22): the 10 shared mappings
            // moved into HealthcareSupport.CaseEvaluation.Exceptions.
            // CaseEvaluationExceptionStatusCodeMappings; same set is
            // invoked from CaseEvaluationAuthServerModule.
            HealthcareSupport.CaseEvaluation.Exceptions.CaseEvaluationExceptionStatusCodeMappings
                .MapSharedRegistrationAndInternalUserCodes(options);

            // Host-specific: InternalUserTenantMismatch + the 4
            // appointment state-machine codes below are HttpApi.Host
            // only -- their controllers don't load into AuthServer.
            options.Map(
                CaseEvaluationDomainErrorCodes.InternalUserTenantMismatch,
                System.Net.HttpStatusCode.BadRequest);

            // BUG-024 follow-up (2026-05-19) -- the appointment state-machine
            // throws this on every illegal transition (Approve from Approved,
            // Reject from Rejected, etc). Same family as BUG-003 / BUG-023:
            // input violates a precondition; HTTP 400 fits the semantic, not
            // 403.
            options.Map(
                CaseEvaluationDomainErrorCodes.AppointmentInvalidTransition,
                System.Net.HttpStatusCode.BadRequest);

            // 2026-05-15 -- one-doctor-per-tenant invariant guards in
            // DoctorsAppService. Both are client-input / precondition
            // violations; HTTP 400 fits, not ABP's default 403 (which the
            // SPA would treat as a permission failure and retry).
            options.Map(
                CaseEvaluationDomainErrorCodes.DoctorOnePerTenantViolated,
                System.Net.HttpStatusCode.BadRequest);
            options.Map(
                CaseEvaluationDomainErrorCodes.DoctorCannotDeleteWithDependents,
                System.Net.HttpStatusCode.BadRequest);

            // IP4 (2026-06-05) -- Location master-data integrity guards in
            // LocationManager. All four are client-input / precondition
            // violations (duplicate name, negative fee, bad zip, in-use delete);
            // HTTP 400 fits, not ABP's default 403 (which the SPA would treat as
            // a permission failure).
            options.Map(
                CaseEvaluationDomainErrorCodes.LocationDuplicateName,
                System.Net.HttpStatusCode.BadRequest);
            options.Map(
                CaseEvaluationDomainErrorCodes.LocationParkingFeeNegative,
                System.Net.HttpStatusCode.BadRequest);
            options.Map(
                CaseEvaluationDomainErrorCodes.LocationZipCodeInvalid,
                System.Net.HttpStatusCode.BadRequest);
            options.Map(
                CaseEvaluationDomainErrorCodes.LocationInUse,
                System.Net.HttpStatusCode.BadRequest);

            // 2026-05-15 -- capacity-aware booking gate
            // (AppointmentsAppService.ValidateDoctorAvailabilityForBooking).
            // All three are client-input precondition violations; HTTP 400 fits.
            options.Map(
                CaseEvaluationDomainErrorCodes.AppointmentBookingSlotFull,
                System.Net.HttpStatusCode.BadRequest);
            options.Map(
                CaseEvaluationDomainErrorCodes.AppointmentBookingSlotClosed,
                System.Net.HttpStatusCode.BadRequest);
            options.Map(
                CaseEvaluationDomainErrorCodes.AppointmentBookingSlotTypeMismatch,
                System.Net.HttpStatusCode.BadRequest);

            // AF3 + AF4 (2026-06-04) -- Panel Number / appointment-type
            // coupling enforced in AppointmentManager. Both are client-input
            // validation failures (required for PQME / not allowed for AME-IME);
            // HTTP 400 fits, not ABP's default 403.
            options.Map(
                CaseEvaluationDomainErrorCodes.AppointmentPanelNumberRequiredForPqme,
                System.Net.HttpStatusCode.BadRequest);
            options.Map(
                CaseEvaluationDomainErrorCodes.AppointmentPanelNumberNotAllowedForType,
                System.Net.HttpStatusCode.BadRequest);

            // BUG-025 (2026-05-21) -- AppointmentDocuments upload size
            // rejections. Extracted to a named static helper so unit
            // tests can verify the mappings without booting the full
            // host (see CaseEvaluationHttpApiHostModuleTests).
            MapAppointmentDocumentErrorCodes(options);
        });
    }

    /// <summary>
    /// BUG-025 (2026-05-21) -- HTTP-status mappings for the
    /// AppointmentDocuments upload path. <c>FileTooLarge</c> -> 413
    /// (RFC 7231 canonical "request entity too large"; distinct from
    /// 400 so the SPA can branch the user-facing message without
    /// parsing the body). <c>FileEmpty</c> -> 400 (client-input
    /// validation failure, same shape as the other input-validator
    /// codes mapped above).
    /// </summary>
    internal static void MapAppointmentDocumentErrorCodes(Volo.Abp.AspNetCore.ExceptionHandling.AbpExceptionHttpStatusCodeOptions options)
    {
        options.Map(
            CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge,
            System.Net.HttpStatusCode.RequestEntityTooLarge);
        options.Map(
            CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty,
            System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// ADR-006 (2026-05-05) -- subdomain tenant routing.
    /// ADR-007 (2026-05-11) -- replaced stock DomainTenantResolveContributor
    /// with HostAwareDomainTenantResolveContributor so the reserved subdomain
    /// "admin" maps to Host context instead of 404. See ADR-007 for the
    /// empirical evidence that the stock contributor 404s on unknown slugs
    /// rather than falling through to host.
    ///
    /// Mirrors AuthServer's resolver config so API requests resolve tenant from
    /// the Host header (e.g. falkinstein.localhost:44327 -> Falkinstein tenant).
    /// QueryString, Cookie, Route, and Header resolvers are dropped so
    /// ?__tenant=GUID cannot override the URL.
    /// </summary>
    private void ConfigureMultiTenancy()
    {
        Configure<AbpTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Clear();
            options.TenantResolvers.Add(new CurrentUserTenantResolveContributor());
            options.TenantResolvers.Add(
                new HostAwareDomainTenantResolveContributor("{0}.localhost"));
        });
    }

    private void ConfigureStudio(IHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsProduction())
        {
            Configure<AbpStudioClientOptions>(options =>
            {
                options.IsLinkEnabled = false;
            });
        }
    }

    private static void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddCaseEvaluationHealthChecks();
    }

    /// <summary>
    /// BUG-025 (2026-05-21) -- defense-in-depth size caps on
    /// multipart/form-data uploads.
    ///
    /// <para>The AppointmentDocuments AppService enforces
    /// <see cref="HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentsAppService.MaxFileSizeBytes"/>
    /// (10 MB) at the application layer. That check fires only after
    /// the request body is buffered, so this method configures the
    /// framework-layer caps (Kestrel + FormOptions) at 12 MB -- a 2 MB
    /// buffer above the 10 MB AppService cap so multipart boundary
    /// headers plus small request overhead don't trip the framework's
    /// raw 413 before the AppService's localized
    /// <c>BusinessException</c> + <c>data.MaxBytes</c> /
    /// <c>data.ActualBytes</c> can fire. Uploads above 12 MB get a
    /// framework 413 with no localized message; uploads between 10 MB
    /// and 12 MB get the friendly localized 413 with data so the SPA
    /// can render "max 10 MB" + the actual size.</para>
    /// </summary>
    internal static void ConfigureUploadLimits(ServiceConfigurationContext context)
    {
        const long FrameworkCapBytes = 12L * 1024 * 1024;

        context.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = FrameworkCapBytes;
        });

        context.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = FrameworkCapBytes;
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["Angular"].RootUrl = configuration["App:AngularUrl"];
            // 2026-05-18 -- confirmation + reset URLs are hosted by the
            // AuthServer Razor pages (custom overrides under Pages/Account/),
            // not the SPA. Repointed from the deleted SPA routes. Mirrors
            // the same config in CaseEvaluationAuthServerModule. See
            // docs/plans/2026-05-18-fix-verification-email-url.md.
            options.Applications["MVC"].Urls[AccountUrlNames.PasswordReset] = "Account/ResetPassword";
            options.Applications["MVC"].Urls[AccountUrlNames.EmailConfirmation] = "Account/EmailConfirmation";
        });
    }

    private void ConfigureCache(IConfiguration configuration)
    {
        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "CaseEvaluation:";
        });
    }

    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (hostingEnvironment.IsDevelopment())
        {
            // See AuthServer module for the rationale: Directory.Exists guard
            // protects the embedded fileset in Docker (where the host source
            // tree isn't mounted). Without the guard, all CaseEvaluation
            // localization JSON gets replaced by an empty directory and
            // every L("Menu:Home"), L("Enum:..."), L("Appointment:Action:...")
            // call returns the literal key.
            var basePath = hostingEnvironment.ContentRootPath;
            string Resolve(string projectName) => Path.Combine(
                basePath,
                string.Format("..{0}..{0}src{0}{1}", Path.DirectorySeparatorChar, projectName));

            var sharedPath = Resolve("HealthcareSupport.CaseEvaluation.Domain.Shared");
            var domainPath = Resolve("HealthcareSupport.CaseEvaluation.Domain");
            var appContractsPath = Resolve("HealthcareSupport.CaseEvaluation.Application.Contracts");
            var appPath = Resolve("HealthcareSupport.CaseEvaluation.Application");
            var httpApiPath = Resolve("HealthcareSupport.CaseEvaluation.HttpApi");

            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                if (Directory.Exists(sharedPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationDomainSharedModule>(sharedPath);
                }
                if (Directory.Exists(domainPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationDomainModule>(domainPath);
                }
                if (Directory.Exists(appContractsPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationApplicationContractsModule>(appContractsPath);
                }
                if (Directory.Exists(appPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationApplicationModule>(appPath);
                }
                if (Directory.Exists(httpApiPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationHttpApiModule>(httpApiPath);
                }
            });
        }
    }

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(CaseEvaluationApplicationModule).Assembly);
        });
    }

    /// <summary>
    /// Phase 10 (2026-05-03) + BUG-035 fix (2026-05-22) -- ASP.NET Core
    /// rate limiter scoped to anonymous endpoints under
    /// <c>/api/public/external-account</c> +
    /// <c>/api/public/appointment-documents</c> +
    /// <c>/api/public/external-signup/register</c>.
    ///
    /// <para><b>Password-reset (dual-partition):</b> two chained
    /// FixedWindow limiters per OWASP Forgot Password Cheat Sheet:
    /// <list type="bullet">
    /// <item><description>Primary: per-account 5/hour partitioned by the
    /// <c>email</c> field of the JSON body (read by
    /// <see cref="RateLimiting.PasswordResetEmailPeekMiddleware"/> and
    /// stashed in <c>HttpContext.Items</c>). Fallback chain when the
    /// stash is empty: <c>?email=</c> query -> JWT sub -> client IP.
    /// </description></item>
    /// <item><description>Secondary: per-IP 50/hour. Catches truly
    /// abusive IPs without punishing NAT/CGNAT-shared users for one
    /// neighbor's behavior (the BUG-035 footgun).</description></item>
    /// </list>
    /// Requests must pass BOTH partitions; either alone returns 429.
    /// </para>
    ///
    /// <para><b>Other prefixes</b> (document-upload-by-code,
    /// external-signup register) keep their existing single-layer
    /// IP-based 5/hour buckets. Those weren't part of BUG-035.</para>
    ///
    /// <para>The middleware reads the body up to a 4 KB cap and
    /// rewinds the stream so MVC's model binding still sees the
    /// body. The original Phase 10 doc-comment claimed body
    /// partitioning was "not worth the per-request cost" -- BUG-035
    /// showed that calculation missed the shared-IP DoS vector. The
    /// 4 KB read is negligible cost compared to the security gap it
    /// closes.</para>
    ///
    /// <para>OnRejected emits <c>Retry-After</c> (OWASP API4
    /// recommendation) so callers can compute wait time without
    /// out-of-band knowledge of the window.</para>
    ///
    /// <para>Refs:
    /// https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html ,
    /// https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit
    /// </para>
    /// </summary>
    private static void ConfigurePasswordResetRateLimiter(ServiceConfigurationContext context)
    {
        context.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = (int)System.Net.HttpStatusCode.TooManyRequests;

            options.OnRejected = (rejectionContext, _) =>
            {
                // Emit Retry-After when the rejecting lease exposes it.
                // FixedWindowRateLimiter does, so callers can wait the
                // window without polling.
                if (rejectionContext.Lease.TryGetMetadata(
                        System.Threading.RateLimiting.MetadataName.RetryAfter,
                        out var retryAfter))
                {
                    rejectionContext.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                return System.Threading.Tasks.ValueTask.CompletedTask;
            };

            // BUG-035: per-account (per-email) primary limiter.
            // 5/hour bucket per unique email keeps brute-force /
            // mailbox-flooding capped at the OWASP-recommended level
            // for password reset.
            var perEmailLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(
                httpContext =>
                {
                    if (IsPasswordResetPath(httpContext))
                    {
                        var key = ResolvePasswordResetEmailPartitionKey(httpContext);
                        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: $"pwd-reset-email:{key}",
                            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromHours(1),
                                QueueLimit = 0,
                                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                                AutoReplenishment = true,
                            });
                    }
                    if (IsDocumentUploadByCodePath(httpContext))
                    {
                        // Phase 14b (2026-05-04) -- per-verification-code
                        // rate limit on the anonymous document-upload
                        // endpoint at /api/public/appointment-documents/{id}/upload-by-code/{code}.
                        // Partition by the code segment so brute-force
                        // attempts against ANY document share the same
                        // bucket per IP / per code.
                        var key = ResolveDocumentUploadPartitionKey(httpContext);
                        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: $"doc-upload:{key}",
                            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromHours(1),
                                QueueLimit = 0,
                                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                                AutoReplenishment = true,
                            });
                    }
                    if (IsExternalSignupRegisterPath(httpContext))
                    {
                        // 2026-05-13 -- rate-limit the anonymous register
                        // endpoint so the BUG-001 fix (generic error
                        // message that no longer echoes the input email)
                        // is not still brute-forceable as an enumeration
                        // oracle via timing or response-byte differentials.
                        // Partition by client IP since this endpoint is
                        // anonymous (no JWT sub).
                        var key = ResolveExternalSignupPartitionKey(httpContext);
                        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: $"signup:{key}",
                            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromHours(1),
                                QueueLimit = 0,
                                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                                AutoReplenishment = true,
                            });
                    }
                    return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("non-rate-limited");
                });

            // BUG-035: per-IP secondary limiter, generous threshold so
            // NAT/CGNAT-shared users don't block each other under
            // normal usage but a single IP can't fan out across 1000
            // emails. Only applied to the password-reset prefix --
            // other prefixes already have their own IP-based partitions.
            var perIpSecondaryLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(
                httpContext =>
                {
                    if (IsPasswordResetPath(httpContext))
                    {
                        var key = ResolvePasswordResetIpPartitionKey(httpContext);
                        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: $"pwd-reset-ip:{key}",
                            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 50,
                                Window = TimeSpan.FromHours(1),
                                QueueLimit = 0,
                                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                                AutoReplenishment = true,
                            });
                    }
                    return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("non-rate-limited-secondary");
                });

            options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.CreateChained(
                perEmailLimiter,
                perIpSecondaryLimiter);
        });
    }

    /// <summary>Policy name used by the password-reset endpoints.</summary>
    public const string PasswordResetRateLimitPolicy = "password-reset-by-email";

    /// <summary>Path prefix matched by the password-reset rate limiter.</summary>
    public const string PasswordResetPathPrefix = "/api/public/external-account";

    /// <summary>Phase 14b: path prefix matched by the document-upload-by-code limiter.</summary>
    public const string DocumentUploadByCodePathPrefix = "/api/public/appointment-documents";

    /// <summary>2026-05-13: full path matched by the register rate limiter.</summary>
    public const string ExternalSignupRegisterPath = "/api/public/external-signup/register";

    /// <summary>
    /// True when the request targets one of the password-reset endpoints
    /// (<c>send-password-reset-code</c> or <c>reset-password</c>) under
    /// <see cref="PasswordResetPathPrefix"/>. Internal-static so unit
    /// tests can pin path-matching edge cases.
    /// </summary>
    internal static bool IsPasswordResetPath(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        return httpContext.Request.Path.StartsWithSegments(PasswordResetPathPrefix);
    }

    /// <summary>
    /// Phase 14b -- true when the request targets the anonymous
    /// document-upload-by-code endpoint under
    /// <see cref="DocumentUploadByCodePathPrefix"/>. Internal-static so
    /// unit tests can pin path-matching edge cases.
    /// </summary>
    internal static bool IsDocumentUploadByCodePath(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        if (!httpContext.Request.Path.StartsWithSegments(DocumentUploadByCodePathPrefix))
        {
            return false;
        }
        // Only POSTs to .../{id}/upload-by-code/{code} are rate limited;
        // a future GET on the same prefix should not be throttled.
        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            return false;
        }
        return httpContext.Request.Path.Value?.Contains("/upload-by-code/", StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// 2026-05-13 -- true when the request targets the anonymous
    /// external-signup register endpoint
    /// (<see cref="ExternalSignupRegisterPath"/>). Only POST is matched
    /// (a future GET on the same path -- e.g. for client-side checks --
    /// would not be brute-forceable in the same way).
    /// </summary>
    internal static bool IsExternalSignupRegisterPath(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            return false;
        }
        return httpContext.Request.Path.Equals(
            ExternalSignupRegisterPath,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 2026-05-13 -- partition key for the external-signup register
    /// limiter. Anonymous endpoint -- partition by client IP (and a
    /// "global" fallback if the connection has no remote IP, e.g.
    /// in some test harnesses).
    /// </summary>
    internal static string ResolveExternalSignupPartitionKey(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            return $"ip:{ip}";
        }
        return "global";
    }

    /// <summary>
    /// Phase 14b -- partition key for the document-upload-by-code
    /// limiter. Precedence: verification-code path segment (so brute
    /// force against ONE code is throttled) -> client IP (so brute
    /// force across many codes is also throttled). Internal-static for
    /// unit-test reach.
    /// </summary>
    internal static string ResolveDocumentUploadPartitionKey(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;
        const string Marker = "/upload-by-code/";
        var idx = path.IndexOf(Marker, StringComparison.Ordinal);
        if (idx >= 0)
        {
            var afterMarker = path.Substring(idx + Marker.Length);
            // Trim any trailing slash / query.
            var slash = afterMarker.IndexOf('/');
            var code = slash >= 0 ? afterMarker.Substring(0, slash) : afterMarker;
            if (!string.IsNullOrWhiteSpace(code))
            {
                return $"code:{code}";
            }
        }
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            return $"ip:{ip}";
        }
        return "global";
    }

    /// <summary>
    /// BUG-035 fix (2026-05-22) -- resolves the per-account
    /// partition key for the password-reset primary limiter.
    /// Precedence:
    /// <list type="number">
    /// <item><description>Email peeked from the JSON body by
    /// <see cref="RateLimiting.PasswordResetEmailPeekMiddleware"/>
    /// and stashed in <c>HttpContext.Items</c> -- the canonical
    /// per-account control per OWASP Forgot Password Cheat Sheet.</description></item>
    /// <item><description><c>?email=</c> query-string (kept for
    /// backward-compatible test access).</description></item>
    /// <item><description>JWT <c>sub</c> claim (only relevant when
    /// the password-reset endpoint is called from an authenticated
    /// context, e.g. self-service profile flows).</description></item>
    /// <item><description>Client IP -- last-resort fallback for
    /// requests where the body peek failed (malformed JSON, body
    /// too large, etc).</description></item>
    /// </list>
    /// Internal-static so unit tests can verify edge cases without
    /// standing up the middleware pipeline.
    /// </summary>
    internal static string ResolvePasswordResetEmailPartitionKey(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(RateLimiting.PasswordResetEmailPeekMiddleware.ContextItemKey, out var stashed)
            && stashed is string stashedEmail
            && !string.IsNullOrWhiteSpace(stashedEmail))
        {
            return $"email:{stashedEmail}";
        }
        var fromQuery = httpContext.Request.Query["email"].ToString();
        if (!string.IsNullOrWhiteSpace(fromQuery))
        {
            return $"email:{fromQuery.Trim().ToLowerInvariant()}";
        }
        var sub = httpContext.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sub))
        {
            return $"sub:{sub}";
        }
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            return $"ip:{ip}";
        }
        return "global";
    }

    /// <summary>
    /// BUG-035 fix (2026-05-22) -- resolves the per-IP secondary
    /// partition key for the password-reset rate limiter. Pure
    /// IP-based; falls back to <c>"global"</c> when the IP isn't
    /// resolvable.
    /// </summary>
    internal static string ResolvePasswordResetIpPartitionKey(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            return $"ip:{ip}";
        }
        return "global";
    }

    private static void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddAbpJwtBearer(options =>
            {
                options.Authority = configuration["AuthServer:Authority"];
                options.RequireHttpsMetadata = configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata");
                options.Audience = "CaseEvaluation";

                // Docker/K8s split-horizon: the API reaches AuthServer via internal DNS
                // (e.g. http://authserver:8080) but tokens carry the public issuer URL
                // (e.g. http://localhost:44368). Three things must align:
                //   1. MetadataAddress  -> internal URL for OIDC discovery fetch
                //   2. ValidIssuer      -> public URL matching the "iss" claim in tokens
                //   3. Authority        -> public URL for default issuer validation
                // When MetaAddress == Authority (local dev), this block is skipped entirely.
                var metaAddress = configuration["AuthServer:MetaAddress"];
                if (!string.IsNullOrEmpty(metaAddress) && metaAddress != configuration["AuthServer:Authority"])
                {
                    options.MetadataAddress = $"{metaAddress.TrimEnd('/')}/.well-known/openid-configuration";
                    options.TokenValidationParameters.ValidIssuer = configuration["AuthServer:Authority"]!.TrimEnd('/') + "/";
                }

                // ADR-006 (2026-05-05) -- subdomain tenant routing.
                //
                // With each tenant served on its own subdomain
                // (e.g. http://falkinstein.localhost:44368), tokens are issued with
                // `iss: http://falkinstein.localhost:44368/` -- one issuer per tenant.
                // The default ValidIssuer set above is the bare-host URL
                // (http://localhost:44368/) and would reject the per-tenant variants.
                //
                // The IssuerValidator callback accepts any issuer whose host pattern
                // is `<slug>.<authority-host>` on the same scheme + port. This turns
                // a single registered ValidIssuer into a wildcard that mirrors the
                // resolver's `{0}.localhost` format. Compromises nothing: the
                // signing key still has to come from the AuthServer's discovery
                // doc, which the API fetches from the internal MetaAddress.
                //
                // Cross-reference: ADR-006 + Volosoft Medium article on Angular +
                // OpenIddict subdomain resolution.
                var authority = configuration["AuthServer:Authority"]!;
                var authorityUri = new Uri(authority);
                var authorityHost = authorityUri.Host;
                var authorityPort = authorityUri.Port;
                var authorityScheme = authorityUri.Scheme;

                options.TokenValidationParameters.IssuerValidator = (issuer, _, _) =>
                {
                    if (string.IsNullOrEmpty(issuer))
                    {
                        throw new Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException("Empty issuer");
                    }

                    var issuerUri = new Uri(issuer);
                    if (issuerUri.Scheme != authorityScheme || issuerUri.Port != authorityPort)
                    {
                        throw new Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException(
                            $"Issuer scheme/port {issuerUri.Scheme}://...:{issuerUri.Port} does not match authority {authorityScheme}://...:{authorityPort}");
                    }

                    // Accept exact-host match (host context, e.g. admin.localhost
                    // resolves to no tenant) OR any single-label subdomain of the
                    // authority host (e.g. falkinstein.localhost when authority
                    // host is localhost). Reject deeper paths, IPs, and unrelated
                    // hosts.
                    var issuerHost = issuerUri.Host;
                    if (string.Equals(issuerHost, authorityHost, StringComparison.OrdinalIgnoreCase))
                    {
                        return issuer;
                    }
                    if (issuerHost.EndsWith("." + authorityHost, StringComparison.OrdinalIgnoreCase))
                    {
                        var slugPart = issuerHost.Substring(0, issuerHost.Length - authorityHost.Length - 1);
                        if (slugPart.Length > 0 && !slugPart.Contains('.', StringComparison.Ordinal))
                        {
                            return issuer;
                        }
                    }
                    throw new Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException(
                        $"Issuer host {issuerHost} is not the authority host or a single-label subdomain of it ({authorityHost}).");
                };
                // Clear ValidIssuer so the framework defers to the callback above.
                options.TokenValidationParameters.ValidIssuer = null;
                options.TokenValidationParameters.ValidateIssuer = true;
            });

        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGenWithOidc(
            configuration["AuthServer:Authority"]!,
            ["CaseEvaluation"],
            [AbpSwaggerOidcFlows.AuthorizationCode],
            configuration["AuthServer:MetaAddress"],
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "CaseEvaluation API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            });
    }

    private static void ConfigureDataProtection(
        ServiceConfigurationContext context,
        IConfiguration configuration,
        IWebHostEnvironment hostingEnvironment)
    {
        if (AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            return;
        }

        var dataProtectionBuilder = context.Services.AddDataProtection().SetApplicationName("CaseEvaluation");

        // Persist DataProtection keys to Redis whenever a Redis connection is
        // configured, in BOTH dev and prod. Reason: AuthServer + HttpApi.Host
        // run as separate Docker containers (separate filesystems), so the
        // default key store at /root/.aspnet/DataProtection-Keys is per-
        // container. ABP-Identity tokens (e.g. EmailConfirmation) generated
        // by the API host fail validation when the AuthServer's confirm-email
        // endpoint tries to decrypt them with a different key ring -- the
        // request returns 403 with "Volo.Abp.Identity:InvalidToken".
        // Redis-backed shared keys + matching SetApplicationName above make
        // both processes interchangeable validators.
        var redisConfig = configuration["Redis:Configuration"];
        if (!string.IsNullOrWhiteSpace(redisConfig))
        {
            var redis = ConnectionMultiplexer.Connect(redisConfig);
            dataProtectionBuilder.PersistKeysToStackExchangeRedis(redis, "CaseEvaluation-Protection-Keys");
        }
    }

    private static void ConfigureDistributedLocking(
        ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        if (AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            return;
        }

        context.Services.AddSingleton<IDistributedLockProvider>(sp =>
        {
            var connection = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]!);
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });
    }

    /// <summary>
    /// Wires Hangfire with SQL Server storage to back ABP's background-jobs runtime.
    /// Wave 0 lays the runtime; Wave 1 capabilities (scheduler-notifications) add the
    /// recurring-job classes. Schema is auto-created on first connection via
    /// <c>PrepareSchemaIfNecessary = true</c> (Hangfire default). Dashboard is mounted
    /// at <c>/hangfire</c> in <c>OnApplicationInitialization</c> below; auth-filter
    /// hardening is deferred to the post-MVP "Wave 0 hardening" tail.
    /// </summary>
    private static void ConfigureHangfire(ServiceConfigurationContext context, IConfiguration configuration)
    {
        if (AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            return;
        }

        context.Services.AddHangfire(config =>
        {
            config.UseSqlServerStorage(
                configuration.GetConnectionString("Default"),
                new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true,
                });
        });
    }

    private static void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]?
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim().RemovePostFix("/"))
                            .ToArray() ?? Array.Empty<string>()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    private static void ConfigureExternalProviders(ServiceConfigurationContext context)
    {
        context.Services
            .AddDynamicExternalLoginProviderOptions<GoogleOptions>(
                GoogleDefaults.AuthenticationScheme,
                options =>
                {
                    options.WithProperty(x => x.ClientId);
                    options.WithProperty(x => x.ClientSecret, isSecret: true);
                }
            )
            .AddDynamicExternalLoginProviderOptions<MicrosoftAccountOptions>(
                MicrosoftAccountDefaults.AuthenticationScheme,
                options =>
                {
                    options.WithProperty(x => x.ClientId);
                    options.WithProperty(x => x.ClientSecret, isSecret: true);
                }
            )
            .AddDynamicExternalLoginProviderOptions<TwitterOptions>(
                TwitterDefaults.AuthenticationScheme,
                options =>
                {
                    options.WithProperty(x => x.ConsumerKey);
                    options.WithProperty(x => x.ConsumerSecret, isSecret: true);
                }
            );
    }


    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();
        app.UseRouting();
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        app.UseCors();
        app.UseAuthentication();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        // BUG-035 fix (2026-05-22) -- peek the JSON body's `email` field
        // for anonymous password-reset POSTs and stash it in
        // HttpContext.Items so the rate-limiter partitioner can use it
        // as the partition key. Must run before UseRateLimiter so the
        // stash is available when the partitioner executes.
        app.UseMiddleware<RateLimiting.PasswordResetEmailPeekMiddleware>();

        // Phase 10 (2026-05-03) -- enable rate limiter middleware so the
        // [EnableRateLimiting] attribute on the password-reset endpoints
        // takes effect. Placed AFTER UseAuthorization so authenticated
        // callers' JWT sub claim is available to the partitioner.
        app.UseRateLimiter();

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "CaseEvaluation API");

            var configuration = context.GetConfiguration();
            options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
        });

        // Hangfire dashboard at /hangfire. Wave 0 ships dev-anonymous access (per the
        // approved plan -- auth filter is policy hardening, deferred to post-MVP tail).
        // Hangfire server starts automatically via AbpBackgroundJobsHangFireModule.
        if (!AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new AnonymousHangfireDashboardAuthorizationFilter() },
                IgnoreAntiforgeryToken = true,
            });

            // W2-10: register the 3 CCR-driven recurring jobs. Cron timezone is
            // explicit America/Los_Angeles per the deep-dive (08:00 PT for CCR
            // jobs, 07:00 PT for the appointment-day job). Job classes are
            // resolved through ABP DI inside an AbpBackgroundJobExecutionWrapper
            // shape that the lambda invokes via the IServiceProvider.
            ConfigureHangfireRecurringJobs();
        }

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }

    /// <summary>
    /// W2-10: register the 3 CCR-driven Hangfire recurring jobs.
    /// Each cron runs in America/Los_Angeles timezone (per CCR text + OLD's
    /// scheduler convention).
    /// </summary>
    private static void ConfigureHangfireRecurringJobs()
    {
        var pacificTime = TryGetPacificTimeZone();
        var options = new RecurringJobOptions { TimeZone = pacificTime };

        global::Hangfire.RecurringJob.AddOrUpdate<HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs.RequestSchedulingReminderJob>(
            HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs.RequestSchedulingReminderJob.RecurringJobId,
            j => j.ExecuteAsync(),
            HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs.RequestSchedulingReminderJob.CronExpression,
            options);

        global::Hangfire.RecurringJob.AddOrUpdate<HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs.CancellationRescheduleReminderJob>(
            HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs.CancellationRescheduleReminderJob.RecurringJobId,
            j => j.ExecuteAsync(),
            HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs.CancellationRescheduleReminderJob.CronExpression,
            options);

        global::Hangfire.RecurringJob.AddOrUpdate<HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs.AppointmentDayReminderJob>(
            HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs.AppointmentDayReminderJob.RecurringJobId,
            j => j.ExecuteAsync(),
            HealthcareSupport.CaseEvaluation.Appointments.Notifications.Jobs.AppointmentDayReminderJob.CronExpression,
            options);

        // Phase 14 (2026-05-04) -- JDF auto-cancel daily 06:00 PT.
        // Earlier than the AppointmentDayReminderJob (07:00) so an
        // auto-cancelled appointment does not also trigger a T-1
        // appointment-day reminder for a visit that won't happen.
        global::Hangfire.RecurringJob.AddOrUpdate<HealthcareSupport.CaseEvaluation.Notifications.Jobs.JointDeclarationAutoCancelJob>(
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.JointDeclarationAutoCancelJob.RecurringJobId,
            j => j.ExecuteAsync(),
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.JointDeclarationAutoCancelJob.CronExpression,
            options);

        // Phase 14b (2026-05-04) -- package-document reminder daily
        // 08:30 PT. Fires after the JDF auto-cancel + appointment-day
        // reminder + CCR jobs so reminders go out in a deterministic
        // order.
        global::Hangfire.RecurringJob.AddOrUpdate<HealthcareSupport.CaseEvaluation.Notifications.Jobs.PackageDocumentReminderJob>(
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.PackageDocumentReminderJob.RecurringJobId,
            j => j.ExecuteAsync(),
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.PackageDocumentReminderJob.CronExpression,
            options);

        // Phase 7 (Category 7, 2026-05-10) -- four OLD SchedulerDomain reminder paths
        // wired into Hangfire RecurringJobs. Cron times chosen so reminders fan out in
        // OLD-style deterministic order on top of the existing 06:00-08:30 PT chain:
        //   08:15 -- DueDateApproachingJob       (T-14/T-7/T-3 days before DueDate)
        //   08:45 -- DueDateDocumentIncompleteJob (T-7 + docs outstanding)
        //   09:00 -- PendingDailyDigestJob       (digest to clinic-staff inbox)
        //   09:15 -- InternalStaffQueueDigestJob (per-staff queue counts)
        global::Hangfire.RecurringJob.AddOrUpdate<HealthcareSupport.CaseEvaluation.Notifications.Jobs.DueDateApproachingJob>(
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.DueDateApproachingJob.RecurringJobId,
            j => j.ExecuteAsync(),
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.DueDateApproachingJob.CronExpression,
            options);

        global::Hangfire.RecurringJob.AddOrUpdate<HealthcareSupport.CaseEvaluation.Notifications.Jobs.DueDateDocumentIncompleteJob>(
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.DueDateDocumentIncompleteJob.RecurringJobId,
            j => j.ExecuteAsync(),
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.DueDateDocumentIncompleteJob.CronExpression,
            options);

        global::Hangfire.RecurringJob.AddOrUpdate<HealthcareSupport.CaseEvaluation.Notifications.Jobs.PendingDailyDigestJob>(
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.PendingDailyDigestJob.RecurringJobId,
            j => j.ExecuteAsync(),
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.PendingDailyDigestJob.CronExpression,
            options);

        global::Hangfire.RecurringJob.AddOrUpdate<HealthcareSupport.CaseEvaluation.Notifications.Jobs.InternalStaffQueueDigestJob>(
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.InternalStaffQueueDigestJob.RecurringJobId,
            j => j.ExecuteAsync(),
            HealthcareSupport.CaseEvaluation.Notifications.Jobs.InternalStaffQueueDigestJob.CronExpression,
            options);
    }

    private static TimeZoneInfo TryGetPacificTimeZone()
    {
        // .NET 6+ supports IANA timezone IDs cross-platform; fall back to the
        // Windows ID if the IANA lookup fails (older runtime / missing tzdata).
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
