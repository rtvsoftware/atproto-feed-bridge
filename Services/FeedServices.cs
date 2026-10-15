public class FeedServices(FeedItemStore feedItemStore)
{
    public async Task<IReadOnlyList<FeedItem>> GetLatestPosts()
    {
        return feedItemStore.GetLatest();
    }
}