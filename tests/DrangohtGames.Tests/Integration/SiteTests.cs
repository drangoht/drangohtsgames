using System.Net;
using Shouldly;

namespace DrangohtGames.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SiteTests : IClassFixture<SiteFactoryFixture>
{
    private readonly SiteFactory _factory;

    public SiteTests(SiteFactoryFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _factory = fixture.Factory;
    }

    private HttpClient CreateClient(string acceptLanguage = "en")
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);
        return client;
    }

    [Fact]
    public async Task Accueil_ListeLesJeuxDuCatalogue()
    {
        var html = await CreateClient().GetStringAsync("/", CancellationToken.None);

        html.ShouldContain("X-Moon");
        html.ShouldContain("/games/x-moon");
    }

    [Fact]
    public async Task Accueil_QuandLeNavigateurDemandeLeFrancais_RendLaPageEnFrancais()
    {
        var html = await CreateClient("fr-FR,fr;q=0.9").GetStringAsync("/", CancellationToken.None);

        html.ShouldContain("lang=\"fr\"");
        html.ShouldContain("Aller au contenu");
    }

    [Fact]
    public async Task Accueil_ParDefaut_RendLaPageEnAnglais()
    {
        var html = await CreateClient("de-DE").GetStringAsync("/", CancellationToken.None);

        html.ShouldContain("lang=\"en\"");
        html.ShouldContain("Skip to content");
    }

    [Fact]
    public async Task Accueil_NeLaisseFuirAucuneDonneePriveeDeCompte()
    {
        var html = await CreateClient().GetStringAsync("/", CancellationToken.None);

        foreach (var terme in new[] { "earnings", "purchases_count", "downloads_count", "views_count" })
        {
            html.ShouldNotContain(terme, Case.Insensitive);
        }
    }

    [Fact]
    public async Task Accueil_MetEnVitrineUnJeuJouableSansQuitterLeSite()
    {
        var html = await CreateClient().GetStringAsync("/", CancellationToken.None);

        html.ShouldContain("/games/x-moon/play");
    }

    [Fact]
    public async Task Accueil_QuandUnFiltreEstActif_EffaceLaVitrine()
    {
        // Filtrer, c'est chercher : la vitrine deviendrait un doublon au-dessus des résultats.
        var html = await CreateClient().GetStringAsync("/?tag=Arcade", CancellationToken.None);

        html.ShouldNotContain("/games/x-moon/play");
    }

    [Fact]
    public async Task Accueil_NeGardeQueLesJeuxJouablesSurPlaceQuandLUrlLeDemande()
    {
        var html = await CreateClient().GetStringAsync("/?playable=true", CancellationToken.None);

        html.ShouldContain("X-Moon");
        html.ShouldNotContain("Y-Sun");
    }

    [Fact]
    public async Task Accueil_FiltreParTagDepuisLaChaineDeRequete()
    {
        var client = CreateClient();

        (await client.GetStringAsync("/?tag=Arcade", CancellationToken.None)).ShouldContain("X-Moon");
        (await client.GetStringAsync("/?tag=Metroidvania", CancellationToken.None))
            .ShouldContain("No game matches these filters");
    }

    [Fact]
    public async Task Accueil_QuandLaPlateformeDeLUrlEstInvalide_NEchouePas()
    {
        var response = await CreateClient().GetAsync("/?platform=nawak", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FicheJeu_AfficheLeJeuEtSonWidgetItchIo()
    {
        var html = await CreateClient().GetStringAsync("/games/x-moon", CancellationToken.None);

        html.ShouldContain("X-Moon");
        html.ShouldContain("https://itch.io/embed/42");
    }

    [Fact]
    public async Task FicheJeu_QuandLeJeuEstAutoHeberge_RenvoieVersLaPageDeJeu()
    {
        var html = await CreateClient().GetStringAsync("/games/x-moon", CancellationToken.None);

        html.ShouldContain("/games/x-moon/play");
    }

    [Fact]
    public async Task FicheJeu_QuandLeJeuNAPasDeBuild_NeProposePasDYJouer()
    {
        var html = await CreateClient().GetStringAsync("/games/y-sun", CancellationToken.None);

        html.ShouldNotContain("/games/y-sun/play");
        html.ShouldContain("https://drangoht.itch.io/y-sun");
    }

    [Fact]
    public async Task PageDeJeu_QuandLeJeuEstAutoHeberge_EncadreSonBuild()
    {
        var html = await CreateClient().GetStringAsync("/games/x-moon/play", CancellationToken.None);

        html.ShouldContain("/play/x-moon/index.html");
        html.ShouldContain("X-Moon");
    }

    [Fact]
    public async Task PageDeJeu_QuandLeJeuNAPasDeBuild_Repond404()
    {
        // Rien à encadrer : mieux vaut un 404 qu'un cadre vide indexé par les moteurs.
        var response = await CreateClient().GetAsync("/games/y-sun/play", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PageDeJeu_QuandLeSlugEstInconnu_Repond404()
    {
        var response = await CreateClient().GetAsync("/games/jeu-fantome/play", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FicheJeu_QuandLeSlugEstInconnu_Repond404()
    {
        var response = await CreateClient().GetAsync("/games/jeu-fantome", CancellationToken.None);

        // Un 200 ferait indexer des pages fantômes par les moteurs de recherche.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RouteInconnue_Repond404()
    {
        var response = await CreateClient().GetAsync("/nimporte-quoi", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_RepondSansDependreDItchIo()
    {
        var response = await CreateClient().GetAsync("/health", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task APropos_EstAccessible()
    {
        var response = await CreateClient().GetAsync("/about", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Culture_SansJetonAntiforgery_EstRefuse()
    {
        var response = await CreateClient().PostAsync(
            "/culture",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("culture", "fr")]),
            CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Culture_MemoriseLaLangueChoisieEtRevientSurLaPageDOrigine()
    {
        var client = CreateClient();
        var token = ExtractAntiforgeryToken(await client.GetStringAsync("/", CancellationToken.None));

        var response = await client.PostAsync("/culture", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("culture", "fr"),
            new KeyValuePair<string, string>("redirectUri", "/about"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]), CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.ToString().ShouldBe("/about");
        response.Headers.GetValues("Set-Cookie")
                .ShouldContain(cookie => cookie.Contains(".AspNetCore.Culture", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Culture_QuandLaLangueNEstPasPubliee_EstRefusee()
    {
        var client = CreateClient();
        var token = ExtractAntiforgeryToken(await client.GetStringAsync("/", CancellationToken.None));

        var response = await client.PostAsync("/culture", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("culture", "de"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]), CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Culture_RefuseDeRedirigerVersUnSiteExterne()
    {
        var client = CreateClient();
        var token = ExtractAntiforgeryToken(await client.GetStringAsync("/", CancellationToken.None));

        var response = await client.PostAsync("/culture", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("culture", "fr"),
            new KeyValuePair<string, string>("redirectUri", "https://exemple-malveillant.test/piege"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]), CancellationToken.None);

        response.Headers.Location!.ToString().ShouldBe("/");
    }

    [Fact]
    public async Task Reponses_PortentLesEntetesDeSecuriteAttendus()
    {
        var response = await CreateClient().GetAsync("/", CancellationToken.None);

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.Contains("Referrer-Policy").ShouldBeTrue();

        // SAMEORIGIN et non DENY depuis l'ADR 0007 : la page de jeu encadre le build servi
        // par le site, et DENY l'interdit même de même origine. Un tiers reste bloqué.
        response.Headers.GetValues("X-Frame-Options").ShouldContain("SAMEORIGIN");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/games/x-moon")]
    [InlineData("/about")]
    public async Task LesPages_RepondentAUneRequeteHead(string chemin)
    {
        // Les services de supervision sondent en HEAD : une page qui n'y répond pas
        // passe pour hors ligne alors qu'elle est servie normalement en GET.
        using var requete = new HttpRequestMessage(HttpMethod.Head, chemin);

        using var response = await CreateClient().SendAsync(requete, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);

        start.ShouldBeGreaterThan(-1, "la page doit porter un jeton antiforgery");
        start += marker.Length;

        return html[start..html.IndexOf('"', start)];
    }
}

/// <summary>Partage une seule instance du site entre les tests de la classe.</summary>
public sealed class SiteFactoryFixture : IDisposable
{
    internal SiteFactory Factory { get; } = new();

    public void Dispose() => Factory.Dispose();
}
