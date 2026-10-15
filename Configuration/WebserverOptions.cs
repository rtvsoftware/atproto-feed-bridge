public sealed class WebserverOptions
{
    public const string SectionName = "Webserver";
    
    public string[] CorsAllowed { get; set; } = [];
}