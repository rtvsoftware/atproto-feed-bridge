using System.Text.Json;
using System.Threading.Channels;
using idunno.AtProto;
using idunno.AtProto.Jetstream;
using Microsoft.Extensions.Options;

public sealed class JetstreamServices(
    ILogger<JetstreamServices> logger,
    FeedItemStore feedItemStore,
    AppViewServices appViewServices,
    Channel<FeedNotification> notifications,
    IOptions<JetstreamOptions> jetstreamOptions,
    IOptions<ATProtoOptions> atProtoOptions)
{
    private readonly JetstreamOptions _jetstreamOptions = jetstreamOptions.Value;
    private readonly ATProtoOptions _atProtoOptions = atProtoOptions.Value;
    private AtProtoJetstream? _jetstream;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var jetstreamOptions = _jetstreamOptions;

        var collections = new[]
            {
                AtProtoCollections.FeedPost,
                AtProtoCollections.FeedRepost
            }
            .Select(Nsid.FromString)
            .ToList();

        var dids = _atProtoOptions.Dids
                .Select(Did.FromString)
                .ToArray();

         _jetstream = new AtProtoJetstream(new Uri(jetstreamOptions.Url), 
            collections: collections,
            dids: dids);
        
        _jetstream.MessageReceived += async (sender, e) =>
        {
            logger.LogInformation("Received message: {Message}", e.Message);

            using var json = JsonDocument.Parse(e.Message);
            var root = json.RootElement;

            if (!root.TryGetProperty("commit", out var commit) ||
                !collections.Any(c => c.ToString() == commit.GetProperty("collection").GetString()))
            {
                return;
            }

            var operation = commit.GetProperty("operation").GetString();
            var did = root.GetProperty("did").GetString();
            var rkey = commit.GetProperty("rkey").GetString();
            var postUri = $"at://{did}/app.bsky.feed.post/{rkey}";
            var record = commit.GetProperty("record");
            var text = record.TryGetProperty("text", out var textElement) ? textElement.GetString() : null;
            var createdAt = record.GetProperty("createdAt").GetDateTimeOffset();

            if (did is not null && text is not null && operation is "create" or "update")
            {
                FeedExternal? feedExternal = null;
                if (record.TryGetProperty("embed", out var embed) &&
                    embed.ValueKind == JsonValueKind.Object)
                {
                    feedExternal = ExternalParser.Parse(embed, did!);
                }

                var feedAuthor = await appViewServices.GetFeedAuthorByDidAsync(did!, CancellationToken.None);

                  var (images, videos) = MediaEmbedParser.Parse(
                                            record,
                                            default,
                                            did.ToString());

                if (videos.Length == 0 &&
                    MediaEmbedParser.ContainsVideo(record))
                {
                    var appViewPost = await appViewServices.GetFeedPostByUriAsync(
                        postUri,
                        CancellationToken.None);
                    if (appViewPost is not null)
                    {
                        images = appViewPost.Images;
                        videos = appViewPost.Videos;
                    }
                }

                var feedPost = new FeedPost(
                    postUri,
                    feedAuthor,
                    text,
                    createdAt,
                    feedExternal,
                    images,
                    videos);

                var feedItem = CreateFeedItem(feedPost, null, feedPost.CreatedAt);

                feedItemStore.Upsert(feedItem);
                if (!notifications.Writer.TryWrite(new FeedPostReceived(feedPost)))
                {
                    throw new InvalidOperationException("Could not queue the Jetstream post notification.");
                }
            }
            else if (operation == "delete")
            {
                feedItemStore.Remove(postUri);
                if (!notifications.Writer.TryWrite(new FeedPostRemoved(postUri)))
                {
                    throw new InvalidOperationException("Could not queue the Jetstream post removal notification.");
                }
            }
            
            logger.LogInformation("Message received: {Message}", json);
        };

        _jetstream.ConnectionStateChanged += (sender, e) =>
        {
            logger.LogInformation("Connection state changed: {State}", e.State);
        };

        _jetstream.FaultRaised += (sender, e) =>
        {
            logger.LogError("Fault raised: {Fault}", e.Fault);
        };

        await _jetstream.ConnectAsync(cancellationToken);
    }


    private FeedItem CreateFeedItem(FeedPost post, FeedAuthor? repostedBy, DateTimeOffset feedTimestamp)
    {
        return new FeedItem(
            Id: post.Uri,
            Post: post,
            RepostedBy: repostedBy,
            FeedTimestamp: feedTimestamp
        );
    }

    public async Task CloseAsync()
    {
        if (_jetstream is not null)
        {
            logger.LogInformation("Closing Jetstream connection.");
            
            await _jetstream.CloseAsync();
        }
    }
}