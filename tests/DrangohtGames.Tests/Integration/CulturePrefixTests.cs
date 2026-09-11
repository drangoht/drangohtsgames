using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace DrangohtGames.Tests.Integration;

/// <summary>
/// La langue vit dans le chemin (ADR 0008) : ce que devient chaque forme d'adresse.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CulturePrefixTests : IClassFixture<SiteFactoryFixture>
{
    private readonly SiteFactory _factory;

    public CulturePrefixTests(SiteFactoryFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _factory = fixture.Factory;
    }

    private HttpClient CreateClient(string acceptLanguage = "en")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        return client;
    }

    [Theory]
    [InlineData("/fr/", "fr")]
    [InlineData("/en/", "en")]
    [InlineData("/fr/about", "fr")]
    [InlineData("/en/about", "en")]
    [InlineData("/fr/games/x-moon", "fr")]
    [InlineData("/en/games/x-moon", "en")]
    public async Task PagePrefixee_EstServieDansLaLangueDeSonChemin(string chemin, string langue)
    {
        // L'en-tête du navigateur dit l'inverse du chemin : c'est le chemin qui doit gagner.
        var contraire = langue == "fr" ? "en" : "fr";

        using var response = await CreateClient(contraire).GetAsync(chemin, CancellationToken.None);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(CancellationToken.None);
        html.ShouldContain($"lang=\"{langue}\"");
    }

    [Theory]
    [InlineData("/games/x-moon", "/en/games/x-moon")]
    [InlineData("/about", "/en/about")]
    [InlineData("/games/x-moon/play", "/en/games/x-moon/play")]
    public async Task AdresseSansPrefixe_RedirigeDefinitivementVersLAnglais(
        string ancienne,
        string attendue)
    {
        // Ces adresses ont été publiées avant l'ADR 0008 : une redirection permanente
        // transmet ce qu'elles ont acquis, sans dépendre de la langue du visiteur.
        using var response = await CreateClient("fr").GetAsync(ancienne, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.ToString().ShouldBe(attendue);
    }

    [Theory]
    [InlineData("fr-FR,fr;q=0.9", "/fr/")]
    [InlineData("de-DE", "/en/")]
    [InlineData("", "/en/")]
    public async Task Racine_OrienteLeVisiteurVersSaLangue(string acceptLanguage, string attendue)
    {
        // La racine est la seule adresse dont la destination dépend du visiteur : la
        // redirection y est temporaire, sans quoi le navigateur la figerait pour tous.
        using var response = await CreateClient(acceptLanguage).GetAsync("/", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.ToString().ShouldBe(attendue);
    }

    [Fact]
    public async Task AdresseSansPrefixe_ConserveSaChaineDeRequete()
    {
        using var response = await CreateClient().GetAsync("/?tag=Arcade", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.ToString().ShouldBe("/en/?tag=Arcade");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/robots.txt")]
    [InlineData("/sitemap.xml")]
    public async Task RessourceSansLangue_ResteAccessibleSansPrefixe(string chemin)
    {
        // Ce que ces adresses servent ne dépend d'aucune langue, et des tiers les
        // connaissent déjà — les préfixer les casserait sans rien apporter.
        using var response = await CreateClient().GetAsync(chemin, CancellationToken.None);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task LangueInconnueDansLeChemin_NeSeFaitPasPasserPourUneLangue()
    {
        // « /de/about » n'est pas une page : le site ne publie pas l'allemand, et cette
        // adresse ne doit pas répondre en anglais comme si de rien n'était.
        using var response = await CreateClient().GetAsync("/de/about", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/en/")]
    [InlineData("/fr/")]
    public async Task PagePrefixee_ServitSesRessourcesCommeSansPrefixe(string chemin)
    {
        // Les feuilles de style sont référencées relativement à <base> : sous un préfixe de
        // langue, le navigateur les demande préfixées. Le préfixe ne doit rien changer à ce
        // qu'elles répondent, sans quoi le site s'afficherait sans style — et aucun test de
        // contenu ne le verrait.
        var client = CreateClient();
        var html = await client.GetStringAsync(chemin, CancellationToken.None);
        var feuilles = StylesheetHrefs(html).ToList();

        feuilles.ShouldNotBeEmpty();

        foreach (var feuille in feuilles)
        {
            using var prefixee = await client.GetAsync(
                new Uri(new Uri(new Uri("http://localhost"), chemin), feuille),
                CancellationToken.None);
            using var nue = await client.GetAsync(
                new Uri(new Uri("http://localhost/"), feuille),
                CancellationToken.None);

            prefixee.StatusCode.ShouldBe(nue.StatusCode, feuille);
        }

        // Une comparaison ne prouve rien si les deux côtés échouent : au moins la feuille
        // du site répond, et elle répond sous le préfixe.
        using var appCss = await client.GetAsync($"{chemin}app.css", CancellationToken.None);
        appCss.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static IEnumerable<string> StylesheetHrefs(string html)
    {
        const string marker = "<link rel=\"stylesheet\" href=\"";

        for (var start = html.IndexOf(marker, StringComparison.Ordinal);
             start >= 0;
             start = html.IndexOf(marker, start + 1, StringComparison.Ordinal))
        {
            var value = start + marker.Length;
            yield return html[value..html.IndexOf('"', value)];
        }
    }

    [Fact]
    public async Task SelecteurDeLangue_RenvoieVersLaMemePageDansLAutreLangue()
    {
        var html = await CreateClient().GetStringAsync("/fr/games/x-moon", CancellationToken.None);

        // Changer de langue, c'est changer de base : ces liens-là sont absolus, et mènent
        // à la page équivalente plutôt qu'à l'accueil.
        html.ShouldContain("href=\"/en/games/x-moon\"");
    }

    [Fact]
    public async Task SelecteurDeLangue_DepuisLAccueilFiltre_ConserveLesCriteres()
    {
        var html = await CreateClient()
            .GetStringAsync("/fr/?tag=Arcade", CancellationToken.None);

        html.ShouldContain("href=\"/en/?tag=Arcade\"");
    }

    [Fact]
    public async Task PagePrefixee_PorteDesLiensInternesQuiRestentDansSaLangue()
    {
        var html = await CreateClient("en")
            .GetStringAsync("/fr/games/x-moon", CancellationToken.None);

        // Le préfixe est porté par <base>, donc les liens internes sont relatifs. Un
        // « href="/… » absolu échapperait à la base et ramènerait le visiteur en anglais.
        html.ShouldContain("<base href=\"/fr/\"");
        html.ShouldNotContain("href=\"/games/");
        html.ShouldNotContain("href=\"/about\"");
    }
}
