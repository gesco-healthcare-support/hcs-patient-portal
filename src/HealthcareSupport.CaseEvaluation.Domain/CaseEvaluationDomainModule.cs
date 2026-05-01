using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HealthcareSupport.CaseEvaluation.Localization;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using System;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.BlobStoring.Database;
using Volo.Abp.Caching;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Commercial.SuiteTemplates;
using Volo.Abp.LanguageManagement;
using Volo.FileManagement;
using Volo.Abp.TextTemplateManagement;
using Volo.Saas;
using Volo.Abp.Gdpr;

namespace HealthcareSupport.CaseEvaluation;

[DependsOn(
    typeof(CaseEvaluationDomainSharedModule),
    typeof(AbpAuditLoggingDomainModule),
    typeof(AbpCachingModule),
    typeof(AbpBackgroundJobsDomainModule),
    typeof(AbpFeatureManagementDomainModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementDomainOpenIddictModule),
    typeof(AbpSettingManagementDomainModule),
    typeof(AbpEmailingModule),
    typeof(AbpIdentityProDomainModule),
    typeof(AbpOpenIddictProDomainModule),
    typeof(SaasDomainModule),
    typeof(TextTemplateManagementDomainModule),
    typeof(LanguageManagementDomainModule),
    typeof(FileManagementDomainModule),
    typeof(VoloAbpCommercialSuiteTemplatesModule),
    typeof(AbpGdprDomainModule),
    typeof(BlobStoringDatabaseDomainModule)
    )]
public class CaseEvaluationDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = MultiTenancyConsts.IsEnabled;
        });



        // Adrian (2026-04-30 / W-A-10): the previous `#if DEBUG` guard never fired in the
        // Docker dev stack because the API is published Release. Switch to a runtime env-var
        // check so Development containers swap in the no-op email sender and Hangfire stops
        // false-reporting Succeeded on auth-failed SMTP sends. Production builds (no
        // ASPNETCORE_ENVIRONMENT=Development) keep the real SMTP sender wired.
        //
        // S-5.7 (2026-04-30): the original W-A-10 swap was unconditional on
        // ASPNETCORE_ENVIRONMENT=Development, which silently no-op'd email
        // delivery in the Docker dev stack even after real Azure ACS SMTP
        // credentials landed in appsettings.secrets.json -- step 6.1's email
        // fan-out could never be verified end-to-end. Gate the swap on
        // placeholder-credential detection: only swap to NullEmailSender when
        // the SMTP UserName / Password is missing or still contains the
        // `REPLACE_ME_LOCALLY` sentinel. Once Adrian fills in real
        // credentials, the real MailKit sender stays wired in Development too,
        // and 6.1 can be exercised against the Hangfire job pipeline. This
        // preserves W-A-10's intent (no false-success Hangfire log spam when
        // credentials are placeholders) without blocking 6.1 verification.
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            var configuration = context.Services.GetConfiguration();
            var smtpUserName = configuration["Settings:Abp.Mailing.Smtp.UserName"];
            var smtpPassword = configuration["Settings:Abp.Mailing.Smtp.Password"];
            if (HasPlaceholderSmtpCredentials(smtpUserName, smtpPassword))
            {
                context.Services.Replace(
                    ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
            }
        }
    }

    private static bool HasPlaceholderSmtpCredentials(string? userName, string? password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return true;
        }
        // S-5.7: detect ANY placeholder marker by the `REPLACE_` prefix, not a
        // single literal sentinel. Two known placeholder strings exist in the
        // repo today and they differ between the two secrets locations:
        //   - `src/.../appsettings.json`            : "REPLACE_ME_LOCALLY"
        //   - `docker/appsettings.secrets.json`     : "REPLACE_WITH_ACS_USERNAME"
        //                                              "REPLACE_WITH_ACS_PASSWORD_CONNECTION_STRING"
        // Matching on the prefix means a future contributor can introduce a
        // new placeholder ("REPLACE_BEFORE_DEPLOY", etc.) without us having to
        // update this list. Real ACS credentials never start with `REPLACE_`,
        // so false positives are not a concern.
        return IsPlaceholder(userName) || IsPlaceholder(password);
    }

    private static bool IsPlaceholder(string value)
    {
        return value.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase);
    }
}
