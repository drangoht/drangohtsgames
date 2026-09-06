using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DrangohtGames.Web.Games.Snapshots;

/// <summary>Configuration de l'instantané de repli.</summary>
public sealed class SnapshotOptions
{
    /// <summary>Section de configuration correspondante.</summary>
    public const string SectionName = "Snapshot";

    /// <summary>
    /// Répertoire dans lequel le dernier catalogue réussi est conservé.
    /// </summary>
    /// <remarks>
    /// En conteneur, ce chemin doit pointer vers un volume : sinon l'instantané disparaît à
    /// chaque redéploiement, précisément quand il serait le plus utile.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string Directory { get; init; } = "/var/lib/drangohtgames";
}

/// <summary>
/// Conserve sur disque le dernier catalogue obtenu d'itch.io, pour pouvoir servir le site
/// quand l'API est indisponible.
/// </summary>
/// <remarks>
/// Aucune opération de ce magasin ne lève : un instantané est un confort. Le perdre ne doit
/// jamais dégrader une requête qui, elle, s'est bien passée.
/// </remarks>
public sealed partial class GameCatalogSnapshotStore
{
    private const string FileName = "games-snapshot.json";

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly string _filePath;
    private readonly string _directory;
    private readonly ILogger<GameCatalogSnapshotStore> _logger;

    /// <summary>Construit le magasin sur le répertoire configuré.</summary>
    public GameCatalogSnapshotStore(
        IOptions<SnapshotOptions> options,
        ILogger<GameCatalogSnapshotStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _directory = options.Value.Directory;
        _filePath = Path.Combine(_directory, FileName);
        _logger = logger;
    }

    /// <summary>Enregistre le catalogue comme instantané de repli.</summary>
    public async Task SaveAsync(IReadOnlyList<Game> games, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(games);

        try
        {
            Directory.CreateDirectory(_directory);

            // Écriture puis remplacement : une coupure en cours d'écriture laisserait
            // sinon un instantané tronqué, donc inutilisable au moment critique.
            var temporaryPath = _filePath + ".tmp";

            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, games, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);

            LogSnapshotSaved(_logger, games.Count, _filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ArgumentException or NotSupportedException)
        {
            LogSnapshotSaveFailed(_logger, exception, _filePath);
        }
    }

    /// <summary>
    /// Relit l'instantané, ou retourne <c>null</c> s'il est absent ou illisible.
    /// </summary>
    public async Task<IReadOnlyList<Game>?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);

            var games = await JsonSerializer
                .DeserializeAsync<List<Game>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return games;
        }
        catch (Exception exception) when (exception is JsonException or IOException
                                              or UnauthorizedAccessException or UriFormatException)
        {
            LogSnapshotUnreadable(_logger, exception, _filePath);
            return null;
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new GameSlugJsonConverter());
        options.Converters.Add(new PriceJsonConverter());
        return options;
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug,
        Message = "Instantané de {Count} jeux écrit dans {Path}.")]
    private static partial void LogSnapshotSaved(ILogger logger, int count, string path);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning,
        Message = "Impossible d'écrire l'instantané dans {Path} : le site continue sans repli.")]
    private static partial void LogSnapshotSaveFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "Instantané {Path} illisible : il sera reconstruit au prochain appel réussi.")]
    private static partial void LogSnapshotUnreadable(ILogger logger, Exception exception, string path);
}
