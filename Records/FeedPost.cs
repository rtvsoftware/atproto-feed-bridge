public sealed record FeedPost(
    string Uri,
    FeedAuthor? PostedBy,
    string Text,
    string? RootUri,
    string? ParentUri,
    int ThreadIndex,
    DateTimeOffset CreatedAt,
    FeedExternal? External,
    FeedImage[] Images,
    FeedVideo[] Videos
    ) 
{
    // Mutable because FeedItemStore.SetThreadIndex() recomputes this after construction.
    public int ThreadIndex { get; set; } = ThreadIndex;
}
