using DrangohtGames.Tests.Builders;
using DrangohtGames.Tests.Fakes;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.Editorial;
using DrangohtGames.Web.Games.ItchIo;
using DrangohtGames.Web.Games.SelfHosted;
using DrangohtGames.Web.Games.Snapshots;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DrangohtGames.Tests.Games;

public sealed class GameCatalogTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "drangohtgames-tests", Guid.NewGuid().ToString("N"));

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private GameCatalogSnapshotStore CreateSnapshotStore() =>
        new(
            Options.Create(new SnapshotOptions { Directory = _directory }),
            NullLogger<GameCatalogSnapshotStore>.Instance);

    private GameCatalog CreateCatalog(
        IItchIoClient client,
        EditorialCatalog? editorial = null,
        GameCatalogSnapshotStore? snapshotStore = null,
        SelfHostedGames? selfHosted = null) =>
        new(
            client,
            editorial ?? EditorialCatalog.Empty,
            selfHosted ?? SelfHostedGames.None,
            snapshotStore ?? CreateSnapshotStore(),
            _cache,
            Options.Create(new ItchIoOptions { ApiKey = "cle", CacheDuration = TimeSpan.FromMinutes(30) }),
            NullLogger<GameCatalog>.Instance);

    [Fact]
    public async Task GetGamesAsync_RetourneLesJeuxDeLApi()
    {
        var catalog = CreateCatalog(
            FakeItchIoClient.Returning(new GameBuilder().WithSlug("x-moon").Build()));

        var games = await catalog.GetGamesAsync(CancellationToken.None);

        games.ShouldHaveSingleItem().Slug.Value.ShouldBe("x-moon");
    }

    [Fact]
    public async Task GetGamesAsync_AppliqueLeContenuEditorial()
    {
        var editorial = EditorialCatalog.FromJson("""
            { "games": { "x-moon": { "engine": "Unity", "tags": ["Shmup"] } } }
            """);
        var catalog = CreateCatalog(
            FakeItchIoClient.Returning(new GameBuilder().WithSlug("x-moon").Build()),
            editorial);

        var game = (await catalog.GetGamesAsync(CancellationToken.None)).ShouldHaveSingleItem();

        game.Engine.ShouldBe("Unity");
        game.Tags.ShouldBe(["Shmup"]);
    }

    [Fact]
    public async Task GetGamesAsync_MarqueLesJeuxDontLeBuildEstPresent()
    {
        var catalog = CreateCatalog(
            FakeItchIoClient.Returning(
                new GameBuilder().WithSlug("x-moon").Build(),
                new GameBuilder().WithSlug("y-sun").WithId(2).Build()),
            selfHosted: SelfHostedGames.For(["x-moon"]));

        var games = await catalog.GetGamesAsync(CancellationToken.None);

        games.Single(game => game.Slug.Value == "x-moon").IsSelfHosted.ShouldBeTrue();
        games.Single(game => game.Slug.Value == "y-sun").IsSelfHosted.ShouldBeFalse();
    }

    [Fact]
    public async Task GetGamesAsync_NInterrogeLApiQuUneFoisPendantLaDureeDeCache()
    {
        var client = FakeItchIoClient.Returning(new GameBuilder().Build());
        var catalog = CreateCatalog(client);

        await catalog.GetGamesAsync(CancellationToken.None);
        await catalog.GetGamesAsync(CancellationToken.None);

        client.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetGamesAsync_EnregistreUnInstantaneApresUnAppelReussi()
    {
        var snapshotStore = CreateSnapshotStore();
        var catalog = CreateCatalog(
            FakeItchIoClient.Returning(new GameBuilder().WithSlug("x-moon").Build()),
            snapshotStore: snapshotStore);

        await catalog.GetGamesAsync(CancellationToken.None);

        var snapshot = await snapshotStore.LoadAsync(CancellationToken.None);
        snapshot.ShouldNotBeNull().ShouldHaveSingleItem().Slug.Value.ShouldBe("x-moon");
    }

    [Fact]
    public async Task GetGamesAsync_QuandLApiEchoue_SertLeDernierInstantane()
    {
        var snapshotStore = CreateSnapshotStore();
        await snapshotStore.SaveAsync(
            [new GameBuilder().WithSlug("x-moon").WithTitle("X-Moon").Build()],
            CancellationToken.None);

        var catalog = CreateCatalog(FakeItchIoClient.Failing(), snapshotStore: snapshotStore);

        var games = await catalog.GetGamesAsync(CancellationToken.None);

        games.ShouldHaveSingleItem().Title.ShouldBe("X-Moon");
    }

    [Fact]
    public async Task GetGamesAsync_QuandLApiEchoueSansInstantane_RetourneUneListeVideSansLever()
    {
        var catalog = CreateCatalog(FakeItchIoClient.Failing());

        var games = await catalog.GetGamesAsync(CancellationToken.None);

        games.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetGamesAsync_QuandLApiEchoue_NeMetPasLEchecEnCache()
    {
        var client = FakeItchIoClient.Failing();
        var catalog = CreateCatalog(client);

        await catalog.GetGamesAsync(CancellationToken.None);
        await catalog.GetGamesAsync(CancellationToken.None);

        // Mettre un catalogue vide en cache figerait la panne pour toute la durée du TTL.
        client.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetGamesAsync_AppliqueLeContenuEditorialAussiAuxJeuxVenantDeLInstantane()
    {
        var snapshotStore = CreateSnapshotStore();
        await snapshotStore.SaveAsync([new GameBuilder().WithSlug("x-moon").Build()], CancellationToken.None);

        var editorial = EditorialCatalog.FromJson("""
            { "games": { "x-moon": { "engine": "Unity" } } }
            """);
        var catalog = CreateCatalog(FakeItchIoClient.Failing(), editorial, snapshotStore);

        var game = (await catalog.GetGamesAsync(CancellationToken.None)).ShouldHaveSingleItem();

        game.Engine.ShouldBe("Unity");
    }

    [Fact]
    public async Task GetGamesAsync_QuandLApiRenvoieUnCatalogueVide_NEcrasePasUnInstantanePeuple()
    {
        var snapshotStore = CreateSnapshotStore();
        await snapshotStore.SaveAsync(
            [new GameBuilder().WithSlug("x-moon").Build()],
            CancellationToken.None);

        var catalog = CreateCatalog(FakeItchIoClient.Returning(), snapshotStore: snapshotStore);
        await catalog.GetGamesAsync(CancellationToken.None);

        // Le repli est le dernier filet du site : une réponse vide anormale ne doit pas
        // le détruire au passage.
        var snapshot = await snapshotStore.LoadAsync(CancellationToken.None);
        snapshot.ShouldNotBeNull().ShouldHaveSingleItem().Slug.Value.ShouldBe("x-moon");
    }

    public void Dispose()
    {
        _cache.Dispose();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
