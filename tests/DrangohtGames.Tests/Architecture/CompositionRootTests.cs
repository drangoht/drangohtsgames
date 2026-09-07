using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.ItchIo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace DrangohtGames.Tests.Architecture;

/// <summary>
/// Le composition root doit produire un graphe résolvable, sans aucune doublure.
/// </summary>
/// <remarks>
/// Les tests d'intégration remplacent <see cref="IItchIoClient"/> par un fake pour qu'aucun
/// appel réseau ne parte de la suite. Ce faisant, ils ne construisent jamais le client HTTP
/// réel : une erreur de câblage y reste invisible jusqu'à la première visite en production.
/// Ces tests résolvent les services tels que <c>Program.cs</c> les enregistre — ils
/// construisent le client, ils ne s'en servent pas, donc rien ne part sur le réseau.
/// </remarks>
public sealed class CompositionRootTests : IDisposable
{
    private readonly string _snapshotDirectory =
        Path.Combine(Path.GetTempPath(), "drangohtgames-composition", Guid.NewGuid().ToString("N"));

    private readonly WebApplicationFactory<Program> _factory;

    public CompositionRootTests() => _factory = CreateFactory(_snapshotDirectory);

    [Fact]
    public void LeClientItchIo_EstConstructibleAvecSonCablageReel()
    {
        using var scope = _factory.Services.CreateScope();

        // Construire ItchIoClient force la résolution du client Refit sous-jacent : c'est
        // précisément là qu'un mauvais mode d'enregistrement se manifeste.
        var client = scope.ServiceProvider.GetRequiredService<IItchIoClient>();

        client.ShouldNotBeNull();
    }

    [Fact]
    public void LeCatalogue_EstConstructibleAvecSonCablageReel()
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IGameCatalog>().ShouldNotBeNull();
    }

    public void Dispose()
    {
        _factory.Dispose();

        if (Directory.Exists(_snapshotDirectory))
        {
            Directory.Delete(_snapshotDirectory, recursive: true);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string snapshotDirectory) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);

            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ItchIo:ApiKey"] = "cle-de-test",
                    ["Snapshot:Directory"] = snapshotDirectory,
                    ["Site:Name"] = "Drangoht Games",
                }));
        });
}
