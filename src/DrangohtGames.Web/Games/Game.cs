namespace DrangohtGames.Web.Games;

/// <summary>
/// Un jeu tel qu'il est présenté sur le site.
/// </summary>
/// <remarks>
/// Ce type est le <em>seul</em> contrat entre le catalogue et les vues. Il ne porte
/// délibérément aucun des compteurs privés renvoyés par l'API itch.io
/// (<c>earnings</c>, <c>purchases_count</c>, <c>downloads_count</c>, <c>views_count</c>) :
/// ce qui n'existe pas dans le modèle ne peut pas fuiter dans une page publique.
/// Un test d'architecture verrouille cette propriété.
/// </remarks>
public sealed record Game
{
    /// <summary>Identifiant itch.io du jeu.</summary>
    public required int Id { get; init; }

    /// <summary>Identifiant du jeu dans les URLs du site.</summary>
    public required GameSlug Slug { get; init; }

    /// <summary>Titre du jeu.</summary>
    public required string Title { get; init; }

    /// <summary>Adresse de la page itch.io du jeu.</summary>
    public required Uri ItchUrl { get; init; }

    /// <summary>Prix plancher.</summary>
    public required Price Price { get; init; }

    /// <summary>Image de couverture, absente si le jeu n'en déclare pas.</summary>
    public Uri? CoverUrl { get; init; }

    /// <summary>Accroche courte (<c>short_text</c> côté itch.io, traduisible localement).</summary>
    public LocalizedText Tagline { get; init; } = LocalizedText.Empty;

    /// <summary>Date de publication, absente pour un jeu jamais publié.</summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Plateformes téléchargeables déclarées.</summary>
    public GamePlatforms Platforms { get; init; } = GamePlatforms.None;

    /// <summary>Tags de classement, alimentés par les métadonnées locales.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Moteur utilisé (Unity, Godot…), alimenté par les métadonnées locales.</summary>
    public string? Engine { get; init; }

    /// <summary>Description longue, alimentée par les métadonnées locales.</summary>
    public LocalizedText Description { get; init; } = LocalizedText.Empty;

    /// <summary>Captures d'écran, alimentées par les métadonnées locales.</summary>
    public IReadOnlyList<Screenshot> Screenshots { get; init; } = [];

    /// <summary>Indique qu'un widget itch.io peut être embarqué pour ce jeu.</summary>
    public bool IsEmbeddable { get; init; }

    /// <summary>
    /// Indique que le jeu est désigné pour la vitrine de l'accueil.
    /// </summary>
    /// <remarks>
    /// Alimenté par le fichier éditorial : itch.io ne connaît pas cette notion, qui relève
    /// d'un choix de présentation.
    /// </remarks>
    public bool IsFeatured { get; init; }

    /// <summary>
    /// Indique que le build Web du jeu est servi par le site, donc jouable sans le quitter.
    /// </summary>
    /// <remarks>
    /// À distinguer de <see cref="IsEmbeddable"/>, qui dit seulement qu'itch.io le publie en
    /// HTML. Un jeu peut être jouable là-bas sans l'être ici : voir l'ADR 0007.
    /// </remarks>
    public bool IsSelfHosted { get; init; }
}
