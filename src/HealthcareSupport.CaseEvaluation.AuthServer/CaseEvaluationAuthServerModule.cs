using System;
using System.IO;
using System.Linq;
using Localization.Resources.AbpUi;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DistributedLocking;
using Volo.Abp.TextTemplateManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Localization;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.HealthChecks;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Server.OpenIddictServerEvents;
using HealthcareSupport.CaseEvaluation.OpenIddict;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.Studio;
using Volo.Abp.AspNetCore.Mvc.UI;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonX;
using Volo.Abp.AspNetCore.Security;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonX.Bundling;
using Volo.Abp.LeptonX.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Auditing;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Caching;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.UI;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.Account.Public.Web;
using Volo.Abp.Account.Public.Web.Impersonation;
using Volo.Saas.Host;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.ExtensionGrantTypes;
using Volo.Abp.OpenIddict.WildcardDomains;
using Volo.Abp.MultiTenancy;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Account.Localization;
using Volo.Abp.Security.Claims;
using Volo.Abp.Studio.Client.AspNetCore;

namespace HealthcareSupport.CaseEvaluation;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpDistributedLockingModule),
    typeof(SaasHostApplicationContractsModule),
    typeof(AbpAccountPublicWebOpenIddictModule),
    typeof(AbpAccountPublicHttpApiModule),
    typeof(AbpAccountPublicApplicationModule),
    typeof(AbpAccountPublicWebImpersonationModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpAspNetCoreMvcUiLeptonXThemeModule),
    typeof(CaseEvaluationEntityFrameworkCoreModule),
    // Phase 1.D follow-up (2026-05-08): DependsOn the Application module so
    // the AuthServer's DI container can resolve IExternalAccountAppService
    // (used by Pages/Account/ResendVerification.cshtml.cs to dispatch the
    // verification email through the same path as the API host).
    typeof(CaseEvaluationApplicationModule)
    )]
public class CaseEvaluationAuthServerModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("CaseEvaluation");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        // ADR-006 (2026-05-05) -- subdomain tenant routing.
        // Tells OpenIddict to accept redirect_uris and post_logout_redirect_uris
        // matching the wildcard pattern http://{slug}.localhost so a single
        // registered client (CaseEvaluation_App) covers every tenant subdomain.
        // The "{0}" token is filled with the resolved tenant slug at request time.
        // Per Volosoft Medium article cited in ADR-006 (sourced 2026-05-05).
        //
        // 2026-05-20 (Option A fix): the wildcard format list is now driven
        // by the `App:WildcardDomainsFormat` config section so parallel-
        // worktree stacks on offset ports (e.g. 4230/44398/44357) can
        // override the main stack's canonical ports without recompiling.
        // The docker-compose.yml passes the ports through as env vars; the
        // hardcoded list below remains as the dev fallback when the config
        // section is empty.
        PreConfigure<AbpOpenIddictWildcardDomainOptions>(options =>
        {
            options.EnableWildcardDomainSupport = true;
            var configuredFormats = configuration
                .GetSection("App:WildcardDomainsFormat")
                .Get<string[]>();
            if (configuredFormats != null && configuredFormats.Length > 0)
            {
                foreach (var format in configuredFormats)
                {
                    if (!string.IsNullOrWhiteSpace(format))
                    {
                        options.WildcardDomainsFormat.Add(format);
                    }
                }
            }
            else
            {
                // Dev fallback (running outside docker on canonical ports).
                options.WildcardDomainsFormat.Add("http://{0}.localhost:4200");
                options.WildcardDomainsFormat.Add("http://{0}.localhost:44368");
                options.WildcardDomainsFormat.Add("http://{0}.localhost:44327");
            }
        });

        // Tighten refresh-token rotation. OpenIddict 7.x defaults to a
        // 30-second RefreshTokenReuseLeeway window during which a
        // redeemed refresh token can still be reused, to tolerate
        // distributed clients firing concurrent refresh requests. That
        // same window also lets a stolen refresh token be replayed once
        // for ~30 s before OpenIddict's cascade-revocation kicks in.
        // Shortening the leeway to 2 s preserves the legitimate
        // concurrent-retry path (network blips during refresh
        // round-trips) while shrinking the stolen-token replay window
        // to a level only a near-real-time attacker can hit. Aligns
        // with RFC 6749 Section 10.4 intent.
        PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
        {
            serverBuilder.SetRefreshTokenReuseLeeway(TimeSpan.FromSeconds(2));

            // Single-session enforcement: shorten the access-token lifetime so a
            // revoked session's old device drops quickly. NEW validates
            // self-contained JWTs at the API (no token-store lookup), so the old
            // access token stays valid until it expires; 15 min bounds that
            // window (OpenIddict default was 1 hour).
            serverBuilder.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));

            // On a fresh interactive login (authorization_code grant), revoke the
            // user's prior refresh tokens so a second device's session ends at its
            // next silent refresh. Reverses the 2026-05-01 multi-session deviation.
            serverBuilder.AddEventHandler<HandleTokenRequestContext>(builder =>
                builder.UseScopedHandler<RevokePreviousSessionsHandler>());
        });

        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx", configuration["AuthServer:CertificatePassPhrase"]!);
                serverBuilder.SetIssuer(new Uri(configuration["AuthServer:Authority"]!));
            });
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        if (hostingEnvironment.IsProduction())
        {
            Configure<AbpStudioClientOptions>(options =>
            {
                options.IsLinkEnabled = false;
            });
        }

        if (!configuration.GetValue<bool>("App:DisablePII"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }

        if (!configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata"))
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                options.DisableTransportSecurityRequirement = true;
            });

            Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

        // Single-session enforcement: ensure the OpenIddict server event handler
        // (registered via UseScopedHandler in PreConfigureServices) resolves from DI.
        context.Services.AddScoped<RevokePreviousSessionsHandler>();

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<CaseEvaluationResource>()
                .AddBaseTypes(
                    typeof(AbpUiResource),
                    typeof(AccountResource)
                );

            // OLD-parity label overrides for stock ABP Razor pages. ABP's
            // .cshtml uses IStringLocalizer<AbpUiResource> /
            // IStringLocalizer<AccountResource> directly; our
            // CaseEvaluationResource inherits from those, so keys defined
            // there only win for derived-resource lookups (NOT for direct
            // base-resource lookups in stock Razor). Inject overrides into
            // the base resources themselves so e.g. AbpUi::Register
            // resolves to "Sign Up" and AbpAccount::AlreadyRegistered
            // resolves to "Already have an account?".
            options.Resources
                .Get<AbpUiResource>()
                .AddVirtualJson("/Localization/AbpUiOverride");

            options.Resources
                .Get<AccountResource>()
                .AddVirtualJson("/Localization/AccountOverride");
        });

        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );

            options.ScriptBundles.Configure(
                LeptonXThemeBundles.Scripts.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-scripts.js");
                }
            );
        });

        Configure<AbpAuditingOptions>(options =>
        {
            options.ApplicationName = "AuthServer";
        });

        if (hostingEnvironment.IsDevelopment())
        {
            // Hot-reload of Domain.Shared / Domain physical files is only
            // useful when the source tree is mounted next to the running
            // host (i.e. `dotnet run` from src/...AuthServer). In Docker the
            // source isn't copied/mounted, so ReplaceEmbeddedByPhysical
            // would replace the embedded fileset with an EMPTY directory --
            // nuking all localization JSON and surfacing literal keys
            // (`AppName`, `Menu:Home`, `Enum:BookingStatus.8`, etc.) on
            // every rendered page. Guard with Directory.Exists so the
            // embedded fileset survives in Docker.
            var sharedPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}HealthcareSupport.CaseEvaluation.Domain.Shared", Path.DirectorySeparatorChar));
            var domainPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}HealthcareSupport.CaseEvaluation.Domain", Path.DirectorySeparatorChar));
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
            });
        }

        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ?? Array.Empty<string>());
            options.Applications["Angular"].RootUrl = configuration["App:AngularUrl"];
            // 2026-05-18 -- confirmation + reset URLs are hosted by the
            // AuthServer Razor pages (custom overrides under Pages/Account/),
            // not the SPA. Repointed from the deleted SPA routes
            // (/account/email-confirmation, /account/reset-password). The
            // project's IAccountEmailer override builds URLs explicitly
            // via Notifications.AuthServerBaseUrl setting, so this
            // AppUrlOptions config is defensive: any future framework code
            // calling IAppUrlProvider for the "MVC" app gets a working URL.
            // See docs/plans/2026-05-18-fix-verification-email-url.md.
            options.Applications["MVC"].Urls[AccountUrlNames.PasswordReset] = "Account/ResetPassword";
            options.Applications["MVC"].Urls[AccountUrlNames.EmailConfirmation] = "Account/EmailConfirmation";
        });

        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = false;
        });

        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "CaseEvaluation:";
        });

        // 2026-06-09: do not re-save static text-template definitions to the
        // DB from this runtime host. The DbMigrator (runs to completion before
        // api/authserver start) already seeds AbpTextTemplateDefinitionRecords.
        // Unlike the permission/setting/feature savers, the text-template saver
        // is NOT guarded by the distributed lock, so api + authserver booting
        // together both INSERT and collide on the unique name index -- a
        // duplicate-key SqlException at startup (Abp.Account.EmailConfirmationCode).
        // Templates still resolve from the in-memory definition providers at
        // runtime, so disabling the host-side save is safe.
        Configure<TextTemplateManagementOptions>(options =>
        {
            options.SaveStaticTemplatesToDatabase = false;
        });

        if (!AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            var dataProtectionBuilder = context.Services.AddDataProtection().SetApplicationName("CaseEvaluation");

            // Persist DataProtection keys to Redis whenever a Redis connection is
            // configured, in BOTH dev and prod. Reason: AuthServer + HttpApi.Host
            // run as separate Docker containers (separate filesystems), so the
            // default key store is per-container. ABP-Identity tokens (e.g.
            // EmailConfirmation) generated by the API host fail validation
            // when the AuthServer's confirm-email endpoint tries to decrypt
            // them -- the request returns 403 with "Volo.Abp.Identity:InvalidToken".
            // Redis-backed shared keys + matching SetApplicationName above make
            // both processes interchangeable validators.
            var redisConfig = configuration["Redis:Configuration"];
            if (!string.IsNullOrWhiteSpace(redisConfig))
            {
                var redis = ConnectionMultiplexer.Connect(redisConfig);
                dataProtectionBuilder.PersistKeysToStackExchangeRedis(redis, "CaseEvaluation-Protection-Keys");
            }

            context.Services.AddSingleton<IDistributedLockProvider>(sp =>
            {
                var connection = ConnectionMultiplexer
                    .Connect(configuration["Redis:Configuration"]!);
                return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
            });
        }

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

        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });

        // External auth providers (Google / Microsoft / Twitter) removed
        // 2026-05-19. Their AddXxx + WithDynamicOptions blocks were wired
        // with per-tenant dynamic ClientId / Secret but never populated;
        // the buttons surfaced empty and risked break-on-tenant-enable
        // without testing. Twitter is now X with a changed OAuth flow
        // anyway. Reintroduce only after a deliberate per-tenant
        // configuration story lands. Audit finding D-13 (final-findings.md).

        context.Services.Configure<AbpAccountOptions>(options =>
        {
            options.TenantAdminUserName = "admin";
            options.ImpersonationTenantPermission = SaasHostPermissions.Tenants.Impersonation;
            options.ImpersonationUserPermission = IdentityPermissions.Users.Impersonation;
        });

        // Phase D (2026-06-25) -- replace the stock "Impersonation" grant with
        // HostIntakeImpersonationExtensionGrant so a host Intake operator can land
        // as their LIMITED per-office shadow Intake user (gated, deny-by-default).
        // Supervisor / IT Admin keep the stock switch-in-as-admin path (the grant
        // delegates to base when the caller holds Saas.Tenants.Impersonation). The
        // "Impersonation" grant_type is already registered by the stock module's
        // PreConfigure<OpenIddictServerBuilder>, so only the handler is swapped.
        Configure<AbpOpenIddictExtensionGrantsOptions>(options =>
        {
            options.Grants.Remove("Impersonation");
            options.Grants.Add("Impersonation", new HostIntakeImpersonationExtensionGrant());
        });

        // 2026-05-12 (Issue 1.5) — light is the default for first-time
        // visitors to AuthServer Razor pages (login / register / email
        // confirmation). Users who explicitly toggle to dark still keep
        // that choice via the LeptonX cookie.
        Configure<LeptonXThemeOptions>(options =>
        {
            options.DefaultStyle = LeptonXStyleNames.Light;
        });

        Configure<AbpSecurityHeadersOptions>(options =>
        {
            options.Headers["X-Frame-Options"] = "DENY";
        });

        // 2026-05-13 -- map RegistrationDuplicateEmail to HTTP 400.
        // Mirrors the same config in CaseEvaluationHttpApiHostModule.
        // The /api/public/external-signup/register endpoint is loaded
        // into BOTH the AuthServer host (port 44369) and the
        // HttpApi.Host (port 44328) -- the SPA hits the AuthServer
        // path during the register flow so BOTH host modules must
        // contribute the same status-code remap.
        //
        // 2026-05-15 -- mirror the six InternalUser:* codes too. The
        // /api/app/internal-users/* endpoints currently live on
        // HttpApi.Host only, but the controller registration would
        // be picked up by AuthServer too if/when the AppService
        // module is loaded there; keeping the status-code map in
        // sync avoids the BUG-003 class of "403 vs 400" inconsistency.
        Configure<Volo.Abp.AspNetCore.ExceptionHandling.AbpExceptionHttpStatusCodeOptions>(options =>
        {
            // BUG-012 Sub-bug 2 (2026-05-22): shared mappings extracted
            // into HealthcareSupport.CaseEvaluation.Exceptions.
            // CaseEvaluationExceptionStatusCodeMappings so the AuthServer
            // host and the HttpApi.Host stay in sync without duplicating
            // the option.Map(...) call list verbatim. Per BUG-003 both
            // hosts must contribute the same status-code remap.
            HealthcareSupport.CaseEvaluation.Exceptions.CaseEvaluationExceptionStatusCodeMappings
                .MapSharedRegistrationAndInternalUserCodes(options);
        });

        context.Services.AddCaseEvaluationAuthServerHealthChecks();

        ConfigureMultiTenancy();
    }

    /// <summary>
    /// ADR-006 (2026-05-05) -- subdomain tenant routing.
    ///
    /// Clears ABP's default tenant resolver chain (CurrentUser, QueryString,
    /// Route, Header, Cookie) and rebuilds it with only two contributors so
    /// the URL is the SOLE source of tenant identity:
    ///   1. CurrentUser  -- security default; must be first per ABP docs.
    ///   2. Domain       -- "{slug}.localhost" pattern reads tenant from Host header.
    ///
    /// Dropping QueryString + Cookie + Header + Route prevents a knowledgeable
    /// caller from sending ?__tenant=GUID and switching tenants from the URL
    /// bar. This is HIPAA-relevant: see ADR-006 Context section.
    /// </summary>
    private void ConfigureMultiTenancy()
    {
        Configure<AbpTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Clear();
            options.TenantResolvers.Add(new CurrentUserTenantResolveContributor());
            // ADR-007 (2026-05-11): HostAwareDomainTenantResolveContributor
            // replaces the stock DomainTenantResolveContributor so the
            // reserved subdomain "admin" maps to Host context instead of
            // 404. The stock contributor sets context.TenantIdOrName from
            // the host and ABP's MultiTenancyMiddleware throws 404 when
            // the slug is not a registered tenant -- which broke the
            // intended Host surface URL admin.localhost:44368.
            options.TenantResolvers.Add(
                new HostAwareDomainTenantResolveContributor("{0}.localhost"));
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {

        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        app.UseForwardedHeaders();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        app.UseCorrelationId();
        app.UseRouting();
        // Issue #107 (2026-05-13) -- the silent-refresh wiring was ripped
        // (broken on the @abp/ng.oauth interceptor; refresh-token rotation
        // covers the access_token-renewal use case without an iframe).
        // The path-scoped X-Frame-Options / CSP override that previously
        // lived here is gone with it; the global "X-Frame-Options: DENY"
        // now applies to every path, restoring clickjacking protection
        // across the whole AuthServer surface.
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        app.UseCors();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }
}
