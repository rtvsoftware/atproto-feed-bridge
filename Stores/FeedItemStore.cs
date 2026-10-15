public sealed class FeedItemStore
{
    private readonly object _lock = new();

    private readonly Dictionary<string, FeedItem> _feedItems = new();

    public IReadOnlyList<FeedItem> GetLatest(int count = 50)
    {
        lock (_lock)
        {
            return _feedItems.Values
                .OrderByDescending(post => post.FeedTimestamp)
                .Take(count)
                .ToList();
        }
    }

    public void Upsert(FeedItem feedItem)
    {
        lock (_lock)
        {
            _feedItems[feedItem.Id] = feedItem;
        }
    }

    public void Remove(string uri)
    {
        lock (_lock)
        {
            _feedItems.Remove(uri);
        }
    }
}