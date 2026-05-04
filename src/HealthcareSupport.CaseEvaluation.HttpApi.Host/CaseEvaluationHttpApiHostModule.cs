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

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["Angular"].RootUrl = configuration["App:AngularUrl"];
            options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
            options.Applications["Angular"].Urls[AccountUrlNames.EmailConfirmation] = "account/email-confirmation";
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
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HealthcareSupport.CaseEvaluation.Domain.Shared", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HealthcareSupport.CaseEvaluation.Domain", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HealthcareSupport.CaseEvaluation.Application.Contracts", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HealthcareSupport.CaseEvaluation.Application", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<CaseEvaluationHttpApiModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HealthcareSupport.CaseEvaluation.HttpApi", Path.DirectorySeparatorChar)));
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
    /// Phase 10 (2026-05-03) -- ASP.NET Core fixed-window rate limiter
    /// scoped to the password-reset surface
    /// (<c>POST /api/public/external-account/send-password-reset-code</c>
    /// and <c>POST /api/public/external-account/reset-password</c>).
    ///
    /// <para>Window: 1 hour. Permit: 5. Queue: 0 (over-limit returns 429
    /// immediately rather than queueing). Partition key precedence:
    /// optional <c>email</c> query-string override -> AuthN <c>sub</c>
    /// claim -> client IP. Body-field partitioning is intentionally NOT
    /// used because partitioners run before model binding and reading
    /// the body here would require enabling rewindable request bodies
    /// across the whole pipeline -- not worth the per-request cost when
    /// IP partitioning already blocks the obvious abuse vector. Email
    /// partitioning is enforced via <c>email</c> query string (used
    /// only by tests).</para>
    ///
    /// <para>This is a NEW addition vs OLD (OLD had no rate limiting on
    /// forgot-password) -- accepted as a security fix that does not
    /// change visible behavior. Cite:
    /// https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit
    /// </para>
    /// </summary>
    private static void ConfigurePasswordResetRateLimiter(ServiceConfigurationContext context)
    {
        context.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = (int)System.Net.HttpStatusCode.TooManyRequests;
            // Global limiter strategy: every request is partitioned, but
            // requests that do NOT match the password-reset prefix are
            // routed into a no-op partition. This avoids needing the
            // [EnableRateLimiting] attribute (which would force the HttpApi
            // class library to take a Microsoft.AspNetCore.App framework
            // reference) while still scoping the 5/hour budget exclusively
            // to the password-reset surface.
            options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(
                httpContext =>
                {
                    if (IsPasswordResetPath(httpContext))
                    {
                        var key = ResolvePasswordResetPartitionKey(httpContext);
                        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: $"pwd-reset:{key}",
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
                    return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("non-rate-limited");
                });
        });
    }

    /// <summary>Policy name used by the password-reset endpoints.</summary>
    public const string PasswordResetRateLimitPolicy = "password-reset-by-email";

    /// <summary>Path prefix matched by the password-reset rate limiter.</summary>
    public const string PasswordResetPathPrefix = "/api/public/external-account";

    /// <summary>Phase 14b: path prefix matched by the document-upload-by-code limiter.</summary>
    public const string DocumentUploadByCodePathPrefix = "/api/public/appointment-documents";

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
    /// Resolves the partition key for the password-reset rate limiter.
    /// Precedence: optional <c>?email=</c> query (for tests) -> JWT
    /// <c>sub</c> claim (caller already authenticated) -> client IP.
    /// Falls back to <c>"global"</c> when none resolves so the policy
    /// always has a deterministic key.
    /// Internal-static so unit tests can verify edge cases without
    /// standing up the middleware pipeline.
    /// </summary>
    internal static string ResolvePasswordResetPartitionKey(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
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
        if (!hostingEnvironment.IsDevelopment())
        {
            var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]!);
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
