# Microsoft 365 Agents SDK for .NET - Release Notes v1.3.0

**Release Date:** October 22, 2025  
**Previous Version:** 1.2.0 (Released August 18, 2025)

## 🎉 What's New in 1.3.0

This release introduces first-class Agent-to-Agent hosting, deepens agentic authorization flows, and ships an OAuth-enabled Copilot Studio connector. It also brings extensible serialization, richer feedback orchestration, and refreshed samples to help you build production-grade agent experiences.

## 🚀 Major Features & Enhancements

### Agent-to-Agent Hosting (Preview)
- Introduced the `Microsoft.Agents.Hosting.AspNetCore.A2A` library with JSON-RPC adapters, task orchestration, and streaming response handling for cross-agent conversations. ([#391](https://github.com/microsoft/Agents-for-net/pull/391))
- Added A2A sample agents (`samples/A2AAgent`, `samples/A2ATCKAgent`) that demonstrate multi-turn tasks, push notifications, and alignment with the A2A TCK. ([#391](https://github.com/microsoft/Agents-for-net/pull/391))

### Agentic Platform Expansion
- Delivered Agentic authorization modules, agentic-aware routes, and channel adapters so agent apps can negotiate enriched responses and capabilities. ([#443](https://github.com/microsoft/Agents-for-net/pull/443), [#445](https://github.com/microsoft/Agents-for-net/pull/445), [#456](https://github.com/microsoft/Agents-for-net/pull/456))
- Added Agentic Adaptive Card Framework (ACF) response handling and refined MSAL integration to honor agent-specific identities. ([#461](https://github.com/microsoft/Agents-for-net/pull/461), [#474](https://github.com/microsoft/Agents-for-net/pull/474))
- Enabled feedback loops and updated samples to use the new agentic route flag, making it easier to capture user evaluations during conversations. ([#480](https://github.com/microsoft/Agents-for-net/pull/480), [#476](https://github.com/microsoft/Agents-for-net/pull/476))

### Copilot Studio Connector with OAuth
- Added the Copilot Studio connector implementation, including OAuth on-behalf-of flows, user token enforcement, and a ready-to-run sample. ([#450](https://github.com/microsoft/Agents-for-net/pull/450))
- Samples now default to requiring authentication and respect configured scopes when calling Copilot Studio services. ([#472](https://github.com/microsoft/Agents-for-net/pull/472), [#450](https://github.com/microsoft/Agents-for-net/pull/450))

### Citation-Aware Responses
- Extended streaming responses with citation management APIs, deduplication safeguards, and richer metadata on AI entities. ([#427](https://github.com/microsoft/Agents-for-net/pull/427))
- Added helper methods to `MessageFactory` and entity models so experiences can surface citations without hand-coding payloads. ([#427](https://github.com/microsoft/Agents-for-net/pull/427))

## 📚 Documentation & Developer Experience

### Expanded API Surface Descriptions
- Added missing XML documentation for `AgentApplicationBuilder`, `IAgentClient`, `IAgentHost`, and quick view responses to improve IntelliSense and API discovery. ([#417](https://github.com/microsoft/Agents-for-net/pull/417))
- Clarified Teams channel support semantics in `Channels` to avoid confusion when routing agent traffic. ([#430](https://github.com/microsoft/Agents-for-net/pull/430))

### Sample Refreshes
- Published the Agentic AI sample with manifests, deployment guidance, and helper extensions to accelerate experimentation. ([#445](https://github.com/microsoft/Agents-for-net/pull/445))
- Updated the Copilot Studio connector sample to highlight OAuth configuration requirements and default security posture. ([#450](https://github.com/microsoft/Agents-for-net/pull/450), [#472](https://github.com/microsoft/Agents-for-net/pull/472))
- Fixed agent-to-agent samples to ensure controller registration and streaming endpoints work out of the box. ([#426](https://github.com/microsoft/Agents-for-net/pull/426), [#429](https://github.com/microsoft/Agents-for-net/pull/429))

## 🔧 Developer Tools & Quality

### Serialization Performance & Extensibility
- Generated assembly-level serialization attributes to eliminate runtime scans and lowered cold-start costs. ([#449](https://github.com/microsoft/Agents-for-net/pull/449), [#441](https://github.com/microsoft/Agents-for-net/pull/441))
- Introduced dynamic entity discovery via `EntityNameAttribute` and public registration hooks, enabling custom entity types without forking the serializer. ([#465](https://github.com/microsoft/Agents-for-net/pull/465))
- Refined entity cleanup and removal logic to prevent stale data from leaking between turns. ([#463](https://github.com/microsoft/Agents-for-net/pull/463))

### Analyzer & Build Infrastructure
- Bundled analyzers inside `Microsoft.Agents.Core` and unified their target framework for consistent diagnostics. ([37deeaf7](https://github.com/microsoft/Agents-for-net/commit/37deeaf7f2be0ea13cb02c45e2c13451d5ebf593), [5fa356d3](https://github.com/microsoft/Agents-for-net/commit/5fa356d3f31942f7ce06107b809b65f900a3f609))
- Moved the repository to .NET SDK 8.0.414 to align with the latest LTS servicing updates. ([#439](https://github.com/microsoft/Agents-for-net/pull/439), [#481](https://github.com/microsoft/Agents-for-net/pull/481))

## 🔐 Authentication & Security Enhancements

### Agentic Identity & Token Handling
- Updated agentic flows to depend on the new `AgenticUserId`, cache MSAL results, and provide clearer error reporting when tokens are missing. ([#474](https://github.com/microsoft/Agents-for-net/pull/474), [#461](https://github.com/microsoft/Agents-for-net/pull/461))

### Connector Safeguards
- Enforced configured user token scopes in the REST channel client and defaulted connector samples to require authentication. ([#450](https://github.com/microsoft/Agents-for-net/pull/450), [#472](https://github.com/microsoft/Agents-for-net/pull/472))
- Truncated overlong conversation identifiers and strengthened ChannelId parsing with nullable annotations and multi-delimiter support. ([#471](https://github.com/microsoft/Agents-for-net/pull/471), [#452](https://github.com/microsoft/Agents-for-net/pull/452), [#469](https://github.com/microsoft/Agents-for-net/pull/469))

## 🐛 Bug Fixes & Maintenance

- Resolved failures when registering `IAgent` implementations via factories in multi-agent hosts. ([#418](https://github.com/microsoft/Agents-for-net/pull/418))
- Ensured Cosmos DB storage can reuse existing `CosmosClient` instances for dependency injection scenarios. ([#446](https://github.com/microsoft/Agents-for-net/pull/446))
- Improved route handlers to understand sub-channel identifiers and alternate blueprint connection names. ([#458](https://github.com/microsoft/Agents-for-net/pull/458), [#445](https://github.com/microsoft/Agents-for-net/pull/445))
- Removed obsolete helper extensions and tightened activity validation to match the latest Teams schemas. ([#461](https://github.com/microsoft/Agents-for-net/pull/461))

## 📦 Package Information

1. **Microsoft.Agents.Hosting.AspNetCore.A2A** – new preview package delivering Agent-to-Agent hosting primitives. ([#391](https://github.com/microsoft/Agents-for-net/pull/391))
2. **Microsoft.Agents.Connector** – now includes the `MCSConnectorClient` plus OAuth-aware authorization modules. ([#450](https://github.com/microsoft/Agents-for-net/pull/450))
3. **Microsoft.Agents.Builder & Microsoft.Agents.Core** – expanded with agentic authorization, feedback loops, and dynamic serialization extensibility. ([#443](https://github.com/microsoft/Agents-for-net/pull/443), [#445](https://github.com/microsoft/Agents-for-net/pull/445), [#480](https://github.com/microsoft/Agents-for-net/pull/480))

## 🚀 Getting Started

Upgrade your projects to the new release with:

```powershell
dotnet add package Microsoft.Agents.Core --version 1.3.0
dotnet add package Microsoft.Agents.Hosting.AspNetCore.A2A --version 1.3.0
dotnet add package Microsoft.Agents.Connector --version 1.3.0
```

Explore the updated samples under `src/samples` (Agentic AI, Copilot Studio Connector, A2A Agent) for end-to-end walkthroughs.

## 🙏 Acknowledgments

Thank you to the Microsoft 365 Agents team and the open-source community for the ideas, code reviews, and contributions that shaped this release.

## 📞 Support & Resources

- **Documentation:** [Microsoft 365 Agents SDK](https://aka.ms/agents)
- **Issues:** [GitHub Issues](https://github.com/microsoft/Agents-for-net/issues)
- **Samples:** [Agent Samples Repository](https://github.com/microsoft/Agents)
- **Community:** Join the discussions and share feedback through GitHub.
