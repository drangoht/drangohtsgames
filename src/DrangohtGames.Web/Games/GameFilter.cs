using System.Globalization;
using System.Text;

namespace DrangohtGames.Web.Games;

/// <summary>
/// Critères de sélection appliqués au catalogue : tag, moteur, plateforme et recherche
/// textuelle, combinés par ET.
/// </summary>
/// <remarks>
/// Le filtrage est calculé en mémoire sur le catalogue déjà chargé : à cette volumétrie,
/// une requête vers itch.io par changement de filtre coûterait bien plus que le parcours.
/// </remarks>
public sealed record GameFilter
{
    /// <summary>Filtre sans aucun critère : tout le catalogue passe.</summary>
    public static GameFilter Empty { get; } = new();

    /// <summary>Tag exigé, insensible à la casse.</summary>
    public string? Tag { get; init; }

    /// <summary>Moteur exigé, insensible à la casse.</summary>
    public string? Engine { get; init; }

    /// <summary>Plateforme exigée ; un jeu multiplateforme correspond dès qu'il la propose.</summary>
    public GamePlatforms? Platform { get; init; }

    /// <summary>Terme recherché dans le titre, les accroches et les tags.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Ne retient que les jeux dont le build Web est servi par le site (ADR 0007).</summary>
    public bool PlayableHere { get; init; }

    /// <summary>Indique qu'au moins un critère exploitable est posé.</summary>
    public bool IsActive =>
        !string.IsNullOrWhiteSpace(Tag)
        || !string.IsNullOrWhiteSpace(Engine)
        || Platform is not null and not GamePlatforms.None
        || !string.IsNullOrWhiteSpace(SearchTerm)
        || PlayableHere;

    /// <summary>Applique les critères en conservant l'ordre du catalogue.</summary>
    public IReadOnlyList<Game> Apply(IEnumerable<Game> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        var searchTerm = Normalize(SearchTerm);

        return
        [
            .. games.Where(game =>
                MatchesTag(game)
                && MatchesEngine(game)
                && MatchesPlatform(game)
                && MatchesPlayableHere(game)
                && MatchesSearch(game, searchTerm)),
        ];
    }

    /// <summary>Tags distincts présents dans le catalogue, triés alphabétiquement.</summary>
    public static IReadOnlyList<string> AvailableTagsOf(IEnumerable<Game> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        return
        [
            .. games.SelectMany(game => game.Tags)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.CurrentCulture),
        ];
    }

    /// <summary>Moteurs distincts présents dans le catalogue, triés alphabétiquement.</summary>
    public static IReadOnlyList<string> AvailableEnginesOf(IEnumerable<Game> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        return
        [
            .. games.Select(game => game.Engine)
                    .OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.CurrentCulture),
        ];
    }

    private bool MatchesTag(Game game) =>
        string.IsNullOrWhiteSpace(Tag)
        || game.Tags.Contains(Tag, StringComparer.OrdinalIgnoreCase);

    private bool MatchesEngine(Game game) =>
        string.IsNullOrWhiteSpace(Engine)
        || string.Equals(game.Engine, Engine, StringComparison.OrdinalIgnoreCase);

    private bool MatchesPlatform(Game game) =>
        Platform is not { } platform
        || platform == GamePlatforms.None
        || game.Platforms.HasFlag(platform);

    private bool MatchesPlayableHere(Game game) => !PlayableHere || game.IsSelfHosted;

    private static bool MatchesSearch(Game game, string? searchTerm)
    {
        if (searchTerm is null)
        {
            return true;
        }

        return Contains(game.Title, searchTerm)
            || Contains(game.Engine, searchTerm)
            || Contains(game.Tagline.English, searchTerm)
            || Contains(game.Tagline.French, searchTerm)
            || game.Tags.Any(tag => Contains(tag, searchTerm));
    }

    private static bool Contains(string? haystack, string needle) =>
        Normalize(haystack) is { } normalized && normalized.Contains(needle, StringComparison.Ordinal);

    /// <summary>
    /// Réduit un texte à une forme comparable : minuscules et sans signes diacritiques.
    /// </summary>
    /// <remarks>
    /// Sans cette normalisation, « exigeant » ne trouverait pas « exigeant » saisi avec
    /// accents, et inversement — cas courant sur un site bilingue.
    /// </remarks>
    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
