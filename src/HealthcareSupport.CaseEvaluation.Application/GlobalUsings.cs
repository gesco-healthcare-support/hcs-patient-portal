// Disambiguates IdentityUser now that the project sees ASP.NET Core Identity (via the
// Microsoft.AspNetCore.App framework reference the stock-account route refusal needs for IHttpContextAccessor).
global using IdentityUser = Volo.Abp.Identity.IdentityUser;
