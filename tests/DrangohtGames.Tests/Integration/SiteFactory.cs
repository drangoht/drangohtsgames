using DrangohtGames.Tests.Builders;
using DrangohtGames.Tests.Fakes;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.ItchIo;
using DrangohtGames.Web.Games.SelfHosted;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DrangohtGames.Tests.Integration;

/// <summary>
/// Monte le site complet en mémoire, avec itch.io remplacé par une doublure.
/// </summary>
/// <remarks>
/// Ces tests vérifient le câblage — routage, localisation, codes de statut, antiforgery —
/// pas les règles métier, déjà couvertes en unitaire.
/// </remarks>
internal sealed class SiteFactory : WebApplicationFactory<Program>
{
    private readonly string _snapshotDirectory =
        Path.Combine(Path.GetTempPath(), "drangohtgames-integration", Guid.NewGuid().ToString("N"));

    public static Game SampleGame { get; } = new GameBuilder()
        .WithId(42)
        .WithSlug("x-moon")
        .WithTitle("X-Moon")
        .WithTags("Arcade")
        .WithEngine("Unity")
        .WithPlatforms(GamePlatforms.Windows | GamePlatforms.Linux)
        .WithCoverUrl("https://img.itch.zone/x-moon-cover.png")
        .WithScreenshots("https://img.itch.zone/x-moon-shot-1.png")
        .Build();

    /// <summary>Un jeu publié sur itch.io dont le build n'est pas hébergé ici (ADR 0007).</summary>
    public static Game GameWithoutBuild { get; } = new GameBuilder()
        .WithId(43)
        .WithSlug("y-sun")
        .WithTitle("Y-Sun")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Production);

        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ItchIo:ApiKey"] = "cle-de-test",
                ["ItchIo:Currency"] = "USD",
                ["Snapshot:Directory"] = _snapshotDirectory,
                ["Site:Name"] = "Drangoht Games",
            }));

        builder.ConfigureServices(services =>
        {
            // Aucun appel réseau ne doit partir d'une suite de tests.
            services.RemoveAll<IItchIoClient>();
            services.AddSingleton<IItchIoClient>(_ =>
                FakeItchIoClient.Returning(SampleGame, GameWithoutBuild));

            // Aucun build n'est téléchargé pendant la suite : on déclare ce que le disque
            // porterait, pour que le routage de la page de jeu soit éprouvé quand même.
            services.RemoveAll<SelfHostedGames>();
            services.AddSingleton(SelfHostedGames.For([SampleGame.Slug.Value]));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_snapshotDirectory))
        {
            Directory.Delete(_snapshotDirectory, recursive: true);
        }
    }
}
