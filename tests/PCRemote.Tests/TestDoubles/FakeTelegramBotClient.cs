using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;

namespace PCRemote.Tests.TestDoubles;

public sealed class FakeTelegramBotClient : ITelegramBotClient
{
    public bool LocalBotServer => false;
    private bool _updateSent;
    public long BotId => 123456;
    public long UnauthorizedUserId { get; set; } = 999999;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int SendMessageCount { get; private set; }
    public int GetUpdatesCount { get; private set; }
    public IExceptionParser ExceptionsParser { get; set; } = null!;

    public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest
    {
        add { }
        remove { }
    }

    public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived
    {
        add { }
        remove { }
    }

    public async Task<TResponse> SendRequest<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine(request.GetType().FullName);

        if (request is GetMeRequest)
        {
            var user = new User
            {
                Id = BotId,
                IsBot = true,
                FirstName = "PCRemote",
                Username = "pcremote_test_bot"
            };

            return (TResponse)(object)user;
        }

        if (request is SetMyCommandsRequest)
        {
            return (TResponse)(object)true;
        }

        if (request is GetUpdatesRequest)
        {
            GetUpdatesCount++;

            if (!_updateSent)
            {
                _updateSent = true;

                var update = new Update
                {
                    Id = 1,
                    Message = new Message
                    {
                        From = new User
                        {
                            Id = UnauthorizedUserId,
                            IsBot = false,
                            FirstName = "Unauthorized"
                        },
                        Chat = new Chat
                        {
                            Id = UnauthorizedUserId,
                            Type = ChatType.Private
                        },
                        Date = DateTime.UtcNow,
                        Text = "/status"
                    }
                };

                return (TResponse)(object)new[] { update };
            }

            await Task.Delay(
                System.Threading.Timeout.Infinite,
                cancellationToken);

            return (TResponse)(object)Array.Empty<Update>();
        }
        
        if (request is SendMessageRequest)
        {
            SendMessageCount++;

            throw new InvalidOperationException(
                "Fake Telegram client should not send messages in this test.");
        }
        
        throw new NotImplementedException();
    }
    
    public Task<bool> TestApi(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DownloadFile(
        string filePath,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DownloadFile(
        TGFile file,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}