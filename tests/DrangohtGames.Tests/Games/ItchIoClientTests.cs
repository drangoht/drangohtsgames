using System.Net;
using DrangohtGames.Tests.Fakes;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.ItchIo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refit;
using Shouldly;

namespace DrangohtGames.Tests.Games;

public sealed class ItchIoClientTests
{
    // Extrait fidèle de la réponse documentée de GET /api/1/KEY/my-games,
    // compteurs privés inclus : c'est précisément ce qui ne doit pas ressortir.
    private const string ReponseItchIo = """
    {
      "games": [
        {
          "cover_url": "https://img.itch.zone/cover/x-moon.png",
          "created_at": "2016-02-18 20:16:00",
          "downloads_count": 109,
          "earnings": [ { "currency": "USD", "amount_formatted": "$50.47", "amount": 5047 } ],
          "id": 3,
          "min_price": 0,
          "p_android": false,
          "p_linux": true,
          "p_osx": true,
          "p_windows": true,
          "published": true,
          "published_at": "2016-02-18 20:16:00",
          "purchases_count": 2,
          "short_text": "Made for a game jam",
          "title": "X-Moon",
          "type": "default",
          "url": "https://drangoht.itch.io/x-moon",
          "views_count": 84
        },
        {
          "cover_url": null,
          "created_at": "2020-01-05 09:00:00",
          "id": 7,
          "min_price": 499,
          "p_android": false,
          "p_linux": false,
          "p_osx": false,
          "p_windows": true,
          "published": false,
          "published_at": null,
          "short_text": "Still cooking",
          "title": "Brouillon",
          "type": "default",
          "url": "https://drangoht.itch.io/brouillon",
          "views_count": 3
        },
        {
          "cover_url": "https://img.itch.zone/cover/web-runner.png",
          "created_at": "2023-07-01 12:30:00",
          "id": 11,
          "min_price": 0,
          "p_android": false,
          "p_linux": false,
          "p_osx": false,
          "p_windows": false,
          "published": true,
          "published_at": "2023-07-02 08:00:00",
          "short_text": "Playable in browser",
          "title": "Web Runner",
          "type": "html",
          "url": "https://drangoht.itch.io/web-runner"
        }
      ]
    }
    """;

    // On branche le vrai client Refit sur un handler de test : la route, l'en-tête
    // d'autorisation et la sérialisation traversés ici sont exactement ceux de production.
    private static ItchIoClient CreateClient(StubHttpMessageHandler handler, string apiKey = "cle-secrete")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://itch.io/") };
        var api = RestService.For<IItchIoApi>(http, ItchIoRefit.Settings);
        var options = Options.Create(new ItchIoOptions { ApiKey = apiKey, Currency = "USD" });

        return new ItchIoClient(api, options, NullLogger<ItchIoClient>.Instance);
    }

    [Fact]
    public async Task GetPublishedGamesAsync_EcarteLesJeuxNonPublies()
    {
        var client = CreateClient(StubHttpMessageHandler.ReturningJson(ReponseItchIo));

        var games = await client.GetPublishedGamesAsync(CancellationToken.None);

        games.Select(g => g.Title).ShouldBe(["Web Runner", "X-Moon"]);
    }

    [Fact]
    public async Task GetPublishedGamesAsync_TrieDuPlusRecentAuPlusAncien()
    {
        var client = CreateClient(StubHttpMessageHandler.ReturningJson(ReponseItchIo));

        var games = await client.GetPublishedGamesAsync(CancellationToken.None);

        games[0].PublishedAt!.Value.ShouldBeGreaterThan(games[1].PublishedAt!.Value);
    }

    [Fact]
    public async Task GetPublishedGamesAsync_TraduitLeFormatDeDateNonIso8601DItchIoEnUtc()
    {
        var client = CreateClient(StubHttpMessageHandler.ReturningJson(ReponseItchIo));

        var xMoon = (await client.GetPublishedGamesAsync(CancellationToken.None))
            .Single(g => g.Id == 3);

        xMoon.PublishedAt.ShouldBe(new DateTimeOffset(2016, 2, 18, 20, 16, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task GetPublishedGamesAsync_AgregeLesDrapeauxDePlateforme()
    {
        var client = CreateClient(StubHttpMessageHandler.ReturningJson(ReponseItchIo));

        var games = await client.GetPublishedGamesAsync(CancellationToken.None);

        games.Single(g => g.Id == 3).Platforms
             .ShouldBe(GamePlatforms.Windows | GamePlatforms.Linux | GamePlatforms.MacOs);
        games.Single(g => g.Id == 11).Platforms.ShouldBe(GamePlatforms.None);
    }

    [Fact]
    public async Task GetPublishedGamesAsync_MarqueEmbarquablesLesJeuxDeTypeHtml()
    {
        var client = CreateClient(StubHttpMessageHandler.ReturningJson(ReponseItchIo));

        var games = await client.GetPublishedGamesAsync(CancellationToken.None);

        games.Single(g => g.Id == 11).IsEmbeddable.ShouldBeTrue();
        games.Single(g => g.Id == 3).IsEmbeddable.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPublishedGamesAsync_ConvertitMinPriceEnPrix()
    {
        var client = CreateClient(StubHttpMessageHandler.ReturningJson(ReponseItchIo));

        var games = await client.GetPublishedGamesAsync(CancellationToken.None);

        games.Single(g => g.Id == 3).Price.IsFree.ShouldBeTrue();
    }

    [Fact]
    public async Task GetPublishedGamesAsync_DeriveLeSlugDeLUrlItchIo()
    {
        var client = CreateClient(StubHttpMessageHandler.ReturningJson(ReponseItchIo));

        var games = await client.GetPublishedGamesAsync(CancellationToken.None);

        games.Single(g => g.Id == 11).Slug.Value.ShouldBe("web-runner");
    }

    [Fact]
    public async Task GetPublishedGamesAsync_EnvoieLaCleApiDansLEnteteAuthorizationEtNonDansLUrl()
    {
        var handler = StubHttpMessageHandler.ReturningJson(ReponseItchIo);
        var client = CreateClient(handler, apiKey: "cle-secrete");

        await client.GetPublishedGamesAsync(CancellationToken.None);

        var request = handler.Requests.Single();
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("cle-secrete");

        // Une clé placée dans le chemin finirait dans les journaux d'accès et les traces.
        request.RequestUri!.ToString().ShouldNotContain("cle-secrete");
    }

    [Fact]
    public async Task GetPublishedGamesAsync_QuandLaCleEstRefusee_LeveItchIoApiException()
    {
        // itch.io ne répond pas 401 sur une clé invalide : il renvoie 200 avec un corps
        // d'erreur. Traité comme un succès, cela produirait un catalogue vide qui écraserait
        // l'instantané de repli.
        var client = CreateClient(
            StubHttpMessageHandler.ReturningJson("""{"errors":["invalid key"]}"""));

        var exception = await Should.ThrowAsync<ItchIoApiException>(
            () => client.GetPublishedGamesAsync(CancellationToken.None));

        exception.Message.ShouldContain("invalid key");
    }

    [Fact]
    public async Task GetPublishedGamesAsync_QuandLeCompteNaAucunJeu_RetourneUneListeVide()
    {
        var client = CreateClient(StubHttpMessageHandler.ReturningJson("""{"games":[]}"""));

        (await client.GetPublishedGamesAsync(CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPublishedGamesAsync_QuandLApiRepondUneErreur_LeveHttpRequestException()
    {
        // Le port promet HttpRequestException. Refit lève son propre ApiException : laisser
        // ce type remonter romprait le repli sur instantané, qui ne guette pas Refit.
        var client = CreateClient(StubHttpMessageHandler.ReturningStatus(HttpStatusCode.Unauthorized));

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => client.GetPublishedGamesAsync(CancellationToken.None));

        exception.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
