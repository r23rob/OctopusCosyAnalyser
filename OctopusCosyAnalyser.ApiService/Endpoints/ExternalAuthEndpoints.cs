using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class ExternalAuthEndpoints
{
    private const string DefaultLanding = "/heatpump";

    public static void MapExternalAuthEndpoints(this WebApplication app)
    {
        // Step 1: SPA navigates here. We issue a Challenge to Google, which redirects
        // the browser to Google's consent screen. After consent, Google redirects to
        // CallbackPath (/api/auth/signin-google), which the Google handler then routes
        // back to RedirectUri (our /callback below).
        app.MapGet("/api/auth/external/google", (HttpContext ctx, string? returnUrl) =>
        {
            var safeReturn = SanitizeReturnUrl(returnUrl);
            var callback = $"/api/auth/external/google/callback?returnUrl={Uri.EscapeDataString(safeReturn)}";
            var properties = new AuthenticationProperties { RedirectUri = callback };
            return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
        }).AllowAnonymous();

        // Step 2: Google handler has signed the user into the External cookie. Now we
        // either match an existing user, link Google to a user with the same email, or
        // create a fresh user. Either way, we sign in to the Application cookie and
        // 302 the browser back to the SPA.
        app.MapGet("/api/auth/external/google/callback", async (
            HttpContext ctx,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            string? returnUrl) =>
        {
            var safeReturn = SanitizeReturnUrl(returnUrl);

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info is null)
            {
                return Results.Redirect("/login?error=external");
            }

            // Already linked? Sign in directly.
            var signInResult = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: true,
                bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                return Results.Redirect(safeReturn);
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return Results.Redirect("/login?error=external_no_email");
            }

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                };
                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return Results.Redirect("/login?error=external_create");
                }
            }

            // Link the Google identity to the existing/new user. Safe because Google
            // verifies the email — this matches the auto-link decision in the plan.
            var addLoginResult = await userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                return Results.Redirect("/login?error=external_link");
            }

            await signInManager.SignInAsync(user, isPersistent: true);

            // Clear the external cookie now that we've upgraded to the application cookie.
            await ctx.SignOutAsync(IdentityConstants.ExternalScheme);

            return Results.Redirect(safeReturn);
        }).AllowAnonymous();
    }

    // Mirrors the SPA's sanitizeRedirect in octopus-cosy-web/src/routes/login.tsx:
    // only same-origin relative paths starting with a single `/` are allowed.
    private static string SanitizeReturnUrl(string? target)
    {
        if (string.IsNullOrEmpty(target)) return DefaultLanding;
        if (!target.StartsWith('/')) return DefaultLanding;
        if (target.StartsWith("//")) return DefaultLanding;
        return target;
    }
}
