using System.Text.Json;

public static class ExternalParser
{
    public static FeedExternal? Parse(
        JsonElement post,
        string authorDid)
    {
        if (!post.TryGetProperty("external", out var external))
        {
            return null;
        }

        string? GetString(string propertyName) =>
            external.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        string? thumbUrl = null;
        if (external.TryGetProperty("thumb", out var thumb) &&
            thumb.TryGetProperty("ref", out var reference) &&
            reference.TryGetProperty("$link", out var cidElement) &&
            cidElement.GetString() is { Length: > 0 } cid)
        {
            thumbUrl = $"https://cdn.bsky.app/img/feed_thumbnail/plain/{authorDid}/{cid}@jpeg";
        }

        return new FeedExternal(
            GetString("uri") ?? string.Empty,
            GetString("title") ?? string.Empty,
            GetString("description") ?? string.Empty,
            thumbUrl ?? string.Empty);
    }
}