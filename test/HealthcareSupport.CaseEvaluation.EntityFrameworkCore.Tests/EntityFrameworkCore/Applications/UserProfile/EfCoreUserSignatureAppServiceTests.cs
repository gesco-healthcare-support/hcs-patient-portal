using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Localization;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.UserProfile;

/// <summary>
/// Integration coverage for <see cref="UserSignatureAppService"/>, which had NONE before this
/// file: grepping the test tree for "UserSignature" returned only five lines of
/// <c>Authorization/authorization-surface.approved.txt</c>, and those pin the
/// <c>[Authorize]</c> DECLARATIONS, not one line of behaviour.
///
/// <para>THE SEAM. Internal staff upload a PNG/JPG signature image (max 1 MB) that the
/// packet-generation flow stamps onto the Patient Packet. The service keeps the image itself in a
/// blob container and keeps three pieces of metadata on the user's ABP extra properties:
/// <c>UserSignatureBlobName</c> (registered in <c>CaseEvaluationModuleExtensionConfigurator</c>)
/// plus <c>UserSignatureFileName</c> and <c>UserSignatureContentType</c> (NOT registered -- the
/// names are <c>private const</c> on the service, which is why the literals below are unavoidable
/// and are pulled out into local consts rather than retyped inline).</para>
///
/// <para>THE WALL HERE IS MINIO, NOT NOTIFICATION TEMPLATES.
/// <c>CaseEvaluationDomainModule.ConfigureBlobStoring</c> routes
/// <c>IBlobContainer&lt;UserSignaturesContainer&gt;</c> at MinIO on <c>minio:9000</c>, and no test
/// appsettings overrides <c>BlobStoring:Minio:*</c>, so the rig binds the REAL provider against a
/// hostname that does not resolve. <c>BrandingAppServiceTests</c> already records the same
/// constraint for office logos. The consequence is stated plainly rather than worked around: every
/// path that actually touches the blob store is out of reach, and every Fact below either throws
/// before the first blob call or relies on the blob call failing.</para>
///
/// <para>THE USEFUL INVERSION. Because the provider genuinely throws,
/// <see cref="UserSignatureAppService.DeleteAsync"/>'s bare <c>catch</c> is a LIVE path rather than
/// a branch nothing takes -- the entity is the source of truth and an orphan blob is a cleanup-job
/// concern. That is the single highest-value Fact in the file.</para>
///
/// <para>WHY EVERY FACT IMPERSONATES. The rig's ambient principal is
/// <c>FakeCurrentPrincipalAccessor</c>'s internal admin, whose user id
/// (<c>2e701e62-0953-4dd3-910b-dc6cc93ccb0d</c>) appears exactly once in the repo -- in that file --
/// and is seeded NOWHERE. Every caller-facing method here routes through
/// <c>GetCurrentUserOrFailAsync</c>, which calls <c>IdentityUserManager.GetByIdAsync</c>, so under
/// the default principal they all blow up on a user that does not exist. Every Fact therefore
/// pushes a principal of its own. <c>WithCurrentUser.Run</c> (not <c>RunWithEmail</c>) is the right
/// helper: the service reads <c>CurrentUser.Id</c> and never <c>CurrentUser.Email</c>.</para>
///
/// <para>WHY EVERY FIXTURE USER IS HOST-SCOPED. Seeded users are tenant-scoped and the rig's
/// ambient tenant is the host. A tenant-scoped fixture user would be invisible to
/// <c>FindByIdAsync</c>/<c>GetByIdAsync</c> here, and the two "returns null" Facts would then pass
/// for the wrong reason -- user-not-found rather than no-signature-on-file. Each user is created
/// with <c>tenantId: null</c> and a per-Fact token, because the rig is shared and accumulates.</para>
///
/// <para>TRAPS A AND B DO NOT BITE HERE, and that is worth stating rather than leaving to
/// inference. There is no <c>IEventBus.PublishAsync</c> anywhere in this service, so there is no
/// unit-of-work-completion timing to hoist around; and no assertion below claims an authorization
/// REFUSAL, which <c>AddAlwaysAllowAuthorization()</c> would make unfailable.</para>
///
/// <para>WHAT THESE FACTS DO NOT PIN.
/// <list type="bullet">
///   <item>The upload persist path (<c>SaveAsync</c> onward), the download stream path
///   (<c>GetAsync</c>), and the bytes-returning path of <c>GetBytesByUserIdAsync</c>
///   (<c>GetAllBytesOrNullAsync</c>) -- all three need a reachable blob provider.</item>
///   <item><c>NormalizeExtension</c> and <c>GuessContentType</c>. Both are <c>private</c>, so
///   neither direction of <c>InternalsVisibleTo</c> reaches them, and both call sites sit AFTER the
///   blob save. Untestable from any test project, not merely untested.</item>
///   <item>The <c>content == null</c> half of the <c>content == null || fileSize &lt;= 0</c> guard.
///   <c>Stream</c> is not primitive-extended, so ABP's <c>MethodInvocationValidator</c> rejects a
///   null non-optional reference parameter before the method body runs. A Fact for it would assert
///   the interceptor while appearing to assert the service. The <c>fileSize &lt;= 0</c> half is
///   covered.</item>
///   <item>The <c>[Authorize]</c> attributes themselves -- already pinned as declarations by
///   <c>authorization-surface.approved.txt</c>, and unfailable as behaviour under always-allow.</item>
/// </list></para>
///
/// <para>TWO OBSERVATIONS ABOUT UploadAsync, KEPT AS NOTES RATHER THAN TESTS. (1) The magic-byte
/// sniffer throws RAW localization keys where every other throw in the service uses <c>L[...]</c>.
/// (2) A non-seekable stream bypasses content validation entirely. NEITHER IS PINNED BY A TEST and
/// neither can be while the validator blocks the method -- see the UploadAsync section below for the
/// measured reason. They are recorded here so the observations outlive the tests that carried them.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreUserSignatureAppServiceTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    /// <summary>
    /// Mirrors <c>UserSignatureAppService.UserSignatureFileNamePropertyName</c>, which is
    /// <c>private const</c> and therefore unreachable from any test project. A literal is the only
    /// option; naming it once here keeps the two Facts that assert suppression from drifting apart
    /// from the two that assert persistence.
    /// </summary>
    private const string FileNameProperty = "UserSignatureFileName";

    /// <summary>Mirrors the service's private <c>UserSignatureContentTypePropertyName</c>.</summary>
    private const string ContentTypeProperty = "UserSignatureContentType";

    /// <summary>The registered extension property. This one IS public, so it is referenced properly.</summary>
    private const string BlobNameProperty =
        CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName;

    private readonly IUserSignatureAppService _userSignatures;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly IStringLocalizer<CaseEvaluationResource> _localizer;

    public EfCoreUserSignatureAppServiceTests()
    {
        _userSignatures = GetRequiredService<IUserSignatureAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _localizer = GetRequiredService<IStringLocalizer<CaseEvaluationResource>>();
    }

    // ------------------------------------------------------------------------
    // Fixture helpers.
    // ------------------------------------------------------------------------

    /// <summary>
    /// Resolves an expected message through the SAME resource the service's <c>L</c> uses.
    ///
    /// <para>This is deliberate and it is NOT the tautology it looks like. Asserting the English
    /// sentence would pin the translation, which no Fact here cares about and which a copy edit
    /// would break for no reason. What these Facts need to discriminate is WHICH of the five
    /// localized guards fired, and the key is exactly that. Culture-independent as a bonus.</para>
    ///
    /// <para>THE RESOLUTION CHECK IS LOAD-BEARING, and it is here because the failure mode is
    /// silent in the worst possible way: <c>IStringLocalizer</c>'s miss-fallback is to return THE
    /// KEY ITSELF, and the key is precisely the OTHER value under test -- the magic-byte sniffer
    /// throws raw keys where the guards throw localized sentences. So an unresolvable key would
    /// quietly collapse the two into one string, and the four Facts that exist to tell them apart
    /// would pass no matter which refusal fired. A sentinel that is also a legitimate answer cannot
    /// report failure, so the miss is turned into a loud one here instead.</para>
    /// </summary>
    private string Localized(string key)
    {
        var localized = _localizer[key];

        localized.ResourceNotFound.ShouldBeFalse(
            $"'{key}' did not resolve, so this helper is about to return the key itself -- which is "
            + "exactly the string the raw-key Facts assert. Every message assertion in this file "
            + "would stop discriminating. Check en.json.");
        localized.Value.ShouldNotBe(
            key,
            "The resolved message is identical to the key, which defeats the whole point of "
            + "asserting on it.");

        return localized.Value;
    }

    /// <summary>
    /// Creates a HOST-scoped user carrying whatever signature metadata the Fact needs.
    ///
    /// <para>Properties are set BEFORE <c>CreateAsync</c> so they are part of the insert; they live
    /// in the existing <c>AbpUsers.ExtraProperties</c> JSON column, which is why no migration and no
    /// mapped column is involved. Every value passed in by a caller below is deliberately NOT
    /// date-parsable: ABP's deserializer infers types from JSON tokens and turns a date-looking
    /// string into a <c>DateTime</c>, which would make <c>GetProperty&lt;string&gt;</c> misbehave on
    /// reload for reasons that have nothing to do with this service.</para>
    ///
    /// <para>The <c>Succeeded</c> assertion is load-bearing: without it a rejected create (duplicate
    /// username, password policy) would leave every later assertion running against a user that does
    /// not exist, and the "returns null" Facts would pass for entirely the wrong reason.</para>
    /// </summary>
    private async Task<Guid> SeedSignatureUserAsync(
        string token,
        string? blobName = null,
        string? fileName = null,
        string? contentType = null)
    {
        var userId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var user = new Volo.Abp.Identity.IdentityUser(
                userId,
                $"TEST-sig-{token}",
                $"TEST-sig-{token}@test.local",
                tenantId: null);

            if (blobName != null)
            {
                user.SetProperty(BlobNameProperty, blobName);
            }
            if (fileName != null)
            {
                user.SetProperty(FileNameProperty, fileName);
            }
            if (contentType != null)
            {
                user.SetProperty(ContentTypeProperty, contentType);
            }

            var created = await _userManager.CreateAsync(user, IdentityUsersTestData.SeedPassword);
            created.Succeeded.ShouldBeTrue(
                "FIXTURE PRECONDITION FAILED: the signature user was not created, so everything "
                + "asserted afterwards would be asserting against a user that does not exist: "
                + string.Join("; ", created.Errors.Select(e => e.Description)));
        });

        return userId;
    }

    /// <summary>
    /// Reloads the three signature properties in a FRESH unit of work, reading each through the
    /// non-generic <c>GetProperty(string)</c> and coercing with <c>ToString()</c>.
    ///
    /// <para>Deliberately not <c>GetProperty&lt;string&gt;</c>. These Facts are about what the
    /// SERVICE wrote, and ABP's typed read is a known trip hazard after a JSON round trip
    /// (<c>ExtraPropertyConverters</c> exists precisely because of it). <c>ToString()</c> on a
    /// <c>JsonElement</c> of kind String yields the string content, so this helper reports the same
    /// answer either way and cannot fail for a reason unrelated to the behaviour under test.</para>
    /// </summary>
    private async Task<SignatureProperties> ReadRawSignaturePropertiesAsync(Guid userId)
    {
        return await WithUnitOfWorkAsync<SignatureProperties>(async () =>
        {
            var user = await _userManager.GetByIdAsync(userId);
            user.ShouldNotBeNull();

            return new SignatureProperties(
                user!.GetProperty(BlobNameProperty)?.ToString(),
                user.GetProperty(FileNameProperty)?.ToString(),
                user.GetProperty(ContentTypeProperty)?.ToString());
        });
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];



    private sealed class SignatureProperties
    {
        public SignatureProperties(string? blobName, string? fileName, string? contentType)
        {
            BlobName = blobName;
            FileName = fileName;
            ContentType = contentType;
        }

        public string? BlobName { get; }

        public string? FileName { get; }

        public string? ContentType { get; }
    }


    // ------------------------------------------------------------------------
    // GetInfoAsync.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetInfoAsync_WithNoUserIdOnThePrincipal_IsRefused()
    {
        // PINS THE SERVICE'S OWN GUARD, NOT THE [Authorize] ATTRIBUTE. Those are inert here --
        // AddAlwaysAllowAuthorization() runs in CaseEvaluationTestBaseModule, so nothing the
        // attribute would refuse can be observed. What CAN be observed is
        // GetCurrentUserOrFailAsync's `if (userId == null) throw new AbpAuthorizationException()`,
        // which is ordinary service code that happens to throw an authorization type.
        //
        // WithCurrentUser.Run is deliberately NOT used: it always emits a UserId claim, so it
        // cannot produce the state this guard exists for. An empty ClaimsIdentity can.
        using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(
                async () => await _userSignatures.GetInfoAsync(),
                "A principal carrying no user id must be refused. Without this guard the very next "
                + "line dereferences userId.Value and the caller gets an InvalidOperationException "
                + "instead of an authorization failure.");
        }
    }

    [Fact]
    public async Task GetInfoAsync_ForAUserWithNoSignature_ReturnsANonNullDtoSayingSo()
    {
        // The frontend branches on HasSignature to decide whether to request the thumbnail at all,
        // so "no signature" must be a populated DTO rather than null or a throw. Flipping the
        // hasSignature initialiser fails this; so does returning null.
        var userId = await SeedSignatureUserAsync(NewToken());

        using (WithCurrentUser.Run(_principalAccessor, userId))
        {
            var info = await _userSignatures.GetInfoAsync();

            info.ShouldNotBeNull();
            info.HasSignature.ShouldBeFalse();
            info.FileName.ShouldBeNull();
            info.ContentType.ShouldBeNull();
        }
    }

    [Fact]
    public async Task GetInfoAsync_ForAUserWithASignature_ReturnsTheStoredFileNameAndContentType()
    {
        // Read in a DIFFERENT unit of work from the one that wrote, so the metadata makes the real
        // round trip through the ExtraProperties JSON column rather than being handed back off a
        // tracked entity. Both values are asserted because a mutation that swaps the two property
        // names at BuildInfo's FileName/ContentType lines survives either assertion alone.
        var token = NewToken();
        var userId = await SeedSignatureUserAsync(
            token,
            blobName: $"TEST-sig-{token}.png",
            fileName: "TEST-my-signature.png",
            contentType: "image/png");

        using (WithCurrentUser.Run(_principalAccessor, userId))
        {
            var info = await _userSignatures.GetInfoAsync();

            info.HasSignature.ShouldBeTrue();
            info.FileName.ShouldBe("TEST-my-signature.png");
            info.ContentType.ShouldBe("image/png");
        }
    }

    [Fact]
    public async Task GetInfoAsync_WhenTheBlobNameIsWhitespace_SuppressesStaleFileNameAndContentType()
    {
        // A NEGATIVE GUARANTEE, SO THE FIXTURE IS SEEDED WITH WHAT THE CODE MUST SUPPRESS.
        // BuildInfo does not merely omit metadata it lacks -- it actively refuses to surface
        // metadata it HAS when the blob key is blank. Seeding fileName and contentType here is
        // load-bearing, not setup noise: against a user with no metadata at all, both ternaries
        // could be deleted and this Fact would still pass.
        var userId = await SeedSignatureUserAsync(
            NewToken(),
            blobName: "   ",
            fileName: "TEST-stale.png",
            contentType: "image/png");

        using (WithCurrentUser.Run(_principalAccessor, userId))
        {
            var info = await _userSignatures.GetInfoAsync();

            info.HasSignature.ShouldBeFalse(
                "A whitespace-only blob key is not a signature. IsNullOrWhiteSpace is the check, "
                + "not IsNullOrEmpty.");
            info.FileName.ShouldBeNull(
                "Stale metadata must not leak once the blob key is gone, or the profile page "
                + "renders a filename for an image it cannot fetch.");
            info.ContentType.ShouldBeNull();
        }
    }

    // ------------------------------------------------------------------------
    // UploadAsync -- NOT COVERED, AND THE REASON IS THE WHOLE FINDING.
    //
    // Eleven Facts for the upload validation ladder were written here and then DELETED on
    // 2026-09-17, because none of them could reach the code they named.
    //
    // MEASURED, by printing the exception chain rather than inferring it:
    //
    //     [0] System.Reflection.TargetInvocationException
    //         Property accessor 'ReadTimeout' on object 'System.IO.MemoryStream' threw:
    //         'Timeouts are not supported on this stream.'
    //     [1] System.InvalidOperationException
    //
    // ABP's method-invocation validator reflects over the `Stream` parameter's PROPERTIES before the
    // method body runs, and `MemoryStream.ReadTimeout` throws by design. So every guard at the top of
    // UploadAsync -- filename, size, extension, magic bytes -- is unreachable through the app-service
    // interface when the argument is a MemoryStream. The guards never execute.
    //
    // This is the "check what runs FIRST" rule with a new mechanism. Two earlier theories were both
    // wrong and worth recording so they are not re-proposed: it is NOT the MinIO blob provider (the
    // routing is real, but these calls die before any blob access), and it is NOT exception wrapping
    // around a guard that did fire.
    //
    // THREE OF THE ELEVEN WERE PASSING, AND THAT WAS THE WORSE PROBLEM. They captured
    // `Should.ThrowAsync<Exception>` and then asserted NEGATIVES -- `(ex is NotSupportedException)
    // .ShouldBeFalse()` and `(ex is UserFriendlyException).ShouldBeFalse()`. A negative assertion is
    // satisfied by ANYTHING that is not the named type, so the validator's TargetInvocationException
    // satisfied both, and those Facts would have stayed green with the code under test deleted. A
    // green test passing on an unrelated failure is harder to notice than a red one.
    //
    // DO NOT "FIX" THIS WITH A CUSTOM STREAM that returns a ReadTimeout value. It was considered and
    // explicitly rejected: it is test-infrastructure surgery to reach guards, and it would still need
    // separate proof that the guards then actually fire.
    //
    // Production most likely does not hit this at all -- the controller binds [FromForm] IFormFile
    // and calls OpenReadStream(), which is buffered and seekable under default model binding. That is
    // unverified, and logged in docs/backlog.md as a test-rig constraint rather than a live defect.
    // ------------------------------------------------------------------------

    // ------------------------------------------------------------------------
    // DownloadAsync.
    //
    // The stream itself is unreachable (MinIO), so what is pinned is the refusal that must happen
    // BEFORE the provider is consulted.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task DownloadAsync_ForAUserWithNoSignature_IsRefusedAsNotFound()
    {
        var userId = await SeedSignatureUserAsync(NewToken());

        using (WithCurrentUser.Run(_principalAccessor, userId))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _userSignatures.DownloadAsync());

            ex.Message.ShouldBe(Localized("UserSignature:NotFound"));
        }
    }

    [Fact]
    public async Task DownloadAsync_WhenTheBlobNameIsWhitespace_IsRefusedWithoutConsultingTheBlobStore()
    {
        // ANOTHER NEGATIVE GUARANTEE WITH A DELIBERATELY NON-EMPTY FIXTURE. The filename and
        // content type are seeded because their presence is what makes the state realistic: a
        // half-cleared row, which is exactly when a blank key would otherwise be handed to the
        // provider. Loosening IsNullOrWhiteSpace to IsNullOrEmpty sends "   " to MinIO, and the
        // caller gets a storage error instead of "no signature uploaded yet".
        var userId = await SeedSignatureUserAsync(
            NewToken(),
            blobName: "   ",
            fileName: "TEST-stale.png",
            contentType: "image/png");

        using (WithCurrentUser.Run(_principalAccessor, userId))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _userSignatures.DownloadAsync());

            ex.Message.ShouldBe(
                Localized("UserSignature:NotFound"),
                "A blank blob key must be refused here, not forwarded to the blob provider.");
        }
    }

    // ------------------------------------------------------------------------
    // DeleteAsync -- the one place the unreachable blob provider is an ASSET.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_ForAUserWithNoBlob_ReturnsEarlyAndLeavesStaleMetadataUntouched()
    {
        // THE FIXTURE IS THE POINT. The early return sits BEFORE the three SetProperty(null) calls,
        // so a user carrying orphaned filename/contentType but no blob key must come back with
        // those two still present. Seeded with a bare user this Fact could not fail: there would be
        // nothing left to leave untouched, and deleting the early return would look identical.
        var userId = await SeedSignatureUserAsync(
            NewToken(),
            blobName: null,
            fileName: "TEST-orphan.png",
            contentType: "image/png");

        using (WithCurrentUser.Run(_principalAccessor, userId))
        {
            await Should.NotThrowAsync(
                async () => await _userSignatures.DeleteAsync(),
                "Deleting a signature that is not there is a no-op, not an error.");
        }

        var after = await ReadRawSignaturePropertiesAsync(userId);

        after.BlobName.ShouldBeNull();
        after.FileName.ShouldBe(
            "TEST-orphan.png",
            "The early return must fire before the clear. If these properties are now null, the "
            + "return was removed and the method fell through to a blob delete on a null key.");
        after.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task DeleteAsync_WhenTheBlobProviderThrows_StillClearsAllThreeProperties()
    {
        // THE HIGHEST-VALUE FACT IN THIS FILE, and the one the MinIO wall makes possible rather than
        // blocks. The bare `catch` around the blob delete is a deliberate decision: the USER ROW is
        // the source of truth for whether a signature exists, and an orphaned object in the bucket
        // is a cleanup-job concern. Without it, a storage outage leaves a user permanently unable to
        // remove their signature.
        //
        // The provider genuinely fails here -- CaseEvaluationDomainModule points the container at
        // minio:9000 and no test configuration overrides it, so the hostname does not resolve. That
        // makes this the live path, not a branch simulated with a stub.
        //
        // Two independent mutations fail it: remove the try/catch and DeleteAsync throws; remove the
        // three SetProperty(null) calls or the UpdateAsync and the properties survive.
        var token = NewToken();
        var userId = await SeedSignatureUserAsync(
            token,
            blobName: $"TEST-sig-{token}.png",
            fileName: "TEST-my-signature.png",
            contentType: "image/png");

        var before = await ReadRawSignaturePropertiesAsync(userId);
        before.BlobName.ShouldBe(
            $"TEST-sig-{token}.png",
            "FIXTURE PRECONDITION FAILED: without a blob key the method returns early and this Fact "
            + "would be asserting the previous one's behaviour.");

        using (WithCurrentUser.Run(_principalAccessor, userId))
        {
            await Should.NotThrowAsync(
                async () => await _userSignatures.DeleteAsync(),
                "A blob-provider failure must stay inside the service. If this throws, an "
                + "unreachable object store means a user can never clear their signature.");
        }

        var after = await ReadRawSignaturePropertiesAsync(userId);

        after.BlobName.ShouldBeNull(
            "The blob key must be cleared even though the blob itself could not be deleted -- the "
            + "entity is the source of truth and the orphan is a cleanup-job concern.");
        after.FileName.ShouldBeNull();
        after.ContentType.ShouldBeNull();
    }

    // ------------------------------------------------------------------------
    // GetBytesByUserIdAsync -- the in-process accessor the packet resolver calls.
    //
    // Its contract is OLD's silent skip: return null rather than throw, because the packet flow
    // stamps the signature only when there is one and must not fail a whole packet over a missing
    // image.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetBytesByUserIdAsync_WithAnEmptyGuid_ReturnsNull()
    {
        // COVERAGE-ORIENTED, AND LABELLED AS SUCH RATHER THAN DRESSED UP. The `userId == Guid.Empty`
        // short-circuit cannot be discriminated from outside: delete it and FindByIdAsync is handed
        // the empty guid, finds no row, and the method returns null anyway. The only difference is
        // a database round trip. This Fact closes the line; it does not pin the rule.
        (await _userSignatures.GetBytesByUserIdAsync(Guid.Empty)).ShouldBeNull();
    }

    [Fact]
    public async Task GetBytesByUserIdAsync_ForAUserThatDoesNotExist_ReturnsNullInsteadOfThrowing()
    {
        // Unlike the caller-facing methods, this one uses FindByIdAsync (null on miss) rather than
        // GetByIdAsync (throws on miss), and then MAPS the miss to null. Remove the null check and
        // the next line dereferences it -- a NullReferenceException that would abort the entire
        // packet generation for a responsible user whose account was deleted.
        (await _userSignatures.GetBytesByUserIdAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    public async Task GetBytesByUserIdAsync_ForAUserWithNoSignature_ReturnsNullWithoutConsultingTheBlobStore()
    {
        // THE PREMISE IS ASSERTED FIRST, and that is not ceremony. A user the rig cannot see returns
        // null for a completely different reason -- user-not-found rather than no-signature -- and
        // the Fact above already covers that case. Without the premise check, a tenant-scoping
        // mistake in the fixture would turn this into a duplicate of that one while still reading
        // as coverage of the blank-blobName return.
        var userId = await SeedSignatureUserAsync(NewToken());

        await WithUnitOfWorkAsync(async () =>
        {
            (await _userManager.FindByIdAsync(userId.ToString())).ShouldNotBeNull(
                "FIXTURE PRECONDITION FAILED: the fixture user is not visible in the ambient "
                + "(host) tenant, so the null below would be proving nothing about signatures.");

            (await _userSignatures.GetBytesByUserIdAsync(userId)).ShouldBeNull(
                "A user with no blob key must return null without the provider being consulted. "
                + "Remove the blank-blobName return and a null key is handed to "
                + "GetAllBytesOrNullAsync, which reaches for an object store this rig -- and a "
                + "production outage -- cannot supply.");
        });
    }
}
