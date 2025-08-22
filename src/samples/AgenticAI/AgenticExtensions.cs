// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticAI
{
    public static class AgenticExtensions
    {
        public static void AddAgenticAdapter(this IServiceCollection services)
        {
            services.AddAsyncAdapterSupport();
            services.AddSingleton<AgenticAdapter>();
        }
    }
}
