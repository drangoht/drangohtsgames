using DrangohtGames.Tests.Builders;
using DrangohtGames.Web.Games;
using Shouldly;

namespace DrangohtGames.Tests.Games;

public sealed class FeaturedGameTests
{
    [Fact]
    public void Of_QuandUnJeuEstDesigne_LeMetEnVitrine()
    {
        Game[] games =
        [
            new GameBuilder().WithSlug("recent").WithPublishedAt(Le(2026, 8)).WithSelfHosted().Build(),
            new GameBuilder().WithSlug("choisi").WithPublishedAt(Le(2026, 1)).WithFeatured().Build(),
        ];

        FeaturedGame.Of(games)?.Slug.Value.ShouldBe("choisi");
    }

    [Fact]
    public void Of_QuandPlusieursJeuxSontDesignes_RetientLePremierDuCatalogue()
    {
        Game[] games =
        [
            new GameBuilder().WithSlug("premier").WithFeatured().Build(),
            new GameBuilder().WithSlug("second").WithFeatured().Build(),
        ];

        FeaturedGame.Of(games)?.Slug.Value.ShouldBe("premier");
    }

    [Fact]
    public void Of_SansDesignation_RetientLeDernierJeuJouableIci()
    {
        Game[] games =
        [
            new GameBuilder().WithSlug("ancien-jouable").WithPublishedAt(Le(2026, 3)).WithSelfHosted().Build(),
            new GameBuilder().WithSlug("recent-jouable").WithPublishedAt(Le(2026, 8)).WithSelfHosted().Build(),
            new GameBuilder().WithSlug("tres-recent-ailleurs").WithPublishedAt(Le(2026, 9)).Build(),
        ];

        FeaturedGame.Of(games)?.Slug.Value.ShouldBe("recent-jouable");
    }

    [Fact]
    public void Of_QuandAucunJeuNEstJouableIci_RetientLeDernierPublie()
    {
        Game[] games =
        [
            new GameBuilder().WithSlug("ancien").WithPublishedAt(Le(2026, 2)).Build(),
            new GameBuilder().WithSlug("recent").WithPublishedAt(Le(2026, 7)).Build(),
        ];

        FeaturedGame.Of(games)?.Slug.Value.ShouldBe("recent");
    }

    [Fact]
    public void Of_QuandLeCatalogueEstVide_NeMetRienEnVitrine()
    {
        FeaturedGame.Of([]).ShouldBeNull();
    }

    [Fact]
    public void Of_QuandLaDateDePublicationManque_NeFaitPasEchouerLaSelection()
    {
        Game[] games =
        [
            new GameBuilder().WithSlug("sans-date").WithPublishedAt(null).WithSelfHosted().Build(),
            new GameBuilder().WithSlug("date").WithPublishedAt(Le(2026, 4)).WithSelfHosted().Build(),
        ];

        FeaturedGame.Of(games)?.Slug.Value.ShouldBe("date");
    }

    private static DateTimeOffset Le(int year, int month) => new(year, month, 1, 0, 0, 0, TimeSpan.Zero);
}
