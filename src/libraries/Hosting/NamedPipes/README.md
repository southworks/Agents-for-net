# Microsoft.Agents.Hosting.NamedPipes

Named pipe transport for the Microsoft Agents SDK, enabling agents to communicate with DirectLineFlex (Azure App Service sidecar) over named pipes without HTTP roundtrips.

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddAgent<MyAgent>();
builder.AddAgentNamedPipeTransport(); // default pipe name: "bfv4.pipes"

var app = builder.Build();
app.MapAgentApplicationEndpoints();
app.Run();
```

## How It Works

When deployed in Azure App Service with DirectLineFlex, the agent communicates via a pair of named pipes (`{pipeName}.incoming` and `{pipeName}.outgoing`) using the Bot Framework wire protocol (48-byte ASCII framed headers with JSON payloads).

- **Inbound**: Activities arrive over the pipe and are dispatched to the agent's turn pipeline via `IChannelAdapter.ProcessActivityAsync`.
- **Outbound**: Reply activities sent via `ITurnContext.SendActivityAsync` are routed back through the pipe (intercepting `urn:botframework:namedpipe:*` service URLs).

## Configuration

The pipe name defaults to `"bfv4.pipes"` and can be customized:

```csharp
builder.AddAgentNamedPipeTransport(pipeName: "my-custom-pipe");
```
