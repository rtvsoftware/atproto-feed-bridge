using System.Text.Json;

public static class MediaEmbedParser
{
    public static (FeedImage[] Images, FeedVideo[] Videos) Parse(
        JsonElement record,
        JsonElement viewEmbed,
        string authorDid)
    {
        var rawEmbed = record.ValueKind == JsonValueKind.Object &&
                       record.TryGetProperty("embed", out var recordEmbed)
            ? recordEmbed
            : record;
        var rawMedia = ResolveMedia(rawEmbed);
        var viewMedia = ResolveMedia(viewEmbed);

        var images = ParseImages(viewMedia, authorDid);
        if (images.Length == 0)
        {
            images = ParseImages(rawMedia, authorDid);
        }

        var videos = ParseVideos(viewMedia);
        if (videos.Length == 0)
        {
            videos = ParseVideos(rawMedia);
        }

        return (images, videos);
    }

    public static bool ContainsVideo(JsonElement record)
    {
        var rawEmbed = record.ValueKind == JsonValueKind.Object &&
                       record.TryGetProperty("embed", out var recordEmbed)
            ? recordEmbed
            : record;
        var media = ResolveMedia(rawEmbed);

        return media.ValueKind == JsonValueKind.Object &&
               (media.TryGetProperty("video", out _) ||
                media.TryGetProperty("playlist", out _) ||
                media.TryGetProperty("videos", out var videos) &&
                videos.ValueKind == JsonValueKind.Array &&
                videos.GetArrayLength() > 0);
    }

    private static JsonElement ResolveMedia(JsonElement embed)
    {
        while (embed.ValueKind == JsonValueKind.Object &&
               embed.TryGetProperty("media", out var media) &&
               media.ValueKind == JsonValueKind.Object)
        {
            embed = media;
        }

        return embed;
    }

    private static FeedImage[] ParseImages(JsonElement embed, string authorDid)
    {
        if (embed.ValueKind != JsonValueKind.Object ||
            !embed.TryGetProperty("images", out var images) ||
            images.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<FeedImage>();
        }

        return images.EnumerateArray()
            .Select(image => ParseImage(image, authorDid))
            .OfType<FeedImage>()
            .ToArray();
    }

    private static FeedImage? ParseImage(JsonElement image, string authorDid)
    {
        if (image.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var thumbUrl = GetString(image, "thumb") ?? GetString(image, "thumbUrl");
        var fullSizeUrl = GetString(image, "fullsize") ?? GetString(image, "fullSizeUrl");

        if (thumbUrl is null || fullSizeUrl is null)
        {
            if (image.TryGetProperty("image", out var blob) &&
                GetBlobCid(blob) is { Length: > 0 } cid)
            {
                thumbUrl ??= $"https://cdn.bsky.app/img/feed_thumbnail/plain/{authorDid}/{cid}@jpeg";
                fullSizeUrl ??= $"https://cdn.bsky.app/img/feed_fullsize/plain/{authorDid}/{cid}@jpeg";
            }
        }

        if (thumbUrl is null && fullSizeUrl is null)
        {
            return null;
        }

        return new FeedImage(
            thumbUrl ?? fullSizeUrl,
            fullSizeUrl ?? thumbUrl,
            GetString(image, "alt") ?? string.Empty);
    }

    private static FeedVideo[] ParseVideos(JsonElement embed)
    {
        if (embed.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<FeedVideo>();
        }

        if (GetString(embed, "playlist") is { Length: > 0 } playlist)
        {
            return
            [
                new FeedVideo(
                    playlist,
                    GetString(embed, "thumbnail") ?? GetString(embed, "thumbUrl") ?? string.Empty,
                    GetString(embed, "alt") ?? string.Empty)
            ];
        }

        if (embed.TryGetProperty("videos", out var videos) &&
            videos.ValueKind == JsonValueKind.Array)
        {
            return videos.EnumerateArray()
                .Select(video => new FeedVideo(
                    GetString(video, "videoUrl") ?? string.Empty,
                    GetString(video, "thumbUrl") ?? string.Empty,
                    GetString(video, "alt") ?? string.Empty))
                .Where(video => video.VideoUrl.Length > 0)
                .ToArray();
        }

        return Array.Empty<FeedVideo>();
    }

    private static string? GetBlobCid(JsonElement blob)
    {
        if (blob.ValueKind != JsonValueKind.Object ||
            !blob.TryGetProperty("ref", out var reference))
        {
            return null;
        }

        if (reference.ValueKind == JsonValueKind.String)
        {
            return reference.GetString();
        }

        return reference.ValueKind == JsonValueKind.Object &&
               reference.TryGetProperty("$link", out var link) &&
               link.ValueKind == JsonValueKind.String
            ? link.GetString()
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
