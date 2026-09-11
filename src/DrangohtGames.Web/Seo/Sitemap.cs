using System.Globalization;
using System.Xml.Linq;
using DrangohtGames.Web.Games;

namespace DrangohtGames.Web.Seo;

/// <summary>Plan du site soumis aux moteurs de recherche.</summary>
public static class Sitemap
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>Construit le document listant les pages indexables du site.</summary>
    public static string Build(IReadOnlyList<Game> games, Uri baseUri)
    {
        ArgumentNullException.ThrowIfNull(games);
        ArgumentNullException.ThrowIfNull(baseUri);

        var urlset = new XElement(
            Ns + "urlset",
            Url(baseUri, string.Empty),
            Url(baseUri, "about"),
            games.Select(game => Url(baseUri, $"games/{game.Slug.Value}", game.PublishedAt)));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), urlset).ToString();
    }

    private static XElement Url(Uri baseUri, string path, DateTimeOffset? lastModified = null) =>
        new(
            Ns + "url",
            new XElement(Ns + "loc", new Uri(baseUri, path)),
            lastModified is { } date
                ? new XElement(Ns + "lastmod", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                : null);
}
