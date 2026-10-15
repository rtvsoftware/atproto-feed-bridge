using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(Channel.CreateUnbounded<FeedNotification>(
    new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    }));

builder.Services.AddSingleton<FeedItemStore>();
builder.Services.AddSingleton<AppViewServices>();
builder.Services.AddSingleton<PostServices>();
builder.Services.AddSingleton<FeedServices>();
builder.Services.AddSingleton<JetstreamServices>();

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

builder.Services.AddHostedService<BackgroundWorker>();

builder.Services
    .AddOptions<WebserverOptions>()
    .Bind(builder.Configuration.GetSection(WebserverOptions.SectionName));
builder.Services
    .AddOptions<JetstreamOptions>()
    .Bind(builder.Configuration.GetSection(JetstreamOptions.SectionName));
builder.Services
    .AddOptions<ATProtoOptions>()
    .Bind(builder.Configuration.GetSection(ATProtoOptions.SectionName));

var weberverConfiguration = builder.Configuration
    .GetSection(WebserverOptions.SectionName)
    .Get<WebserverOptions>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
        policy.WithOrigins(weberverConfiguration?.CorsAllowed ?? [])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddSignalR();

var app = builder.Build();

app.UseCors("DefaultCorsPolicy");

app.MapHub<FeedHub>("/feedHub");

// Serve default files and static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Echo for testing connectivity
app.MapGet("/echo", () =>
{
    return Results.Ok();
});

// Get the latest feed posts
app.MapGet("/feed", async (FeedServices feedServices) =>
{
    var latestPosts = await feedServices.GetLatestPosts();
    return Results.Ok(latestPosts);
});

app.Run();
