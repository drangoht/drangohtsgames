using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.ItchIo;

namespace DrangohtGames.Tests.Fakes;

/// <summary>
/// Doublure en mémoire du client itch.io. Un fake plutôt qu'un mock : les tests décrivent
/// ce que l'API renvoie, pas la séquence d'appels qu'on lui fait.
/// </summary>
internal sealed class FakeItchIoClient : IItchIoClient
{
    private readonly IReadOnlyList<Game> _games;
    private readonly Exception? _failure;

    private FakeItchIoClient(IReadOnlyList<Game> games, Exception? failure)
    {
        _games = games;
        _failure = failure;
    }

    public int CallCount { get; private set; }

    public static FakeItchIoClient Returning(params Game[] games) => new(games, failure: null);

    public static FakeItchIoClient Failing(Exception? failure = null) =>
        new([], failure ?? new HttpRequestException("itch.io est injoignable."));

    public Task<IReadOnlyList<Game>> GetPublishedGamesAsync(CancellationToken cancellationToken)
    {
        CallCount++;

        return _failure is not null
            ? Task.FromException<IReadOnlyList<Game>>(_failure)
            : Task.FromResult(_games);
    }
}
