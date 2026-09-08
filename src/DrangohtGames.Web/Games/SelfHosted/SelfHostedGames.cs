namespace DrangohtGames.Web.Games.SelfHosted;

/// <summary>
/// Les jeux dont le build Web est servi par le site lui-même (ADR 0007).
/// </summary>
/// <remarks>
/// La liste est constatée, jamais déclarée : elle vient de ce qui se trouve réellement sur
/// le disque, pas d'un manifeste. Un bouton « Jouer » ne peut donc pas mener à un jeu
/// absent de l'image.
/// </remarks>
public sealed class SelfHostedGames
{
    private readonly IReadOnlySet<string> _slugs;

    private SelfHostedGames(IReadOnlySet<string> slugs) => _slugs = slugs;

    /// <summary>Aucun jeu auto-hébergé : toutes les fiches renvoient vers itch.io.</summary>
    public static SelfHostedGames None { get; } =
        new(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Retient les slugs donnés comme auto-hébergés.</summary>
    public static SelfHostedGames For(IEnumerable<string> slugs)
    {
        ArgumentNullException.ThrowIfNull(slugs);

        return new SelfHostedGames(slugs.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Marque le jeu comme jouable sur le site, s'il a un build.</summary>
    public Game Apply(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return _slugs.Contains(game.Slug.Value)
            ? game with { IsSelfHosted = true }
            : game;
    }
}
