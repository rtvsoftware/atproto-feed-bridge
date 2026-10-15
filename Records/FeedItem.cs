public sealed record FeedItem(
    string Id,
    FeedPost Post,
    FeedAuthor? RepostedBy,
    DateTimeOffset FeedTimestamp
);