# Microsoft.Agents.Hosting.AspNetCore

- Required package for a hosting an Agent is AspNet.
- Provides extension methods to add an Agents to DI.

## How-to use

```cs
builder.AddAgent<TAgent>();
```

Or

```cs
builder.AddAgent(sp =>
{
	var options = new AgentApplicationOptions()
	{
	};

	var app = new AgentApplication(options);

	return app;
});
```
