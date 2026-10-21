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

            SetThreadIndex();
        }
    }

    public void Remove(string uri)
    {
        lock (_lock)
        {
            _feedItems.Remove(uri);
        }
    }

    private void SetThreadIndex()
    {
        lock (_lock)
        {
            var threadGroups = _feedItems.Values.ToLookup(item => item.Post.RootUri);

            foreach (var group in threadGroups)
            {
                var i = 1;
                foreach (var post in group
                    .Where(p => p.Post.ParentUri is not null)
                    .OrderBy(p => p.FeedTimestamp))
                {
                    post.Post.ThreadIndex = i++;
                }    
            }
        }
    }
}