using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Staff actions that (re)send the activate-your-account email: from a row of the identity Users
/// screen (office or host), and from a practice's row on the Practices screen for the office's
/// administrator. Both refuse an account that is already active or switched off, and both wait a
/// minute between two sends to the same place.
/// </summary>
public interface IAccountActivationAppService : IApplicationService
{
    /// <summary>
    /// Sends a fresh activation link to an account in the caller's own scope (the office the caller
    /// is in, or the host). Needs <c>AbpIdentity.Users.Update</c>; a caller without a privileged
    /// role cannot send to an account that has one, the same rule the Users screen applies to edits.
    /// </summary>
    Task ResendToUserAsync(Guid userId);

    /// <summary>
    /// Host only: sends a fresh activation link to an office's administrator, the account New
    /// Practice creates. Needs <c>Saas.Tenants.Create</c>, the permission that creates practices.
    /// </summary>
    Task ResendToOfficeAdministratorAsync(Guid officeId);
}
