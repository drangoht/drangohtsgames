namespace DrangohtGames.Web.Games.ItchIo;

internal sealed partial class ItchIoClient
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "itch.io a renvoyé {ReceivedCount} jeux, dont {PublishedCount} publiés.")]
    private static partial void LogGamesFetched(ILogger logger, int receivedCount, int publishedCount);
}
