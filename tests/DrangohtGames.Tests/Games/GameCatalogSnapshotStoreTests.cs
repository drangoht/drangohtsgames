using DrangohtGames.Tests.Builders;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DrangohtGames.Tests.Games;

public sealed class GameCatalogSnapshotStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "drangohtgames-tests", Guid.NewGuid().ToString("N"));

    private GameCatalogSnapshotStore CreateStore() =>
        new(
            Options.Create(new SnapshotOptions { Directory = _directory }),
            NullLogger<GameCatalogSnapshotStore>.Instance);

    [Fact]
    public async Task SaveAsync_PuisLoadAsync_RestitueLesJeux()
    {
        var store = CreateStore();
        var games = new[]
        {
            new GameBuilder().WithId(3).WithSlug("x-moon").WithTitle("X-Moon")
                             .WithTags("Arcade").WithEngine("Unity")
                             .WithPrice(Price.FromCents(499, "EUR"))
                             .WithPlatforms(GamePlatforms.Windows | GamePlatforms.Linux)
                             .Build(),
        };

        await store.SaveAsync(games, CancellationToken.None);
        var restored = await store.LoadAsync(CancellationToken.None);

        restored.ShouldNotBeNull();
        var game = restored.ShouldHaveSingleItem();
        game.Id.ShouldBe(3);
        game.Slug.Value.ShouldBe("x-moon");
        game.Title.ShouldBe("X-Moon");
        game.Tags.ShouldBe(["Arcade"]);
        game.Engine.ShouldBe("Unity");
        game.Price.AmountInCents.ShouldBe(499);
        game.Price.Currency.ShouldBe("EUR");
        game.Platforms.ShouldBe(GamePlatforms.Windows | GamePlatforms.Linux);
    }

    [Fact]
    public async Task SaveAsync_CreeLeRepertoireSilNExistePas()
    {
        var store = CreateStore();

        await store.SaveAsync([new GameBuilder().Build()], CancellationToken.None);

        Directory.Exists(_directory).ShouldBeTrue();
    }

    [Fact]
    public async Task LoadAsync_QuandAucunInstantaneNExiste_RetourneNull()
    {
        var store = CreateStore();

        (await store.LoadAsync(CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_QuandLInstantaneEstCorrompu_RetourneNullPlutotQueDeLever()
    {
        var store = CreateStore();
        await store.SaveAsync([new GameBuilder().Build()], CancellationToken.None);
        await File.WriteAllTextAsync(
            Directory.GetFiles(_directory).Single(),
            "{ tronqué",
            CancellationToken.None);

        // Un instantané illisible est un cache perdu, pas une panne : on repart de l'API.
        (await store.LoadAsync(CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task SaveAsync_EcraseLInstantanePrecedent()
    {
        var store = CreateStore();

        await store.SaveAsync([new GameBuilder().WithSlug("premier").Build()], CancellationToken.None);
        await store.SaveAsync([new GameBuilder().WithSlug("second").Build()], CancellationToken.None);

        var restored = await store.LoadAsync(CancellationToken.None);

        restored.ShouldNotBeNull();
        restored.ShouldHaveSingleItem().Slug.Value.ShouldBe("second");
    }

    [Fact]
    public async Task SaveAsync_QuandLeRepertoireEstInaccessible_NeLevePas()
    {
        // Un volume non monté ne doit pas faire tomber une requête qui a, elle, réussi.
        var store = new GameCatalogSnapshotStore(
            Options.Create(new SnapshotOptions { Directory = Path.Combine(_directory, "\0invalide") }),
            NullLogger<GameCatalogSnapshotStore>.Instance);

        await Should.NotThrowAsync(() => store.SaveAsync([new GameBuilder().Build()], CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
