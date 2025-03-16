# Microsoft.Agents.Client

Contains the implementation for agent-to-agent handling.

## Main Types

- IChannelHost
- ConfigurationChannelHost : IChannelHost that loads Channel definitions from configuration.
- BotResponses, which works with AgentApplication to provide bot reply handling.
- ServiceExtension `AddChannelHost` that adds agent-to-agent.