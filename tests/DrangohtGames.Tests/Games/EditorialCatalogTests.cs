using System.Globalization;
using DrangohtGames.Tests.Builders;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.Editorial;
using Shouldly;

namespace DrangohtGames.Tests.Games;

public sealed class EditorialCatalogTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private const string Contenu = """
    {
      "games": {
        "web-runner": {
          "tags": ["Arcade", "Runner"],
          "engine": "Godot",
          "tagline": { "fr": "Un runner jouable dans le navigateur" },
          "description": {
            "en": "Dodge obstacles at increasing speed.",
            "fr": "Esquivez les obstacles à vitesse croissante."
          },
          "screenshots": [
            { "url": "https://cdn.example/run-1.png", "caption": { "en": "First level", "fr": "Premier niveau" } }
          ]
        }
      }
    }
    """;

    [Fact]
    public void Apply_RenseigneLesTagsLeMoteurEtLaDescription()
    {
        var catalog = EditorialCatalog.FromJson(Contenu);
        var game = new GameBuilder().WithSlug("web-runner").Build();

        var enriched = catalog.Apply(game);

        enriched.Tags.ShouldBe(["Arcade", "Runner"]);
        enriched.Engine.ShouldBe("Godot");
        enriched.Description.For(French).ShouldBe("Esquivez les obstacles à vitesse croissante.");
        enriched.Description.For(English).ShouldBe("Dodge obstacles at increasing speed.");
    }

    [Fact]
    public void Apply_AjouteLaTraductionFrancaiseSansPerdreLAccrocheDItchIo()
    {
        var catalog = EditorialCatalog.FromJson(Contenu);
        var game = new GameBuilder()
            .WithSlug("web-runner")
            .WithTagline(LocalizedText.FromEnglish("Playable in browser"))
            .Build();

        var enriched = catalog.Apply(game);

        enriched.Tagline.For(French).ShouldBe("Un runner jouable dans le navigateur");
        enriched.Tagline.For(English).ShouldBe("Playable in browser");
    }

    [Fact]
    public void Apply_RenseigneLesCapturesAvecLeurTexteAlternatif()
    {
        var catalog = EditorialCatalog.FromJson(Contenu);
        var game = new GameBuilder().WithSlug("web-runner").Build();

        var capture = catalog.Apply(game).Screenshots.ShouldHaveSingleItem();

        capture.Url.ShouldBe(new Uri("https://cdn.example/run-1.png"));
        capture.Caption.For(French).ShouldBe("Premier niveau");
    }

    [Fact]
    public void Apply_QuandLeJeuNaPasDEntree_LeRetourneInchange()
    {
        var catalog = EditorialCatalog.FromJson(Contenu);
        var game = new GameBuilder().WithSlug("jeu-sans-fiche").Build();

        var enriched = catalog.Apply(game);

        enriched.ShouldBe(game);
        enriched.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_IgnoreLaCasseDuSlug()
    {
        var catalog = EditorialCatalog.FromJson("""
            { "games": { "Web-Runner": { "engine": "Godot" } } }
            """);
        var game = new GameBuilder().WithSlug("web-runner").Build();

        catalog.Apply(game).Engine.ShouldBe("Godot");
    }

    [Fact]
    public void FromJson_QuandUneEntreeEstPartielle_NEcrasePasCeQuiVientDItchIo()
    {
        var catalog = EditorialCatalog.FromJson("""
            { "games": { "web-runner": { "tags": ["Arcade"] } } }
            """);
        var game = new GameBuilder()
            .WithSlug("web-runner")
            .WithTagline(LocalizedText.FromEnglish("Playable in browser"))
            .Build();

        var enriched = catalog.Apply(game);

        enriched.Tags.ShouldBe(["Arcade"]);
        enriched.Tagline.For(English).ShouldBe("Playable in browser");
        enriched.Description.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void FromJson_QuandLeContenuEstVide_ProduitUnCatalogueNeutre()
    {
        var catalog = EditorialCatalog.FromJson("{}");
        var game = new GameBuilder().Build();

        catalog.Apply(game).ShouldBe(game);
        catalog.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void FromJson_QuandLeJsonEstInvalide_LeveDesLeChargement()
    {
        // Ce fichier est versionné avec le code : une erreur de syntaxe doit faire échouer
        // le démarrage, pas produire un site silencieusement amputé de ses descriptions.
        Should.Throw<System.Text.Json.JsonException>(() => EditorialCatalog.FromJson("{ pas du json"));
    }

    [Fact]
    public void Apply_QuandLEntreeMetLeJeuEnAvant_LeSignaleAuCatalogue()
    {
        var catalog = EditorialCatalog.FromJson("""
            { "games": { "web-runner": { "featured": true } } }
            """);
        var game = new GameBuilder().WithSlug("web-runner").Build();

        catalog.Apply(game).IsFeatured.ShouldBeTrue();
    }

    [Fact]
    public void Apply_SansMentionDeMiseEnAvant_NeMetPasLeJeuEnAvant()
    {
        var catalog = EditorialCatalog.FromJson(Contenu);
        var game = new GameBuilder().WithSlug("web-runner").Build();

        catalog.Apply(game).IsFeatured.ShouldBeFalse();
    }

    [Fact]
    public void SlugsInconnus_SignaleLesEntreesQuiNeCorrespondentAAucunJeu()
    {
        var catalog = EditorialCatalog.FromJson(Contenu);
        var games = new[] { new GameBuilder().WithSlug("autre-jeu").Build() };

        catalog.FindOrphanEntries(games).ShouldBe(["web-runner"]);
    }
}
