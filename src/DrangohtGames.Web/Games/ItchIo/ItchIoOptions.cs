using System.ComponentModel.DataAnnotations;

namespace DrangohtGames.Web.Games.ItchIo;

/// <summary>Configuration de l'accès à l'API itch.io.</summary>
public sealed class ItchIoOptions
{
    /// <summary>Section de configuration correspondante.</summary>
    public const string SectionName = "ItchIo";

    /// <summary>
    /// Clé d'API personnelle (scope <c>profile:games</c>), générée sur
    /// <c>https://itch.io/user/settings/api-keys</c>.
    /// </summary>
    /// <remarks>
    /// C'est un secret : il est fourni par variable d'environnement
    /// (<c>ItchIo__ApiKey</c>) ou par les user-secrets en développement, jamais par
    /// <c>appsettings.json</c>.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Devise dans laquelle les prix plancher sont libellés.
    /// </summary>
    /// <remarks>
    /// <c>/my-games</c> renvoie <c>min_price</c> sans devise associée : itch.io la porte au
    /// niveau du compte. On la configure donc une fois, globalement.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "La devise doit être un code ISO 4217 de trois lettres.")]
    public string Currency { get; init; } = "USD";

    /// <summary>Durée pendant laquelle la réponse de l'API est réutilisée sans nouvel appel.</summary>
    [Range(typeof(TimeSpan), "00:00:30", "24:00:00")]
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Délai maximal accordé à un appel à l'API, tentatives de reprise comprises.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
