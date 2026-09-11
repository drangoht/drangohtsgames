using DrangohtGames.Tests.Builders;
using DrangohtGames.Web.Seo;
using Shouldly;

namespace DrangohtGames.Tests.Seo;

public sealed class SitemapTests
{
    private static readonly Uri BaseUri = new("https://exemple.test/");

    [Fact]
    public void Build_ListeLaPageDeChaqueJeu_DansChaqueLangue()
    {
        var jeux = new[]
        {
            new GameBuilder().WithSlug("x-moon").Build(),
            new GameBuilder().WithSlug("y-sun").Build(),
        };

        var xml = Sitemap.Build(jeux, BaseUri);

        xml.ShouldContain("https://exemple.test/en/games/x-moon");
        xml.ShouldContain("https://exemple.test/fr/games/x-moon");
        xml.ShouldContain("https://exemple.test/en/games/y-sun");
        xml.ShouldContain("https://exemple.test/fr/games/y-sun");
    }

    [Fact]
    public void Build_ListeLAccueilEtLaPageAPropos_DansChaqueLangue()
    {
        var xml = Sitemap.Build([new GameBuilder().Build()], BaseUri);

        xml.ShouldContain("<loc>https://exemple.test/en/</loc>");
        xml.ShouldContain("<loc>https://exemple.test/fr/</loc>");
        xml.ShouldContain("<loc>https://exemple.test/en/about</loc>");
        xml.ShouldContain("<loc>https://exemple.test/fr/about</loc>");
    }

    [Fact]
    public void Build_NAnnoncePlusAucuneAdresseSansLangue()
    {
        // Une adresse sans préfixe redirige (ADR 0008) : l'annoncer ferait explorer au
        // moteur une redirection pour chaque page, sans rien lui apprendre.
        var xml = Sitemap.Build([new GameBuilder().WithSlug("x-moon").Build()], BaseUri);

        xml.ShouldNotContain("<loc>https://exemple.test/</loc>");
        xml.ShouldNotContain("<loc>https://exemple.test/about</loc>");
        xml.ShouldNotContain("<loc>https://exemple.test/games/x-moon</loc>");
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
