// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI;

public class AgenticAdapter : ChannelAdapter, IAgentHttpAdapter
{
    private readonly ILogger<AgenticAdapter> _logger;

    public AgenticAdapter(ILogger<AgenticAdapter>? logger = null) : base(logger!)
    {
        _logger = logger ?? NullLogger<AgenticAdapter>.Instance;
    }

    public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
    {
        var activity = await HttpHelper.ReadRequestAsync<IActivity>(httpRequest);
        var identity = HttpHelper.GetClaimsIdentity(httpRequest);

        // Just handle request synchronously for now
        var response = await ProcessActivityAsync(identity, activity, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);
        await HttpHelper.WriteResponseAsync(httpResponse, response).ConfigureAwait(false);
    }

    public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
    {
        var context = new TurnContext(this, activity, claimsIdentity);
        await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        return null!;
    }

    public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
