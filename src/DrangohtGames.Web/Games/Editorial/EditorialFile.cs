using System.Text.Json.Serialization;

namespace DrangohtGames.Web.Games.Editorial;

/// <summary>Fichier de contenu éditorial, indexé par slug de jeu.</summary>
internal sealed record EditorialFile
{
    [JsonPropertyName("games")]
    public Dictionary<string, EditorialEntry> Games { get; init; } = [];
}

/// <summary>
/// Ce qu'itch.io ne fournit pas : tags, moteur, description longue, captures, traductions.
/// </summary>
/// <remarks>
/// Toutes les propriétés sont facultatives. Une entrée partielle complète les données
/// itch.io sans jamais les effacer.
/// </remarks>
internal sealed record EditorialEntry
{
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonPropertyName("engine")]
    public string? Engine { get; init; }

    [JsonPropertyName("tagline")]
    public EditorialText? Tagline { get; init; }

    [JsonPropertyName("description")]
    public EditorialText? Description { get; init; }

    [JsonPropertyName("screenshots")]
    public IReadOnlyList<EditorialScreenshot> Screenshots { get; init; } = [];

    [JsonPropertyName("featured")]
    public bool Featured { get; init; }
}

/// <summary>Texte bilingue tel qu'il est saisi dans le fichier éditorial.</summary>
internal sealed record EditorialText
{
    [JsonPropertyName("en")]
    public string? English { get; init; }

    [JsonPropertyName("fr")]
    public string? French { get; init; }

    public LocalizedText ToLocalizedText() => new(English, French);
}

/// <summary>Capture d'écran telle qu'elle est saisie dans le fichier éditorial.</summary>
internal sealed record EditorialScreenshot
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("caption")]
    public EditorialText? Caption { get; init; }
}
