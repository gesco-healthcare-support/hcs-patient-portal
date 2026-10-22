using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// 2026-10-09 (walkthrough Q5) -- the in-office editor for the packet letterhead. Separate from
/// <see cref="BrandingAppService"/> because that file is already at the size ceiling and the two
/// share nothing but the row: branding is read anonymously by subdomain at boot, the
/// letterhead only by an authenticated office user and the packet job.
///
/// <para>Two scopes (see <see cref="IOfficeLetterheadAppService"/>): the CURRENT office, and one
/// office by id for the HOST branding page. The by-id pair refuses any caller inside an office --
/// Branding is a both-sides permission, so an office admin holds it, and an office-side caller
/// naming another office's id must not reach that office's row -- and refuses an id that names no
/// office. The row lives in the HOST database, so writes run under
/// <c>CurrentTenant.Change(null)</c> against the office id captured BEFORE the switch -- inside the
/// block <c>CurrentTenant.Id</c> is null.</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Branding.Default)]
public class OfficeLetterheadAppService : CaseEvaluationAppService, IOfficeLetterheadAppService
{
    private readonly OfficeLetterheadResolver _resolver;
    private readonly IRepository<OfficeBranding, Guid> _brandingRepository;
    private readonly IRepository<Tenant, Guid> _officeRepository;

    public OfficeLetterheadAppService(
        OfficeLetterheadResolver resolver,
        IRepository<OfficeBranding, Guid> brandingRepository,
        IRepository<Tenant, Guid> officeRepository)
    {
        _resolver = resolver;
        _brandingRepository = brandingRepository;
        _officeRepository = officeRepository;
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
        await SaveAsync(officeId, input);
        return ToDto(await _resolver.LoadSourceAsync());
    }

    public virtual async Task<OfficeLetterheadDto> GetForOfficeAsync(Guid officeId)
    {
        await EnsureHostTargetsAnOfficeAsync(officeId);
        return ToDto(await LoadSourceForAsync(officeId));
    }

    [Authorize(CaseEvaluationPermissions.Branding.Edit)]
    public virtual async Task<OfficeLetterheadDto> UpdateForOfficeAsync(Guid officeId, UpdateOfficeLetterheadInput input)
    {
        Check.NotNull(input, nameof(input));
        await EnsureHostTargetsAnOfficeAsync(officeId);
        await SaveAsync(officeId, input);
        return ToDto(await LoadSourceForAsync(officeId));
    }

    private Guid EnsureOffice()
    {
        return CurrentTenant.Id ?? throw new UserFriendlyException(L["Branding:LetterheadNeedsOffice"]);
    }

    private async Task EnsureHostTargetsAnOfficeAsync(Guid officeId)
    {
        if (CurrentTenant.Id != null)
        {
            throw new AbpAuthorizationException("Another office's letterhead can be read or changed only from the host.");
        }
        if (officeId == Guid.Empty || await _officeRepository.FindAsync(officeId) == null)
        {
            throw new EntityNotFoundException(typeof(Tenant), officeId);
        }
    }

    /// <summary>The source as that office's own packets will read it: its doctor row, its branding row.</summary>
    private async Task<OfficeLetterheadSource> LoadSourceForAsync(Guid officeId)
    {
        using (CurrentTenant.Change(officeId))
        {
            return await _resolver.LoadSourceAsync();
        }
    }

    private async Task SaveAsync(Guid officeId, UpdateOfficeLetterheadInput input)
    {
        using (CurrentTenant.Change(null))
        {
            var branding = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == officeId)
                ?? await _brandingRepository.InsertAsync(
                    new OfficeBranding(GuidGenerator.Create(), officeId), autoSave: true);
            branding.SetLetterhead(ToValues(input));
            await _brandingRepository.UpdateAsync(branding, autoSave: true);
        }
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
