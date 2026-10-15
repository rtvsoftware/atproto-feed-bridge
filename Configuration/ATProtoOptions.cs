public sealed class ATProtoOptions
{
    public const string SectionName = "Application:ATProto";

    public string AgentUrl { get; set; } = "";

    public string[] Dids { get; init; } = [];
}