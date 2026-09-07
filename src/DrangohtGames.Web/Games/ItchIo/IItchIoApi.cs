using Refit;

namespace DrangohtGames.Web.Games.ItchIo;

/// <summary>
/// Contrat HTTP de l'API itch.io, tel que Refit l'implémente.
/// </summary>
/// <remarks>
/// Ce type décrit le protocole — route, en-tête, forme de la réponse — et rien d'autre :
/// aucune décision ne s'y prend. La traduction en <see cref="Game"/> revient à
/// <see cref="ItchIoClient"/>, seul à porter les règles du site.
/// </remarks>
internal interface IItchIoApi
{
    /// <summary>
    /// Appelle <c>GET /api/1/key/my-games</c>, qui liste les jeux du compte porteur de la clé.
    /// </summary>
    /// <param name="apiKey">
    /// Clé d'API. Elle passe par l'en-tête <c>Authorization</c> : dans le chemin, elle
    /// atterrirait dans les journaux d'accès d'itch.io comme dans nos traces sortantes.
    /// </param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    [Get("/api/1/key/my-games")]
    Task<ItchIoGamesResponse> GetMyGamesAsync(
        [Authorize("Bearer")] string apiKey,
        CancellationToken cancellationToken);
}
