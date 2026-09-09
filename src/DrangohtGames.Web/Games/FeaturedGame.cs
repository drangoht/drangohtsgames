namespace DrangohtGames.Web.Games;

/// <summary>
/// Désigne le jeu qui occupe la vitrine de la page d'accueil.
/// </summary>
/// <remarks>
/// La désignation éditoriale prime : elle exprime une intention que le catalogue ne porte
/// pas. À défaut, le dernier jeu jouable sans quitter le site est le meilleur candidat,
/// puisque c'est le seul dont la vitrine peut proposer de jouer tout de suite.
/// </remarks>
public static class FeaturedGame
{
    /// <summary>
    /// Retourne le jeu à mettre en vitrine, ou <see langword="null"/> si le catalogue est vide.
    /// </summary>
    public static Game? Of(IEnumerable<Game> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        var catalog = games as IReadOnlyList<Game> ?? [.. games];

        return catalog.FirstOrDefault(game => game.IsFeatured)
            ?? MostRecent(catalog.Where(game => game.IsSelfHosted))
            ?? MostRecent(catalog);
    }

    /// <remarks>
    /// Un jeu sans date de publication ne peut pas être le plus récent, mais il reste
    /// sélectionnable si c'est le seul du catalogue.
    /// </remarks>
    private static Game? MostRecent(IEnumerable<Game> games) =>
        games.MaxBy(game => game.PublishedAt ?? DateTimeOffset.MinValue);
}
