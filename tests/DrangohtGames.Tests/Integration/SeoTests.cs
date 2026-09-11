using System.Net;
using Shouldly;

namespace DrangohtGames.Tests.Integration;

/// <summary>
/// Ce que le site présente aux moteurs de recherche et aux aperçus de partage.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SeoTests : IClassFixture<SiteFactoryFixture>
{
    private readonly SiteFactory _factory;

    public SeoTests(SiteFactoryFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _factory = fixture.Factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task RobotsTxt_AutoriseLExplorationEtDesigneLePlanDuSite()
    {
        using var response = await CreateClient().GetAsync("/robots.txt", CancellationToken.None);

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");

        var texte = await response.Content.ReadAsStringAsync(CancellationToken.None);
        texte.ShouldContain("User-agent: *");
        texte.ShouldContain("Sitemap: http://localhost/sitemap.xml");
    }

    [Fact]
    public async Task SitemapXml_ListeLesPagesIndexablesDuSite()
    {
        using var response = await CreateClient().GetAsync("/sitemap.xml", CancellationToken.None);

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");

        var xml = await response.Content.ReadAsStringAsync(CancellationToken.None);
        // Depuis l'ADR 0008, chaque page indexable existe dans les deux langues, et le
        // plan les annonce toutes : une version absente du plan n'est explorée que par
        // hasard, au gré des liens.
        xml.ShouldContain("http://localhost/en/games/x-moon");
        xml.ShouldContain("http://localhost/fr/games/x-moon");
        xml.ShouldContain("http://localhost/en/about");
        xml.ShouldContain("http://localhost/fr/about");

        // La page de jeu porte déjà `noindex` : l'annoncer au sitemap serait contradictoire.
        xml.ShouldNotContain("/play");
    }

    [Fact]
    public async Task FicheJeu_DesigneSonAdresseCanonique()
    {
        var html = await CreateClient().GetStringAsync("/en/games/x-moon", CancellationToken.None);

        html.ShouldContain("<link rel=\"canonical\" href=\"http://localhost/en/games/x-moon\"");
    }

    [Fact]
    public async Task Accueil_SousFiltre_DesigneLAccueilNuCommeAdresseCanonique()
    {
        // Chaque pastille produit une URL : sans canonique, la combinatoire complète des
        // filtres serait indexée comme autant de copies de l'accueil.
        var html = await CreateClient().GetStringAsync("/en/?tag=Arcade&engine=Unity", CancellationToken.None);

        html.ShouldContain("<link rel=\"canonical\" href=\"http://localhost/en/\"");
    }

    [Fact]
    public async Task FicheJeu_PorteUneCarteDePartageComplete()
    {
        var html = await CreateClient().GetStringAsync("/en/games/x-moon", CancellationToken.None);

        html.ShouldContain("property=\"og:url\" content=\"http://localhost/en/games/x-moon\"");
        html.ShouldContain("property=\"og:title\" content=\"X-Moon\"");
        html.ShouldContain("property=\"og:site_name\" content=\"Drangoht Games\"");
        html.ShouldContain("name=\"twitter:card\" content=\"summary_large_image\"");
    }

    [Fact]
    public async Task FicheJeu_IllustreLePartageAvecUneCapturePlutotQuLaCouverture()
    {
        // La couverture itch.io ne fait que 315 pixels de large : une grande carte de
        // partage la rendrait floue.
        var html = await CreateClient().GetStringAsync("/en/games/x-moon", CancellationToken.None);

        html.ShouldContain("property=\"og:image\" content=\"https://img.itch.zone/x-moon-shot-1.png\"");
        html.ShouldNotContain("property=\"og:image\" content=\"https://img.itch.zone/x-moon-cover.png\"");
    }

    [Fact]
    public async Task Accueil_PorteUneCarteDePartage()
    {
        var html = await CreateClient().GetStringAsync("/en/", CancellationToken.None);

        html.ShouldContain("property=\"og:url\" content=\"http://localhost/en/\"");
        html.ShouldContain("property=\"og:image\" content=\"https://img.itch.zone/x-moon-shot-1.png\"");
    }

    [Fact]
    public async Task FicheJeu_PorteLesDonneesStructureesDuJeu()
    {
        // Razor encode le « + » du type de média en « &#x2B; », qu'un analyseur HTML
        // redécode : c'est le document décodé que lit le moteur de recherche.
        var html = WebUtility.HtmlDecode(
            await CreateClient().GetStringAsync("/en/games/x-moon", CancellationToken.None));

        html.ShouldContain("<script type=\"application/ld+json\">");
        html.ShouldContain("\"@type\":\"VideoGame\"");
        html.ShouldContain("\"name\":\"X-Moon\"");
    }

    [Fact]
    public async Task APropos_DesigneSonAdresseCanonique()
    {
        var html = await CreateClient().GetStringAsync("/en/about", CancellationToken.None);

        html.ShouldContain("<link rel=\"canonical\" href=\"http://localhost/en/about\"");
    }

    [Theory]
    [InlineData("/en/games/x-moon", "/games/x-moon")]
    [InlineData("/fr/games/x-moon", "/games/x-moon")]
    [InlineData("/en/about", "/about")]
    [InlineData("/fr/about", "/about")]
    [InlineData("/en/", "/")]
    [InlineData("/fr/", "/")]
    public async Task PageIndexable_DeclareChaqueLangueYComprisLaSienne(string chemin, string page)
    {
        // La réciprocité est la règle que Google fait respecter : chaque version doit
        // désigner toutes les versions, elle-même comprise. Une seule déclaration manquante
        // et l'ensemble du groupe est ignoré, sans que rien ne le signale.
        var html = await CreateClient().GetStringAsync(chemin, CancellationToken.None);

        html.ShouldContain(
            $"<link rel=\"alternate\" hreflang=\"en\" href=\"http://localhost/en{page}\"");
        html.ShouldContain(
            $"<link rel=\"alternate\" hreflang=\"fr\" href=\"http://localhost/fr{page}\"");
    }

    [Theory]
    [InlineData("/en/games/x-moon", "/games/x-moon")]
    [InlineData("/fr/games/x-moon", "/games/x-moon")]
    public async Task PageIndexable_DesigneLAnglaisCommeVersionParDefaut(string chemin, string page)
    {
        // x-default répond au visiteur dont la langue n'est ni l'une ni l'autre.
        var html = await CreateClient().GetStringAsync(chemin, CancellationToken.None);

        html.ShouldContain(
            $"<link rel=\"alternate\" hreflang=\"x-default\" href=\"http://localhost/en{page}\"");
    }

    [Fact]
    public async Task PageIndexable_AnnonceSaLangueEtCellesQuiLaDoublent()
    {
        var html = await CreateClient().GetStringAsync("/fr/games/x-moon", CancellationToken.None);

        html.ShouldContain("property=\"og:locale\" content=\"fr\"");
        html.ShouldContain("property=\"og:locale:alternate\" content=\"en\"");
    }

    [Fact]
    public async Task PageDeJeu_NeDeclareAucuneAlternative()
    {
        // La page de jeu porte déjà noindex : lui donner des alternatives reviendrait à
        // demander leur indexation à celles-là mêmes qu'on exclut.
        var html = await CreateClient().GetStringAsync("/en/games/x-moon/play", CancellationToken.None);

        html.ShouldNotContain("rel=\"alternate\"");
    }
}
