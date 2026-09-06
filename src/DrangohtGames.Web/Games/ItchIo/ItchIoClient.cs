using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DrangohtGames.Web.Games.ItchIo;

/// <summary>
/// Traduit l'API itch.io en jeux du site.
/// </summary>
/// <remarks>
/// C'est la seule classe du projet qui connaît le vocabulaire d'itch.io. Elle constitue la
/// couche anti-corruption : au-delà, on ne manipule plus que des <see cref="Game"/>.
/// </remarks>
public sealed partial class ItchIoClient : IItchIoClient
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly HttpClient _httpClient;
    private readonly ItchIoOptions _options;
    private readonly ILogger<ItchIoClient> _logger;

    /// <summary>Construit le client sur un <see cref="HttpClient"/> typé.</summary>
    public ItchIoClient(HttpClient httpClient, IOptions<ItchIoOptions> options, ILogger<ItchIoClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Game>> GetPublishedGamesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/1/key/my-games")
        {
            // La clé passe par l'en-tête : dans le chemin, elle atterrirait dans les
            // journaux d'accès d'itch.io comme dans nos propres traces sortantes.
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey) },
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<ItchIoGamesResponse>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? new ItchIoGamesResponse();

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

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ItchIoDateTimeOffsetConverter());
        return options;
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
