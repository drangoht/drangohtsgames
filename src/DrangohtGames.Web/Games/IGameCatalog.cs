namespace DrangohtGames.Web.Games;

/// <summary>
/// Source des jeux affichés par le site, du point de vue des pages.
/// </summary>
public interface IGameCatalog
{
    /// <summary>
    /// Retourne les jeux publiés, du plus récent au plus ancien.
    /// </summary>
    /// <remarks>Ne lève pas quand la source distante est indisponible : voir les implémentations.</remarks>
    Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken cancellationToken);
}
