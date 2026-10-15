# ATProto Bridge .NET library 

AtProtoBridge is a lightweight ASP.NET Core service that bridges the [AT Protocol](https://docs.bsky.app/docs/api/at-protocol-xrpc-api) / [Bluesky](https://bsky.social/) Jetstream firehose into your own applications. It listens for posts and reposts from a configurable set of accounts, enriches them with author and media data from the Bluesky AppView, and makes the resulting feed available both as a simple REST snapshot and as a real-time SignalR stream.

A working example of displaying the live feed over `wss`/SignalR can be found in [wwwroot/index.html](wwwroot/index.html): it loads the initial snapshot from `GET /feed` and then connects to the `/feedHub` SignalR hub to receive and render new posts (and removals) in real time. It is served automatically when you run the project and open the site's root URL.


## Overview

* Connects to a [Jetstream](https://github.com/bluesky-social/jetstream) instance and subscribes to `app.bsky.feed.post` and `app.bsky.feed.repost` events for a configurable list of account DIDs.
* On startup, backfills the most recent posts and reposts for those accounts directly from their PDS, so the feed is populated immediately.
* Resolves author profiles and parses link previews, images and video embeds through the public Bluesky AppView API, with in-memory caching to limit outgoing requests.
* Keeps the latest feed items in memory and exposes them through a REST endpoint and a SignalR hub.
* Intended as a reusable building block for running your own curated Bluesky feed (for a community, organization or region) without having to build a full feed generator or Jetstream consumer from scratch.

## Getting Started

1. Edit `appsettings.json` with your own DIDs, PDS/agent URL and allowed CORS origins — see [Configuration](#configuration) below.
2. Run the project: `dotnet run`.
3. The service starts consuming Jetstream immediately and serves:
   * `GET /feed` — the current feed as JSON
   * `/feedHub` — a SignalR hub (`wss://`) for real-time updates
   * `GET /echo` — a simple connectivity check

## Configuration

All settings live in `appsettings.json`:

| Section | Key | Description |
|---|---|---|
| `Webserver` | `CorsAllowed` | Array of allowed CORS origins — add the URL(s) of the website(s)/app(s) that will call `/feed` or connect to `/feedHub`. |
| `Application:ATProto` | `AgentUrl` | Base URL of the AT Protocol agent/PDS used to resolve accounts and backfill posts on startup (e.g. `https://pds.oso.social` or `https://bsky.social`). |
| `Application:ATProto` | `Dids` | The account DIDs (`did:plc:...`) whose posts and reposts are included in the feed. This is the main setting to change when reusing this project for a different community or set of accounts. |
| `Application:Jetstream` | `Url` | WebSocket URL of the Jetstream instance to consume, e.g. `wss://jetstream1.eurosky.network/receive`. See the [Jetstream repository](https://github.com/bluesky-social/jetstream) for public instances. |

> The sample `appsettings.json` also contains `Application:ATProto:Handle` and `Application:ATProto:Collections` keys. These are not currently read by the application — the tracked collections are hard-coded to `app.bsky.feed.post` and `app.bsky.feed.repost` — so they can be ignored or removed.

## Usage

### REST: `GET /feed`

Returns the latest feed items (default 50), newest first, as a JSON array:

```http
GET /feed
```

```json
[
  {
    "id": "at://did:plc:example/app.bsky.feed.post/abc123",
    "post": {
      "uri": "at://did:plc:example/app.bsky.feed.post/abc123",
      "postedBy": {
        "did": "did:plc:example",
        "handle": "example.bsky.social",
        "displayName": "Example",
        "avatarUrl": "https://..."
      },
      "text": "Hello from Bluesky!",
      "createdAt": "2026-01-01T12:00:00Z",
      "external": null,
      "images": [],
      "videos": []
    },
    "repostedBy": null,
    "feedTimestamp": "2026-01-01T12:00:00Z"
  }
]
```

### Real-time: SignalR hub `/feedHub`

Connect over WebSockets (`wss://`) using a [SignalR client](https://learn.microsoft.com/aspnet/core/signalr/client-features) and listen for:

* `ReceivePost` — invoked with the post payload whenever a new post or repost is received from Jetstream.
* `RemovePost` — invoked with the post `uri` (`string`) whenever a post is deleted.

Example using `@microsoft/signalr`:

```js
const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://your-host/feedHub")
  .withAutomaticReconnect()
  .build();

connection.on("ReceivePost", (post) => {
  console.log("New post", post);
});

connection.on("RemovePost", (uri) => {
  console.log("Post removed", uri);
});

await connection.start();
```

## License

This package is available under the MIT license,
see the [LICENSE](LICENSE) file for more information.

## Support Policy

Only the latest version of the libraries are supported. All bug fixes, including security fixes, will be made in the latest version.
Older versions will not receive any updates.

The library is supported on all in-support versions of .NET.


## Dependencies

This version takes a dependency on `idunno.AtProto` .

### External dependencies

* [idunno.AtProto](https://github.com/blowdart/idunno.Bluesky)