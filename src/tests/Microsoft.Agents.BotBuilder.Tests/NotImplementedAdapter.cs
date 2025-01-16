﻿using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.Tests
{
    internal class NotImplementedAdapter : ChannelAdapter
    {
        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
