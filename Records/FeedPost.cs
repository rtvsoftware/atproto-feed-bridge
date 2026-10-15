

public sealed record FeedPost(
    string Uri,
    FeedAuthor? PostedBy,
    string Text,
    DateTimeOffset CreatedAt,
    FeedExternal? External,
    FeedImage[] Images,
    FeedVideo[] Videos) 
{

}

