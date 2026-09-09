using System.Text.Json;

namespace DrangohtGames.Web.Games.Editorial;

/// <summary>
/// Contenu rédigé à la main qui complète ce qu'itch.io ne publie pas par son API :
/// tags, moteur, description longue, captures d'écran et traductions françaises.
/// </summary>
/// <remarks>
/// Le catalogue est indexé par slug de jeu — l'identifiant que l'on lit dans l'URL itch.io,
/// donc celui qu'un humain reconnaît en éditant le fichier. Une entrée absente n'est pas une
/// erreur : le jeu reste affiché avec les seules données de l'API.
/// </remarks>
public sealed class EditorialCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IReadOnlyDictionary<string, EditorialEntry> _entriesBySlug;

    private EditorialCatalog(IReadOnlyDictionary<string, EditorialEntry> entriesBySlug) =>
        _entriesBySlug = entriesBySlug;

    /// <summary>Catalogue sans aucune entrée : tous les jeux restent tels qu'itch.io les décrit.</summary>
    public static EditorialCatalog Empty { get; } =
        new(new Dictionary<string, EditorialEntry>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Indique qu'aucun jeu n'est enrichi.</summary>
    public bool IsEmpty => _entriesBySlug.Count == 0;

    /// <summary>
    /// Charge le contenu éditorial depuis son JSON.
    /// </summary>
    /// <exception cref="JsonException">
    /// Le fichier est syntaxiquement invalide. Il est versionné avec le code : mieux vaut un
    /// démarrage en échec qu'un site amputé de ses descriptions sans que personne ne le voie.
    /// </exception>
    public static EditorialCatalog FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var file = JsonSerializer.Deserialize<EditorialFile>(json, SerializerOptions);

        if (file is null)
        {
            return Empty;
        }

        var entries = new Dictionary<string, EditorialEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var (slug, entry) in file.Games)
        {
            entries[slug.Trim()] = entry;
        }

        return new EditorialCatalog(entries);
    }

    /// <summary>
    /// Complète un jeu avec son entrée éditoriale, s'il en a une.
    /// </summary>
    public Game Apply(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (!_entriesBySlug.TryGetValue(game.Slug.Value, out var entry))
        {
            return game;
        }

        return game with
        {
            Tags = entry.Tags.Count > 0 ? entry.Tags : game.Tags,
            Engine = entry.Engine ?? game.Engine,
            Tagline = game.Tagline.OverlaidWith(entry.Tagline?.ToLocalizedText()),
            Description = game.Description.OverlaidWith(entry.Description?.ToLocalizedText()),
            Screenshots = ToScreenshots(entry.Screenshots) is { Count: > 0 } screenshots
                ? screenshots
                : game.Screenshots,
            IsFeatured = entry.Featured,
        };
    }

    /// <summary>
    /// Retourne les slugs du fichier éditorial qui ne correspondent à aucun jeu publié.
    /// </summary>
    /// <remarks>
    /// Une faute de frappe dans le fichier produit sinon un enrichissement silencieusement
    /// ignoré. On les journalise au démarrage.
    /// </remarks>
    public IReadOnlyList<string> FindOrphanEntries(IEnumerable<Game> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        var knownSlugs = games
            .Select(game => game.Slug.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. _entriesBySlug.Keys.Where(slug => !knownSlugs.Contains(slug)).Order(StringComparer.Ordinal)];
    }

    private static IReadOnlyList<Screenshot> ToScreenshots(IReadOnlyList<EditorialScreenshot> screenshots) =>
    [
        .. screenshots
            .Select(screenshot => new
            {
                Uri = Uri.TryCreate(screenshot.Url, UriKind.Absolute, out var uri) ? uri : null,
                screenshot.Caption,
            })
            .Where(screenshot => screenshot.Uri is not null)
            .Select(screenshot => new Screenshot(
                screenshot.Uri!,
                screenshot.Caption?.ToLocalizedText() ?? LocalizedText.Empty)),
    ];
}
