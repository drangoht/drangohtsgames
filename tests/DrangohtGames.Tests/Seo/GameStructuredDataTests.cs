using System.Text.Json;
using DrangohtGames.Tests.Builders;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Seo;
using Shouldly;

namespace DrangohtGames.Tests.Seo;

public sealed class GameStructuredDataTests
{
    private static readonly Uri GameUrl = new("https://exemple.test/games/x-moon");

    [Fact]
    public void ToJsonLd_DecritLeJeuPourLesMoteursDeRecherche()
    {
        var jeu = new GameBuilder().WithSlug("x-moon").WithTitle("X-Moon").Build();

        var document = JsonDocument.Parse(GameStructuredData.ToJsonLd(jeu, GameUrl, null));

        document.RootElement.GetProperty("@context").GetString().ShouldBe("https://schema.org");
        document.RootElement.GetProperty("@type").GetString().ShouldBe("VideoGame");
        document.RootElement.GetProperty("name").GetString().ShouldBe("X-Moon");
        document.RootElement.GetProperty("url").GetString().ShouldBe(GameUrl.ToString());
    }

    [Fact]
    public void ToJsonLd_AnnonceLePrixPlancherEtSaDevise()
    {
        var jeu = new GameBuilder().WithPrice(Price.FromCents(499, "EUR")).Build();

        var offre = JsonDocument.Parse(GameStructuredData.ToJsonLd(jeu, GameUrl, null))
            .RootElement.GetProperty("offers");

        // Le prix schema.org s'écrit en unité principale, avec un point décimal, quelle que
        // soit la culture du visiteur.
        offre.GetProperty("price").GetString().ShouldBe("4.99");
        offre.GetProperty("priceCurrency").GetString().ShouldBe("EUR");
        offre.GetProperty("url").GetString().ShouldBe("https://drangoht.itch.io/un-jeu");
    }

    [Fact]
    public void ToJsonLd_QuandLeJeuEstGratuit_AnnonceUnPrixNul()
    {
        var jeu = new GameBuilder().WithPrice(Price.FromCents(0, "USD")).Build();

        var offre = JsonDocument.Parse(GameStructuredData.ToJsonLd(jeu, GameUrl, null))
            .RootElement.GetProperty("offers");

        offre.GetProperty("price").GetString().ShouldBe("0.00");
    }

    [Fact]
    public void ToJsonLd_DecritLIllustrationLesPlateformesEtLaDateDePublication()
    {
        var jeu = new GameBuilder()
            .WithScreenshots("https://img.itch.zone/shot.png")
            .WithPlatforms(GamePlatforms.Windows | GamePlatforms.Linux)
            .WithPublishedAt(new DateTimeOffset(2024, 3, 17, 22, 10, 0, TimeSpan.FromHours(2)))
            .Build();

        var racine = JsonDocument.Parse(GameStructuredData.ToJsonLd(jeu, GameUrl, null)).RootElement;

        racine.GetProperty("image").GetString().ShouldBe("https://img.itch.zone/shot.png");
        racine.GetProperty("datePublished").GetString().ShouldBe("2024-03-17");
        racine.GetProperty("gamePlatform").EnumerateArray()
            .Select(plateforme => plateforme.GetString())
            .ShouldBe(["Windows", "Linux"]);
    }

    [Fact]
    public void ToJsonLd_QuandLeJeuNaNiIllustrationNiDate_OmetCesProprietes()
    {
        var jeu = new GameBuilder().WithPublishedAt(null).WithPlatforms(GamePlatforms.None).Build();

        var racine = JsonDocument.Parse(GameStructuredData.ToJsonLd(jeu, GameUrl, null)).RootElement;

        // Une propriété schema.org vide vaut moins qu'une propriété absente : elle décrit
        // le jeu comme dépourvu de ce qu'on n'a simplement pas su renseigner.
        racine.TryGetProperty("image", out _).ShouldBeFalse();
        racine.TryGetProperty("datePublished", out _).ShouldBeFalse();
        racine.TryGetProperty("gamePlatform", out _).ShouldBeFalse();
    }

    [Fact]
    public void ToJsonLd_QuandLeTitreContientDuBalisage_NeFermePasLaBaliseScript()
    {
        // Les titres viennent d'itch.io : le document est inséré dans un <script> de la page.
        var jeu = new GameBuilder().WithTitle("X-Moon</script><script>alert(1)</script>").Build();

        var json = GameStructuredData.ToJsonLd(jeu, GameUrl, null);

        json.ShouldNotContain("</script>", Case.Insensitive);
        JsonDocument.Parse(json).RootElement.GetProperty("name").GetString()
            .ShouldBe("X-Moon</script><script>alert(1)</script>");
    }

    [Fact]
    public void ToJsonLd_JointLaDescriptionDeLaPage()
    {
        var jeu = new GameBuilder().Build();

        var racine = JsonDocument.Parse(
            GameStructuredData.ToJsonLd(jeu, GameUrl, "Un shoot spatial en tour par tour.")).RootElement;

        racine.GetProperty("description").GetString().ShouldBe("Un shoot spatial en tour par tour.");
    }

    [Fact]
    public void ToJsonLd_QuandLaDescriptionManque_OmetLaPropriete()
    {
        var racine = JsonDocument.Parse(
            GameStructuredData.ToJsonLd(new GameBuilder().Build(), GameUrl, "   ")).RootElement;

        racine.TryGetProperty("description", out _).ShouldBeFalse();
    }
}
