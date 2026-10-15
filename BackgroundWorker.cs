using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;

public class BackgroundWorker(
    JetstreamServices jetstreamServices,
    PostServices postServices,
    Channel<FeedNotification> notifications,
    IHubContext<FeedHub> feedHub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await postServices.SyncLatestPostsAsync(cancellationToken);
            await jetstreamServices.RunAsync(cancellationToken);

            await HandleNotifications(cancellationToken);
        }
        finally
        {
            await jetstreamServices.CloseAsync();
        }
    }

    private async Task HandleNotifications(CancellationToken cancellationToken)
    {
        await foreach (var notification in notifications.Reader.ReadAllAsync(cancellationToken))
        {
            switch (notification)
            {
                case FeedPostReceived received:
                    await feedHub.Clients.All.SendAsync(
                        "ReceivePost",
                        received.Post,
                        cancellationToken);
                    break;
                case FeedPostRemoved removed:
                    await feedHub.Clients.All.SendAsync(
                        "RemovePost",
                        removed.Uri,
                        cancellationToken);
                    break;
            }
        }
    }
}