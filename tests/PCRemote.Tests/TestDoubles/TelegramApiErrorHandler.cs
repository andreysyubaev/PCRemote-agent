using System.Net;
using System.Text;

namespace PCRemote.Tests.TestDoubles;

public sealed class TelegramApiErrorHandler : HttpMessageHandler
{
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;

        var responseBody = """
                           {
                               "ok": false,
                               "error_code": 500,
                               "description": "Simulated Telegram API error"
                           }
                           """;

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(
                responseBody,
                Encoding.UTF8,
                "application/json")
        });
    }
}