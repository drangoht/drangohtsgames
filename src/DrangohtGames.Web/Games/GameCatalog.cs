using System.Text.Json;
using DrangohtGames.Web.Games.Editorial;
using DrangohtGames.Web.Games.ItchIo;
using DrangohtGames.Web.Games.SelfHosted;
using DrangohtGames.Web.Games.Snapshots;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DrangohtGames.Web.Games;

/// <summary>
/// Fournit les jeux du site : données itch.io mises en cache, complétées par le contenu
/// éditorial local, avec repli sur le dernier instantané connu quand l'API est indisponible.
/// </summary>
/// <remarks>
/// Le contenu éditorial et le recensement des builds auto-hébergés sont appliqués
/// <em>en sortie</em>, jamais avant la mise en cache ni avant l'écriture de l'instantané :
/// corriger une description ou ajouter un jeu jouable prend ainsi effet immédiatement,
/// même si itch.io est injoignable.
/// </remarks>
public sealed partial class GameCatalog : IGameCatalog, IDisposable
{
    private const string CacheKey = "games:published";

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly IItchIoClient _client;
    private readonly EditorialCatalog _editorial;
    private readonly SelfHostedGames _selfHosted;
    private readonly GameCatalogSnapshotStore _snapshotStore;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration;
    private readonly ILogger<GameCatalog> _logger;

    /// <summary>Construit le catalogue.</summary>
    public GameCatalog(
        IItchIoClient client,
        EditorialCatalog editorial,
        SelfHostedGames selfHosted,
        GameCatalogSnapshotStore snapshotStore,
        IMemoryCache cache,
        IOptions<ItchIoOptions> options,
        ILogger<GameCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(editorial);
        ArgumentNullException.ThrowIfNull(selfHosted);
        ArgumentNullException.ThrowIfNull(snapshotStore);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _editorial = editorial;
        _selfHosted = selfHosted;
        _snapshotStore = snapshotStore;
        _cache = cache;
        _cacheDuration = options.Value.CacheDuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken cancellationToken)
    {
        var games = await GetOrRefreshAsync(cancellationToken).ConfigureAwait(false);

        return [.. games.Select(_editorial.Apply).Select(_selfHosted.Apply)];
    }

    /// <inheritdoc />
    public void Dispose() => _refreshGate.Dispose();

    private async Task<IReadOnlyList<Game>> GetOrRefreshAsync(CancellationToken cancellationToken)
    {
        if (TryGetCached(out var cached))
        {
            return cached;
        }

        // Un démarrage à froid sous trafic déclencherait autant d'appels à itch.io que de
        // requêtes simultanées : une seule les rafraîchit, les autres attendent le résultat.
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (TryGetCached(out cached))
            {
                return cached;
            }

            return await FetchAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private bool TryGetCached(out IReadOnlyList<Game> games)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<Game>? cached) && cached is not null)
        {
            games = cached;
            return true;
        }

        games = [];
        return false;
    }

    private async Task<IReadOnlyList<Game>> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var games = await _client.GetPublishedGamesAsync(cancellationToken).ConfigureAwait(false);

            _cache.Set(CacheKey, games, _cacheDuration);

            if (games.Count > 0)
            {
                await _snapshotStore.SaveAsync(games, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Défense en profondeur : un catalogue vide ne remplace pas un instantané
                // peuplé. Le repli est le dernier filet du site quand itch.io est en panne.
                LogEmptyCatalogNotSnapshotted(_logger);
            }

            return games;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Le visiteur est parti : ce n'est pas une panne d'itch.io.
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
                                              or OperationCanceledException
                                              or JsonException
                                              or ItchIoApiException)
        {
            LogFetchFailed(_logger, exception);

            // L'échec n'est délibérément pas mis en cache : le figer pour toute la durée du
            // TTL transformerait une indisponibilité passagère en panne de trente minutes.
            var snapshot = await _snapshotStore.LoadAsync(cancellationToken).ConfigureAwait(false);

            if (snapshot is null)
            {
                LogNoSnapshotAvailable(_logger);
                return [];
            }

            LogServedFromSnapshot(_logger, snapshot.Count);
            return snapshot;
        }
    }

    [LoggerMessage(EventId = 3000, Level = LogLevel.Error,
        Message = "Échec de la récupération des jeux depuis itch.io.")]
    private static partial void LogFetchFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "itch.io est indisponible : {Count} jeux servis depuis le dernier instantané.")]
    private static partial void LogServedFromSnapshot(ILogger logger, int count);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning,
        Message = "itch.io n'a renvoyé aucun jeu : l'instantané précédent est conservé.")]
    private static partial void LogEmptyCatalogNotSnapshotted(ILogger logger);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Critical,
        Message = "itch.io est indisponible et aucun instantané n'existe : le catalogue est vide.")]
    private static partial void LogNoSnapshotAvailable(ILogger logger);
}
