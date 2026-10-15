public abstract record FeedNotification;

public sealed record FeedPostReceived(FeedPost Post) : FeedNotification;

public sealed record FeedPostRemoved(string Uri) : FeedNotification;
