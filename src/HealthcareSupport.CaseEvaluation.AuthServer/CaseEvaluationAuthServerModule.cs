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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Localization;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.HealthChecks;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
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
using Volo.Abp.Account.Public.Web.ExternalProviders;
using Volo.Abp.Account.Public.Web.Impersonation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.Twitter;
using Volo.Saas.Host;
using Volo.Abp.OpenIddict;
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
    typeof(CaseEvaluationEntityFrameworkCoreModule)
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
        PreConfigure<AbpOpenIddictWildcardDomainOptions>(options =>
        {
            options.EnableWildcardDomainSupport = true;
            // Wildcard formats are matched against incoming redirect_uri values.
            // Each registered RootUrl below is rewritten {0} -> tenant-slug at runtime.
            options.WildcardDomainsFormat.Add("http://{0}.localhost:4200");
            options.WildcardDomainsFormat.Add("http://{0}.localhost:44368");
            options.WildcardDomainsFormat.Add("http://{0}.localhost:44327");
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
            options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
            options.Applications["Angular"].Urls[AccountUrlNames.EmailConfirmation] = "account/email-confirmation";
        });

        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = false;
        });

        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "CaseEvaluation:";
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

        context.Services.AddAuthentication()
        .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
        {
            options.ClaimActions.MapJsonKey(AbpClaimTypes.Picture, "picture");
        })
        .WithDynamicOptions<GoogleOptions, GoogleHandler>(
            GoogleDefaults.AuthenticationScheme,
            options =>
            {
                options.WithProperty(x => x.ClientId);
                options.WithProperty(x => x.ClientSecret, isSecret: true);
            }
        )
        .AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, options =>
        {
            //Personal Microsoft accounts as an example.
            options.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
            options.TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";

            options.ClaimActions.MapCustomJson("picture", _ => "https://graph.microsoft.com/v1.0/me/photo/$value");
            options.SaveTokens = true;
        })
        .WithDynamicOptions<MicrosoftAccountOptions, MicrosoftAccountHandler>(
            MicrosoftAccountDefaults.AuthenticationScheme,
            options =>
            {
                options.WithProperty(x => x.ClientId);
                options.WithProperty(x => x.ClientSecret, isSecret: true);
            }
        )
        .AddTwitter(TwitterDefaults.AuthenticationScheme, options =>
        {
            options.ClaimActions.MapJsonKey(AbpClaimTypes.Picture, "profile_image_url_https");
            options.RetrieveUserDetails = true;
        })
        .WithDynamicOptions<TwitterOptions, TwitterHandler>(
            TwitterDefaults.AuthenticationScheme,
            options =>
            {
                options.WithProperty(x => x.ConsumerKey);
                options.WithProperty(x => x.ConsumerSecret, isSecret: true);
            }
        );

        context.Services.Configure<AbpAccountOptions>(options =>
        {
            options.TenantAdminUserName = "admin";
            options.ImpersonationTenantPermission = SaasHostPermissions.Tenants.Impersonation;
            options.ImpersonationUserPermission = IdentityPermissions.Users.Impersonation;
        });

        Configure<LeptonXThemeOptions>(options =>
        {
            options.DefaultStyle = LeptonXStyleNames.System;
        });

        Configure<AbpSecurityHeadersOptions>(options =>
        {
            options.Headers["X-Frame-Options"] = "DENY";
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
            options.AddDomainTenantResolver("{0}.localhost");
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
