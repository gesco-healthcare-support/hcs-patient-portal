using System.ComponentModel.DataAnnotations;
using Volo.Abp.Identity;
using Volo.Abp.ObjectExtending;
using Volo.Abp.Threading;

namespace HealthcareSupport.CaseEvaluation;

/// <summary>
/// Registers ABP module-extension properties on framework entities.
///
/// Phase 2.4 (2026-05-01) extends <c>IdentityUser</c> with four properties
/// that OLD carried as first-class columns on its custom <c>User</c> table:
/// <list type="bullet">
///   <item><c>FirmName</c> -- attorney's law-firm name (Applicant or Defense Attorney roles only).</item>
///   <item><c>FirmEmail</c> -- attorney's law-firm contact email (attorney roles only).</item>
///   <item><c>IsExternalUser</c> -- true for the four external roles (Patient / Adjuster / Applicant Attorney / Defense Attorney). Replaces OLD's <c>RoleUserTypes</c> bridge table -- post-login routing and feature visibility branches on this flag.</item>
///   <item><c>IsAccessor</c> -- true when the row was auto-created via <c>AppointmentAccessorManager.CreateAccountAsync</c> for an email that didn't already exist; gates the "first-login finalize-profile" prompt.</item>
/// </list>
/// All four are stored in the <c>AbpUserExtraProperties</c> JSON column --
/// no schema migration required.
/// Cite: ABP Object-Extensions doc -- https://docs.abp.io/en/abp/latest/Object-Extensions
/// </summary>
public static class CaseEvaluationModuleExtensionConfigurator
{
    private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();

    public const string FirmNamePropertyName = "FirmName";
    public const string FirmEmailPropertyName = "FirmEmail";
    public const string IsExternalUserPropertyName = "IsExternalUser";
    public const string IsAccessorPropertyName = "IsAccessor";

    public const int FirmNameMaxLength = 256;
    public const int FirmEmailMaxLength = 256;

    public static void Configure()
    {
        OneTimeRunner.Run(ConfigureIdentityUserExtensions);
    }

    private static void ConfigureIdentityUserExtensions()
    {
        ObjectExtensionManager.Instance.Modules()
            .ConfigureIdentity(identity =>
            {
                identity.ConfigureUser(user =>
                {
                    user.AddOrUpdateProperty<string>(
                        FirmNamePropertyName,
                        property =>
                        {
                            property.Attributes.Add(new StringLengthAttribute(FirmNameMaxLength));
                        });

                    user.AddOrUpdateProperty<string>(
                        FirmEmailPropertyName,
                        property =>
                        {
                            property.Attributes.Add(new StringLengthAttribute(FirmEmailMaxLength));
                            property.Attributes.Add(new EmailAddressAttribute());
                        });

                    user.AddOrUpdateProperty<bool>(
                        IsExternalUserPropertyName,
                        property =>
                        {
                            property.DefaultValue = false;
                        });

                    user.AddOrUpdateProperty<bool>(
                        IsAccessorPropertyName,
                        property =>
                        {
                            property.DefaultValue = false;
                        });
                });
            });
    }
}
