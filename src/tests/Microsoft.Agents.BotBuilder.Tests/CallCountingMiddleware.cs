﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.Tests
{
    public class CallCountingMiddleware : IMiddleware
    {
        public int Calls { get; set; }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken)
        {
            Calls++;
            await next(cancellationToken);
        }
    }
}
