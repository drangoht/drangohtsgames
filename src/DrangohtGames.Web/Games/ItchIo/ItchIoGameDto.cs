using System.Text.Json.Serialization;

namespace DrangohtGames.Web.Games.ItchIo;

/// <summary>Enveloppe de la réponse de <c>GET /api/1/KEY/my-games</c>.</summary>
internal sealed record ItchIoGamesResponse
{
    [JsonPropertyName("games")]
    public IReadOnlyList<ItchIoGameDto> Games { get; init; } = [];

    /// <summary>
    /// Erreurs signalées par itch.io — dans un corps de réponse <c>200 OK</c>.
    /// </summary>
    [JsonPropertyName("errors")]
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Un jeu tel que l'API itch.io le décrit.
/// </summary>
/// <remarks>
/// Ce type reproduit le contrat externe et ne sort jamais de ce dossier : il est traduit en
/// <see cref="Game"/> par <see cref="ItchIoClient"/>. Les champs privés du contrat
/// (<c>earnings</c>, <c>purchases_count</c>, <c>downloads_count</c>, <c>views_count</c>) ne
/// sont volontairement pas déclarés — non désérialisés, ils ne peuvent pas être exposés par
/// inadvertance.
/// </remarks>
internal sealed record ItchIoGameDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("cover_url")]
    public string? CoverUrl { get; init; }

    [JsonPropertyName("short_text")]
    public string? ShortText { get; init; }

    /// <summary><c>html</c> pour un jeu jouable en navigateur, <c>default</c> sinon.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("published")]
    public bool Published { get; init; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }

    [JsonPropertyName("min_price")]
    public int MinPrice { get; init; }

    [JsonPropertyName("p_windows")]
    public bool PublishedOnWindows { get; init; }

    [JsonPropertyName("p_linux")]
    public bool PublishedOnLinux { get; init; }

    [JsonPropertyName("p_osx")]
    public bool PublishedOnMacOs { get; init; }

    [JsonPropertyName("p_android")]
    public bool PublishedOnAndroid { get; init; }
}
