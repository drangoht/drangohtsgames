using System.Text.Json;
using Microsoft.Extensions.Options;
using Refit;

namespace DrangohtGames.Web.Games.ItchIo;

/// <summary>
/// Branche le client Refit vers itch.io et l'enveloppe de sa couche anti-corruption.
/// </summary>
/// <remarks>
/// Les réglages de sérialisation vivent ici et non dans le composition root : le format de
/// date non standard d'itch.io est une connaissance du contrat externe, elle n'a pas à
/// remonter dans <c>Program.cs</c>. Les tests réutilisent <see cref="Settings"/> pour
/// éprouver le même câblage que la production.
/// </remarks>
internal static class ItchIoRefit
{
    /// <summary>Réglages Refit du contrat itch.io.</summary>
    public static RefitSettings Settings { get; } =
        new(new SystemTextJsonContentSerializer(CreateSerializerOptions()));

    /// <summary>Enregistre l'appel HTTP à itch.io et le catalogue qui le traduit.</summary>
    public static IServiceCollection AddItchIoCatalog(this IServiceCollection services)
    {
        // AddRefitGeneratedClient, et non AddRefitClient : le second résout un constructeur
        // de requêtes par réflexion, absent du paquet depuis Refit 15. L'implémentation
        // générée à la compilation est la seule disponible ici.
        services.AddRefitGeneratedClient<IItchIoApi>(Settings)
            .ConfigureHttpClient((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ItchIoOptions>>().Value;

                client.BaseAddress = new Uri("https://itch.io/");
                client.Timeout = options.Timeout;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DrangohtGames/1.0 (+https://github.com/drangoht)");
            })
            // Reprise avec back-off et jitter, plus un disjoncteur : une lecture est
            // idempotente, la retenter est sans risque.
            .AddStandardResilienceHandler();

        services.AddTransient<IItchIoClient, ItchIoClient>();

        return services;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ItchIoDateTimeOffsetConverter());
        return options;
    }
}
