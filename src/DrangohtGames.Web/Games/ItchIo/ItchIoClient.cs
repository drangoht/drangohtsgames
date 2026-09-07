using Microsoft.Extensions.Options;
using Refit;

namespace DrangohtGames.Web.Games.ItchIo;

/// <summary>
/// Traduit l'API itch.io en jeux du site.
/// </summary>
/// <remarks>
/// C'est la seule classe du projet qui connaît le vocabulaire d'itch.io. Elle constitue la
/// couche anti-corruption : au-delà, on ne manipule plus que des <see cref="Game"/>. Le
/// transport est délégué à <see cref="IItchIoApi"/> — ici, plus aucun appel réseau, que des
/// décisions.
/// </remarks>
internal sealed partial class ItchIoClient : IItchIoClient
{
    private readonly IItchIoApi _api;
    private readonly ItchIoOptions _options;
    private readonly ILogger<ItchIoClient> _logger;

    /// <summary>Construit le traducteur sur le contrat HTTP d'itch.io.</summary>
    public ItchIoClient(IItchIoApi api, IOptions<ItchIoOptions> options, ILogger<ItchIoClient> logger)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Game>> GetPublishedGamesAsync(CancellationToken cancellationToken)
    {
        ItchIoGamesResponse payload;

        try
        {
            payload = await _api.GetMyGamesAsync(_options.ApiKey, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException exception)
        {
            // Refit signale les codes HTTP d'erreur par son propre type, qui ne dérive pas de
            // HttpRequestException. Le laisser remonter ferait sortir le SDK de ce dossier et
            // priverait le catalogue de son repli sur instantané, qui guette ce type-là.
            throw new HttpRequestException(exception.Message, exception, exception.StatusCode);
        }

        // itch.io renvoie ses erreurs applicatives dans un corps 200 : sans ce contrôle,
        // une clé révoquée se lirait comme un compte sans jeu et effacerait l'instantané.
        if (payload.Errors.Count > 0)
        {
            throw new ItchIoApiException(payload.Errors);
        }

        var games = payload.Games
            .Where(IsPubliclyListable)
            .Select(ToGame)
            .OrderByDescending(game => game.PublishedAt)
            .ToArray();

        LogGamesFetched(_logger, payload.Games.Count, games.Length);

        return games;
    }

    /// <summary>
    /// Un jeu ne rejoint le site que s'il est publié et exploitable : sans titre ni URL,
    /// il n'y a ni carte à afficher ni route vers laquelle pointer.
    /// </summary>
    private static bool IsPubliclyListable(ItchIoGameDto dto) =>
        dto.Published
        && !string.IsNullOrWhiteSpace(dto.Title)
        && Uri.TryCreate(dto.Url, UriKind.Absolute, out _);

    private Game ToGame(ItchIoGameDto dto)
    {
        var itchUrl = new Uri(dto.Url!, UriKind.Absolute);

        return new Game
        {
            Id = dto.Id,
            Slug = GameSlug.FromItchUrl(itchUrl, dto.Id),
            Title = dto.Title!,
            ItchUrl = itchUrl,
            CoverUrl = ParseOptionalUri(dto.CoverUrl),
            Tagline = LocalizedText.FromEnglish(dto.ShortText),
            PublishedAt = dto.PublishedAt,
            Price = Price.FromCents(Math.Max(dto.MinPrice, 0), _options.Currency),
            Platforms = ToPlatforms(dto),
            IsEmbeddable = string.Equals(dto.Type, "html", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static Uri? ParseOptionalUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;

    private static GamePlatforms ToPlatforms(ItchIoGameDto dto)
    {
        var platforms = GamePlatforms.None;

        if (dto.PublishedOnWindows)
        {
            platforms |= GamePlatforms.Windows;
        }

        if (dto.PublishedOnLinux)
        {
            platforms |= GamePlatforms.Linux;
        }

        if (dto.PublishedOnMacOs)
        {
            platforms |= GamePlatforms.MacOs;
        }

        if (dto.PublishedOnAndroid)
        {
            platforms |= GamePlatforms.Android;
        }

        return platforms;
    }
}
