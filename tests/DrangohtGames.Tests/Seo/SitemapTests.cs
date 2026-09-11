using DrangohtGames.Tests.Builders;
using DrangohtGames.Web.Seo;
using Shouldly;

namespace DrangohtGames.Tests.Seo;

public sealed class SitemapTests
{
    private static readonly Uri BaseUri = new("https://exemple.test/");

    [Fact]
    public void Build_ListeLaPageDeChaqueJeu()
    {
        var jeux = new[]
        {
            new GameBuilder().WithSlug("x-moon").Build(),
            new GameBuilder().WithSlug("y-sun").Build(),
        };

        var xml = Sitemap.Build(jeux, BaseUri);

        xml.ShouldContain("https://exemple.test/games/x-moon");
        xml.ShouldContain("https://exemple.test/games/y-sun");
    }

    [Fact]
    public void Build_ListeLAccueilEtLaPageAPropos()
    {
        var xml = Sitemap.Build([new GameBuilder().Build()], BaseUri);

        xml.ShouldContain("<loc>https://exemple.test/</loc>");
        xml.ShouldContain("<loc>https://exemple.test/about</loc>");
    }

    [Fact]
    public void Build_QuandLeJeuEstPublie_DateSaDerniereModification()
    {
        var jeu = new GameBuilder()
            .WithSlug("x-moon")
            .WithPublishedAt(new DateTimeOffset(2024, 3, 17, 22, 10, 0, TimeSpan.FromHours(2)))
            .Build();

        var xml = Sitemap.Build([jeu], BaseUri);

        // Le format W3C réduit à la date : l'heure de publication n'apporte rien à un
        // moteur qui passe au mieux une fois par jour.
        xml.ShouldContain("<lastmod>2024-03-17</lastmod>");
    }

    [Fact]
    public void Build_QuandLeJeuNaPasDeDateDePublication_OmetLaDerniereModification()
    {
        var jeu = new GameBuilder().WithPublishedAt(null).Build();

        var xml = Sitemap.Build([jeu], BaseUri);

        xml.ShouldNotContain("lastmod");
    }
}
