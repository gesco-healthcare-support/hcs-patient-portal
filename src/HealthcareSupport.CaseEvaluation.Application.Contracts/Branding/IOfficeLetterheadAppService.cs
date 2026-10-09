using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Reads and edits the CURRENT office's packet letterhead -- what the generated packets print
/// as the office's identity. Office scope only: there is no host-wide letterhead, and a host
/// operator edits one by impersonating the office (the same model as the in-office branding
/// editor).
/// </summary>
public interface IOfficeLetterheadAppService : IApplicationService
{
    Task<OfficeLetterheadDto> GetAsync();

    Task<OfficeLetterheadDto> UpdateAsync(UpdateOfficeLetterheadInput input);
}
