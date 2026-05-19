# NamedPipeAgent Sample

This sample extends the [EmptyAgent](../EmptyAgent/README.md) by adding **named-pipe transport** via the `Microsoft.Agents.Hosting.DirectLine.NamedPipes` library. This enables the agent to receive activities over named pipes in addition to the standard HTTP endpoint — the pattern used when deploying to Azure App Service with **DirectLineFlex**.

## What's Different from EmptyAgent

One additional line in `Program.cs`:

```csharp
builder.AddAgentNamedPipeTransport();
```
- A `NamedPipeHostedService` that listens on `bfv4.pipes.incoming` / `bfv4.pipes.outgoing`
- A `DelegatingHandler` that routes outbound HTTP calls to `urn:botframework:namedpipe:*` back through the pipe

## Prerequisites

- [.NET](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) (for HTTP endpoint testing)
- [Microsoft 365 Agents Toolkit](https://github.com/OfficeDev/microsoft-365-agents-toolkit)

## Running Locally

```bash
dotnet run
```

The agent starts with both:
- **HTTP** endpoint at `http://localhost:3978/api/messages`
- **Named pipe** listener on `bfv4.pipes` (used by the Azure App Service sidecar)

### Custom Pipe Name

To use a different pipe name, pass it to the extension:

```csharp
builder.AddAgentNamedPipeTransport("my-custom-pipe");
```

## Deployment to Azure App Service

When deployed to Azure App Service with DirectLineFlex enabled:
1. The App Service sidecar connects to your agent over named pipes
2. External HTTP auth is handled by the sidecar
3. The pipe connection is trusted (no JWT validation needed on the pipe)
4. The HTTP endpoint remains available for health checks and direct access

## QuickestStart using Agent Toolkit

1. Install the Agents Playground:
   ```
   winget install agentsplayground
   ```
2. Start the Agent in VS or VS Code in debug
3. Start Agents Playground: `agentsplayground`
4. Interact with the Agent via the browser (uses the HTTP endpoint)

## Further reading

- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)
- [EmptyAgent Sample](../EmptyAgent/README.md) — base sample without named pipes
