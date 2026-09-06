using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Localization;

namespace DrangohtGames.Web.Localization;

/// <summary>Point d'entrée du changement de langue.</summary>
public static class CultureEndpoints
{
    /// <summary>
    /// Enregistre <c>POST /culture</c>, qui mémorise la langue choisie dans un cookie puis
    /// renvoie le visiteur sur la page d'où il vient.
    /// </summary>
    public static IEndpointRouteBuilder MapCultureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/culture", async (HttpContext context) =>
        {
            // Le middleware antiforgery signale l'échec par cette caractéristique sans
            // interrompre la requête. Lire le formulaire sans l'avoir consultée lève, et le
            // visiteur récolterait une erreur serveur au lieu d'un refus net.
            if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false })
            {
                return Results.BadRequest();
            }

            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var culture = form["culture"].ToString();

            // Une culture arbitraire recopiée dans le cookie serait rejouée à chaque requête :
            // on n'accepte que celles que le site publie réellement.
            if (!SupportedCultures.IsSupported(culture))
            {
                return Results.BadRequest();
            }

            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(
                    new RequestCulture(new CultureInfo(culture))),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                });

            // Redirection restreinte aux chemins internes : un `redirectUri` absolu — ou
            // commençant par `//`, que le navigateur lit comme un hôte — transformerait ce
            // point d'entrée en tremplin de redirection ouverte.
            var redirectUri = form["redirectUri"].ToString();
            var destination = redirectUri.StartsWith('/')
                              && !redirectUri.StartsWith("//", StringComparison.Ordinal)
                              && Uri.IsWellFormedUriString(redirectUri, UriKind.Relative)
                ? redirectUri
                : "/";

            return Results.LocalRedirect(destination);
        })
        // Sans liaison [FromForm], l'endpoint ne réclamerait pas de jeton : on l'exige ici,
        // et la lecture manuelle du formulaire permet de répondre 400 plutôt que de lever.
        .WithMetadata(RequireAntiforgery.Instance)
        .WithName("SetCulture");

        return endpoints;
    }

    /// <summary>
    /// Impose la validation du jeton antiforgery sur un endpoint qui ne lie pas de formulaire.
    /// </summary>
    /// <remarks>
    /// La validation automatique ne s'active que pour les endpoints à liaison
    /// <c>[FromForm]</c> — or cette liaison lit le formulaire avant qu'on ait pu consulter le
    /// résultat de la validation, ce qui produit une erreur serveur au lieu d'un refus.
    /// </remarks>
    private sealed class RequireAntiforgery : IAntiforgeryMetadata
    {
        public static RequireAntiforgery Instance { get; } = new();

        public bool RequiresValidation => true;
    }
}
