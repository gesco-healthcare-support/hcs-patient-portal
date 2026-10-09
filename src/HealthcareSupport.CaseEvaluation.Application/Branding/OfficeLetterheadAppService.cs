using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// 2026-10-09 (walkthrough Q5) -- the in-office editor for the packet letterhead. Separate from
/// <see cref="BrandingAppService"/> because that file is already at the size ceiling and the two
/// share nothing but the row: branding is read anonymously by subdomain at boot, the
/// letterhead only by an authenticated office user and the packet job.
///
/// <para>Office scope only (see <see cref="IOfficeLetterheadAppService"/>). The row lives in the
/// HOST database, so writes run under <c>CurrentTenant.Change(null)</c> against the office id
/// captured BEFORE the switch -- inside the block <c>CurrentTenant.Id</c> is null.</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Branding.Default)]
public class OfficeLetterheadAppService : CaseEvaluationAppService, IOfficeLetterheadAppService
{
    private readonly OfficeLetterheadResolver _resolver;
    private readonly IRepository<OfficeBranding, Guid> _brandingRepository;

    public OfficeLetterheadAppService(
        OfficeLetterheadResolver resolver,
        IRepository<OfficeBranding, Guid> brandingRepository)
    {
        _resolver = resolver;
        _brandingRepository = brandingRepository;
    }

    public virtual async Task<OfficeLetterheadDto> GetAsync()
    {
        EnsureOffice();
        return ToDto(await _resolver.LoadSourceAsync());
    }

    [Authorize(CaseEvaluationPermissions.Branding.Edit)]
    public virtual async Task<OfficeLetterheadDto> UpdateAsync(UpdateOfficeLetterheadInput input)
    {
        Check.NotNull(input, nameof(input));
        var officeId = EnsureOffice();

        using (CurrentTenant.Change(null))
        {
            var branding = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == officeId)
                ?? await _brandingRepository.InsertAsync(
                    new OfficeBranding(GuidGenerator.Create(), officeId), autoSave: true);
            branding.SetLetterhead(ToValues(input));
            await _brandingRepository.UpdateAsync(branding, autoSave: true);
        }

        return ToDto(await _resolver.LoadSourceAsync());
    }

    private Guid EnsureOffice()
    {
        return CurrentTenant.Id ?? throw new UserFriendlyException(L["Branding:LetterheadNeedsOffice"]);
    }

    private static OfficeLetterheadDto ToDto(OfficeLetterheadSource source)
    {
        var stored = source.Branding?.GetLetterhead() ?? new OfficeLetterheadValues();
        var displayName = source.Branding?.DisplayName;
        var defaults = OfficeLetterhead.Compose(null, displayName, source.DoctorFirstName, source.DoctorLastName);
        // What a blank heading / practice name falls back to is the physician AS IT WILL PRINT,
        // i.e. the stored physician name when the office set one, not the derived default.
        var effective = OfficeLetterhead.Compose(stored, displayName, source.DoctorFirstName, source.DoctorLastName);

        return new OfficeLetterheadDto
        {
            LetterheadName = stored.LetterheadName,
            LetterheadTagline = stored.LetterheadTagline,
            PhysicianName = stored.PhysicianName,
            PracticeName = stored.PracticeName,
            MailingStreet = stored.MailingStreet,
            MailingCity = stored.MailingCity,
            MailingState = stored.MailingState,
            MailingZip = stored.MailingZip,
            Phone = stored.Phone,
            Fax = stored.Fax,
            RecordsDeliveryAddress = stored.RecordsDeliveryAddress,
            RecordsReleaseAddress = stored.RecordsReleaseAddress,
            MissedAppointmentFee = stored.MissedAppointmentFee,
            DefaultPhysicianName = defaults.PhysicianName,
            DefaultLetterheadName = effective.PhysicianName,
            DefaultPracticeName = string.IsNullOrWhiteSpace(displayName) ? effective.PhysicianName : displayName.Trim(),
        };
    }

    private static OfficeLetterheadValues ToValues(UpdateOfficeLetterheadInput input)
    {
        return new OfficeLetterheadValues
        {
            LetterheadName = input.LetterheadName,
            LetterheadTagline = input.LetterheadTagline,
            PhysicianName = input.PhysicianName,
            PracticeName = input.PracticeName,
            MailingStreet = input.MailingStreet,
            MailingCity = input.MailingCity,
            MailingState = input.MailingState,
            MailingZip = input.MailingZip,
            Phone = input.Phone,
            Fax = input.Fax,
            RecordsDeliveryAddress = input.RecordsDeliveryAddress,
            RecordsReleaseAddress = input.RecordsReleaseAddress,
            MissedAppointmentFee = input.MissedAppointmentFee,
        };
    }
}
