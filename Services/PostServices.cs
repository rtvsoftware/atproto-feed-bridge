using System.Text.Json;
using idunno.AtProto;
using idunno.AtProto.Repo;
using Microsoft.Extensions.Options;

public sealed class PostServices(
    AppViewServices appViewServices,
    FeedItemStore feedItemStore,
    IOptions<ATProtoOptions> atProtoOptions)
{
    private readonly ATProtoOptions _atProtoOptions = atProtoOptions.Value;

    public async Task SyncLatestPostsAsync(CancellationToken cancellationToken)
    {
        using AtProtoAgent agent = new(new Uri(_atProtoOptions.AgentUrl));

        foreach (var didText in _atProtoOptions.Dids)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var did = Did.FromString(didText);
            var pds = await agent.ResolvePds(did);

            if (pds is null)
            {
                // Log dat er voor deze DID geen PDS gevonden is.
                continue;
            }

            var collections = new[]
            {
                AtProtoCollections.FeedPost,
                AtProtoCollections.FeedRepost
            };

            var feedAuthor = await appViewServices.GetFeedAuthorByDidAsync(
                                didText,
                                cancellationToken);

            foreach (var collection in collections)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var result = await agent.ListRecords<AtProtoRecord>(
                    did,
                    limit: 50,
                    collection: collection,
                    service: pds);

                if (result.Result is not null)
                {
                    foreach (var atRecord in result.Result)
                    {
                        var recordUri = atRecord.Uri?.ToString();

                        if (recordUri is not null)
                        {
                            if (collection is AtProtoCollections.FeedPost)
                            {
                                var extensionData = atRecord.Value?.ExtensionData;
                                if (extensionData is not null)
                                {
                                    var record = JsonSerializer.SerializeToElement(extensionData);

                                    var text = record.TryGetProperty("text", out var textElement)
                                        ? textElement.GetString()
                                        : null;

                                    if (text is not null)
                                    {
                                        var createdAt = record.GetProperty("createdAt").GetDateTimeOffset();

                                        FeedExternal? feedExternal = null;
                                        if (record.TryGetProperty("embed", out var embed) &&
                                            embed.ValueKind == JsonValueKind.Object)
                                        {
                                            feedExternal = ExternalParser.Parse(embed, did!);
                                        }

                                        var (images, videos) = MediaEmbedParser.Parse(
                                            record,
                                            default,
                                            did.ToString());

                                        if (videos.Length == 0 &&
                                            MediaEmbedParser.ContainsVideo(record))
                                        {
                                            var appViewPost = await appViewServices.GetFeedPostByUriAsync(
                                                recordUri,
                                                cancellationToken);
                                            if (appViewPost is not null)
                                            {
                                                images = appViewPost.Images;
                                                videos = appViewPost.Videos;
                                            }
                                        }

                                        var feedPost = new FeedPost(
                                           recordUri,
                                           feedAuthor,
                                           text,
                                           createdAt,
                                           feedExternal,
                                           images,
                                           videos);

                                        var feedItem = CreateFeedItem(recordUri, feedPost, null, feedPost.CreatedAt);
                                        feedItemStore.Upsert(feedItem);
                                    }
                                }
                            }
                            if (collection is AtProtoCollections.FeedRepost)
                            {
                                var extensionData = atRecord.Value?.ExtensionData;
                                if (extensionData is not null)
                                {
                                    var jsonRecord = JsonSerializer.SerializeToElement(extensionData);

                                    if (jsonRecord.TryGetProperty("subject", out var subjectProperty) &&
                                        subjectProperty.TryGetProperty("uri", out var subjectUriProperty) &&
                                        jsonRecord.TryGetProperty("createdAt", out var createdAtProperty))
                                    {
                                        var subjectUri = subjectUriProperty.ToString();
                                        var createdAt = DateTimeOffset.Parse(createdAtProperty.ToString() ?? DateTimeOffset.UtcNow.ToString());

                                        var feedPost = await appViewServices.GetFeedPostByUriAsync(subjectUri, cancellationToken);
                                        if (feedPost is not null)
                                        {
                                            var feedItem = CreateFeedItem(recordUri, feedPost, feedAuthor, createdAt);
                                            feedItemStore.Upsert(feedItem);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private FeedItem CreateFeedItem(string postUri, FeedPost post, FeedAuthor? repostedBy, DateTimeOffset feedTimestamp) => 
        new FeedItem(
            Id: postUri,
            Post: post,
            RepostedBy: repostedBy,
            FeedTimestamp: feedTimestamp
        );
}