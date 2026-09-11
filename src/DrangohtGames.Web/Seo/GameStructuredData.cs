using System.Globalization;
using System.Text.Json;
using DrangohtGames.Web.Games;

namespace DrangohtGames.Web.Seo;

/// <summary>
/// Décrit un jeu au vocabulaire schema.org, pour que les moteurs de recherche en fassent
/// une fiche plutôt qu'un lien bleu.
/// </summary>
public static class GameStructuredData
{
    /// <summary>Construit le document JSON-LD d'un jeu.</summary>
    /// <param name="game">Jeu décrit.</param>
    /// <param name="gameUrl">Adresse de sa fiche sur le site.</param>
    /// <param name="description">Description dans la langue de la page, si elle existe.</param>
    public static string ToJsonLd(Game game, Uri gameUrl, string? description)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(gameUrl);

        var document = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "VideoGame",
            ["name"] = game.Title,
            ["url"] = gameUrl.ToString(),
            ["offers"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["@type"] = "Offer",
                ["price"] = game.Price.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                ["priceCurrency"] = game.Price.Currency,
                ["availability"] = "https://schema.org/InStock",
                ["url"] = game.ItchUrl.ToString(),
            },
        };

        // Une propriété absente décrit mieux le jeu qu'une propriété vide, qui affirmerait
        // qu'il n'a ni description, ni image, ni date.
        if (!string.IsNullOrWhiteSpace(description))
        {
            document["description"] = description;
        }

        if (game.ShowcaseImageUrl is { } image)
        {
            document["image"] = image.ToString();
        }

        if (game.PublishedAt is { } publishedAt)
        {
            document["datePublished"] = publishedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var platforms = PlatformNamesOf(game.Platforms);

        if (platforms.Count > 0)
        {
            document["gamePlatform"] = platforms;
        }

        return JsonSerializer.Serialize(document);
    }

    private static IReadOnlyList<string> PlatformNamesOf(GamePlatforms platforms) =>
    [
        .. new[]
        {
            (GamePlatforms.Windows, "Windows"),
            (GamePlatforms.Linux, "Linux"),
            (GamePlatforms.MacOs, "macOS"),
            (GamePlatforms.Android, "Android"),
        }
        .Where(candidate => platforms.HasFlag(candidate.Item1))
        .Select(candidate => candidate.Item2),
    ];
}
