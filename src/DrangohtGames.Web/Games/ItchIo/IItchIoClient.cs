namespace DrangohtGames.Web.Games.ItchIo;

/// <summary>
/// Accès à l'API itch.io, du point de vue du catalogue.
/// </summary>
/// <remarks>
/// Cette abstraction isole un appel réseau : elle coûte une interface et un enregistrement
/// dans le conteneur, et le paie aujourd'hui en permettant de tester le cache, le repli sur
/// snapshot et la fusion des métadonnées sans monter de serveur HTTP.
/// </remarks>
public interface IItchIoClient
{
    /// <summary>
    /// Retourne les jeux publiés du compte, du plus récent au plus ancien.
    /// </summary>
    /// <exception cref="HttpRequestException">L'API est injoignable ou refuse la clé.</exception>
    Task<IReadOnlyList<Game>> GetPublishedGamesAsync(CancellationToken cancellationToken);
}
