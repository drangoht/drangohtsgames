namespace DrangohtGames.Web.Games;

/// <summary>
/// Identifiant lisible d'un jeu dans les URLs du site, dérivé de son adresse itch.io.
/// </summary>
public readonly record struct GameSlug
{
    private GameSlug(string value) => Value = value;

    /// <summary>Valeur textuelle du slug, toujours en minuscules.</summary>
    public string Value { get; }

    /// <summary>
    /// Dérive le slug du dernier segment de l'URL itch.io du jeu.
    /// </summary>
    /// <remarks>
    /// itch.io garantit l'unicité de ce segment par créateur. Quand l'URL n'en porte pas
    /// (profil racine, donnée tronquée côté API), on retombe sur l'identifiant numérique :
    /// une route moins jolie vaut mieux qu'un jeu inatteignable.
    /// </remarks>
    public static GameSlug FromItchUrl(Uri itchUrl, int gameId)
    {
        ArgumentNullException.ThrowIfNull(itchUrl);

        var lastSegment = itchUrl.Segments[^1].Trim('/');

        return lastSegment.Length == 0
            ? new GameSlug(gameId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            : new GameSlug(lastSegment.ToLowerInvariant());
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
