using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

public class AppViewServices(
    ILogger<AppViewServices> logger,
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    FeedItemStore feedItemStore)
{
    public async Task<FeedPost?> GetFeedPostByUriAsync(
        string postUri, 
        CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(postUri, out FeedPost? cachedFeedPost))
        {
            return cachedFeedPost;
        }

        var getPostsUrl =
            "https://public.api.bsky.app/xrpc/app.bsky.feed.getPosts?uris=" +
            Uri.EscapeDataString(postUri);

        logger.LogInformation("Fetching feed post for URI: {PostUri}", postUri);

        var httpClient = httpClientFactory.CreateClient();
        using var response = await httpClient.GetAsync(getPostsUrl, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning("Feed post not found for URI: {PostUri}", postUri);
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken
        );

        var posts = json.RootElement.GetProperty("posts");
        if (posts.GetArrayLength() == 0)
        {
            logger.LogWarning("No posts found for URI: {PostUri}", postUri);
            return null;
        }

        var original = posts[0];
        var record = original.GetProperty("record");

        var text = record.TryGetProperty("text", out var textElement)
                                               ? textElement.GetString()
                                               : null;

        if (text is not null)
        {
            var createdAt = record.GetProperty("createdAt").GetDateTimeOffset();

            var author = original.GetProperty("author");
            var authorDid = author.GetProperty("did").GetString();

            if (authorDid is not null)
            {
                var feedAuthor = await GetFeedAuthorByDidAsync(authorDid, cancellationToken);
                
                FeedExternal? feedExternal = null;
                if (record.TryGetProperty("embed", out var embed) &&
                    embed.ValueKind == JsonValueKind.Object)
                {
                    feedExternal = ExternalParser.Parse(embed, authorDid);
                }

                var viewEmbed = original.TryGetProperty("embed", out var embedView)
                    ? embedView
                    : default;
                var (images, videos) = MediaEmbedParser.Parse(record, viewEmbed, authorDid);

                string rootUri = postUri;
                string? parentUri = null;
                int threadIndex = 0;

                if (record.TryGetProperty("reply", out var replyProperty))
                {
                    if (replyProperty.TryGetProperty("root", out var rootProperty) && 
                        rootProperty.TryGetProperty("uri", out var rootUriProperty))
                    {
                        rootUri = rootUriProperty.GetString() ?? postUri;
                    }

                    if (replyProperty.TryGetProperty("parent", out var parentProperty) && 
                        parentProperty.TryGetProperty("uri", out var parentUriProperty))
                    {
                        parentUri = parentUriProperty.GetString();
                    }

                    threadIndex = feedItemStore.GetLatest().Count(item => item.Post.RootUri == rootUri);
                }

                var feedPost = new FeedPost(
                   postUri,
                   feedAuthor,
                   text,
                   rootUri,
                   parentUri,
                   threadIndex,
                   createdAt,
                   feedExternal,
                   images,
                   videos);

                memoryCache.Set(postUri, feedPost, absoluteExpiration: DateTimeOffset.UtcNow.AddMinutes(5));
                return feedPost;
            }
        }

        logger.LogWarning("Failed to retrieve feed post for URI: {PostUri}", postUri);
        return null;
    }

    public async Task<FeedAuthor?> GetFeedAuthorByDidAsync(string did, CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(did, out FeedAuthor? cachedFeedAuthor))
        {
            return cachedFeedAuthor;
        }

        var getProfileUrl =
            "https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor=" +
            Uri.EscapeDataString(did);

        logger.LogInformation("Fetching profile for DID: {Did}", did);

        var httpClient = httpClientFactory.CreateClient();
        using var response = await httpClient.GetAsync(getProfileUrl, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning("Profile not found for DID: {Did}", did);
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken
        );

        var root = json.RootElement;

        var handler = root.TryGetProperty("handle", out var handlerElement)
            ? handlerElement.GetString()
            : null;
        var displayName = root.TryGetProperty("displayName", out var displayNameElement)
            ? displayNameElement.GetString()
            : null;
        var avatarUrl = root.TryGetProperty("avatar", out var avatarUrlElement)
            ? avatarUrlElement.GetString()
            : null;

        var feedAuthor = new FeedAuthor(did, handler, displayName, avatarUrl);
        memoryCache.Set(did, feedAuthor, absoluteExpiration: DateTimeOffset.UtcNow.AddMinutes(60));
        logger.LogInformation("Successfully retrieved profile for DID: {Did}", did);
        return feedAuthor;
    }
}