using DrangohtGames.Tests.Builders;
using DrangohtGames.Web.Games;
using Shouldly;

namespace DrangohtGames.Tests.Games;

public sealed class GameFilterTests
{
    private static readonly Game XMoon = new GameBuilder()
        .WithSlug("x-moon").WithTitle("X-Moon")
        .WithTags("Arcade", "Shmup").WithEngine("Unity")
        .WithPlatforms(GamePlatforms.Windows | GamePlatforms.Linux)
        .WithTagline(new LocalizedText("A tough space shooter", "Un shoot spatial exigeant"))
        .Build();

    private static readonly Game WebRunner = new GameBuilder()
        .WithSlug("web-runner").WithTitle("Web Runner")
        .WithTags("Arcade", "Runner").WithEngine("Godot")
        .WithPlatforms(GamePlatforms.None)
        .WithTagline(LocalizedText.FromEnglish("Playable in browser"))
        .Build();

    private static readonly Game PuzzleBox = new GameBuilder()
        .WithSlug("puzzle-box").WithTitle("Puzzle Box")
        .WithTags("Puzzle").WithEngine("Godot")
        .WithPlatforms(GamePlatforms.Windows | GamePlatforms.Android)
        .WithSelfHosted()
        .Build();

    private static readonly Game[] Tous = [XMoon, WebRunner, PuzzleBox];

    [Fact]
    public void Apply_QuandAucunCritereNEstPose_RetourneToutLeCatalogue()
    {
        GameFilter.Empty.Apply(Tous).ShouldBe(Tous);
    }

    [Fact]
    public void Apply_FiltreParTagSansTenirCompteDeLaCasse()
    {
        var filtre = GameFilter.Empty with { Tag = "arcade" };

        filtre.Apply(Tous).Select(g => g.Title).ShouldBe(["X-Moon", "Web Runner"]);
    }

    [Fact]
    public void Apply_FiltreParMoteur()
    {
        var filtre = GameFilter.Empty with { Engine = "Godot" };

        filtre.Apply(Tous).Select(g => g.Title).ShouldBe(["Web Runner", "Puzzle Box"]);
    }

    [Fact]
    public void Apply_FiltreParPlateformeEnGardantLesJeuxQuiLaProposentParmiDAutres()
    {
        var filtre = GameFilter.Empty with { Platform = GamePlatforms.Windows };

        filtre.Apply(Tous).Select(g => g.Title).ShouldBe(["X-Moon", "Puzzle Box"]);
    }

    [Fact]
    public void Apply_ChercheDansLeTitre()
    {
        var filtre = GameFilter.Empty with { SearchTerm = "runner" };

        filtre.Apply(Tous).ShouldHaveSingleItem().Title.ShouldBe("Web Runner");
    }

    [Fact]
    public void Apply_ChercheAussiDansLAccrocheDansLesDeuxLangues()
    {
        (GameFilter.Empty with { SearchTerm = "spatial" }).Apply(Tous)
            .ShouldHaveSingleItem().Title.ShouldBe("X-Moon");

        (GameFilter.Empty with { SearchTerm = "browser" }).Apply(Tous)
            .ShouldHaveSingleItem().Title.ShouldBe("Web Runner");
    }

    [Fact]
    public void Apply_ChercheSansTenirCompteDesAccents()
    {
        // Un visiteur qui tape « exigeant » depuis un clavier sans accents doit trouver.
        var filtre = GameFilter.Empty with { SearchTerm = "exigeant" };

        filtre.Apply(Tous).ShouldHaveSingleItem().Title.ShouldBe("X-Moon");
    }

    [Fact]
    public void Apply_ChercheDansLesTags()
    {
        var filtre = GameFilter.Empty with { SearchTerm = "puzzle" };

        filtre.Apply(Tous).ShouldHaveSingleItem().Title.ShouldBe("Puzzle Box");
    }

    [Fact]
    public void Apply_CombineLesCriteresParEt()
    {
        var filtre = GameFilter.Empty with { Tag = "Arcade", Engine = "Godot" };

        filtre.Apply(Tous).ShouldHaveSingleItem().Title.ShouldBe("Web Runner");
    }

    [Fact]
    public void Apply_QuandLeTermeEstFaitDEspaces_LeTraiteCommeAbsent()
    {
        (GameFilter.Empty with { SearchTerm = "   " }).Apply(Tous).ShouldBe(Tous);
    }

    [Fact]
    public void Apply_QuandAucunJeuNeCorrespond_RetourneUneListeVide()
    {
        (GameFilter.Empty with { Tag = "Metroidvania" }).Apply(Tous).ShouldBeEmpty();
    }

    [Fact]
    public void Apply_PreserveLOrdreDuCatalogue()
    {
        var filtre = GameFilter.Empty with { Tag = "Arcade" };

        filtre.Apply(Tous).ShouldBe([XMoon, WebRunner]);
    }

    [Fact]
    public void IsActive_DistingueUnFiltrePoseDUnFiltreVide()
    {
        GameFilter.Empty.IsActive.ShouldBeFalse();
        (GameFilter.Empty with { Tag = "Arcade" }).IsActive.ShouldBeTrue();
        (GameFilter.Empty with { SearchTerm = " " }).IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Apply_QuandSeulsLesJeuxJouablesIciSontDemandes_EcarteLesAutres()
    {
        var filtre = GameFilter.Empty with { PlayableHere = true };

        filtre.Apply(Tous).ShouldBe([PuzzleBox]);
    }

    [Fact]
    public void Apply_QuandLeCritereJouableIciEstFaux_NEcarteAucunJeu()
    {
        var filtre = GameFilter.Empty with { PlayableHere = false };

        filtre.Apply(Tous).ShouldBe(Tous);
    }

    [Fact]
    public void IsActive_QuandSeulLeCritereJouableIciEstPose_EstVrai()
    {
        (GameFilter.Empty with { PlayableHere = true }).IsActive.ShouldBeTrue();
    }

    [Fact]
    public void AvailableTagsOf_ListeLesTagsDistinctsTriesDuCatalogue()
    {
        GameFilter.AvailableTagsOf(Tous).ShouldBe(["Arcade", "Puzzle", "Runner", "Shmup"]);
    }

    [Fact]
    public void AvailableEnginesOf_ListeLesMoteursDistinctsTries()
    {
        GameFilter.AvailableEnginesOf(Tous).ShouldBe(["Godot", "Unity"]);
    }
}
