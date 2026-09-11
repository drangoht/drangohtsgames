using System.Globalization;
using System.Xml.Linq;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Localization;

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

        // Chaque page existe dans chaque langue et chacune a sa propre adresse (ADR 0008) :
        // le plan les annonce toutes, faute de quoi une version ne serait explorée qu'au
        // hasard des liens.
        var urlset = new XElement(
            Ns + "urlset",
            SupportedCultures.All.SelectMany(culture => new[]
            {
                Url(baseUri, $"{culture}/"),
                Url(baseUri, $"{culture}/about"),
            }.Concat(games.Select(game =>
                Url(baseUri, $"{culture}/games/{game.Slug.Value}", game.PublishedAt)))));

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
