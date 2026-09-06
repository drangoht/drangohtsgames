namespace DrangohtGames.Web.Games.ItchIo;

/// <summary>
/// L'API itch.io a répondu, mais son corps décrit une erreur.
/// </summary>
/// <remarks>
/// itch.io ne signale pas une clé invalide par un code HTTP d'erreur : il répond
/// <c>200 OK</c> avec <c>{"errors":["invalid key"]}</c>. Sans cette exception, une clé
/// expirée passerait pour un compte sans jeu.
/// </remarks>
public sealed class ItchIoApiException : Exception
{
    /// <summary>Construit l'exception à partir des messages renvoyés par itch.io.</summary>
    public ItchIoApiException(IReadOnlyList<string> errors)
        : base($"itch.io a rejeté la requête : {string.Join(" ; ", errors ?? [])}") =>
        Errors = errors ?? [];

    /// <inheritdoc />
    public ItchIoApiException()
        : this([])
    {
    }

    /// <inheritdoc />
    public ItchIoApiException(string message)
        : base(message) => Errors = [];

    /// <inheritdoc />
    public ItchIoApiException(string message, Exception innerException)
        : base(message, innerException) => Errors = [];

    /// <summary>Messages d'erreur tels qu'itch.io les a formulés.</summary>
    public IReadOnlyList<string> Errors { get; }
}
