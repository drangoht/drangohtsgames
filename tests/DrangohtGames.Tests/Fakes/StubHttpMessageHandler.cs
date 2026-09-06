using System.Net;
using System.Text;

namespace DrangohtGames.Tests.Fakes;

/// <summary>
/// Handler HTTP de test : enregistre les requêtes reçues et rejoue une réponse fixée.
/// On ne mocke pas <see cref="HttpClient"/> — on branche un vrai client sur ce handler.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    private StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        _respond = respond;

    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpMessageHandler ReturningJson(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    public static StubHttpMessageHandler ReturningStatus(HttpStatusCode status) =>
        new(_ => new HttpResponseMessage(status));

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new(_ => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_respond(request));
    }
}
